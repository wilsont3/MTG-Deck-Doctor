# Deck Doctor — build status

Implements Phases 1-8 of `docs/IMPLEMENTATION_PLAN.md`. Built with **zero NuGet dependencies**
(no CsvHelper — a small hand-rolled CSV parser instead) so it builds with no package restore step;
`NuGet.Config` clears package sources so `dotnet build` doesn't even attempt to reach nuget.org.

## Build & run

```
dotnet build
dotnet run --project src/DeckDoctor.Cli -- <archidekt-deck-url> --collection data/collection.csv --out report.json
```

Requires .NET 8 SDK. `data/collection.csv` should be your Archidekt collection export
(Quantity, Name, Scryfall ID, Mana Value, Identities, Mana cost).

## Fixed after real-data testing (2026-08-31, third pass)

Two real bugs from your first live run against actual data:

- **Collection crash on duplicate rows.** Your Archidekt collection export lists one row PER
  OWNED COPY (Quantity=1 each) rather than one row with a summed quantity — confirmed 214 cards
  with 2+ identical rows (e.g. "Aang's Journey" ×2, same Scryfall ID). `CollectionRepository` now
  aggregates by name, summing quantities, instead of crashing on the second occurrence.

- **All weaknesses read as 0/LOW — thresholds were checking tags that don't exist.** The plan
  assumed a flat taxonomy (`"ramp"`, `"draw"`, `"removal"`, `"wincon"` as literal tag names).
  Scryfall's real oracle tags are far more granular — verified on your actual deck: `"pure draw"`,
  `"land ramp"`, `"removal-creature"`, `"tutor-to-hand"`, etc. None of the plan's literal names
  exist verbatim. Thresholds now match by substring against a curated list per category instead.

## Wincons: text patterns, not tags (2026-08-31, fourth pass)

Neither Scryfall's oracle tags nor EDHREC's category list has anything resembling "this closes the
game" as a concept — confirmed by checking EDHREC's own 14 cardlist categories (`newcards`,
`highsynergycards`, `topcards`, `gamechangers`, `creatures`, `instants`, ... `lands`), none of
which fit. So Wincons works differently from the other three categories: `WinConditionPatterns.cs`
matches oracle **text** via regex instead — "you win the game", "extra combat phase", "creatures
you control gain flying/get +X/+X" (mass pump ETBs), draw-triggered damage, poison/mill alt-wincons,
damage doublers.

Verified against Council of Four: this correctly re-identified **Moonshaker Cavalry** (`mass pump +
evasion ETB`) and **Psychosis Crawler** (`direct damage/drain`) — the exact two cards established as
this deck's real win conditions across many turns of manual conversation earlier in this project —
plus found Blind Obedience/Make a Stand/Rootborn Defenses as plausible additional hits. Wincons now
reads **OK (5)**, not LOW.

**What this still can't tell you**: fast vs. slow, redundant vs. not. A raw count crossing a
threshold treats "1 explosive finisher + 4 marginal drips" the same as "5 genuinely fast closers"
— that distinction is exactly what the narrative layer (Claude Code reading `report.json`) is for.
This pattern list is a starting hypothesis to expand as it's tested against more decks, not a
finished taxonomy — false negatives (missing a real win-con) are more likely than false positives
right now. `BracketSignals` (Game Changers/tutors/extra-turns) remains a separate, simpler,
fully-reliable deterministic signal, worth keeping distinct from this pattern list rather than
merged into it.

## Wincons refined against real false positives (2026-08-31, fifth pass)

You caught real false positives: Make a Stand and Rootborn Defenses were matching the evasion
pattern, but they're protection effects, not closers — the pattern originally treated
"indestructible" as equivalent to flying/trample/menace, and accepted any `+X/+X` (including a
trivial `+1/+0`) as a real pump. Fixed by requiring an actual evasion keyword — protection and
tiny static pumps no longer count. Re-verified: Moonshaker Cavalry still matches, both false
positives are gone, Wincons drops from 5 to 3 (still `OK`, right at the threshold).

Looked at commandersalt.com's actual approach for comparison — pulled real API responses for
Sheoldred, the Apocalypse and Kenrith, the Returned King. Worth knowing: **even their much more
sophisticated line-by-line rules parser doesn't have a clean "wincon" category either.** Both cards
came back with `categories.categories: {}` — empty — and only `categories.stats` populated, with
archetype-flavored tags like `combat`, `reanimator`, `stompy`, `plusOnePlusOneCounters`. This is a
genuinely unsolved categorization problem across MTG tooling generally, not a gap specific to this
project's regex approach.

**What's still unfixed, and isn't a bug**: magnitude/speed. "direct damage/drain" correctly matches
both Psychosis Crawler (established as a real, if slow, win condition) and Blind Obedience's Extort
(1 life per spell cast — much more marginal). Telling "this incremental effect actually matters in
this deck" from "this one barely does" needs judgment about the specific deck's shape and pace —
that's what the narrative layer (Claude Code reading `report.json`) is for, not something a regex
should try to fake with more pattern engineering.

## Commander Spellbook combo detection (2026-08-31, sixth pass) — UNTESTED against live data

