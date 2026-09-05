namespace DeckDoctor.Core.Models;

/// <summary>
/// The full structured output written to report.json (Section 7.3 / 8.3 of the plan) — this is
/// what gets handed to Claude Code for the narrative audit pass. Deliberately includes owned
/// flags already resolved here, so the LLM step doesn't have to re-derive ownership.
/// </summary>
public class DeckReport
{
    public string DeckName { get; set; } = "";
    public List<string> Commanders { get; set; } = new();
    public List<string> ColorIdentity { get; set; } = new();

    public List<DeckReportCard> DeckList { get; set; } = new();

    public Dictionary<string, DeckReportWeakness> Weaknesses { get; set; } = new();

    public List<DeckReportSuggestion> Suggestions { get; set; } = new();

    /// <summary>Cards that couldn't be resolved on Scryfall (name mismatches, unusual printings, etc.) — excluded from tag-based analysis.</summary>
    public List<string> NotFoundOnScryfall { get; set; } = new();

    public List<DeckReportCombo> CombosInDeck { get; set; } = new();
    public List<DeckReportCombo> CombosOneStepAwayInCollection { get; set; } = new();

    /// <summary>
    /// Deterministic bracket-relevant facts, computed from fields Archidekt already provides
    /// per card (gameChanger/tutor/extraTurns/massLandDenial — confirmed real fields, 2026-08-31)
    /// plus EDHREC's own "gamechangers" category where available. This is data, not judgment —
    /// the narrative layer (Claude Code) still does the actual bracket reasoning against it.
    /// </summary>
    public DeckBracketSignals BracketSignals { get; set; } = new();
}

public class DeckBracketSignals
{
    public List<string> GameChangerCards { get; set; } = new();
    public int TutorCount { get; set; }
    public List<string> ExtraTurnsCards { get; set; } = new();
    public List<string> MassLandDenialCards { get; set; } = new();
    /// <summary>Cards EDHREC's own commander page tags as gamechangers for this specific commander (may overlap with GameChangerCards, may not — EDHREC's list is commander-specific and can differ).</summary>
    public List<string> EdhrecGameChangerNames { get; set; } = new();
}

public class DeckReportCombo
{
    public string Permalink { get; set; } = "";
    public List<string> CardNames { get; set; } = new();
    public List<string> Results { get; set; } = new();
    /// <summary>Empty for combos already fully in the deck. Populated (from your on-color collection) for combos you're one or more owned cards away from completing.</summary>
    public List<string> MissingCardsYouOwn { get; set; } = new();
}

public class DeckReportCard
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string? TypeLine { get; set; }
    public string? ManaCost { get; set; }
    public string? OracleText { get; set; } // null/absent if Scryfall lookup failed for this card
    public List<string> OTags { get; set; } = new();
}

public class DeckReportWeakness
{
    public int Count { get; set; }
    public string Level { get; set; } = ""; // "Ok" | "Low"
}

public class DeckReportSuggestion
{
    public string CardName { get; set; } = "";
    public string Tag { get; set; } = "";
    public double Score { get; set; }
    public bool Owned { get; set; }
    public string? OracleText { get; set; }
}
