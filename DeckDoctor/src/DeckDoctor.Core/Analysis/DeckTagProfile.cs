using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Analysis;

public class DeckTagProfile
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tags that are structurally/cosmetically true of huge swaths of cards regardless of deck
    /// strategy — excluded so the histogram surfaces archetype signal, not noise. Confirmed
    /// against a real deck (2026-09-06): "activated ability" and "triggered ability" topped
    /// Council of Four's histogram at 28 and 19 respectively despite saying nothing about what
    /// kind of deck it is, and "single english word name"/"alliteration" tag a card's NAME, not
    /// its function, at all. This is a noise filter, not an archetype classifier — it excludes,
    /// it doesn't assert what a deck IS.
    /// </summary>
    private static readonly HashSet<string> GenericNoiseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "activated ability", "triggered ability", "static ability", "keyword ability",
        "single english word name", "alliteration", "named after a person", "flavor text matters",
    };

    /// <summary>Build from any source of per-card tag lists — Archidekt's oracleCard.oTags for
    /// deck cards, or Scryfall-joined-with-bulk-tags for collection candidates.</summary>
    public static DeckTagProfile Build(IEnumerable<IEnumerable<string>> tagListsPerCard)
    {
        var profile = new DeckTagProfile();
        foreach (var tags in tagListsPerCard)
            foreach (var tag in tags)
            {
                if (GenericNoiseTags.Contains(tag)) continue;
                profile._counts[tag] = profile._counts.GetValueOrDefault(tag) + 1;
            }
        return profile;
    }

    public int GetCount(string tag) => _counts.GetValueOrDefault(tag);

    public List<(string Tag, int Count)> TopTags(int n) =>
        _counts.OrderByDescending(kv => kv.Value).Take(n).Select(kv => (kv.Key, kv.Value)).ToList();

    public IReadOnlyDictionary<string, int> TagHistogram() => _counts;
}
