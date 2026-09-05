using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Analysis;

public record Suggestion(string CardName, string Tag, double Score, bool Owned, string? OracleText);

public class SuggestionEngine
{
    // Weights per the plan's Section 6.2 — transparent and tunable, not a black box.
    public double SynergyWeight { get; set; } = 0.6;
    public double CurveFitWeight { get; set; } = 0.4;
    public double PreferredMvLow { get; set; } = 2;
    public double PreferredMvHigh { get; set; } = 4;

    public List<Suggestion> SuggestFor(
        string categoryDisplayName,
        string[] matchSubstrings,
        bool useTextPatterns,
        IEnumerable<CollectionCard> onColorCollection,
        Dictionary<string, EdhrecCardEntry> edhrecByName, // name -> entry, may be empty if EDHREC unavailable
        int topN = 5)
    {
        var candidates = onColorCollection
            .Where(c => c.ScryfallData != null && (useTextPatterns
                ? WinConditionPatterns.Matches(c.ScryfallData.OracleText)
                : c.ScryfallData.HasAnyTagContaining(matchSubstrings)))
            .Select(c =>
            {
                var synergy = edhrecByName.TryGetValue(c.Name, out var e) ? Normalize(e.SynergyScore) : 0.5; // neutral default if EDHREC data absent
                var curveFit = CurveFit(c.ManaValue);
                var score = SynergyWeight * synergy + CurveFitWeight * curveFit;
                return new Suggestion(c.Name, categoryDisplayName, Math.Round(score, 2), Owned: true, c.ScryfallData?.OracleText);
            })
            .OrderByDescending(s => s.Score)
            .Take(topN)
            .ToList();

        return candidates;
    }

    private double CurveFit(double? mv)
    {
        if (mv == null) return 0.5;
        if (mv >= PreferredMvLow && mv <= PreferredMvHigh) return 1.0;
        var distance = mv < PreferredMvLow ? PreferredMvLow - mv.Value : mv.Value - PreferredMvHigh;
        return Math.Max(0, 1.0 - distance * 0.15);
    }

    // EDHREC's raw synergy scores are roughly in [-1, 1] in community documentation of the
    // (unofficial) data; clamp/rescale to [0,1] defensively since the real range hasn't been
    // confirmed against a live response in this environment.
    private double Normalize(double raw) => Math.Clamp((raw + 1) / 2, 0, 1);
}
