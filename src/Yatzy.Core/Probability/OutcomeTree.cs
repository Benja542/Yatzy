using System.Collections.Concurrent;
using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Probability;

/// <summary>
/// Én mulig "behold"-beslutning med den sandsynlighed den fører til.
/// </summary>
/// <param name="KeepId">Id på "behold"-mængden i <see cref="DiceCatalog"/>.</param>
/// <param name="Kept">Terningerne der beholdes.</param>
/// <param name="Probability">Sandsynligheden for at slå slaget hvis man beholder netop de terninger og spiller optimalt derefter.</param>
public readonly record struct KeepOption(int KeepId, int[] Kept, double Probability);

/// <summary>
/// En knude i udfaldstræet, som det vises i brugerfladen.
/// </summary>
/// <param name="Counts">Håndens tællevektor.</param>
/// <param name="Dice">Håndens terningeøjne.</param>
/// <param name="BranchProbability">Sandsynligheden for at nå hertil fra forældreknuden.</param>
/// <param name="PathProbability">Sandsynligheden for at nå hertil fra roden.</param>
/// <param name="Value">Sandsynligheden for at slå slaget herfra med optimal videre spil.</param>
/// <param name="RerollsLeft">Antal omkast tilbage i knuden.</param>
/// <param name="Kept">Terningerne der beholdes i knuden (tom i bladknuder).</param>
/// <param name="IsHit">Er slaget allerede opfyldt i knuden?</param>
/// <param name="Children">Udfaldene af det næste omkast.</param>
/// <param name="TruncatedChildren">Antal udfald der ikke vises (træet klippes for overskuelighedens skyld).</param>
/// <param name="TruncatedProbability">Samlet sandsynlighed for de udfald der ikke vises.</param>
public sealed record OutcomeTreeNode(
    int[] Counts,
    int[] Dice,
    double BranchProbability,
    double PathProbability,
    double Value,
    int RerollsLeft,
    int[] Kept,
    bool IsHit,
    IReadOnlyList<OutcomeTreeNode> Children,
    int TruncatedChildren,
    double TruncatedProbability);

/// <summary>
/// Den eksakte løsning for ét slag: sandsynligheden for at slå det fra enhver hånd
/// og med et vilkårligt antal omkast tilbage - samt den optimale beslutning.
/// </summary>
public sealed class CategorySolution
{
    private readonly DiceCatalog _catalog;

    internal CategorySolution(
        Category category,
        ScoreGoal goal,
        DiceCatalog catalog,
        double[][] value,
        double[][] keepValue,
        int[][] bestKeep,
        long edgeCount)
    {
        Category = category;
        Goal = goal;
        _catalog = catalog;
        Value = value;
        KeepValue = keepValue;
        BestKeep = bestKeep;
        EdgeCount = edgeCount;
    }

    public Category Category { get; }

    /// <summary>Hvad der tælles som en succes - point overhovedet, eller maks point.</summary>
    public ScoreGoal Goal { get; }

    /// <summary>Højeste antal omkast løsningen er beregnet for.</summary>
    public int MaxRerolls => Value.Length - 1;

    /// <summary>P(slaget slås) indekseret som [antal omkast tilbage][hånd].</summary>
    public double[][] Value { get; }

    /// <summary>P(slaget slås) for en "behold"-mængde, indekseret som [antal kast tilbage][behold-id].</summary>
    public double[][] KeepValue { get; }

    /// <summary>Den optimale "behold"-mængde, indekseret som [antal omkast tilbage][hånd].</summary>
    public int[][] BestKeep { get; }

    /// <summary>Antal sandsynlighedsvægtede kanter der blev beregnet i træet.</summary>
    public long EdgeCount { get; }

    /// <summary>Sandsynligheden for at slå slaget fra en given hånd med et antal omkast tilbage.</summary>
    public double Probability(int stateId, int rerollsLeft) => Value[rerollsLeft][stateId];

