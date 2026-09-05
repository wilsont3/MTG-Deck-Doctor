using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Analysis;

public class DeckTagProfile
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Build from any source of per-card tag lists — Archidekt's oracleCard.oTags for
    /// deck cards, or Scryfall-joined-with-bulk-tags for collection candidates.</summary>
    public static DeckTagProfile Build(IEnumerable<IEnumerable<string>> tagListsPerCard)
    {
        var profile = new DeckTagProfile();
        foreach (var tags in tagListsPerCard)
            foreach (var tag in tags)
                profile._counts[tag] = profile._counts.GetValueOrDefault(tag) + 1;
        return profile;
    }

    public int GetCount(string tag) => _counts.GetValueOrDefault(tag);

    public List<(string Tag, int Count)> TopTags(int n) =>
        _counts.OrderByDescending(kv => kv.Value).Take(n).Select(kv => (kv.Key, kv.Value)).ToList();

    public IReadOnlyDictionary<string, int> TagHistogram() => _counts;
}
