using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

public class ArchidektDeck
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<ArchidektDeckCard> Cards { get; set; } = new();

    public List<string> CommanderNames =>
        Cards.Where(c => c.Categories.Any(cat => cat.Contains("Commander", StringComparison.OrdinalIgnoreCase)))
             .Select(c => c.Name)
             .Distinct()
             .ToList();

    /// <summary>The actual 100 — excludes both Maybeboard and Sideboard. Archidekt's own
    /// "includedInDeck" flag on the Sideboard category is misleadingly true, so this filters
    /// by category name rather than trusting that flag (confirmed against a real response).</summary>
    public List<ArchidektDeckCard> MainboardCards =>
        Cards.Where(c => !c.Categories.Any(cat =>
                cat.Contains("Maybeboard", StringComparison.OrdinalIgnoreCase) ||
                cat.Contains("Sideboard", StringComparison.OrdinalIgnoreCase)))
             .ToList();
}

public class ArchidektDeckCard
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public List<string> Categories { get; set; } = new();

    public string? ManaCost { get; set; }
    public double? Cmc { get; set; }
    public string? OracleText { get; set; }
    public List<string> ColorIdentity { get; set; } = new();
    public List<string> OTags { get; set; } = new();
    public bool GameChanger { get; set; }
    public bool IsTutor { get; set; }
    public bool ExtraTurns { get; set; }
    public bool MassLandDenial { get; set; }
    public List<string> SuperTypes { get; set; } = new();
    public List<string> Types { get; set; } = new();
    public List<string> SubTypes { get; set; } = new();

    /// <summary>Built from superTypes/types/subTypes — confirmed present, e.g. "Legendary Creature — Human Noble".</summary>
    public string TypeLine
    {
        get
        {
            var left = string.Join(" ", SuperTypes.Concat(Types));
            return SubTypes.Count > 0 ? $"{left} — {string.Join(" ", SubTypes)}" : left;
        }
    }

    public bool HasTag(string tag) => OTags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Matches by substring against any of the given fragments — for the real granular oTag vocabulary, not exact category names.</summary>
    public bool HasAnyTagContaining(IEnumerable<string> substrings) =>
        OTags.Any(t => substrings.Any(s => t.Contains(s, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// DTOs matching Archidekt's real JSON shape (https://archidekt.com/api/decks/{id}/), confirmed
/// against an actual response on 2026-08-31 — this is no longer a guess.
/// </summary>
public class ArchidektDeckDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("cards")] public List<ArchidektCardEntryDto> Cards { get; set; } = new();
}

public class ArchidektCardEntryDto
{
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("card")] public ArchidektCardDto? Card { get; set; }
    [JsonPropertyName("categories")] public List<string>? Categories { get; set; }
}

public class ArchidektCardDto
{
    [JsonPropertyName("oracleCard")] public ArchidektOracleCardDto? OracleCard { get; set; }
}

public class ArchidektOracleCardDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("manaCost")] public string? ManaCost { get; set; }
    [JsonPropertyName("cmc")] public double? Cmc { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("colorIdentity")] public List<string>? ColorIdentity { get; set; }
    [JsonPropertyName("oTags")] public List<string>? OTags { get; set; }
    [JsonPropertyName("gameChanger")] public bool GameChanger { get; set; }
    [JsonPropertyName("tutor")] public bool Tutor { get; set; }
    [JsonPropertyName("extraTurns")] public bool ExtraTurns { get; set; }
    [JsonPropertyName("massLandDenial")] public bool MassLandDenial { get; set; }
    [JsonPropertyName("superTypes")] public List<string>? SuperTypes { get; set; }
    [JsonPropertyName("types")] public List<string>? Types { get; set; }
    [JsonPropertyName("subTypes")] public List<string>? SubTypes { get; set; }
}