    /// <summary>Sandsynligheden for at slå slaget ud fra rå terningeøjne.</summary>
    public double Probability(ReadOnlySpan<int> dice, int rerollsLeft) =>
        Value[rerollsLeft][_catalog.FullStateIdFromDice(dice)];

    /// <summary>
    /// Sandsynligheden for at slå slaget når man starter forfra og har
    /// <paramref name="rollsLeft"/> kast tilbage (3 = en hel tur).
    /// </summary>
    public double ProbabilityFromScratch(int rollsLeft) => KeepValue[rollsLeft][_catalog.EmptyKeepId];

    /// <summary>Den optimale "behold"-mængde for en hånd.</summary>
    public int BestKeepId(int stateId, int rerollsLeft) => BestKeep[rerollsLeft][stateId];

    /// <summary>
    /// Sandsynligheden for at nå målet hvis man <b>beholder præcis de terninger</b>
    /// og derefter spiller optimalt. Det er værdien af netop den gren i udfaldstræet.
    /// </summary>
    /// <param name="keepId">"Behold"-mængdens id i <see cref="DiceCatalog"/>.</param>
    /// <param name="rerollsLeft">Antal omkast tilbage, inklusive det der kastes nu.</param>
    public double ProbabilityWithKeep(int keepId, int rerollsLeft) =>
        rerollsLeft <= 0
            ? throw new ArgumentOutOfRangeException(nameof(rerollsLeft), rerollsLeft, "Der skal være mindst ét omkast tilbage.")
            : KeepValue[rerollsLeft][keepId];

    /// <summary>
    /// Sandsynligheden for at nå målet ud fra de terninger der beholdes. Er
    /// <paramref name="keepCounts"/> <c>null</c>, bruges den optimale "behold"-mængde.
    /// </summary>
    public double Probability(int stateId, int rerollsLeft, int[]? keepCounts)
    {
        if (rerollsLeft <= 0 || keepCounts is null)
        {
            return Value[rerollsLeft][stateId];
        }

        return ProbabilityWithKeep(_catalog.KeepId(keepCounts), rerollsLeft);
    }

    /// <summary>Alle "behold"-muligheder for en hånd, sorteret efter sandsynlighed.</summary>
    public IReadOnlyList<KeepOption> RankKeeps(int stateId, int rerollsLeft)
    {
        if (rerollsLeft <= 0)
        {
            return Array.Empty<KeepOption>();
        }

        var options = new List<KeepOption>();
        foreach (var keepId in _catalog.SubKeeps(stateId))
        {
            options.Add(new KeepOption(keepId, DiceCatalog.ToDice(_catalog.Keep(keepId)), KeepValue[rerollsLeft][keepId]));
        }

        options.Sort(static (a, b) =>
        {
            var byProbability = b.Probability.CompareTo(a.Probability);
            return byProbability != 0 ? byProbability : b.Kept.Length.CompareTo(a.Kept.Length);
        });

        return options;
    }

    /// <summary>
    /// Bygger den del af udfaldstræet der skal vises: rod = den aktuelle hånd, kanterne
    /// er de mulige udfald af de resterende omkast når der spilles optimalt.
    /// </summary>
    /// <param name="stateId">Håndens id.</param>
    /// <param name="rerollsLeft">Antal omkast tilbage.</param>
    /// <param name="maxChildren">Højst så mange udfald pr. knude (de mest sandsynlige vises).</param>
    /// <param name="forcedKeep">
    /// Beholdes disse terninger i roden i stedet for de optimale? Bruges når spilleren
    /// selv har markeret hvilke terninger der skal blive liggende. Dybere i træet
    /// spilles der optimalt.
    /// </param>
    public OutcomeTreeNode BuildTree(int stateId, int rerollsLeft, int maxChildren = 6, int[]? forcedKeep = null)
    {
        var forcedKeepId = rerollsLeft > 0 && forcedKeep is not null ? _catalog.KeepId(forcedKeep) : (int?)null;
        return BuildNode(stateId, rerollsLeft, branchProbability: 1d, pathProbability: 1d, maxChildren, forcedKeepId);
    }

