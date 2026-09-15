using System.Text.Json;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

public class DeckCheckClient
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://deckcheck.co/api/external";

    public DeckCheckClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckDoctor/1.0 (personal Commander deck analysis tool)");
    }

    /// <summary>
    /// Fetches CRISPI/bracket data for a deck that already exists on DeckCheck. Accepts either a
    /// bare deck ID or a full DeckCheck URL — figures out which parameter to send based on
    /// whether the input looks like a URL.
    /// </summary>
    public async Task<DeckCheckSummary> GetDeckAsync(string deckIdOrUrl)
    {
        var isUrl = deckIdOrUrl.Contains("://") || deckIdOrUrl.Contains("deckcheck.co");
        var param = isUrl ? $"deck_url={Uri.EscapeDataString(deckIdOrUrl)}" : $"deck_id={Uri.EscapeDataString(deckIdOrUrl)}";
        var url = $"{BaseUrl}/deck?{param}";

        var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            var errMsg = body;
            try
            {
                var errDto = JsonSerializer.Deserialize<DeckCheckErrorDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (errDto?.Error != null) errMsg = errDto.Error;
            }
            catch { /* fall back to raw body if it's not the documented error shape */ }

            var hint = resp.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound => "Deck not found — double check the deck ID/URL is a DeckCheck link, not an Archidekt one.",
                System.Net.HttpStatusCode.Forbidden => "Deck is private — make it public on DeckCheck, or check you're using the right share link.",
                _ => "",
            };
            throw new HttpRequestException($"DeckCheck returned HTTP {(int)resp.StatusCode}: {errMsg}. {hint}");
        }

        var dto = JsonSerializer.Deserialize<DeckCheckDeckDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new DeckCheckDeckDto();

        if (dto.Format != "commander" && !string.IsNullOrEmpty(dto.Format))
            Console.WriteLine($"  Note: DeckCheck reports this deck's format as '{dto.Format}', not 'commander' — bracket/CRISPI fields will be null per their docs (Commander-only).");

        return new DeckCheckSummary
        {
            Name = dto.Name,
            Bracket = dto.Bracket,
            Crispi = dto.Crispi == null ? null : new DeckCheckCrispi
            {
                Overall = dto.Crispi.Overall,
                Consistency = dto.Crispi.Consistency,
                Resilience = dto.Crispi.Resilience,
                Interaction = dto.Crispi.Interaction,
                Speed = dto.Crispi.Speed,
            },
            FullAnalysis = dto.FullAnalysis,
            PublicUrl = dto.PublicUrl,
        };
    }
}
