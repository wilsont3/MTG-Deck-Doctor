using System.Text.Json;
using System.Text.RegularExpressions;
using DeckDoctor.Core.Models;

namespace DeckDoctor.Core.Services;

public class ArchidektClient
{
    private readonly HttpClient _http;

    public ArchidektClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckDoctor/1.0 (personal Commander deck analysis tool)");
    }

    public static string? ExtractDeckId(string urlOrId)
    {
        var match = Regex.Match(urlOrId, @"archidekt\.com/decks/(\d+)");
        if (match.Success) return match.Groups[1].Value;
        return Regex.IsMatch(urlOrId.Trim(), @"^\d+$") ? urlOrId.Trim() : null;
    }

    public async Task<ArchidektDeck> FetchDeckAsync(string deckIdOrUrl)
    {
        var id = ExtractDeckId(deckIdOrUrl)
            ?? throw new ArgumentException($"Couldn't find a deck ID in '{deckIdOrUrl}'.");

        var url = $"https://archidekt.com/api/decks/{id}/";
        var resp = await _http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Archidekt returned HTTP {(int)resp.StatusCode} for deck {id}. " +
                $"Body (first 300 chars): {body[..Math.Min(300, body.Length)]}");

        // Shape confirmed against a real Archidekt response on 2026-08-31 — this DTO is no
        // longer a guess. If deserialization ever comes back empty, it's more likely Archidekt
        // changed their API than a mismatch in this mapping.
        ArchidektDeckDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ArchidektDeckDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                $"Got a 200 from Archidekt but couldn't parse the expected shape: {ex.Message}. " +
                $"This DTO was verified against a real response before, so this likely means " +
                $"Archidekt changed something — worth diffing a fresh response against ArchidektModels.cs.", ex);
        }

        if (dto == null) throw new JsonException("Archidekt response parsed to null.");

        var deck = new ArchidektDeck { Id = dto.Id, Name = dto.Name };
        foreach (var entry in dto.Cards)
        {
            var oc = entry.Card?.OracleCard;
            if (oc == null || string.IsNullOrWhiteSpace(oc.Name)) continue;
            deck.Cards.Add(new ArchidektDeckCard
            {
                Name = oc.Name,
                Quantity = entry.Quantity,
                Categories = entry.Categories ?? new List<string>(),
                ManaCost = oc.ManaCost,
                Cmc = oc.Cmc,
                OracleText = oc.Text,
                ColorIdentity = oc.ColorIdentity ?? new List<string>(),
                OTags = oc.OTags ?? new List<string>(),
                GameChanger = oc.GameChanger,
                IsTutor = oc.Tutor,
                ExtraTurns = oc.ExtraTurns,
                MassLandDenial = oc.MassLandDenial,
                SuperTypes = oc.SuperTypes ?? new List<string>(),
                Types = oc.Types ?? new List<string>(),
                SubTypes = oc.SubTypes ?? new List<string>(),
            });
        }
        return deck;
    }
}
