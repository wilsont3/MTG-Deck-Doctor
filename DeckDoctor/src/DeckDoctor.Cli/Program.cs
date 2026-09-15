using System.Text.Json;
using DeckDoctor.Core.Analysis;
using DeckDoctor.Core.Models;
using DeckDoctor.Core.Services;

if (args.Length < 1)
{
    Console.WriteLine("Usage: DeckDoctor.Cli <archidekt-deck-url-or-id> [--collection path/to/collection.csv] [--out report.json] [--deckcheck deck-id-or-url]");
    return 1;
}

var deckArg = args[0];
var collectionPath = GetOption(args, "--collection") ?? "data/collection.csv";
var outPath = GetOption(args, "--out") ?? "report.json";
var deckCheckArg = GetOption(args, "--deckcheck");

var archidekt = new ArchidektClient();
var scryfall = new ScryfallClient();
var edhrec = new EdhrecClient();
var collectionRepo = new CollectionRepository();

Console.WriteLine($"Fetching deck from Archidekt ({deckArg})...");
ArchidektDeck deck;
try
{
    deck = await archidekt.FetchDeckAsync(deckArg);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to fetch deck: {ex.Message}");
    return 1;
}
var mainboard = deck.MainboardCards;
Console.WriteLine($"  Loaded \"{deck.Name}\" — {mainboard.Count} mainboard cards, commander(s): {string.Join(" / ", deck.CommanderNames)}");
Console.WriteLine("  (Archidekt already provides oracle text, mana cost, color identity, and tags per card — no separate Scryfall/bulk-tag fetch needed for these.)");

var colorIdentity = mainboard
    .Where(c => deck.CommanderNames.Contains(c.Name))
    .SelectMany(c => c.ColorIdentity)
    .Distinct()
    .ToList();
Console.WriteLine($"Commander color identity: {string.Join("", colorIdentity)}");

Console.WriteLine($"Loading collection from {collectionPath}...");
List<CollectionCard> collection;
try
{
    collection = collectionRepo.LoadFromCsv(collectionPath);
    Console.WriteLine($"  Loaded {collection.Count} rows.");
}
catch (Exception ex)
{
    Console.WriteLine($"  Failed to load collection: {ex.Message}");
    collection = new List<CollectionCard>();
}

var onColor = collectionRepo.FilterOnColor(collection, colorIdentity);
Console.WriteLine($"  {onColor.Count} cards fit this commander's color identity.");

// Collection candidates are NOT in the fetched deck, so they still need Scryfall + the oracle
// tags bulk file (deck cards get tags straight from Archidekt now, see above). Cached and only
// refreshed when the CSV itself changes — see CollectionCache for why this isn't a real DB.
Console.WriteLine("Loading enriched collection data (cached — only re-fetches if the CSV changed since last run)...");
var allNotFound = new List<string>();
try
{
    var cache = new CollectionCache(scryfall, collectionRepo);
    var (enrichedFull, notFoundInCollection) = await cache.LoadEnrichedAsync(collectionPath);
    // GroupBy+First instead of ToDictionary: defensive even though CollectionRepository now
    // aggregates by name internally — a cache file written before that fix could still have dupes.
    var enrichedByName = enrichedFull
        .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    foreach (var c in onColor)
        if (enrichedByName.TryGetValue(c.Name, out var e))
            c.ScryfallData = e.ScryfallData;
    allNotFound.AddRange(notFoundInCollection);
    Console.WriteLine($"  {onColor.Count(c => c.ScryfallData != null)}/{onColor.Count} on-color cards have Scryfall data.");
}
catch (Exception ex)
{
    Console.WriteLine($"  Collection enrichment failed: {ex.Message} — suggestions will be limited.");
}

Console.WriteLine("Fetching EDHREC synergy data...");
var edhrecByName = new Dictionary<string, EdhrecCardEntry>(StringComparer.OrdinalIgnoreCase);
var edhrecGameChangers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var commander in deck.CommanderNames)
{
    try
    {
        var page = await edhrec.FetchCommanderPageAsync(commander);
        foreach (var entry in page.Cards)
            edhrecByName[entry.Name] = entry;
        foreach (var name in page.GameChangerNames)
            edhrecGameChangers.Add(name);
        Console.WriteLine($"  Got {page.Cards.Count} EDHREC entries for {commander} ({page.GameChangerNames.Count} tagged as game changers for this commander specifically).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  EDHREC fetch failed for {commander}: {ex.Message}");
        Console.WriteLine("  Continuing without EDHREC data — suggestions will use a neutral synergy score.");
    }
}

Console.WriteLine("Analyzing deck...");
var analyzer = new DeckAnalyzer();
var weaknessReport = analyzer.Analyze(mainboard.Select(c => new CardAnalysisInput(c.OTags, c.OracleText)));

var tagProfile = DeckTagProfile.Build(mainboard.Select(c => (IEnumerable<string>)c.OTags));
var topTags = tagProfile.TopTags(15);
Console.WriteLine();
Console.WriteLine("Dominant tags across the whole deck (for archetype identification, not weakness scoring):");
foreach (var (tag, count) in topTags)
    Console.WriteLine($"  {tag}: {count}");

