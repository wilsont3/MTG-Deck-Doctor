namespace DeckDoctor.Core.Models;

/// <summary>
/// One row of the Archidekt collection CSV export:
/// Quantity, Name, Scryfall ID, Mana Value, Identities, Mana cost
/// </summary>
public class CollectionCard
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string? ScryfallId { get; set; }
    public double? ManaValue { get; set; }
    public List<string> Identities { get; set; } = new(); // e.g. ["White","Blue"]
    public string? ManaCost { get; set; }

    /// <summary>Populated after joining against Scryfall (by name) — optional enrichment.</summary>
    public ScryfallCard? ScryfallData { get; set; }

    public bool IsSubsetOf(IEnumerable<string> commanderColorIdentity)
    {
        var allowed = new HashSet<string>(commanderColorIdentity, StringComparer.OrdinalIgnoreCase);
        return Identities.All(c => allowed.Contains(c)) || Identities.Count == 0;
    }
}

/// <summary>Maps Archidekt's full color words ("White") to WUBRG letters used by Scryfall.</summary>
public static class ColorNames
{
    public static readonly Dictionary<string, string> FullToLetter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["White"] = "W",
        ["Blue"] = "U",
        ["Black"] = "B",
        ["Red"] = "R",
        ["Green"] = "G",
    };

    public static readonly Dictionary<string, string> LetterToFull = new()
    {
        ["W"] = "White",
        ["U"] = "Blue",
        ["B"] = "Black",
        ["R"] = "Red",
        ["G"] = "Green",
    };
}
