using Yatzy.Core.Dice;
using Yatzy.Core.Math;
using Yatzy.Core.Probability;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Tests;

public class AnalyticProbabilityTests
{
    [Fact]
    public void AlleFormlerStemmerMedEnFuldOptælling()
    {
        // Den stærkeste test vi kan lave: hver analytisk formel sammenlignes med
        // en gennemgang af samtlige 6^6 = 46.656 ordnede udfald.
        foreach (var result in AnalyticProbability.All())
        {
            Assert.Equal(AnalyticProbability.BruteForceCount(result.Category), result.Favourable);
        }
    }

    [Fact]
    public void PartitionerneDækkerAlleUdfald()
    {
        // Summen over alle kastformer skal give præcis antallet af udfald.
        var total = 0L;
        foreach (var shape in AnalyticProbability.Partitions(YatzyRules.DiceCount, YatzyRules.DiceCount))
        {
            total += DiceCatalog.Arrangements(ToCounts(shape)) * FaceAssignments(shape);
        }

        Assert.Equal(AnalyticProbability.TotalOutcomes, total);
    }

    [Theory]
    [InlineData(Category.Sixes, "31031/46656")]
    [InlineData(Category.OnePair, "319/324")]      // 45936/46656
    [InlineData(Category.TwoPairs, "4325/7776")]   // 25950/46656
    [InlineData(Category.ThreeOfAKind, "119/324")] // 17136/46656
    [InlineData(Category.FourOfAKind, "203/3888")] //  2436/46656
    [InlineData(Category.SmallStraight, "35/648")] //  2520/46656
    [InlineData(Category.LargeStraight, "35/648")] //  2520/46656
    [InlineData(Category.FullHouse, "1325/7776")]  //  7950/46656
    [InlineData(Category.Yatzy, "1/7776")]         //     6/46656
    [InlineData(Category.Chance, "1")]
    public void SandsynlighederneErDeForventedeBrøker(Category category, string expected) =>
        Assert.Equal(expected, AnalyticProbability.Compute(category).Probability.ToString());

    [Fact]
    public void ØversteSlagHarAlleSammeSandsynlighed()
    {
        var reference = AnalyticProbability.Compute(Category.Ones).Probability;
        foreach (var category in Categories.Upper)
        {
            Assert.Equal(reference, AnalyticProbability.Compute(category).Probability);
        }
    }

    [Fact]
    public void BrøkenSvarerTilKommatallet() =>
        Assert.Equal(2520d / 46656d, AnalyticProbability.Compute(Category.SmallStraight).Value, 12);

    private static int[] ToCounts(int[] shape)
    {
        var counts = new int[YatzyRules.Faces];
        for (var i = 0; i < shape.Length; i++)
        {
            counts[i] = shape[i];
        }

        return counts;
    }

    private static long FaceAssignments(int[] shape)
    {
        long result = 1;
        for (var i = 0; i < shape.Length; i++)
        {
            result *= YatzyRules.Faces - i;
        }

        foreach (var multiplicity in shape.GroupBy(part => part).Select(g => g.Count()))
        {
            for (var i = 2; i <= multiplicity; i++)
            {
                result /= i;
            }
        }

        return result;
    }
}

public class FractionTests
{
    [Fact]
    public void BrøkerForkortes() => Assert.Equal("35/648", new Fraction(2520, 46656).ToString());

    [Fact]
    public void HeltalSkrivesUdenNævner() => Assert.Equal("2", new Fraction(4, 2).ToString());

    [Fact]
    public void Regnearter()
    {
        Assert.Equal(new Fraction(5, 6), new Fraction(1, 2) + new Fraction(1, 3));
        Assert.Equal(new Fraction(1, 6), new Fraction(1, 2) - new Fraction(1, 3));
        Assert.Equal(new Fraction(1, 6), new Fraction(1, 2) * new Fraction(1, 3));
        Assert.Equal(new Fraction(3, 2), new Fraction(1, 2) / new Fraction(1, 3));
    }

    [Fact]
    public void NegativNævnerNormaliseres() => Assert.Equal("-1/2", new Fraction(1, -2).ToString());

    [Fact]
    public void NulNævnerErUlovlig() => Assert.Throws<DivideByZeroException>(() => new Fraction(1, 0));

    [Fact]
    public void Sammenligning() => Assert.True(new Fraction(1, 3) < new Fraction(1, 2));
}