var commanderTags = deck.CommanderNames
    .Select(name => mainboard.FirstOrDefault(c => c.Name == name))
    .Where(c => c != null)
    .ToDictionary(c => c!.Name, c => c!.OTags);
Console.WriteLine();
Console.WriteLine("Commander's own tags (not diluted by the rest of the deck — often the real build-around signal):");
foreach (var (name, tags) in commanderTags)
    Console.WriteLine($"  {name}: {string.Join(", ", tags)}");

Console.WriteLine();
Console.WriteLine($"Deck: {deck.Name}");
Console.WriteLine($"Commander: {string.Join(" / ", deck.CommanderNames)}");
Console.WriteLine();
Console.WriteLine("Weaknesses:");
foreach (var t in DeckAnalyzer.DefaultThresholds)
{
    var (count, level) = weaknessReport.Weaknesses[t.DisplayName];
    Console.WriteLine($"- {t.DisplayName}: {(level == WeaknessLevel.Low ? "LOW" : "OK")} ({count})");
}

// Deterministic bracket signals — real fields Archidekt provides, not LLM judgment.
var bracketSignals = new DeckBracketSignals
{
    GameChangerCards = mainboard.Where(c => c.GameChanger).Select(c => c.Name).ToList(),
    TutorCount = mainboard.Count(c => c.IsTutor),
    ExtraTurnsCards = mainboard.Where(c => c.ExtraTurns).Select(c => c.Name).ToList(),
    MassLandDenialCards = mainboard.Where(c => c.MassLandDenial).Select(c => c.Name).ToList(),
    EdhrecGameChangerNames = mainboard.Select(c => c.Name).Where(edhrecGameChangers.Contains).ToList(),
};
Console.WriteLine();
Console.WriteLine($"Bracket signals: {bracketSignals.GameChangerCards.Count} Game Changer(s), {bracketSignals.TutorCount} tutor(s), " +
    $"{bracketSignals.ExtraTurnsCards.Count} extra-turns card(s), {bracketSignals.MassLandDenialCards.Count} mass land denial card(s).");
if (bracketSignals.GameChangerCards.Count > 0)
    Console.WriteLine($"  Game Changers: {string.Join(", ", bracketSignals.GameChangerCards)}");

Console.WriteLine();
if (allNotFound.Count > 0)
{
    Console.WriteLine($"Could not look up {allNotFound.Count} card(s) on Scryfall (excluded from tag-based analysis for those specific cards):");
    foreach (var name in allNotFound.Distinct())
        Console.WriteLine($"  - {name}");
}
else
{
    Console.WriteLine("All lookups resolved — no cards missing Scryfall data.");
}

