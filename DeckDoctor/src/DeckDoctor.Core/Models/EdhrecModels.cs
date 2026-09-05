using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

public class EdhrecCommanderPage
{
    public string CommanderName { get; set; } = "";
    public List<EdhrecCardEntry> Cards { get; set; } = new();

    /// <summary>Cards EDHREC itself categorizes as Game Changers for this commander's page.</summary>
    public List<string> GameChangerNames =>
        Cards.Where(c => c.Category == "gamechangers").Select(c => c.Name).ToList();
}

public class EdhrecCardEntry
{
    public string Name { get; set; } = "";
    public double SynergyScore { get; set; }
    public string? Category { get; set; }
    public int? NumDecks { get; set; }
    public int? PotentialDecks { get; set; }

    /// <summary>What fraction of decks that could run this card actually do — a real popularity signal, separate from synergy.</summary>
    public double? InclusionRate => (NumDecks.HasValue && PotentialDecks is > 0)
        ? (double)NumDecks.Value / PotentialDecks.Value
        : null;
}

/// <summary>
/// json.edhrec.com/pages/commanders/{slug}.json — confirmed against a real response on
/// 2026-08-31. Cards live under container.json_dict.cardlists[], each list tagged by category
/// (e.g. "topcards", "gamechangers", "creatures", "highsynergycards"), each with a cardviews[]
/// array carrying name/synergy/num_decks/potential_decks.
/// </summary>
public class EdhrecPageDto
{
    [JsonPropertyName("container")] public EdhrecContainerDto? Container { get; set; }
}

public class EdhrecContainerDto
{
    [JsonPropertyName("json_dict")] public EdhrecJsonDictDto? JsonDict { get; set; }
}

public class EdhrecJsonDictDto
{
    [JsonPropertyName("cardlists")] public List<EdhrecCardListDto>? CardLists { get; set; }
}

public class EdhrecCardListDto
{
    [JsonPropertyName("tag")] public string? Tag { get; set; }
    [JsonPropertyName("cardviews")] public List<EdhrecCardViewDto>? CardViews { get; set; }
}

public class EdhrecCardViewDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("synergy")] public double? Synergy { get; set; }
    [JsonPropertyName("num_decks")] public int? NumDecks { get; set; }
    [JsonPropertyName("potential_decks")] public int? PotentialDecks { get; set; }
}
