using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Probability;

/// <summary>
/// Én række i sandsynlighedstabellen - de tre metoder side om side for ét slag.
/// </summary>
/// <param name="Category">Slaget.</param>
/// <param name="TreeProbability">Eksakt sandsynlighed fra udfaldstræet ud fra den aktuelle hånd.</param>
/// <param name="FullTurnProbability">Eksakt sandsynlighed for en hel tur (3 kast) startet forfra.</param>
/// <param name="Analytic">Analytisk beregnet sandsynlighed for ét enkelt kast.</param>
/// <param name="MonteCarlo">Monte Carlo-estimat af <paramref name="TreeProbability"/>.</param>
/// <param name="BestKeep">Den optimale "behold"-mængde givet slaget.</param>
/// <param name="CurrentScore">Point hånden ville give lige nu.</param>
/// <param name="AlreadyHit">Er slaget allerede i hus?</param>
public sealed record ProbabilityRow(
    Category Category,
    double TreeProbability,
    double FullTurnProbability,
    AnalyticResult Analytic,
    MonteCarloResult? MonteCarlo,
    int[] BestKeep,
    int CurrentScore,
    bool AlreadyHit)
{
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
public static class ProbabilityReport
{
    /// <summary>
    /// Bygger tabellen.
    /// </summary>
    /// <param name="categories">De slag der skal med (typisk de åbne).</param>
    /// <param name="counts">Den aktuelle hånd, eller <c>null</c> hvis der ikke er kastet endnu.</param>
    /// <param name="rerollsLeft">Antal omkast tilbage (3 før første kast).</param>
    /// <param name="monteCarloTrials">Antal simuleringer pr. slag. 0 slår Monte Carlo fra.</param>
    /// <param name="seed">Frø til simuleringen - sat = reproducerbart.</param>
    public static IReadOnlyList<ProbabilityRow> Build(
        IEnumerable<Category> categories,
        int[]? counts,
        int rerollsLeft,
        long monteCarloTrials = 0,
        int? seed = null)
    {
        var solver = OutcomeTreeSolver.Instance;
        var catalog = DiceCatalog.Instance;
        var rows = new List<ProbabilityRow>();
        var seedOffset = 0;

        foreach (var category in categories)
        {
            var solution = solver.Solve(category);
            double treeProbability;
            int[] bestKeep;
            var currentScore = 0;
            var alreadyHit = false;

            if (counts is null)
            {
                treeProbability = solution.ProbabilityFromScratch(rerollsLeft);
                bestKeep = [];
            }
            else
            {
                var stateId = catalog.FullStateId(counts);
                treeProbability = solution.Probability(stateId, rerollsLeft);
                bestKeep = rerollsLeft > 0
                    ? DiceCatalog.ToDice(catalog.Keep(solution.BestKeepId(stateId, rerollsLeft)))
                    : [];
                currentScore = YatzyRules.Score(category, counts);
                alreadyHit = currentScore > 0;
            }

            MonteCarloResult? monteCarlo = null;
            if (monteCarloTrials > 0)
            {
                monteCarlo = MonteCarloEstimator.Estimate(
                    solution,
                    counts,
                    counts is null ? rerollsLeft - 1 : rerollsLeft,
                    monteCarloTrials,
                    seed.HasValue ? seed.Value + seedOffset : null);
            }

            rows.Add(new ProbabilityRow(
                category,
                treeProbability,
                solution.ProbabilityFromScratch(YatzyRules.RollsPerTurn),
                AnalyticProbability.Compute(category),
                monteCarlo,
                bestKeep,
                currentScore,
                alreadyHit));

            seedOffset++;
        }

        return rows;
    }
}
