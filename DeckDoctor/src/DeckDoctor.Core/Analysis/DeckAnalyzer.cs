using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Analysis;

public enum WeaknessLevel { Ok, Low }

/// <summary>One card's worth of analysis input — tags for most categories, oracle text for Wincons.</summary>
public record CardAnalysisInput(List<string> OTags, string? OracleText);

public record WeaknessThreshold(string DisplayName, string[] MatchSubstrings, int MinimumCount, bool UseTextPatterns = false);

public class DeckWeaknessReport
{
    public Dictionary<string, (int Count, WeaknessLevel Level)> Weaknesses { get; } = new();

    public IEnumerable<string> LowTags => Weaknesses.Where(kv => kv.Value.Level == WeaknessLevel.Low).Select(kv => kv.Key);
}

public class DeckAnalyzer
{
    // Ramp/Draw/Removal matched by real observed oTag substrings (validated against a real deck:
    // Ramp 14, Draw 36, Removal 8 — all sensible). Wincons uses oracle TEXT patterns instead — see
    // WinConditionPatterns.cs for why tag matching doesn't work for this one category at all.
    public static readonly List<WeaknessThreshold> DefaultThresholds = new()
    {
        new("Ramp", new[] { "ramp", "mana rock" }, 8),
        new("Draw", new[] { "draw", "cantrip", "loot", "wheel", "restock" }, 8),
        new("Removal", new[] { "removal", "sweeper" }, 8),
        new("Wincons", Array.Empty<string>(), 3, UseTextPatterns: true),
    };

    private readonly List<WeaknessThreshold> _thresholds;

    public DeckAnalyzer(List<WeaknessThreshold>? thresholds = null)
    {
        _thresholds = thresholds ?? DefaultThresholds;
    }

    public DeckWeaknessReport Analyze(IEnumerable<CardAnalysisInput> cards)
    {
        var allCards = cards.ToList();
        var report = new DeckWeaknessReport();

        foreach (var t in _thresholds)
        {
            var count = t.UseTextPatterns
                ? allCards.Count(c => WinConditionPatterns.Matches(c.OracleText))
                : allCards.Count(c => c.OTags.Any(tag => t.MatchSubstrings.Any(sub =>
                    tag.Contains(sub, StringComparison.OrdinalIgnoreCase))));
            var level = count < t.MinimumCount ? WeaknessLevel.Low : WeaknessLevel.Ok;
            report.Weaknesses[t.DisplayName] = (count, level);
        }
        return report;
    }
}
