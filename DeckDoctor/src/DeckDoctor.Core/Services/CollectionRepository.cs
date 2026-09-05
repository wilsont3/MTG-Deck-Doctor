using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

public class CollectionRepository
{
    public List<CollectionCard> LoadFromCsv(string path)
    {
        var text = File.ReadAllText(path);
        var rows = CsvReader.ReadWithHeader(text);

        // Archidekt's export gives one row PER OWNED COPY (Quantity=1 each) rather than one row
        // with a summed quantity — confirmed against a real export with 214 cards appearing as
        // 2+ identical rows (same name, same Scryfall ID). Aggregate by name so each logical card
        // appears once with its true total quantity, rather than crashing downstream code that
        // assumes names are unique (and rather than silently only counting the last copy seen).
        var byName = new Dictionary<string, CollectionCard>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var name = row.GetValueOrDefault("Name", "").Trim();
            if (name.Length == 0) continue;

            var qty = int.TryParse(row.GetValueOrDefault("Quantity"), out var q) ? q : 1;

            if (byName.TryGetValue(name, out var existing))
            {
                existing.Quantity += qty;
                continue;
            }

            byName[name] = new CollectionCard
            {
                Name = name,
                Quantity = qty,
                ScryfallId = string.IsNullOrWhiteSpace(row.GetValueOrDefault("Scryfall ID")) ? null : row["Scryfall ID"],
                ManaValue = double.TryParse(row.GetValueOrDefault("Mana Value"), out var mv) ? mv : null,
                ManaCost = row.GetValueOrDefault("Mana cost"),
                Identities = (row.GetValueOrDefault("Identities") ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
            };
        }
        return byName.Values.ToList();
    }

    /// <summary>Cards whose color identity is a subset of the given commander identity (colorless always included).</summary>
    public List<CollectionCard> FilterOnColor(IEnumerable<CollectionCard> collection, IEnumerable<string> commanderColorIdentity)
    {
        var identity = commanderColorIdentity.ToList();
        return collection.Where(c => c.IsSubsetOf(identity)).ToList();
    }
}
