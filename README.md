# Deck Doctor

A tool for auditing a Magic: The Gathering Commander (EDH) deck's actual ability to **win games**
— not just how synergistic or well-built it is generally. Pulls your decklist, cross-references
your card collection, and hands off to Claude Code for a narrative read on redundancy, speed, and
bracket fit.

Full background and design rationale: `docs/IMPLEMENTATION_PLAN.md`.

## What it does

1. Fetches a decklist from **Archidekt** (by URL) — including oracle text, mana cost, color
   identity, and Scryfall's oracle tags, all already attached per card.
2. Cross-references your **collection** (a CSV export) against the deck's color identity, to find
   what you own that could improve it.
3. Enriches collection candidates with **Scryfall** data (oracle text, tags) — deck cards already
   have this from Archidekt; only cards outside the deck need a separate lookup.
4. Pulls **EDHREC** synergy data for the deck's commander(s).
5. Checks **Commander Spellbook** for combos already in the deck, and combos you could complete
   with cards you own but haven't included.
6. Runs deterministic analysis: tag-based weakness detection (ramp/draw/removal), a text-pattern
   based win-condition scan, a filtered tag histogram (which mechanisms actually dominate this
   deck — a fingerprint for archetype identification, not a classification itself), and bracket
   signals (Game Changers, tutors, extra turns, mass land denial — real per-card flags, not
   inference).
