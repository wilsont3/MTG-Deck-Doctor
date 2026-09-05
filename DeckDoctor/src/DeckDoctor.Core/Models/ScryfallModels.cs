using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

public class ScryfallCard
{
    public string Name { get; set; } = "";
    public string? OracleId { get; set; }
    public string? ManaCost { get; set; }
    public double? Cmc { get; set; }
    public string? TypeLine { get; set; }
    public string? OracleText { get; set; }
    public List<string> ColorIdentity { get; set; } = new();
    public bool GameChanger { get; set; }

    /// <summary>Populated after joining against the Oracle Tags bulk file by OracleId.</summary>
    public List<string> OTags { get; set; } = new();

    public bool HasTag(string tag) => OTags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    public bool HasAnyTagContaining(IEnumerable<string> substrings) =>
        OTags.Any(t => substrings.Any(s => t.Contains(s, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// DTO matching a card object returned by /cards/collection or /cards/named — confirmed field-
/// for-field against a real response on 2026-08-31, no longer a guess.
/// </summary>
public class ScryfallCardDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("oracle_id")] public string? OracleId { get; set; }
    [JsonPropertyName("mana_cost")] public string? ManaCost { get; set; }
    [JsonPropertyName("cmc")] public double? Cmc { get; set; }
    [JsonPropertyName("type_line")] public string? TypeLine { get; set; }
    [JsonPropertyName("oracle_text")] public string? OracleText { get; set; }
    [JsonPropertyName("color_identity")] public List<string>? ColorIdentity { get; set; }
    [JsonPropertyName("game_changer")] public bool GameChanger { get; set; }

    public ScryfallCard ToModel() => new()
    {
        Name = Name,
        OracleId = OracleId,
        ManaCost = ManaCost,
        Cmc = Cmc,
        TypeLine = TypeLine,
        OracleText = OracleText,
        ColorIdentity = ColorIdentity ?? new List<string>(),
        GameChanger = GameChanger,
    };
}

public class ScryfallCollectionResponseDto
{
    [JsonPropertyName("data")] public List<ScryfallCardDto> Data { get; set; } = new();
    [JsonPropertyName("not_found")] public List<ScryfallNotFoundDto>? NotFound { get; set; }
}

public class ScryfallNotFoundDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

/// <summary>One entry in the bulk-data listing (GET /bulk-data).</summary>
public class BulkDataItemDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("download_uri")] public string? DownloadUri { get; set; }
    [JsonPropertyName("jsonl_download_uri")] public string? JsonlDownloadUri { get; set; }
    [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }

    /// <summary>
    /// Scryfall retired the old (non-JSONL) download_uri in favor of jsonl_download_uri as of
    /// July 20, 2026 (per their blog). Prefer the new field, fall back for safety in case a
    /// given bulk type still serves the old one.
    /// </summary>
    public string EffectiveDownloadUri => JsonlDownloadUri ?? DownloadUri
        ?? throw new InvalidOperationException($"Bulk data entry '{Type}' has neither download_uri nor jsonl_download_uri.");
}

public class BulkDataListDto
{
    [JsonPropertyName("data")] public List<BulkDataItemDto> Data { get; set; } = new();
}

/// <summary>
/// One record from the oracle_tags bulk file. Per Scryfall's Tags API docs, each entry has an
/// oracle_id and a taggings array; we flatten that into (oracle_id -> [tag labels]) at load time.
/// </summary>
public class OracleTagRecordDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("taggings")] public List<OracleTaggingDto>? Taggings { get; set; }
}

public class OracleTaggingDto
{
    [JsonPropertyName("oracle_id")] public string? OracleId { get; set; }
}
