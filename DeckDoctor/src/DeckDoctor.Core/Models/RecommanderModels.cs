using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

/// <summary>
/// Confirmed directly against https://recommander.cards/api/ (fetched in full, no bot-blocking,
/// no guessing needed — unlike EDHREC and Commander Spellbook, this is a fully public, documented
/// API). Recommends cards based on commander + current deck contents, trained on real public
/// decklist trends — a second, independent synergy signal alongside EDHREC.
/// </summary>
public class RecommanderRecommendation
{
    public string OracleId { get; set; } = "";
    public string Name { get; set; } = "";
    public double Score { get; set; }
}

public class RecommendQueryDto
{
    [JsonPropertyName("card_format")] public string CardFormat { get; set; } = "name";
    [JsonPropertyName("commander")] public string Commander { get; set; } = "";
    [JsonPropertyName("partner")] public string? Partner { get; set; }
    [JsonPropertyName("deck")] public List<string> Deck { get; set; } = new();
}

public class CardRecommendationDto
{
    [JsonPropertyName("oracle_id")] public string OracleId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("score")] public double Score { get; set; }
}

public class RecommendResultDto
{
    [JsonPropertyName("recommendations")] public List<CardRecommendationDto> Recommendations { get; set; } = new();
}

public class ApiResultDto
{
    [JsonPropertyName("result_code")] public string ResultCode { get; set; } = "";
    [JsonPropertyName("data")] public RecommendResultDto? Data { get; set; }
    [JsonPropertyName("error")] public ApiErrorDto? Error { get; set; }
}

public class ApiErrorDto
{
    [JsonPropertyName("messages")] public List<string>? Messages { get; set; }
}
