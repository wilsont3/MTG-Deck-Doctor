using System.Text;
using System.Text.Json;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

public class RecommanderClient
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.recommander.cards/public-release";

    public RecommanderClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckDoctor/1.0 (personal Commander deck analysis tool)");
    }

    public async Task<List<RecommanderRecommendation>> FindRecommendationsAsync(
        string commander, string? partner, IEnumerable<string> deckCardNames)
    {
        var query = new RecommendQueryDto
        {
            CardFormat = "name",
            Commander = commander,
            Partner = partner,
            Deck = deckCardNames.ToList(),
        };
        var payload = JsonSerializer.Serialize(query);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/decks/recommend/top")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Recommander returned HTTP {(int)resp.StatusCode}: {body[..Math.Min(300, body.Length)]}");

        var parsed = JsonSerializer.Deserialize<ApiResultDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ApiResultDto();

        if (parsed.ResultCode != "success")
        {
            var messages = parsed.Error?.Messages != null ? string.Join("; ", parsed.Error.Messages) : "no error detail provided";
            throw new InvalidOperationException($"Recommander returned result_code '{parsed.ResultCode}': {messages}");
        }

        return (parsed.Data?.Recommendations ?? new()).Select(r => new RecommanderRecommendation
        {
            OracleId = r.OracleId,
            Name = r.Name,
            Score = r.Score,
        }).ToList();
    }
}