public class DiceCatalogTests
{
    private readonly DiceCatalog _catalog = DiceCatalog.Instance;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 6)]
    [InlineData(2, 21)]
    [InlineData(3, 56)]
    [InlineData(4, 126)]
    [InlineData(5, 252)]
    [InlineData(6, 462)]
    public void AntalTilstandeErBinomialkoefficienten(int size, int expected) =>
        Assert.Equal(expected, _catalog.StatesOfSize(size).Count);

    [Fact]
    public void SamletAntalBeholdMængder() => Assert.Equal(924, _catalog.KeepCount);

    [Fact]
    public void OvergangssandsynlighederSummererTilEt()
    {
        for (var keepId = 0; keepId < _catalog.KeepCount; keepId++)
        {
            Assert.Equal(1d, _catalog.TransitionProbabilities(keepId).Sum(), 12);
        }
    }

    [Fact]
    public void FordelingenAfEtFrisktKastSummererTilEt() =>
        Assert.Equal(1d, _catalog.FreshRollProbabilities.Sum(), 12);

    [Fact]
    public void AntalUdfaldSummererTilSeksISjettePotens()
    {
        var total = 0L;
        for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
        {
            total += _catalog.FreshRollCount(stateId);
        }

        Assert.Equal(46656L, total);
    }

    [Fact]
    public void EnHåndKanDelesIOpTil64BeholdMængder()
    {
        // Seks forskellige terninger giver 2^6 = 64 delmængder; seks ens giver kun 7.
        var allDifferent = _catalog.FullStateIdFromDice([1, 2, 3, 4, 5, 6]);
        Assert.Equal(64, _catalog.SubKeeps(allDifferent).Length);

        var allSame = _catalog.FullStateIdFromDice([4, 4, 4, 4, 4, 4]);
        Assert.Equal(7, _catalog.SubKeeps(allSame).Length);
    }

    [Fact]
    public void IdOgTællevektorPasserSammen()
    {
        for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
        {
            Assert.Equal(stateId, _catalog.FullStateId(_catalog.FullState(stateId)));
        }
    }
}

public class OutcomeTreeTests
{
    private readonly OutcomeTreeSolver _solver = new();
    private readonly DiceCatalog _catalog = DiceCatalog.Instance;

    [Fact]
    public void UdenOmkastErSandsynlighedenBare0Eller1()
    {
        var solution = _solver.Solve(Category.Yatzy);
        Assert.Equal(1d, solution.Probability(_catalog.FullStateIdFromDice([2, 2, 2, 2, 2, 2]), 0));
        Assert.Equal(0d, solution.Probability(_catalog.FullStateIdFromDice([2, 2, 2, 2, 2, 3]), 0));
    }