Console.WriteLine();
Console.WriteLine("Checking Commander Spellbook for combos...");
var combosInDeck = new List<DeckReportCombo>();
var combosOneStepAway = new List<DeckReportCombo>();
try
{
    var csb = new CommanderSpellbookClient();
    var nonCommanderMainboard = mainboard.Where(c => !deck.CommanderNames.Contains(c.Name)).Select(c => c.Name);

    var (included, almostIncluded) = await csb.FindCombosAsync(nonCommanderMainboard, deck.CommanderNames);
    Console.WriteLine($"  {included.Count} combo(s) already fully in the deck.");
    Console.WriteLine($"  {almostIncluded.Count} combo(s) almost included (missing a small number of cards).");

    combosInDeck = included.Select(v => new DeckReportCombo
    {
        Permalink = v.Permalink,
        CardNames = v.CardNames,
        Results = v.Results,
    }).ToList();

    // "Almost included" is Commander Spellbook's own server-side definition (missing a small
    // number of cards from the DECK, not specific to what you own). Cross-reference the missing
    // cards against your on-color collection locally — no extra API calls needed for this part.
    var deckCardNameSet = new HashSet<string>(mainboard.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
    var ownedNameSet = new HashSet<string>(onColor.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
    combosOneStepAway = almostIncluded
        .Select(v => new DeckReportCombo
        {
            Permalink = v.Permalink,
            CardNames = v.CardNames,
            Results = v.Results,
            MissingCardsYouOwn = v.CardNames.Where(n => !deckCardNameSet.Contains(n) && ownedNameSet.Contains(n)).ToList(),
        })
        .Where(c => c.MissingCardsYouOwn.Count > 0) // only surface ones you can actually complete from your own collection
        .ToList();
    Console.WriteLine($"  {combosOneStepAway.Count} of those are completable using cards you already own.");
}
catch (Exception ex)
{
    Console.WriteLine($"  Commander Spellbook check failed: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Checking Recommander for a second, independent synergy signal...");
var recommanderSuggestions = new List<DeckReportSuggestion>();
try
{
    var recommander = new RecommanderClient();
    var primaryCommander = deck.CommanderNames.FirstOrDefault();
    var secondaryCommander = deck.CommanderNames.Count > 1 ? deck.CommanderNames[1] : null;
    if (primaryCommander == null)
    {
        Console.WriteLine("  No commander detected — skipping Recommander (it requires one).");
    }
    else
    {
        var nonCommanderNames = mainboard.Where(c => !deck.CommanderNames.Contains(c.Name)).Select(c => c.Name);
        var recs = await recommander.FindRecommendationsAsync(primaryCommander, secondaryCommander, nonCommanderNames);
        var deckCardNameSet = new HashSet<string>(mainboard.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        var ownedNameSet = new HashSet<string>(onColor.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        recommanderSuggestions = recs
            .Where(r => !deckCardNameSet.Contains(r.Name)) // only suggest cards not already in the deck
            .Select(r => new DeckReportSuggestion
            {
                CardName = r.Name,
                Tag = "recommander-synergy",
                Score = r.Score,
                Owned = ownedNameSet.Contains(r.Name),
            })
            .ToList();
        Console.WriteLine($"  {recommanderSuggestions.Count} recommendation(s) not already in the deck ({recommanderSuggestions.Count(s => s.Owned)} of which you own).");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Recommander check failed: {ex.Message}");
}

DeckCheckSummary? deckCheckAnalysis = null;
if (deckCheckArg != null)
{
    Console.WriteLine();
    Console.WriteLine($"Fetching DeckCheck analysis ({deckCheckArg})...");
    try
    {
        var deckCheck = new DeckCheckClient();
        deckCheckAnalysis = await deckCheck.GetDeckAsync(deckCheckArg);
        if (deckCheckAnalysis.Crispi != null)
        {
            Console.WriteLine($"  Bracket: {deckCheckAnalysis.Bracket}");
            Console.WriteLine($"  CRISPI — Overall: {deckCheckAnalysis.Crispi.Overall}, Consistency: {deckCheckAnalysis.Crispi.Consistency}, " +
                $"Resilience: {deckCheckAnalysis.Crispi.Resilience}, Interaction: {deckCheckAnalysis.Crispi.Interaction}, Speed: {deckCheckAnalysis.Crispi.Speed}");
        }
        else
        {
            Console.WriteLine("  No CRISPI data returned (format may not be Commander, or analysis hasn't completed on DeckCheck yet).");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  DeckCheck check failed: {ex.Message}");
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("No --deckcheck argument provided — skipping DeckCheck (requires a deck already built on DeckCheck's own platform, not just Archidekt).");
}

var suggestionEngine = new SuggestionEngine();
var allSuggestions = new List<DeckReportSuggestion>();
Console.WriteLine();
Console.WriteLine("Suggested Upgrades:");
foreach (var lowCategory in weaknessReport.LowTags)
{
    var threshold = DeckAnalyzer.DefaultThresholds.First(t => t.DisplayName == lowCategory);
    var suggestions = suggestionEngine.SuggestFor(threshold.DisplayName, threshold.MatchSubstrings, threshold.UseTextPatterns, onColor, edhrecByName);
    if (suggestions.Count == 0)
    {
        var matchDesc = threshold.UseTextPatterns
            ? $"oracle text patterns ({string.Join(", ", WinConditionPatterns.All.Select(p => p.Label))})"
            : $"tags containing: {string.Join(", ", threshold.MatchSubstrings)}";
        Console.WriteLine($"- [{lowCategory}] No owned candidates found matching {matchDesc}. " +
            $"Either you genuinely don't own anything in this category, or (more likely for Wincons specifically) the pattern list itself needs work — see README.");
        continue;
    }
    foreach (var s in suggestions)
    {
        Console.WriteLine($"- [{lowCategory}] {s.CardName} (score {s.Score})");
        allSuggestions.Add(new DeckReportSuggestion
        {
            CardName = s.CardName,
            Tag = s.Tag,
            Score = s.Score,
            Owned = s.Owned,
            OracleText = s.OracleText,
        });
    }
}

var report = new DeckReport
{
    DeckName = deck.Name,
    Commanders = deck.CommanderNames,
    ColorIdentity = colorIdentity,
    DeckList = mainboard.Select(c => new DeckReportCard
    {
        Name = c.Name,
        Quantity = c.Quantity,
        TypeLine = c.TypeLine,
        ManaCost = c.ManaCost,
        OracleText = c.OracleText,
        OTags = c.OTags,
    }).ToList(),
    Weaknesses = weaknessReport.Weaknesses.ToDictionary(
        kv => kv.Key,
        kv => new DeckReportWeakness { Count = kv.Value.Count, Level = kv.Value.Level.ToString() }),
    Suggestions = allSuggestions,
    BracketSignals = bracketSignals,
    NotFoundOnScryfall = allNotFound.Distinct().ToList(),
    CombosInDeck = combosInDeck,
    CombosOneStepAwayInCollection = combosOneStepAway,
    DominantTags = topTags.Select(t => new TagCount(t.Tag, t.Count)).ToList(),
    CommanderTags = commanderTags,
    DeckCheckAnalysis = deckCheckAnalysis,
    RecommanderSuggestions = recommanderSuggestions,
};

var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(outPath, json);
Console.WriteLine();
Console.WriteLine($"Full report written to {outPath} — hand this to Claude Code for the narrative audit pass.");

return 0;

static string? GetOption(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
