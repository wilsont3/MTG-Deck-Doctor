using System.Text.Json;
using System.Text.RegularExpressions;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

public class EdhrecClient
{
    private readonly HttpClient _http;

    public EdhrecClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckDoctor/1.0 (personal Commander deck analysis tool)");
    }

    /// <summary>Normalize a commander's name to EDHREC's URL slug: lowercase, no symbols, dashes for spaces.</summary>
    public static string ToSlug(string commanderName)
    {
        var noSymbols = Regex.Replace(commanderName.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
        var dashed = Regex.Replace(noSymbols.Trim(), @"\s+", "-");
        return dashed;
    }

    /// <summary>
    /// Fetches https://json.edhrec.com/pages/commanders/{slug}.json. Shape confirmed against a
    /// real response on 2026-08-31 (see EdhrecModels.cs) — no longer a guess.
    /// </summary>
    public async Task<EdhrecCommanderPage> FetchCommanderPageAsync(string commanderName)
    {
        var slug = ToSlug(commanderName);
        var url = $"https://json.edhrec.com/pages/commanders/{slug}.json";
        var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"EDHREC returned HTTP {(int)resp.StatusCode} for slug '{slug}' (from '{commanderName}'). Body: {body[..Math.Min(300, body.Length)]}");

        var page = new EdhrecCommanderPage { CommanderName = commanderName };
        try
        {
            var dto = JsonSerializer.Deserialize<EdhrecPageDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var cardLists = dto?.Container?.JsonDict?.CardLists ?? new List<EdhrecCardListDto>();
            foreach (var list in cardLists)
            {
                foreach (var cv in list.CardViews ?? new List<EdhrecCardViewDto>())
                {
                    page.Cards.Add(new EdhrecCardEntry
                    {
                        Name = cv.Name,
                        SynergyScore = cv.Synergy ?? 0,
                        Category = list.Tag,
                        NumDecks = cv.NumDecks,
                        PotentialDecks = cv.PotentialDecks,
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"Got a 200 from EDHREC but the shape didn't match EdhrecPageDto ({ex.Message}). " +
                "This shape was previously confirmed against a real response, so this more likely " +
                "means EDHREC changed something server-side than a stale assumption on my part — " +
                "worth diffing a fresh response against EdhrecModels.cs either way.", ex);
        }
        return page;
    }
}
