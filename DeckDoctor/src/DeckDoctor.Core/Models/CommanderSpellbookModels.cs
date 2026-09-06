using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

/// <summary>
/// CONFIRMED against the official generated TypeScript client (@space-cow-media/spellbook-client
/// v6.3.3, pulled from npm on 2026-09-05 — npm is reachable from this sandbox even though the API
/// itself isn't). This is real, not a guess: the request takes card NAMES directly (no numeric ID
/// resolution needed at all — that per-card lookup step was the actual cause of the 429s, and it's
/// now gone entirely, not just rate-limited). The response natively includes both fully-included
/// and "almost included" combos in one call.
///
/// Also confirmed straight from the client's doc comment: rate limit is ~80 requests/minute, and
/// there's an official bulk file (/variants.json.gz) for anyone needing the whole dataset — not
/// used here since a per-deck call is well within the rate limit on its own.
/// </summary>
public class CommanderSpellbookVariant
{
    public string Id { get; set; } = "";
    public string Permalink => $"https://commanderspellbook.com/combo/{Id}";
    public List<string> CardNames { get; set; } = new();
    public string ColorIdentity { get; set; } = "";
    public List<string> Results { get; set; } = new();
    public string? Description { get; set; }
}

public record CardInDeckRequestDto(
    [property: JsonPropertyName("card")] string Card,
    [property: JsonPropertyName("quantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Quantity = null);

public class DeckRequestDto
{
    [JsonPropertyName("main")] public List<CardInDeckRequestDto> Main { get; set; } = new();
    [JsonPropertyName("commanders")] public List<CardInDeckRequestDto> Commanders { get; set; } = new();
}

public class CsbCardDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class CsbFeatureDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class CsbCardInVariantDto
{
    [JsonPropertyName("card")] public CsbCardDto? Card { get; set; }
}

public class CsbFeatureProducedDto
{
    [JsonPropertyName("feature")] public CsbFeatureDto? Feature { get; set; }
}

public class CsbVariantDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("uses")] public List<CsbCardInVariantDto>? Uses { get; set; }
    [JsonPropertyName("produces")] public List<CsbFeatureProducedDto>? Produces { get; set; }
    [JsonPropertyName("identity")] public string? Identity { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

/// <summary>Matches PaginatedFindMyCombosResponseListResults from the real client — the "results" object inside the paginated envelope.</summary>
public class CsbFindMyCombosResultsDto
{
    [JsonPropertyName("identity")] public string Identity { get; set; } = "";
    [JsonPropertyName("included")] public List<CsbVariantDto> Included { get; set; } = new();
    [JsonPropertyName("almostIncluded")] public List<CsbVariantDto> AlmostIncluded { get; set; } = new();
}

/// <summary>The outer paginated envelope — DRF-style pagination wrapping a single "results" object for this particular endpoint (confirmed: results is an object here, not an array, since find-my-combos returns one categorized result set, not a list of independent items).</summary>
public class CsbFindMyCombosResponseDto
{
    [JsonPropertyName("results")] public CsbFindMyCombosResultsDto? Results { get; set; }
}
