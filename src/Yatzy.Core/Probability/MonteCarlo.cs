using System.Diagnostics;
using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Probability;

/// <summary>
/// Resultatet af en Monte Carlo-simulering.
/// </summary>
/// <param name="Category">Slaget der blev simuleret.</param>
/// <param name="Trials">Antal simulerede ture.</param>
/// <param name="Hits">Antal gange slaget blev slået.</param>
/// <param name="Estimate">Den estimerede sandsynlighed (hits / trials).</param>
/// <param name="StandardError">Standardfejlen på estimatet.</param>
/// <param name="LowerBound">Nedre grænse i 95 %-konfidensintervallet (Wilson).</param>
/// <param name="UpperBound">Øvre grænse i 95 %-konfidensintervallet (Wilson).</param>
/// <param name="ElapsedMilliseconds">Tidsforbrug.</param>
public readonly record struct MonteCarloResult(
    Category Category,
    long Trials,
    long Hits,
    double Estimate,
    double StandardError,
    double LowerBound,
    double UpperBound,
    double ElapsedMilliseconds)
{
    /// <summary>Halv bredde af konfidensintervallet - praktisk som "± x" i tabellen.</summary>
    public double MarginOfError => (UpperBound - LowerBound) / 2d;
}

/// <summary>
/// Estimerer sandsynligheden for at slå et slag ved at kaste terninger tilfældigt
/// mange gange - Monte Carlo-metoden.
/// </summary>
/// <remarks>
/// Simuleringen bruger nøjagtig samme strategi som udfaldstræet regner med (den
/// optimale "behold"-beslutning fra <see cref="CategorySolution"/>). Derfor skal de to
/// tal konvergere mod hinanden, og forskellen er ren simuleringsusikkerhed.
/// Det gør estimatet til en uafhængig kontrol af den eksakte beregning.
/// </remarks>
public sealed class MonteCarloEstimator
{
    private const double Z95 = 1.959963984540054;

    private readonly CategorySolution _solution;
    private readonly DiceCatalog _catalog;
    private readonly int[]? _startCounts;
    private readonly int _rerollsLeft;
    private readonly Random _random;
    private readonly int[] _counts = new int[YatzyRules.Faces];

    private double _elapsedMilliseconds;

    /// <summary>
    /// Opretter en estimator.
    /// </summary>
    /// <param name="solution">Den løste kategori - leverer strategien.</param>
    /// <param name="startCounts">
    /// Den hånd der simuleres fra, eller <c>null</c> for at starte forfra med et
    /// friskt kast med alle seks terninger.
    /// </param>
    /// <param name="rerollsLeft">Antal omkast tilbage efter starthånden.</param>
    /// <param name="seed">Frø til tilfældighedsgeneratoren. Sat = reproducerbart resultat.</param>
    public MonteCarloEstimator(CategorySolution solution, int[]? startCounts, int rerollsLeft, int? seed = null)
    {
        if (rerollsLeft < 0 || rerollsLeft > solution.MaxRerolls)
        {
            throw new ArgumentOutOfRangeException(nameof(rerollsLeft), rerollsLeft, "Ugyldigt antal omkast.");
        }

        _solution = solution;
        _catalog = DiceCatalog.Instance;
        _startCounts = startCounts?.ToArray();
        _rerollsLeft = rerollsLeft;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public Category Category => _solution.Category;

    /// <summary>Antal simulerede ture indtil nu.</summary>
    public long Trials { get; private set; }

    /// <summary>Antal gange slaget blev slået indtil nu.</summary>
    public long Hits { get; private set; }

    /// <summary>Kører yderligere <paramref name="trials"/> simuleringer og lægger dem oveni.</summary>
    public void Run(long trials)
    {
        var stopwatch = Stopwatch.StartNew();
        for (long i = 0; i < trials; i++)
        {
            if (SimulateOnce())
            {
                Hits++;
            }
        }

        Trials += trials;
        stopwatch.Stop();
        _elapsedMilliseconds += stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>Det aktuelle estimat med 95 %-konfidensinterval.</summary>
    public MonteCarloResult Result => Build(Category, Trials, Hits, _elapsedMilliseconds);

    private bool SimulateOnce()
    {
        int rerolls;
        if (_startCounts is null)
        {
            // Frisk tur: første kast med alle seks terninger, derefter de resterende omkast.
            Array.Clear(_counts);
            RollInto(_counts, YatzyRules.DiceCount);
            rerolls = _rerollsLeft;
        }
        else
        {
            Array.Copy(_startCounts, _counts, _counts.Length);
            rerolls = _rerollsLeft;
        }

        for (var r = rerolls; r >= 1; r--)
        {
            var stateId = _catalog.FullStateId(_counts);
            var keepId = _solution.BestKeepId(stateId, r);
            var keep = _catalog.Keep(keepId);
            Array.Copy(keep, _counts, _counts.Length);
            RollInto(_counts, YatzyRules.DiceCount - _catalog.KeepSize(keepId));
        }

        return YatzyRules.IsHit(Category, _counts);
    }

    private void RollInto(int[] counts, int dice)
    {
        for (var i = 0; i < dice; i++)
        {
            counts[_random.Next(YatzyRules.Faces)]++;
        }
    }

    /// <summary>Kører en simulering fra en given hånd i én kaldelse.</summary>
    public static MonteCarloResult Estimate(
        CategorySolution solution, int[]? startCounts, int rerollsLeft, long trials, int? seed = null)
    {
        var estimator = new MonteCarloEstimator(solution, startCounts, rerollsLeft, seed);
        estimator.Run(trials);
        return estimator.Result;
    }

    /// <summary>
    /// Kører en simulering af en hel tur forfra: første kast plus
    /// <paramref name="rollsLeft"/> - 1 omkast.
    /// </summary>
    public static MonteCarloResult EstimateFromScratch(
        CategorySolution solution, int rollsLeft, long trials, int? seed = null) =>
        Estimate(solution, startCounts: null, rerollsLeft: rollsLeft - 1, trials, seed);

    /// <summary>Bygger et resultat med Wilson-konfidensinterval ud fra antal forsøg og hits.</summary>
    public static MonteCarloResult Build(Category category, long trials, long hits, double elapsedMilliseconds)
    {
        if (trials <= 0)
        {
            return new MonteCarloResult(category, 0, 0, double.NaN, double.NaN, double.NaN, double.NaN, elapsedMilliseconds);
        }

        var n = (double)trials;
        var p = hits / n;
        var standardError = System.Math.Sqrt(p * (1 - p) / n);

        // Wilson score-interval - langt mere retvisende end det simple normalinterval
        // når sandsynligheden er meget lille, hvilket den er for fx yatzy.
        var z2 = Z95 * Z95;
        var denominator = 1 + z2 / n;
        var centre = (p + z2 / (2 * n)) / denominator;
        var spread = Z95 * System.Math.Sqrt(p * (1 - p) / n + z2 / (4 * n * n)) / denominator;

        return new MonteCarloResult(
            category, trials, hits, p, standardError,
            System.Math.Max(0d, centre - spread),
            System.Math.Min(1d, centre + spread),
            elapsedMilliseconds);
    }
}
