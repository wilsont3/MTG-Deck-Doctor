using System.Text;
using System.Text.Json;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

/// <summary>
/// Combo detection via Commander Spellbook's real REST API (confirmed via their schema listing:
/// find-my-combos, estimate-bracket, variants, cards, etc. — a proper Django REST backend with
/// an OpenAPI schema, not a reverse-engineered endpoint). UNVERIFIED against a live response
/// though — see the note on CommanderSpellbookModels.cs. This is the one integration in the
/// whole project that hasn't been tested against real data at all; treat the first real run as
/// the actual verification step.
/// </summary>
public class CommanderSpellbookClient
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://backend.commanderspellbook.com";

    public CommanderSpellbookClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckDoctor/1.0 (personal Commander deck analysis tool)");
    }

    /// <summary>
    /// Resolve card names to Commander Spellbook's own numeric card IDs (their find-my-combos
    /// endpoint takes IDs, not names, per the documented MCP wrapper). Best-effort: queries
    /// /cards?search=<name> per card since a confirmed bulk-lookup shape isn't known. This means
    /// one HTTP call per card — fine for a ~100-card deck, worth batching/caching if ever pointed
    /// at a full collection (see note in Program.cs on why we don't do that here).
    /// </summary>
    public async Task<Dictionary<string, int>> ResolveCardIdsAsync(IEnumerable<string> names)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var url = $"{BaseUrl}/cards?search={Uri.EscapeDataString(name)}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) continue;
                var body = await resp.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<CsbCardSearchResponseDto>(body, JsonOpts());
                var match = parsed?.Results.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? parsed?.Results.FirstOrDefault(); // fall back to best guess if exact match not found
                if (match != null) result[name] = match.Id;
            }
            catch
            {
                // Best-effort — a card Commander Spellbook doesn't know about (or a request shape
                // mismatch) shouldn't kill the whole lookup. Missing cards just can't be checked
                // for combos, which is a reasonable degradation.
            }
        }
        return result;
    }

    /// <summary>
    /// Find combos fully satisfied by the given set of card IDs. NOTE: does not attempt to parse
    /// "almost included"/partial-match data from this response even though the underlying
    /// endpoint reportedly supports it — the response shape for that is unconfirmed. Instead,
    /// "combos you're close to" is computed client-side in Program.cs by calling this twice (deck
    /// alone, then deck+collection) and diffing which combos newly become fully satisfied — this
    /// only requires trusting the "fully satisfied" shape, not guessing at partial-match fields.
    /// </summary>
    public async Task<List<CommanderSpellbookVariant>> FindCombosAsync(IEnumerable<int> cardIds)
    {
        var payload = JsonSerializer.Serialize(new { cards = cardIds.ToList() });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/find-my-combos")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Commander Spellbook /find-my-combos returned HTTP {(int)resp.StatusCode}: {body[..Math.Min(300, body.Length)]}. " +
                $"This is the least-verified integration in the project — if this fails, the request shape " +
                $"(currently POST with a JSON body {{\"cards\": [...]}}) is the first thing to check against " +
                $"whatever the real API actually expects.");

        var parsed = JsonSerializer.Deserialize<CsbFindCombosResponseDto>(body, JsonOpts()) ?? new CsbFindCombosResponseDto();
        var variantDtos = parsed.Included ?? parsed.Results ?? new List<CsbVariantDto>();

        return variantDtos.Select(v => new CommanderSpellbookVariant
        {
            Id = v.Id,
            CardNames = (v.Uses ?? v.Cards ?? new List<CsbVariantCardRefDto>())
                .Select(c => c.Card?.Name).Where(n => n != null).Select(n => n!).ToList(),
            ColorIdentity = (v.Identity ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            HasBannedCard = v.BannedCard ?? false,
            HasSpoiledCard = v.PreviewedCard ?? false,
            Results = (v.Produces ?? new List<CsbProducesDto>()).Select(p => p.Feature?.Name).Where(n => n != null).Select(n => n!).ToList(),
        }).ToList();
    }

    private static JsonSerializerOptions JsonOpts() => new() { PropertyNameCaseInsensitive = true };
}