    private OutcomeTreeNode BuildNode(
        int stateId, int rerollsLeft, double branchProbability, double pathProbability, int maxChildren, int? forcedKeepId = null)
    {
        var counts = _catalog.FullState(stateId);
        var isHit = YatzyRules.IsAchieved(Category, Goal, counts);

        if (rerollsLeft == 0)
        {
            return new OutcomeTreeNode(
                counts, DiceCatalog.ToDice(counts), branchProbability, pathProbability, Value[0][stateId],
                rerollsLeft, Array.Empty<int>(), isHit, Array.Empty<OutcomeTreeNode>(), 0, 0d);
        }

        var keepId = forcedKeepId ?? BestKeep[rerollsLeft][stateId];
        var value = forcedKeepId.HasValue ? KeepValue[rerollsLeft][keepId] : Value[rerollsLeft][stateId];
        var targets = _catalog.TransitionTargets(keepId);
        var probabilities = _catalog.TransitionProbabilities(keepId);

        // Sorteres efter sandsynlighed, og ved lige sandsynlighed efter hånd-id. Man kunne
        // fristes til at lade de bedste udfald komme først, men så ville de viste grene
        // give et alt for optimistisk indtryk af træet.
        var order = Enumerable.Range(0, targets.Length)
            .OrderByDescending(i => probabilities[i])
            .ThenBy(i => targets[i])
            .ToArray();

        var shown = System.Math.Min(maxChildren, order.Length);
        var children = new List<OutcomeTreeNode>(shown);
        for (var i = 0; i < shown; i++)
        {
            var index = order[i];
            children.Add(BuildNode(
                targets[index],
                rerollsLeft - 1,
                probabilities[index],
                pathProbability * probabilities[index],
                maxChildren));
        }

        var truncatedProbability = 0d;
        for (var i = shown; i < order.Length; i++)
        {
            truncatedProbability += probabilities[order[i]];
        }

        return new OutcomeTreeNode(
            counts, DiceCatalog.ToDice(counts), branchProbability, pathProbability, value,
            rerollsLeft, DiceCatalog.ToDice(_catalog.Keep(keepId)), isHit,
            children, order.Length - shown, truncatedProbability);
    }
}

/// <summary>
/// Beregner sandsynligheder <b>eksakt</b> ved at folde udfaldstræet sammen bagfra
/// (dynamisk programmering / expectimax).
/// </summary>
/// <remarks>
/// <para>
/// Rekursionen er:
/// <code>
/// V(hånd, 0 omkast)  = 1 hvis målet er nået (point, eller maks point), ellers 0
/// V(hånd, r omkast)  = max over "behold"-mængder K ⊆ hånd af
///                      Σ P(udfald u) · V(K ∪ u, r-1)
/// </code>
/// Maks-leddet er spillerens valg (beslutningsknude), summen er terningernes
/// tilfældighed (chanceknude). Resultatet er den eksakte sandsynlighed for at nå målet
/// når man spiller optimalt netop efter det slag.
/// </para>
/// <para>
/// Har spilleren selv markeret hvilke terninger der skal beholdes, springes maks-leddet
/// over i første omkast: så bruges <see cref="CategorySolution.ProbabilityWithKeep"/>,
/// som er værdien af netop den gren. Resten af træet regnes stadig med optimalt spil.
/// </para>
/// <para>
/// Et fuldt udfoldet træ over ordnede kast har 46.656 grene pr. kast og op til 64
/// beslutninger pr. knude, altså i størrelsesordenen 10^20 blade. Ved at regne på
/// tællevektorer i stedet for ordnede kast, og ved at genbruge <c>V</c> for hver
/// hånd, kommer vi ned på ca. 40.000 kanter pr. slag.
/// </para>
/// </remarks>
public sealed class OutcomeTreeSolver
{
    private readonly DiceCatalog _catalog;
    private readonly ConcurrentDictionary<(Category Category, ScoreGoal Goal, int MaxRerolls), CategorySolution> _cache = new();

