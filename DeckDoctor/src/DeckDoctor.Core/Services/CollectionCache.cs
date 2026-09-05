using System.Text.Json;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

/// <summary>
/// Caches the ENRICHED collection (Scryfall data + oTags attached to each CollectionCard) to a
/// local JSON file, invalidated by comparing against the source CSV's last-write time rather than
/// a fixed age — so it naturally refreshes exactly when you re-export from Archidekt (new cards,
/// updated quantities) and stays untouched otherwise. This is intentionally NOT a real database
/// (SQLite, etc.) — see the note in README.md on why a flat JSON cache is the right call here.
/// </summary>
public class CollectionCache
{
    private readonly ScryfallClient _scryfall;
    private readonly CollectionRepository _collectionRepo;

    public CollectionCache(ScryfallClient scryfall, CollectionRepository collectionRepo)
    {
        _scryfall = scryfall;
        _collectionRepo = collectionRepo;
    }

    public async Task<(List<CollectionCard> Cards, List<string> NotFound)> LoadEnrichedAsync(
        string csvPath,
        string cachePath = "data/collection-cache.json")
    {
        if (File.Exists(cachePath) && File.Exists(csvPath))
        {
            var cacheWrittenAt = File.GetLastWriteTimeUtc(cachePath);
            var csvModifiedAt = File.GetLastWriteTimeUtc(csvPath);
            if (cacheWrittenAt >= csvModifiedAt)
            {
                try
                {
                    var cachedJson = await File.ReadAllTextAsync(cachePath);
                    var cached = JsonSerializer.Deserialize<CachedCollection>(cachedJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cached != null)
                        return (cached.Cards, cached.NotFound);
                }
                catch (JsonException)
                {
                    // Corrupt cache — fall through and rebuild rather than failing the whole run.
                }
            }
        }

        // Cache missing, stale (CSV re-exported since), or corrupt — rebuild it.
        var collection = _collectionRepo.LoadFromCsv(csvPath);
        var lookup = await _scryfall.FetchCardsByNameAsync(collection.Select(c => c.Name));
        var tagsByOracleId = await _scryfall.FetchOracleTagsCachedAsync();
        _scryfall.AttachTags(lookup.Found.Values, tagsByOracleId);

        foreach (var c in collection)
            if (lookup.Found.TryGetValue(c.Name, out var sc))
                c.ScryfallData = sc;

        var dir = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var toWrite = new CachedCollection { Cards = collection, NotFound = lookup.NotFound };
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(toWrite));

        return (collection, lookup.NotFound);
    }

    private class CachedCollection
    {
        public List<CollectionCard> Cards { get; set; } = new();
        public List<string> NotFound { get; set; } = new();
    }
}