7. Writes everything to `report.json` — a structured summary meant to be handed to **Claude Code**
   for the actual narrative audit (is this deck's win-con package fast enough? redundant enough?
   what's the honest bracket read?). The C# side does the mechanical analysis; the judgment call
   happens in that conversation, not in more C#.

## Setup

Requires the .NET 10 SDK. No NuGet packages, no `dotnet restore` step — everything is built on
the base class library on purpose (see "Design notes" below).

```
dotnet build
```

Put your Archidekt collection export at `data/collection.csv` (columns: Quantity, Name, Scryfall
ID, Mana Value, Identities, Mana cost — check the column-picker on Archidekt's export screen for
the last three, they're not on by default).

## Usage

```
dotnet run --project src/DeckDoctor.Cli -- <archidekt-deck-url> [--collection path/to/collection.csv] [--out report.json] [--deckcheck deck-id-or-url]
```

`--deckcheck` is optional and requires the SAME deck to already exist separately on DeckCheck's
own platform (not just Archidekt) — see "Notable data-source quirks" below for why.

Example:

```
dotnet run --project src/DeckDoctor.Cli -- https://archidekt.com/decks/7093281/the_council_of_four
```

In Visual Studio: open `DeckDoctor.sln`, set `DeckDoctor.Cli` as the startup project, and edit
arguments via the project's Debug Properties (pre-filled in `Properties/launchSettings.json`).

Once `report.json` is written, attach it to a claude.ai chat (or a Project) and ask for the audit
— the `mtg-commander-wincon-audit` skill should recognize the file's shape and know how to use
each field. Claude Code would work the same way and keeps this fully within a subscription's
usage, but requires a paid plan — attaching the file in a normal chat works on the Free plan too.

## Project structure

```
DeckDoctor/
  src/
    DeckDoctor.Core/
      Models/       — DTOs for each external API, plus the internal domain models
      Services/     — one client per external source (Archidekt, Scryfall, EDHREC, Commander
                       Spellbook, Recommander, DeckCheck), plus collection loading and caching
      Analysis/      — the deterministic analysis: tag profiling, weakness thresholds, win-
                       condition text patterns, upgrade suggestion scoring
    DeckDoctor.Cli/
      Program.cs     — wires the whole pipeline together, prints a console summary, writes
                       report.json
  data/
    collection.csv       — your collection export (gitignored — personal data)
    *-cache.json          — local caches for Scryfall's oracle tags and enriched collection data
                            (gitignored, auto-rebuilt when stale)
  docs/
    IMPLEMENTATION_PLAN.md — the original design doc this was built from
```

## Design notes (the "why," not a changelog)

**No NuGet packages.** Built entirely on `HttpClient` and `System.Text.Json` from the base class
library, with a small hand-rolled CSV parser instead of CsvHelper. This means `dotnet build` never
needs to reach `nuget.org` — zero restore step, builds offline.

**A flat JSON cache instead of a real database.** Your collection is a few thousand rows, read far
more often than written, with no relational queries beyond "match this color identity and this
tag" — plain LINQ over an in-memory list. `CollectionCache` invalidates itself by comparing against
your CSV's file-modified time, so it only re-fetches when you actually re-export from Archidekt.
Worth revisiting if this ever needs to serve multiple people's collections or handle concurrent
writes — not needed for a personal tool.

**Win conditions are matched by oracle *text* patterns, not tags.** Neither Scryfall's oracle tags
nor EDHREC's category list has anything resembling "this closes the game" as a concept — confirmed
by checking both directly, and by checking a much more sophisticated third-party tool
(commandersalt.com) which doesn't have a clean category for it either. `WinConditionPatterns.cs`
matches regexes instead (explicit win text, extra combat, mass evasion ETBs, draw-triggered
damage, alt-wincons). This is a starting hypothesis, not a finished taxonomy — expect false
negatives more than false positives, and know that it can't distinguish a genuinely fast closer
from a marginal incremental effect that happens to share the same mechanism. That distinction is
exactly what the Claude Code narrative pass is for.

**The tag histogram (`DominantTags`) identifies signal, not archetype.** It's the deck's most
common oTags, filtered against a small noise list (`activated ability`/`triggered ability` are
true of almost every deck and were the top two entries before filtering; a couple of tags that
describe a card's *name* rather than function got excluded too). Naming the actual archetype from
this histogram, and finding real strategy-appropriate finishers for it, is left to whoever reads
`report.json` — a hardcoded tag-combination-to-archetype table would be guessing at coverage built
from a small number of real decks, and "what are good finishers for archetype X" doesn't have
clean structured data behind it anyway (see the Commander Spellbook section below) — that's
exactly the kind of question web search answers and a static C# table can't.

**The commander's own tags (`CommanderTags`) are surfaced separately, not just folded into the
aggregate.** Real gap confirmed on Council of Four: its own oTags include `"repeatable creature
tokens"`, but that never made the aggregate top-15 — it's rare across the other 75 cards, drowned
out by sheer draw-card volume. The commander is the one card always present and usually the actual
build-around piece; averaging its signal into 76 data points buries exactly the thing most likely
to matter. With both pieces available, the real picture (draw engine AND token maker, not just
draw engine) becomes visible instead of one strategy quietly outweighing the other by card count.

**Why Claude Code is a manual step, not automated.** Claude Code used interactively (a human
triggering it, reading `report.json` themselves) draws on the same usage as a normal Claude.ai
subscription — free in that sense. Programmatic/headless invocation from inside `Program.cs`
(e.g. shelling out to `claude -p`) would shift that usage to separately-billed API costs. Keeping
the handoff manual is a deliberate cost decision, not a missing feature.

## Notable data-source quirks

Things that weren't obvious going in and cost real debugging time — worth knowing before touching
the relevant client:

- **Archidekt's `Sideboard` category has `includedInDeck: true`** in its own metadata, despite not
  being part of the actual 100 cards. `ArchidektDeck.MainboardCards` filters by category name
  (excluding both `Sideboard` and `Maybeboard`), not by that flag.
- **Archidekt names double-faced cards as `"Front // Back"`**, but Scryfall's `/cards/collection`
  lookup wants just the front face. `ScryfallClient` strips this automatically before querying,
  while still keying results by the original full name.
- **Your collection export has one row per owned copy**, not one row with a summed quantity —
  `CollectionRepository` aggregates by name on load.
- **Scryfall's oracle-tags bulk file is a gzipped JSONL archive** (`.jsonl.gz`) — file-level gzip,
  not HTTP transport compression, so it needs explicit `GZipStream` decompression, then one JSON
  object parsed per line (not as a single JSON array). It's also cached locally on a ~weekly
  cadence (`FetchOracleTagsCachedAsync`), matching Scryfall's own ~12-24 hour refresh rate — no
  reason to re-download more often than that.
- **Commander Spellbook's `find-my-combos` takes card names directly**, not their internal numeric
  IDs — an earlier version of this integration resolved names to IDs one HTTP call at a time,
  which is what caused real 429 rate-limit errors on a 77-card deck. The fix came from pulling the
  official generated TypeScript client (`@space-cow-media/spellbook-client`) off npm and reading
  its real, versioned type definitions directly, rather than guessing from documentation prose.
  The response also already separates fully-included and "almost included" combos in one call —
  no need to make two requests and diff them client-side.
- **DeckCheck's `/deck` endpoint takes a DeckCheck-native deck ID or URL, not an Archidekt one.**
  There's no import-from-Archidekt shortcut in their read API — the same deck has to be separately
  built or imported on DeckCheck's own platform to get CRISPI/bracket data for it. This is real
  ongoing duplication (keeping two platforms in sync as you edit a deck), not a one-time setup
  cost like the collection CSV — a deliberate trade-off, not an oversight, made because the CRISPI
  breakdown (Consistency, Resilience, Interaction, Speed, Overall) is genuinely richer bracket/
  power data than anything computed locally, especially the Speed dimension which maps closely
  onto the fast-vs-slow distinction `WinConditionPatterns` can't fully capture on its own.
- **Recommander and Commander Spellbook were both fully documented and reachable on the first
  try** — a nice change from EDHREC (reverse-engineered, `robots.txt`-blocked for direct fetch)
  and the original Commander Spellbook attempt (needed the official npm client's source to get
  right). Both `RecommanderModels.cs` and `DeckCheckModels.cs` were verified against real example
  JSON pulled directly from their own docs, not guessed — genuine confidence, not the usual
  "verify on your first live run" caveat that applies to EDHREC and Commander Spellbook.

## Known limitations / not implemented

- `estimate-bracket` (Commander Spellbook's combo-specific bracket classification) — real,
  confirmed-to-exist endpoint, not yet wired in.
- Browsing combos generally within a commander's color identity (as opposed to checking a specific
  decklist) — same story.
- The "Wincons" weakness category's text patterns are validated against exactly one real deck
  (Council of Four) — treat the pattern list as a hypothesis to expand, not a finished system.
- Weakness thresholds (`DeckAnalyzer.DefaultThresholds`) are starting guesses, not calibrated
  against multiple real decks yet.
