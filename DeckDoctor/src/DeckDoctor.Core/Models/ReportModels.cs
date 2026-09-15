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
    /// Raw oTag counts across the deck, most common first — NOT an archetype classification.
    /// Deliberately not turned into a tag-combination-to-archetype lookup table in C#: same
    /// reason commandersalt.com avoids "Assassin's Trophy is removal"-style hardcoding — a table
    /// built from one deck's tag sample would be guessing at coverage that can't be validated,
    /// and it'd go stale as new cards/tags appear. Naming the actual archetype from this histogram
    /// (and researching real strategy-appropriate finishers for it) is a judgment call for the
    /// narrative layer (Claude Code), not something to bake into the deterministic pipeline.
    /// </summary>
    public List<TagCount> DominantTags { get; set; } = new();

    /// <summary>
    /// The commander's own oTags, verbatim — not filtered, not aggregated. Confirmed real gap
    /// (2026-09-06): Council of Four's own tags include "repeatable creature tokens", but that
    /// never surfaced in DominantTags' aggregate top-15 because it's rare across the other 75
    /// cards, drowned out by sheer draw-card volume. The commander is the one card always
    /// present and usually the actual build-around piece — its tags deserve to be seen on their
    /// own, not diluted into one data point among many.
    /// </summary>
    public Dictionary<string, List<string>> CommanderTags { get; set; } = new();

    /// <summary>
    /// Deterministic bracket-relevant facts, computed from fields Archidekt already provides
    /// per card (gameChanger/tutor/extraTurns/massLandDenial — confirmed real fields, 2026-08-31)
    /// plus EDHREC's own "gamechangers" category where available. This is data, not judgment —
    /// the narrative layer (Claude Code) still does the actual bracket reasoning against it.
    /// </summary>
    public DeckBracketSignals BracketSignals { get; set; } = new();

    /// <summary>Populated only if a DeckCheck deck ID/URL was provided via --deckcheck — null
    /// otherwise. Requires the deck to already exist on DeckCheck's own platform, separately from
    /// Archidekt (see README on why this is a real workflow catch, not just an API detail).</summary>
    public DeckCheckSummary? DeckCheckAnalysis { get; set; }

    /// <summary>A second, independent synergy signal alongside EDHREC — from Recommander's public API.</summary>
    public List<DeckReportSuggestion> RecommanderSuggestions { get; set; } = new();
}

public record TagCount(string Tag, int Count);

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