Added `CommanderSpellbookClient`, using their real, documented REST API (confirmed via their own
schema listing: `find-my-combos`, `estimate-bracket`, `variants`, `cards`, `templates` — a proper
Django backend with an OpenAPI schema, unlike EDHREC's reverse-engineered endpoint).

**Honest status: this is the one integration in the whole project I could not verify against real
data at all.** `backend.commanderspellbook.com` isn't reachable from my sandbox, and direct fetch
is blocked by their `robots.txt` — so unlike Archidekt/Scryfall/EDHREC, there was no "upload the
real response, fix the DTO" cycle here. Every field name in `CommanderSpellbookModels.cs` is a
best-effort guess from a third-party client library's documented shape and an MCP wrapper's
documented request format, not confirmed JSON. **Expect the first real run to need a DTO fix** —
`FindCombosAsync`'s error message points at the request shape as the first thing to check.

What IS verified: the **diff logic** for "combos you're one step away from" — tested with
fabricated data, confirmed correct. Rather than trying to parse Commander Spellbook's own
"almost included" response fields (shape unknown), the approach calls `find-my-combos` twice —
once with just the deck's cards, once with deck + a curve-limited sample of your on-color
collection — and diffs which combos become newly satisfied. This only requires trusting the
"fully satisfied" response shape, not guessing at partial-match semantics too.

**Deliberate scope limit**: only checks up to 300 on-color collection cards (cheapest by mana
value first), not your full ~1,300+ card on-color pool — `ResolveCardIdsAsync` makes one HTTP call
per card (no confirmed bulk-lookup endpoint), so checking everything would be slow and is unlikely
to matter much anyway (expensive combo pieces you don't already have in a deck are less likely
"one step away" candidates than cheap ones).

**Not implemented**: `estimate-bracket` (combo-specific bracket classification) and a general
"browse combos in this commander's colors" feature — both real, confirmed-to-exist endpoints, just
out of scope for this pass. Worth adding once the core integration is confirmed working against
live data.

## What's actually verified vs. what's a best-effort guess

All three DTOs (Archidekt, Scryfall, EDHREC) are now **confirmed against real response files**, not
guesses — this happened across two rounds: real API responses were uploaded and diffed against the
code, catching several real bugs in the process (see "Fixed after real-data testing" below).

**Still genuinely uncertain:**
- Whether Archidekt's/Scryfall's/EDHREC's shapes stay stable over time — none of these are
  officially versioned APIs (EDHREC's especially), so a future schema change could break parsing.
  No code fix for this; just something to watch for if a previously-working call starts throwing.

## Fixed after real-data testing (2026-08-31, second pass)

- **Sideboard cards were leaking into the mainboard.** `MainboardCards` only excluded
  `Maybeboard`; Archidekt's `Sideboard` category is separate, and its own `includedInDeck` flag is
  misleadingly `true` for Sideboard — so the fix filters by category name, not that flag. Verified
  against Council of Four: 155 total entries → 43 Sideboard + 36 Maybeboard + 76 real mainboard.
- **Scryfall lookups print which cards failed**, both to console and in `report.json`
  (`NotFoundOnScryfall`). Root cause for Council of Four's 2 failures: double-faced cards use
  Archidekt's `Front // Back` naming, but Scryfall's collection endpoint wants just the front face
  — now stripped automatically before the lookup, while results are still keyed by the original
  name so callers don't need to know this happened.
- Archidekt already provides oracle text/tags/mana cost per card — deck cards no longer need a
  separate Scryfall round-trip at all, only collection candidates do.
- Added `BracketSignals` (Game Changer count, tutor count, extra-turns/MLD cards) — deterministic,
  sourced from real per-card fields Archidekt provides, not LLM guessing.

## On caching the collection (your database question)

Short answer: **a flat JSON cache file, not a real database** — `CollectionCache` now handles this.
It stores your enriched collection (Scryfall data + oTags already attached) and only re-fetches
when your collection CSV's file-modified-time is newer than the cache — i.e. exactly when you
re-export from Archidekt after buying/adding cards, never otherwise. Reasoning for not reaching for
SQLite or similar:

- Your collection is a few thousand rows, read far more often than written, with no relational
  queries beyond "give me cards matching this color identity and this tag" — both trivial LINQ over
  an in-memory list, no need for SQL.
- A real DB adds a NuGet dependency (`Microsoft.Data.Sqlite` or similar) — fine on your machine
  (normal internet access, unlike my sandbox), but it's real complexity (schema, migrations) for a
  personal tool with no concurrent-write scenario to justify it.
- The invalidate-on-CSV-mtime approach means you never have to think about it — no manual
  "--refresh" flag needed for the common case, it just knows.

If this grows significantly (tracking multiple people's collections, a real UI on top, concurrent
access) that calculus changes and SQLite becomes worth it — flagging that as a real fork, not
pretending the flat-file answer is forever-right regardless of scale.

## Not yet implemented

- Phase 9 (Claude Code narrative layer) — this is meant to be you, interactively, pointing Claude
  Code at `report.json`, not more C# code. See Section 7 of the implementation plan.
- Tag-count thresholds (`DeckAnalyzer.DefaultThresholds`) are starting guesses, not calibrated
  against real decks yet — expect to tune them once you see real output.
