using System.Text;
using System.Text.Json;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

/// <summary>
/// Combo detection via Commander Spellbook's real REST API. Request/response shapes confirmed
/// against the official generated client (@space-cow-media/spellbook-client v6.3.3 from npm) —
/// see CommanderSpellbookModels.cs for details. This replaces an earlier version that resolved
/// card names to Commander Spellbook's internal numeric IDs one HTTP call at a time, which is
/// what caused 429 Too Many Requests on a 77-card deck — that whole step is gone now, since the
/// real API takes card names directly.
/// </summary>
public class CommanderSpellbookClient
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://backend.commanderspellbook.com";

    public CommanderSpellbookClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        // Confirmed guidance from the client's own doc comment: name your tool in the User-Agent,
        // and expect/handle 429s even at reasonable usage (~80 req/min limit).
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckDoctor/1.0 (personal Commander deck analysis tool)");
    }

    /// <summary>
    /// Finds combos for a deck, given card names directly — no ID resolution step. Returns both
    /// combos already fully in the deck (Included) and combos missing only a small number of
    /// cards (AlmostIncluded, as defined by Commander Spellbook's own server-side logic) in a
    /// single request.
    /// </summary>
    public async Task<(List<CommanderSpellbookVariant> Included, List<CommanderSpellbookVariant> AlmostIncluded)> FindCombosAsync(
        IEnumerable<string> mainDeckCardNames,
        IEnumerable<string> commanderNames)
    {
        var request = new DeckRequestDto
        {
            Main = mainDeckCardNames.Select(n => new CardInDeckRequestDto(n)).ToList(),
            Commanders = commanderNames.Select(n => new CardInDeckRequestDto(n)).ToList(),
        };
        var payload = JsonSerializer.Serialize(request);

        HttpResponseMessage resp;
        int attempt = 0;
        while (true)
        {
            attempt++;
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/find-my-combos")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            resp = await _http.SendAsync(req);

            if (resp.StatusCode != System.Net.HttpStatusCode.TooManyRequests || attempt >= 3)
                break;

            // Confirmed real possibility per their own docs (~80 req/min limit) — back off and
            // retry once or twice rather than failing immediately on a single 429.
            var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * attempt);
            await Task.Delay(retryAfter);
        }

        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Commander Spellbook /find-my-combos returned HTTP {(int)resp.StatusCode} after {attempt} attempt(s): {body[..Math.Min(300, body.Length)]}");

        var parsed = JsonSerializer.Deserialize<CsbFindMyCombosResponseDto>(body, JsonOpts());
        var results = parsed?.Results ?? new CsbFindMyCombosResultsDto();

        return (results.Included.Select(ToModel).ToList(), results.AlmostIncluded.Select(ToModel).ToList());
    }

    private static CommanderSpellbookVariant ToModel(CsbVariantDto v) => new()
    {
        Id = v.Id,
        CardNames = (v.Uses ?? new()).Select(u => u.Card?.Name).Where(n => n != null).Select(n => n!).ToList(),
        ColorIdentity = v.Identity ?? "",
        Results = (v.Produces ?? new()).Select(p => p.Feature?.Name).Where(n => n != null).Select(n => n!).ToList(),
        Description = v.Description,
    };

    private static JsonSerializerOptions JsonOpts() => new() { PropertyNameCaseInsensitive = true };
}