    [Fact]
    public void FlereOmkastKanAldrigGøreDetVærre()
    {
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category);
            for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
            {
                Assert.True(solution.Probability(stateId, 1) >= solution.Probability(stateId, 0) - 1e-12);
                Assert.True(solution.Probability(stateId, 2) >= solution.Probability(stateId, 1) - 1e-12);
            }
        }
    }

    [Fact]
    public void SandsynlighederLiggerMellem0Og1()
    {
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category);
            for (var rerolls = 0; rerolls <= solution.MaxRerolls; rerolls++)
            {
                for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
                {
                    var probability = solution.Probability(stateId, rerolls);
                    Assert.InRange(probability, 0d, 1d);
                }
            }
        }
    }

    [Fact]
    public void EtEnkeltKastGiverSammeTalSomDenAnalytiskeFormel()
    {
        // Med nul omkast tilbage er "sandsynligheden for at slå slaget efter ét kast"
        // netop den analytiske sandsynlighed - to helt uafhængige udregninger.
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category);
            Assert.Equal(
                AnalyticProbability.Compute(category).Value,
                solution.ProbabilityFromScratch(1),
                12);
        }
    }

    [Fact]
    public void ØversteSlagRammerSomForventetIEnHelTur()
    {
        // Når man beholder alle terninger med den rigtige øjenværdi, får hver terning
        // tre uafhængige forsøg: P = 1 - ((5/6)^3)^6.
        var expected = 1 - System.Math.Pow(System.Math.Pow(5d / 6d, 3), 6);
        foreach (var category in Categories.Upper)
        {
            Assert.Equal(expected, _solver.Solve(category).ProbabilityFromScratch(3), 12);
        }
    }

    [Fact]
    public void ChanceRammerAltid() =>
        Assert.Equal(1d, _solver.Solve(Category.Chance).ProbabilityFromScratch(3), 12);

    [Fact]
    public void DeToStraightsErSymmetriske() =>
        Assert.Equal(
            _solver.Solve(Category.SmallStraight).ProbabilityFromScratch(3),
            _solver.Solve(Category.LargeStraight).ProbabilityFromScratch(3),
            12);

    [Fact]
    public void DenBedsteBeholdMængdeErOgsåDenBedstRangerede()
    {
        var solution = _solver.Solve(Category.Yatzy);
        var stateId = _catalog.FullStateIdFromDice([5, 5, 5, 2, 3, 4]);

        var ranked = solution.RankKeeps(stateId, 2);
        Assert.Equal(solution.BestKeepId(stateId, 2), ranked[0].KeepId);
        // Til yatzy beholder man naturligvis de tre femmere.
        Assert.Equal(new[] { 5, 5, 5 }, ranked[0].Kept);
    }

    [Fact]
    public void TræetsGreneSummererTilEt()
    {
        var solution = _solver.Solve(Category.LargeStraight);
        var stateId = _catalog.FullStateIdFromDice([2, 3, 4, 6, 6, 1]);
        var tree = solution.BuildTree(stateId, 2, maxChildren: 4);

        var shown = tree.Children.Sum(child => child.BranchProbability);
        Assert.Equal(1d, shown + tree.TruncatedProbability, 12);
    }

    [Fact]
    public void TræetsRodVærdiPasserMedTabellen()
    {
        var solution = _solver.Solve(Category.FullHouse);
        var stateId = _catalog.FullStateIdFromDice([3, 3, 3, 5, 1, 2]);
        Assert.Equal(solution.Probability(stateId, 2), solution.BuildTree(stateId, 2).Value, 12);
    }
}

public class MonteCarloTests
{
    private readonly OutcomeTreeSolver _solver = new();

    [Fact]
    public void SimuleringenRammerDenEksakteVærdi()
    {
        // Med 200.000 forsøg pr. slag skal den eksakte værdi ligge inden for
        // konfidensintervallet. Fast frø, så testen ikke er flaky.
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category);
            var exact = solution.ProbabilityFromScratch(YatzyRules.RollsPerTurn);
            var result = MonteCarloEstimator.EstimateFromScratch(solution, YatzyRules.RollsPerTurn, 200_000, seed: 20240607);

            Assert.InRange(exact, result.LowerBound, result.UpperBound);
        }
    }

    [Fact]
    public void SimuleringenErReproducerbarMedSammeFrø()
    {
        var solution = _solver.Solve(Category.FourOfAKind);
        var first = MonteCarloEstimator.EstimateFromScratch(solution, 3, 5_000, seed: 42);
        var second = MonteCarloEstimator.EstimateFromScratch(solution, 3, 5_000, seed: 42);
        Assert.Equal(first.Hits, second.Hits);
    }

    [Fact]
    public void KonfidensintervalletBliverSmallereMedFlereForsøg()
    {
        var solution = _solver.Solve(Category.SmallStraight);
        var few = MonteCarloEstimator.EstimateFromScratch(solution, 3, 1_000, seed: 7);
        var many = MonteCarloEstimator.EstimateFromScratch(solution, 3, 100_000, seed: 7);
        Assert.True(many.MarginOfError < few.MarginOfError);
    }

    [Fact]
    public void WilsonIntervalletErGyldigtVedNulHits()
    {
        var result = MonteCarloEstimator.Build(Category.Yatzy, trials: 1_000, hits: 0, elapsedMilliseconds: 0);
        Assert.Equal(0d, result.Estimate);
        Assert.Equal(0d, result.LowerBound, 12);
        Assert.InRange(result.UpperBound, 0d, 0.01d);
    }

    [Fact]
    public void SimuleringFraEnGivenHåndFølgerUdfaldstræet()
    {
        var solution = _solver.Solve(Category.Yatzy);
        int[] counts = [0, 0, 0, 0, 5, 1]; // fem femmere og en sekser
        var exact = solution.Probability(DiceCatalog.Instance.FullStateId(counts), 2);
        var result = MonteCarloEstimator.Estimate(solution, counts, 2, 200_000, seed: 99);

        Assert.InRange(exact, result.LowerBound, result.UpperBound);
    }
}