    public OutcomeTreeSolver(DiceCatalog? catalog = null) => _catalog = catalog ?? DiceCatalog.Instance;

    /// <summary>Delt instans med cache, så tabellen i brugerfladen kun beregnes én gang.</summary>
    public static OutcomeTreeSolver Instance { get; } = new();

    public DiceCatalog Catalog => _catalog;

    /// <summary>Løser ét slag for et givet mål. Resultatet caches.</summary>
    public CategorySolution Solve(
        Category category,
        ScoreGoal goal = ScoreGoal.AnyPoints,
        int maxRerolls = YatzyRules.RollsPerTurn) =>
        _cache.GetOrAdd((category, goal, maxRerolls), key => Compute(key.Category, key.Goal, key.MaxRerolls));

    /// <summary>Løser alle 20 slag for et givet mål.</summary>
    public IReadOnlyDictionary<Category, CategorySolution> SolveAll(
        ScoreGoal goal = ScoreGoal.AnyPoints,
        int maxRerolls = YatzyRules.RollsPerTurn) =>
        Categories.All.ToDictionary(category => category, category => Solve(category, goal, maxRerolls));

    private CategorySolution Compute(Category category, ScoreGoal goal, int maxRerolls)
    {
        var states = _catalog.FullStateCount;
        var value = new double[maxRerolls + 1][];
        var keepValue = new double[maxRerolls + 1][];
        var bestKeep = new int[maxRerolls + 1][];
        var edgeCount = 0L;

        value[0] = new double[states];
        bestKeep[0] = new int[states];
        keepValue[0] = Array.Empty<double>();
        for (var stateId = 0; stateId < states; stateId++)
        {
            value[0][stateId] = YatzyRules.IsAchieved(category, goal, _catalog.FullState(stateId)) ? 1d : 0d;
            bestKeep[0][stateId] = _catalog.KeepId(_catalog.FullState(stateId));
        }

        for (var r = 1; r <= maxRerolls; r++)
        {
            // Chanceknuderne: værdien af hver mulig "behold"-mængde.
            var previous = value[r - 1];
            var q = new double[_catalog.KeepCount];
            for (var keepId = 0; keepId < _catalog.KeepCount; keepId++)
            {
                var targets = _catalog.TransitionTargets(keepId);
                var probabilities = _catalog.TransitionProbabilities(keepId);
                var sum = 0d;
                for (var i = 0; i < targets.Length; i++)
                {
                    sum += probabilities[i] * previous[targets[i]];
                }

                q[keepId] = sum;
                edgeCount += targets.Length;
            }

            // Beslutningsknuderne: spilleren vælger den bedste "behold"-mængde.
            var current = new double[states];
            var choice = new int[states];
            for (var stateId = 0; stateId < states; stateId++)
            {
                var best = -1d;
                var bestId = -1;
                foreach (var keepId in _catalog.SubKeeps(stateId))
                {
                    var candidate = q[keepId];
                    // Ved lige værdi beholdes flest mulige terninger - det giver de pæneste råd.
                    if (candidate > best + 1e-12 ||
                        (candidate > best - 1e-12 && bestId >= 0 && _catalog.KeepSize(keepId) > _catalog.KeepSize(bestId)))
                    {
                        best = candidate;
                        bestId = keepId;
                    }
                }

                current[stateId] = best;
                choice[stateId] = bestId;
                edgeCount += _catalog.SubKeeps(stateId).Length;
            }

            value[r] = current;
            keepValue[r] = q;
            bestKeep[r] = choice;
        }

        return new CategorySolution(category, goal, _catalog, value, keepValue, bestKeep, edgeCount);
    }
}
