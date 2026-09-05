using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

/// <summary>
/// UNVERIFIED against a real response — backend.commanderspellbook.com isn't reachable from my
/// sandbox (network allowlist) and direct fetch is blocked by their robots.txt, so unlike
/// Archidekt/Scryfall/EDHREC, none of this has been confirmed against live data. Built from:
/// a public third-party client's documented response shape (npm "commander-spellbook" package)
/// and an MCP tool wrapper's documented request shape (numeric card IDs, not names). Treat every
/// field name here as a hypothesis — the first real run against this API needs the same
/// dump-the-raw-body-and-compare treatment the other three integrations got.
/// </summary>
public class CommanderSpellbookVariant
{
    public string Id { get; set; } = "";
    public string Permalink => $"https://commanderspellbook.com/?id={Id}";
    public List<string> CardNames { get; set; } = new();
    public List<string> ColorIdentity { get; set; } = new();
    public bool HasBannedCard { get; set; }
    public bool HasSpoiledCard { get; set; }
    public List<string> Results { get; set; } = new();
}

public class CsbCardDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class CsbCardSearchResponseDto
{
    [JsonPropertyName("results")] public List<CsbCardDto> Results { get; set; } = new();
}

public class CsbFindCombosResponseDto
{
    // Best-effort guess: "included" for fully-satisfied combos given the input card set. The
    // real response may use different key names or nest this differently — see class-level note.
    [JsonPropertyName("included")] public List<CsbVariantDto>? Included { get; set; }
    [JsonPropertyName("results")] public List<CsbVariantDto>? Results { get; set; } // fallback shape guess
}

public class CsbVariantDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("uses")] public List<CsbVariantCardRefDto>? Uses { get; set; }
    [JsonPropertyName("cards")] public List<CsbVariantCardRefDto>? Cards { get; set; } // fallback field name guess
    [JsonPropertyName("identity")] public string? Identity { get; set; }
    [JsonPropertyName("bannedCard")] public bool? BannedCard { get; set; }
    [JsonPropertyName("previewedCard")] public bool? PreviewedCard { get; set; }
    [JsonPropertyName("produces")] public List<CsbProducesDto>? Produces { get; set; }
}

public class CsbVariantCardRefDto
{
    [JsonPropertyName("card")] public CsbCardDto? Card { get; set; }
}

public class CsbProducesDto
{
    [JsonPropertyName("feature")] public CsbFeatureDto? Feature { get; set; }
}

public class CsbFeatureDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}
