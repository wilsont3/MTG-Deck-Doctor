using System.Text.Json.Serialization;

namespace DeckDoctor.Core.Models;

/// <summary>
/// Confirmed directly against https://deckcheck.co/docs/api (fetched in full — real, public,
/// documented API, no bot-blocking). Note the real workflow catch: this endpoint takes a
/// DeckCheck-native deck ID/URL, NOT an Archidekt URL — the deck has to already exist on
/// DeckCheck's platform. Requires manually maintaining the deck on both Archidekt and DeckCheck.
/// </summary>
public class DeckCheckSummary
{
    public string Name { get; set; } = "";
    public int? Bracket { get; set; }
    public DeckCheckCrispi? Crispi { get; set; }
    public string? FullAnalysis { get; set; }
    public string PublicUrl { get; set; } = "";
}

public class DeckCheckCrispi
{
    public double Overall { get; set; }
    public double Consistency { get; set; }
    public double Resilience { get; set; }
    public double Interaction { get; set; }
    public double Speed { get; set; }
}

public class DeckCheckCrispiDto
{
    [JsonPropertyName("overall")] public double Overall { get; set; }
    [JsonPropertyName("consistency")] public double Consistency { get; set; }
    [JsonPropertyName("resilience")] public double Resilience { get; set; }
    [JsonPropertyName("interaction")] public double Interaction { get; set; }
    [JsonPropertyName("speed")] public double Speed { get; set; }
}

public class DeckCheckDeckDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("bracket")] public int? Bracket { get; set; }
    [JsonPropertyName("crispi")] public DeckCheckCrispiDto? Crispi { get; set; }
    [JsonPropertyName("fullAnalysis")] public string? FullAnalysis { get; set; }
    [JsonPropertyName("publicUrl")] public string PublicUrl { get; set; } = "";
    [JsonPropertyName("format")] public string Format { get; set; } = "";
}

public class DeckCheckErrorDto
{
    [JsonPropertyName("error")] public string Error { get; set; } = "";
}
