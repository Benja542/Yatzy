using System.Diagnostics;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Game;

/// <summary>
/// Opsummering af en Monte Carlo-simulering af hele spil.
/// </summary>
public sealed class GameSimulationSummary
{
    /// <summary>Antal spillede spil.</summary>
    public long Games { get; init; }

    /// <summary>Gennemsnitlig slutscore.</summary>
    public double AverageTotal { get; init; }

    /// <summary>Spredningen på slutscoren.</summary>
    public double StandardDeviation { get; init; }

    /// <summary>Standardfejlen på gennemsnittet.</summary>
    public double StandardError { get; init; }

    public int MinTotal { get; init; }

    public int MaxTotal { get; init; }

    /// <summary>Medianen af slutscoren.</summary>
    public double MedianTotal { get; init; }

    /// <summary>Andelen af spil hvor bonussen blev opnået.</summary>
    public double BonusRate { get; init; }

    /// <summary>Gennemsnitlig score i hvert slag.</summary>
    public double[] AverageScore { get; init; } = [];

    /// <summary>Andelen af spil hvor slaget gav point.</summary>
    public double[] HitRate { get; init; } = [];

    /// <summary>Fordeling af slutscoren i intervaller af <see cref="HistogramBucketSize"/>.</summary>
    public IReadOnlyList<(int From, int To, long Count)> Histogram { get; init; } = [];

    public int HistogramBucketSize { get; init; }

    public double ElapsedMilliseconds { get; init; }
}

/// <summary>
/// Kører mange hele spil med <see cref="AutoPlayer"/> og opsummerer resultaterne.
/// Simuleringen kan køres i portioner, så brugerfladen kan vise fremdrift undervejs.
/// </summary>
public sealed class GameSimulation
{
    public const int BucketSize = 25;

    private readonly AutoPlayer _player;
    private readonly Random _random;
    private readonly List<int> _totals = [];
    private readonly double[] _scoreSum = new double[YatzyRules.CategoryCount];
    private readonly long[] _hitCount = new long[YatzyRules.CategoryCount];

    private long _bonusCount;
    private double _elapsedMilliseconds;

    public GameSimulation(int? seed = null, double bonusBias = 2.0)
    {
        _player = new AutoPlayer(bonusBias);
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Antal spillede spil indtil nu.</summary>
    public long Games { get; private set; }

    /// <summary>Spiller yderligere <paramref name="games"/> spil.</summary>
    public void Run(int games)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < games; i++)
        {
            var result = _player.PlayGame(_random);
            _totals.Add(result.Total);

            for (var c = 0; c < YatzyRules.CategoryCount; c++)
            {
                _scoreSum[c] += result.Scores[c];
                if (result.Scores[c] > 0)
                {
                    _hitCount[c]++;
                }
            }

            if (result.Bonus > 0)
            {
                _bonusCount++;
            }

            Games++;
        }

        stopwatch.Stop();
        _elapsedMilliseconds += stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>Den aktuelle opsummering.</summary>
    public GameSimulationSummary Summarize()
    {
        if (Games == 0)
        {
            return new GameSimulationSummary
            {
                AverageScore = new double[YatzyRules.CategoryCount],
                HitRate = new double[YatzyRules.CategoryCount],
                HistogramBucketSize = BucketSize,
            };
        }

        var average = _totals.Average();
        var variance = Games > 1
            ? _totals.Sum(total => (total - average) * (total - average)) / (Games - 1)
            : 0d;
        var standardDeviation = System.Math.Sqrt(variance);

        var sorted = _totals.Order().ToArray();
        var median = sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2d;

        var buckets = new SortedDictionary<int, long>();
        foreach (var total in _totals)
        {
            var bucket = total / BucketSize;
            buckets[bucket] = buckets.GetValueOrDefault(bucket) + 1;
        }

        return new GameSimulationSummary
        {
            Games = Games,
            AverageTotal = average,
            StandardDeviation = standardDeviation,
            StandardError = standardDeviation / System.Math.Sqrt(Games),
            MinTotal = sorted[0],
            MaxTotal = sorted[^1],
            MedianTotal = median,
            BonusRate = (double)_bonusCount / Games,
            AverageScore = _scoreSum.Select(sum => sum / Games).ToArray(),
            HitRate = _hitCount.Select(count => (double)count / Games).ToArray(),
            Histogram = buckets
                .Select(pair => (From: pair.Key * BucketSize, To: (pair.Key + 1) * BucketSize - 1, pair.Value))
                .ToList(),
            HistogramBucketSize = BucketSize,
            ElapsedMilliseconds = _elapsedMilliseconds,
        };
    }
}
