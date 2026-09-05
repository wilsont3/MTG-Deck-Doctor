Deck Doctor – Implementation Plan (C# / .NET)
A structured, end‑to‑end plan for implementing the Deck Doctor application using:

Archidekt deck ingestion

Scryfall card + otag enrichment

EDHREC commander synergy data

Local collection CSV

Tag‑based deck analysis

Upgrade suggestions

1. Project Structure
Code
DeckDoctor/
  src/
    DeckDoctor.Core/
      Models/
      Services/
      Analysis/
    DeckDoctor.Cli/
      Program.cs
  data/
    collection.csv
  docs/
    IMPLEMENTATION_PLAN.md
Projects
DeckDoctor.Core  
All logic: API clients, DTOs, tag processing, analysis, suggestion engine.

DeckDoctor.Cli  
Console interface for running the analyzer.

2. Core Models to Implement
2.1. Archidekt Models
ArchidektDeck

ArchidektDeckCard

DTOs matching Archidekt JSON structure

2.2. Scryfall Models
ScryfallCard

ScryfallCollectionResponse

OracleTagRecord (from bulk tag file)

2.3. EDHREC Models
EdhrecCommanderPage

EdhrecCardEntry

EdhrecSynergyRecord

2.4. Collection Models
CollectionCard  
(CSV fields: Quantity, Name, Scryfall ID, Mana Value, Identities, Mana Cost)

3. Services to Implement
3.1. ArchidektClient
Responsibilities:

Extract deck ID from URL

Fetch deck JSON from https://archidekt.com/api/decks/{id}/

Map JSON → ArchidektDeck

3.2. ScryfallClient
Responsibilities:

Fetch card details via /cards/collection

Load Scryfall bulk data via /bulk-data

Parse oracle tag bulk file (JSONL)

Build dictionary: oracleId → otags

Merge otags into ScryfallCard

Note: a prior browser-based prototype of this project hit hard CSP/sandbox blocks calling Scryfall,
Archidekt, and EDHREC directly from a Claude Artifact (confirmed via a `TypeError: Failed to fetch`
with no HTTP response ever reached — the signature of a sandbox block, not a rejection from those
sites). That restriction is specific to the Artifact browser sandbox and doesn't apply to a normal
.NET HttpClient running locally — this should just work. Worth confirming early in Phase 2 anyway
rather than assuming, since Scryfall's own bot-detection was separately observed blocking an
automated fetch tool during that prototyping — likely an infrastructure-fingerprint issue specific
to that tool, not something a normal home-network request should trigger, but cheap to verify first.

3.3. EdhrecClient
Responsibilities:

Normalize commander name → EDHREC slug

Fetch commander JSON from
https://json.edhrec.com/pages/commanders/{slug}.json

Extract:

Top cards

Synergy scores

Categories

3.4. CollectionRepository
Responsibilities:

Load CSV using CsvHelper

Map rows → CollectionCard

Fetch Scryfall data for each card

Attach otags

4. Tagging System (OTag Strategy)
4.1. OTag Loading
Download Scryfall bulk tag file (oracle_tags type, from the /bulk-data endpoint)

Parse JSONL lines

Build dictionary:

Code
Dictionary<string, string[]> OTagsByOracleId
Join key is oracle_id, NOT card name — card names aren't unique across reprints/double-faced
cards, and oracle_id is the stable identifier both the Oracle Cards bulk file and the Oracle Tags
bulk file use to link a tagging to a card. Confirmed against Scryfall's own Tags API docs.

Note: the tag bulk file is only regenerated every 12-24 hours (per Scryfall's docs) and there's no
per-card tag endpoint — this has to be a batch download-and-cache step on a schedule, not something
fetched per-request during analysis.

4.2. Card Tag Attachment
When loading Scryfall cards:

Look up oracle ID

Attach OTags array to ScryfallCard

4.3. Tag Categories
Useful tags include:

ramp

draw

removal

wincon

token-maker

politics

treasure

counterspell

flicker

stax

combo-piece

5. Deck Analysis Module
5.1. DeckTagProfile
Aggregate otags across all deck cards

Count tags

Provide helper methods:

GetCount(tag)

TopTags(n)

TagHistogram()

5.2. DeckWeaknessReport
Thresholds (tunable):

Ramp < 10 → NeedsRamp

Draw < 8 → NeedsDraw

Removal < 8 → NeedsRemoval

Wincons < 5 → NeedsWincons

5.3. DeckAnalyzer
Responsibilities:

Build tag profile

Compare tag counts to thresholds

Produce DeckWeaknessReport

6. Suggestion Engine
6.1. Candidate Filtering
For each weakness:

Filter collection cards by otag

e.g., c.OTags.Contains("ramp")

6.2. Scoring Algorithm
Combine:

EDHREC synergy score

Curve fit (prefer MV 2–4)

Role fit (how badly the deck needs this tag)

Color identity match

Example:

Code
score = 0.6 * synergy + 0.4 * curveFit
6.3. Suggestion Output
Return:

Top N ramp upgrades

Top N draw upgrades

Top N removal upgrades

Top N wincon upgrades

7. Narrative Audit Layer (Claude Code, human-triggered)
7.1. Why this layer exists
Everything through Section 6 is deterministic: tag counts, thresholds, a transparent linear
scoring formula. That's the right approach for "what's missing and what are the candidates" — fast,
reproducible, free, no LLM needed. It won't produce judgment calls that depend on reading the
specific cards and the deck's actual intent, though — e.g. recognizing that a 0/8 commander is a
deliberate political stall rather than a weak body, that two win conditions are redundant only if
both are actually *fast*, or that an off-turn draw trigger chain (Curse of Verbosity feeding
Psychosis Crawler) matters more than either card looks like alone. That synthesis is what an LLM
step adds on top of the mechanical analysis, not a replacement for it.

7.2. How to keep this free
Claude Code, used interactively (terminal or IDE, a human in the loop), draws on the same
subscription usage pool as regular Claude.ai chats — no separate charge on a Pro/Max/Team plan.
Programmatic/headless invocation (the Claude Agent SDK, `claude -p` called from inside another
program) is billed separately as of June 2026. The distinction that matters here: DeckAnalyzer.Cli
should NOT shell out to `claude` itself as an automated pipeline step. Instead:

- Cli's final step writes the structured weakness report + ranked candidates to a file
  (`report.json` or similar) and exits.
- The user then opens Claude Code interactively in the DeckDoctor repo and asks it to read that
  file and produce the narrative audit (verdict, redundancy read, bracket reasoning, etc.).
- This keeps 100% of the LLM usage on the free/subscription-covered interactive path. If a fully
  automated hands-off pipeline is wanted later, that's a deliberate future trade of convenience for
  cost, not something to default into.

7.3. What the LLM step should receive
Feed it the DeckWeaknessReport, the ranked candidate list per weakness (with owned/needs-acquiring
flags already resolved deterministically by the C# side — don't make the LLM re-derive ownership),
and the raw deck list with oracle text attached. Explicitly prompt for: current win condition(s)
labeled fast vs. slow, redundancy given that distinction, and a bracket read tied to specific cards
— mirroring the audit framework already validated in the `mtg-commander-wincon-audit` Claude skill.

8. CLI Application
8.1. Input
Archidekt deck URL

8.2. Pipeline
Extract deck ID

Fetch Archidekt deck

Fetch Scryfall cards

Load otags

Fetch EDHREC commander data

Load collection CSV

Analyze deck

Suggest upgrades

Print report AND write structured report.json (see Section 7.3) for the narrative layer

8.3. Output Format
Example (console summary):

Code
Deck: The Council of Four
Commander: The Council of Four

Weaknesses:
- Ramp: LOW (6)
- Draw: OK (9)
- Removal: LOW (4)
- Wincons: LOW (2)

Suggested Upgrades:
- [Ramp] Cultivate (score 0.82)
- [Ramp] Nature's Lore (score 0.79)
- [Wincon] Approach of the Second Sun (score 0.75)
- [Removal] Generous Gift (score 0.72)

Full detail also written to report.json — hand this to Claude Code for the narrative audit pass.

9. Implementation Order (Checklist)
Phase 1 — Foundation
[ ] Create project structure

[ ] Add Models folder

[ ] Add Services folder

[ ] Add Analysis folder

Phase 2 — Data Ingestion
[ ] Implement ArchidektClient

[ ] Implement ScryfallClient (cards only)

[ ] Test fetching deck + cards

Phase 3 — OTag Integration
[ ] Load Scryfall bulk tag file

[ ] Parse JSONL

[ ] Build otag dictionary

[ ] Attach otags to ScryfallCard

Phase 4 — EDHREC Integration
[ ] Implement EdhrecClient

[ ] Extract synergy + categories

Phase 5 — Collection Integration
[ ] Load CSV

[ ] Fetch Scryfall data for collection

[ ] Attach otags

Phase 6 — Analysis
[ ] Implement DeckTagProfile

[ ] Implement DeckAnalyzer

[ ] Implement DeckWeaknessReport

Phase 7 — Suggestions
[ ] Implement SuggestionEngine

[ ] Implement scoring algorithm

[ ] Produce ranked suggestions

Phase 8 — CLI
[ ] Wire all services together

[ ] Print deck profile

[ ] Print weaknesses

[ ] Print upgrade suggestions

[ ] Write structured report.json (weakness report + ranked candidates + owned flags + oracle text)

Phase 9 — Narrative Audit Layer
[ ] Confirm report.json shape is easy for Claude Code to consume in one read

[ ] Draft the audit prompt (adapt from the mtg-commander-wincon-audit skill: fast-vs-slow win
    conditions, redundancy, bracket read tied to specific cards)

[ ] Do a manual test run: open Claude Code interactively in the repo, point it at report.json,
    confirm the output quality is worth keeping as a standing step

[ ] (Explicitly deferred) Automated/headless invocation — only revisit if manual triggering becomes
    a real friction point, since it moves usage off the subscription-covered path

10. Future Enhancements (Optional)
Web UI (Blazor or ASP.NET Core)

Export reports to Markdown

Compare your deck to top Archidekt lists

Commander‑specific heuristics

Combo detection via otags

Mana base optimization