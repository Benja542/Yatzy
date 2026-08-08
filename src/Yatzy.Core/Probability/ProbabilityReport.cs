using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Probability;

/// <summary>
/// Én række i sandsynlighedstabellen - de tre metoder side om side for ét slag,
/// regnet ud fra de terninger der beholdes.
/// </summary>
/// <param name="Category">Slaget.</param>
/// <param name="Goal">Hvad rækken handler om: point overhovedet, eller maks point.</param>
/// <param name="CurrentScore">Point hånden ville give lige nu.</param>
/// <param name="MaxScore">Den højst mulige score i slaget.</param>
/// <param name="TreeProbability">
/// Eksakt sandsynlighed fra udfaldstræet, betinget af de terninger der beholdes.
/// </param>
/// <param name="BestProbability">
/// Eksakt sandsynlighed hvis man i stedet beholdt det bedst mulige.
/// </param>
/// <param name="BestKeep">Den "behold"-mængde der giver <paramref name="BestProbability"/>.</param>
/// <param name="Analytic">Analytisk beregnet sandsynlighed for ét enkelt kast med alle seks terninger.</param>
/// <param name="MonteCarlo">Monte Carlo-estimat af <paramref name="TreeProbability"/>.</param>
public sealed record ProbabilityRow(
    Category Category,
    ScoreGoal Goal,
    int CurrentScore,
    int MaxScore,
    double TreeProbability,
    double BestProbability,
    int[] BestKeep,
    AnalyticResult Analytic,
    MonteCarloResult? MonteCarlo)
{
    /// <summary>Er slaget allerede i hus med de terninger der ligger?</summary>
    public bool AlreadyAchieved => Goal == ScoreGoal.MaxPoints
        ? CurrentScore >= MaxScore
        : CurrentScore > 0;

    /// <summary>Hvor meget der tabes ved at beholde noget andet end det bedste.</summary>
    public double LostByKeep => BestProbability - TreeProbability;

    /// <summary>Afvigelsen mellem simuleringen og den eksakte beregning.</summary>
    public double? MonteCarloDeviation => MonteCarlo is { } mc ? mc.Estimate - TreeProbability : null;

    /// <summary>Ligger den eksakte værdi inden for simuleringens konfidensinterval?</summary>
    public bool? WithinConfidenceInterval => MonteCarlo is { } mc
        ? TreeProbability >= mc.LowerBound && TreeProbability <= mc.UpperBound
        : null;
}

/// <summary>
/// Samler de tre beregningsmetoder - analytisk formel, udfaldstræ og Monte Carlo -
/// i én tabel over de slag der stadig er åbne.
/// </summary>
/// <remarks>
/// Udfaldstræet og simuleringen svarer på præcis samme spørgsmål:
/// <i>hvis jeg beholder netop disse terninger og spiller resten af turen så godt som
/// muligt, hvad er så sandsynligheden for at nå målet?</i> Derfor skal de to tal
/// stemme overens, og forskellen er ren simuleringsusikkerhed.
/// </remarks>
public static class ProbabilityReport
{
    /// <summary>
    /// Bygger tabellen.
    /// </summary>
    /// <param name="categories">De slag der skal med (typisk de åbne).</param>
    /// <param name="counts">Den aktuelle hånd, eller <c>null</c> hvis der ikke er kastet endnu.</param>
    /// <param name="keepCounts">
    /// De terninger der beholdes, eller <c>null</c> for at regne med den bedst mulige
    /// "behold"-mængde for hvert slag.
    /// </param>
    /// <param name="rerollsLeft">Antal omkast tilbage (3 før første kast).</param>
    /// <param name="goal">Om målet er point overhovedet, eller maks point.</param>
    /// <param name="monteCarloTrials">Antal simuleringer pr. slag. 0 slår Monte Carlo fra.</param>
    /// <param name="seed">Frø til simuleringen - sat = reproducerbart.</param>
    public static IReadOnlyList<ProbabilityRow> Build(
        IEnumerable<Category> categories,
        int[]? counts,
        int[]? keepCounts,
        int rerollsLeft,
        ScoreGoal goal = ScoreGoal.AnyPoints,
        long monteCarloTrials = 0,
        int? seed = null)
    {
        var solver = OutcomeTreeSolver.Instance;
        var catalog = DiceCatalog.Instance;
        var rows = new List<ProbabilityRow>();
        var seedOffset = 0;

        // Kun meningsfuldt at låse "behold"-mængden når der ligger terninger og der er omkast tilbage.
        var effectiveKeep = counts is not null && rerollsLeft > 0 ? keepCounts : null;

        foreach (var category in categories)
        {
            var solution = solver.Solve(category, goal);
            double treeProbability;
            double bestProbability;
            int[] bestKeep;
            var currentScore = 0;

            if (counts is null)
            {
                treeProbability = solution.ProbabilityFromScratch(rerollsLeft);
                bestProbability = treeProbability;
                bestKeep = [];
            }
            else
            {
                var stateId = catalog.FullStateId(counts);
                bestProbability = solution.Probability(stateId, rerollsLeft);
                bestKeep = rerollsLeft > 0
                    ? DiceCatalog.ToDice(catalog.Keep(solution.BestKeepId(stateId, rerollsLeft)))
                    : [];
                treeProbability = effectiveKeep is null
                    ? bestProbability
                    : solution.ProbabilityWithKeep(catalog.KeepId(effectiveKeep), rerollsLeft);
                currentScore = YatzyRules.Score(category, counts);
            }

            MonteCarloResult? monteCarlo = null;
            if (monteCarloTrials > 0)
            {
                monteCarlo = MonteCarloEstimator.Estimate(
                    solution,
                    counts,
                    counts is null ? rerollsLeft - 1 : rerollsLeft,
                    monteCarloTrials,
                    seed.HasValue ? seed.Value + seedOffset : null,
                    effectiveKeep);
            }

            rows.Add(new ProbabilityRow(
                category,
                goal,
                currentScore,
                Categories.MaxScore(category),
                treeProbability,
                bestProbability,
                bestKeep,
                AnalyticProbability.Compute(category, goal),
                monteCarlo));

            seedOffset++;
        }

        return rows;
    }
}
