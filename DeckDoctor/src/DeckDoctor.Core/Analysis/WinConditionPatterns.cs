using System.Text.RegularExpressions;

namespace DeckDoctor.Core.Analysis;

/// <summary>
/// Detects win-condition-relevant mechanisms by matching oracle TEXT patterns, not tags — because
/// (confirmed 2026-08-31) neither Scryfall's oracle tag vocabulary nor EDHREC's category list has
/// anything resembling "this closes the game" as a concept. Tags describe mechanisms ("extra
/// combat", "gives flying") but not the higher-level idea of "this is how you win."
///
/// This list is a starting point built from patterns actually discussed for Council of Four
/// across this project (Moonshaker Cavalry's pump+evasion ETB, Approach of the Second Sun's
/// explicit win text, Psychosis Crawler's draw-triggered damage) — treat it as a hypothesis to
/// expand as it gets tested against more decks, not a finished taxonomy. False negatives (a real
/// win-con this misses) are much more likely than false positives right now.
///
/// FIXED (2026-08-31, real false positives caught against Council of Four): the evasion pattern
/// originally included "indestructible" alongside flying/trample/menace, and accepted any +X/+X
/// as sufficient — this wrongly matched Make a Stand (+1/+0 and indestructible, a combat trick)
/// and Rootborn Defenses (indestructible only, pure protection). Protection is not evasion, and a
/// trivial pump isn't a closer. Fixed by requiring an actual evasion keyword.
///
/// STILL UNRESOLVED — genuinely hard, not a bug: magnitude/speed isn't assessed at all. The
/// "direct damage/drain" pattern correctly matches both Psychosis Crawler (a real, if slow, win
/// condition established across this whole project) AND Blind Obedience's Extort (1 life per
/// spell cast — much more marginal). Distinguishing "this incremental effect matters" from "this
/// one barely does" needs judgment about the specific deck's shape, not a regex — this is exactly
/// what the narrative layer (Claude Code reading report.json) is for. Worth noting: even
/// commandersalt.com's much more sophisticated line-by-line rules parser doesn't have a clean
/// "wincon" category either (checked real API responses for Sheoldred and Kenrith — both came
/// back with empty categories.categories, only populated with archetype tags like "combat",
/// "reanimator", "stompy" via categories.stats) — this really is an unsolved categorization
/// problem in the wider MTG tooling space, not a gap specific to this implementation.
/// </summary>
public static class WinConditionPatterns
{
    public record Pattern(string Label, Regex Regex);

    public static readonly List<Pattern> All = new()
    {
        new("explicit win text", new Regex(@"\byou win the game\b", RegexOptions.IgnoreCase)),
        new("extra combat", new Regex(@"\b(an?\s+)?(extra|additional)\s+combat\s+phase\b", RegexOptions.IgnoreCase)),
        new("mass evasion ETB (real closer)", new Regex(@"creatures you control (get|gain).*(flying|trample|menace|horsemanship|can't be blocked|unblockable)", RegexOptions.IgnoreCase)),
        new("direct damage/drain to opponents", new Regex(@"(each opponent|target (player|opponent)) loses \d+ life|deals? \d+ damage to (each opponent|that player|target player)", RegexOptions.IgnoreCase)),
        new("draw-triggered damage payoff", new Regex(@"whenever you draw a card, .*(damage|loses? life)", RegexOptions.IgnoreCase)),
        new("alternate win condition (poison/mill)", new Regex(@"(poison counter|mills? (a player|target player|that player) their (entire )?library)", RegexOptions.IgnoreCase)),
        new("damage doubler", new Regex(@"if .*would deal damage.*instead", RegexOptions.IgnoreCase)),
    };

    public static bool Matches(string? oracleText)
    {
        if (string.IsNullOrWhiteSpace(oracleText)) return false;
        return All.Any(p => p.Regex.IsMatch(oracleText));
    }

    public static List<string> MatchedLabels(string? oracleText)
    {
        if (string.IsNullOrWhiteSpace(oracleText)) return new List<string>();
        return All.Where(p => p.Regex.IsMatch(oracleText)).Select(p => p.Label).ToList();
    }
}
