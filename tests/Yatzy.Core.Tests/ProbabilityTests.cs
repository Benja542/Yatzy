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
    [InlineData(Category.ThreePairs, "25/648")]    //  1800/46656
    [InlineData(Category.FiveOfAKind, "31/7776")]  //   186/46656
    [InlineData(Category.FullStraight, "5/324")]   //   720/46656
    [InlineData(Category.House, "1325/7776")]      //  7950/46656
    [InlineData(Category.Villa, "25/3888")]        //   300/46656
    [InlineData(Category.Tower, "25/2592")]        //   450/46656
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
        var solution = _solver.Solve(Category.House);
        var stateId = _catalog.FullStateIdFromDice([3, 3, 3, 5, 1, 2]);
        Assert.Equal(solution.Probability(stateId, 2), solution.BuildTree(stateId, 2).Value, 12);
    }
}

public class MaxPointsTests
{
    private readonly OutcomeTreeSolver _solver = new();
    private readonly DiceCatalog _catalog = DiceCatalog.Instance;

    [Fact]
    public void MaksPointErAldrigMereSandsynligtEndBareAtRamme()
    {
        foreach (var category in Categories.All)
        {
            var hit = _solver.Solve(category, ScoreGoal.AnyPoints);
            var max = _solver.Solve(category, ScoreGoal.MaxPoints);

            for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
            {
                for (var rerolls = 0; rerolls <= 2; rerolls++)
                {
                    Assert.True(
                        max.Probability(stateId, rerolls) <= hit.Probability(stateId, rerolls) + 1e-12,
                        $"{Categories.DanishName(category)} med {rerolls} omkast");
                }
            }
        }
    }

    [Fact]
    public void DeToMålErEnsForSlagMedFastPointværdi()
    {
        // Straights og yatzy giver enten deres faste point eller ingenting.
        foreach (var category in Categories.All.Where(ScoreGoals.AreEquivalent))
        {
            var hit = _solver.Solve(category, ScoreGoal.AnyPoints);
            var max = _solver.Solve(category, ScoreGoal.MaxPoints);
            Assert.Equal(hit.ProbabilityFromScratch(3), max.ProbabilityFromScratch(3), 12);
        }
    }

    [Fact]
    public void MaksPointIEtKastStemmerMedEnFuldOptælling()
    {
        // Den analytiske optælling over hænder mod en gennemgang af alle 46.656 udfald.
        foreach (var category in Categories.All)
        {
            var analytic = AnalyticProbability.Compute(category, ScoreGoal.MaxPoints);
            Assert.Equal(AnalyticProbability.BruteForceCount(category, ScoreGoal.MaxPoints), analytic.Favourable);
        }
    }

    [Fact]
    public void EtEnkeltKastGiverSammeTalSomUdfaldstræet()
    {
        foreach (var category in Categories.All)
        {
            Assert.Equal(
                AnalyticProbability.Compute(category, ScoreGoal.MaxPoints).Value,
                _solver.Solve(category, ScoreGoal.MaxPoints).ProbabilityFromScratch(1),
                12);
        }
    }

    [Theory]
    // Maks i enere er 6 point, altså seks 1'ere - lige så svært som en yatzy.
    [InlineData(Category.Ones, "1/46656")]
    [InlineData(Category.Sixes, "1/46656")]
    // Maks i chance er seks seksere.
    [InlineData(Category.Chance, "1/46656")]
    // Maks i yatzy er yatzy - seks ens af en hvilken som helst øjenværdi.
    [InlineData(Category.Yatzy, "1/7776")]
    public void KendteMaksSandsynligheder(Category category, string expected) =>
        Assert.Equal(expected, AnalyticProbability.Compute(category, ScoreGoal.MaxPoints).Probability.ToString());
}

public class KeepConditionedTests
{
    private readonly OutcomeTreeSolver _solver = new();
    private readonly DiceCatalog _catalog = DiceCatalog.Instance;

    [Fact]
    public void DenBedsteMarkeringGiverSammeTalSomDenOptimaleVærdi()
    {
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category, ScoreGoal.MaxPoints);
            for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
            {
                var best = solution.BestKeepId(stateId, 2);
                Assert.Equal(solution.Probability(stateId, 2), solution.ProbabilityWithKeep(best, 2), 12);
            }
        }
    }

    [Fact]
    public void IngenMarkeringKanSlåDenOptimale()
    {
        var solution = _solver.Solve(Category.Sixes, ScoreGoal.MaxPoints);
        for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
        {
            var optimal = solution.Probability(stateId, 2);
            foreach (var keepId in _catalog.SubKeeps(stateId))
            {
                Assert.True(solution.ProbabilityWithKeep(keepId, 2) <= optimal + 1e-12);
            }
        }
    }

    [Fact]
    public void EnDårligMarkeringGiverEnLavereSandsynlighed()
    {
        // Fem seksere og en etter, mål: maks i seksere (36 point).
        // Beholder man de fem seksere, mangler man kun én; beholder man etteren i
        // stedet, skal alle fem omkastede blive seksere.
        var solution = _solver.Solve(Category.Sixes, ScoreGoal.MaxPoints);
        var stateId = _catalog.FullStateIdFromDice([6, 6, 6, 6, 6, 1]);

        var keepSixes = solution.ProbabilityWithKeep(_catalog.KeepId([0, 0, 0, 0, 0, 5]), 1);
        var keepOne = solution.ProbabilityWithKeep(_catalog.KeepId([1, 0, 0, 0, 0, 0]), 1);

        Assert.Equal(1d / 6d, keepSixes, 12);
        Assert.Equal(0d, keepOne, 12);
        Assert.Equal(keepSixes, solution.Probability(stateId, 1), 12);
    }

    [Fact]
    public void SimuleringenFølgerMarkeringenOgIkkeStrategien()
    {
        // Med en bevidst dårlig markering skal simuleringen ramme den lave, eksakte værdi
        // - ikke den optimale. Det er testen af at markeringen faktisk bliver brugt.
        var solution = _solver.Solve(Category.Sixes, ScoreGoal.MaxPoints);
        int[] counts = [1, 0, 0, 0, 0, 5];
        int[] badKeep = [1, 0, 0, 0, 0, 2];

        var exact = solution.ProbabilityWithKeep(_catalog.KeepId(badKeep), 2);
        var optimal = solution.Probability(_catalog.FullStateId(counts), 2);
        var result = MonteCarloEstimator.Estimate(solution, counts, 2, 200_000, seed: 4242, firstKeep: badKeep);

        Assert.True(exact < optimal, "den dårlige markering skal være ringere end den optimale");
        Assert.InRange(result.Estimate, exact - 4 * result.StandardError - 1e-4, exact + 4 * result.StandardError + 1e-4);
    }

    [Fact]
    public void MarkeringenSkalVæreEnDelmængdeAfHånden()
    {
        var solution = _solver.Solve(Category.Sixes, ScoreGoal.MaxPoints);
        int[] counts = [0, 0, 0, 0, 0, 6];
        int[] impossible = [3, 0, 0, 0, 0, 3];

        Assert.Throws<ArgumentException>(
            () => new MonteCarloEstimator(solution, counts, 2, seed: 1, firstKeep: impossible));
    }

    [Fact]
    public void TræetsRodBrugerDenValgteMarkering()
    {
        var solution = _solver.Solve(Category.Sixes, ScoreGoal.MaxPoints);
        var stateId = _catalog.FullStateIdFromDice([6, 6, 6, 6, 6, 1]);
        int[] keep = [1, 0, 0, 0, 0, 4];

        var tree = solution.BuildTree(stateId, 2, maxChildren: 4, forcedKeep: keep);

        Assert.Equal(new[] { 1, 6, 6, 6, 6 }, tree.Kept);
        Assert.Equal(solution.ProbabilityWithKeep(_catalog.KeepId(keep), 2), tree.Value, 12);
        Assert.Equal(1d, tree.Children.Sum(child => child.BranchProbability) + tree.TruncatedProbability, 12);
    }

    [Fact]
    public void RapportenBrugerMarkeringenIAlleTreKolonner()
    {
        int[] counts = [1, 0, 0, 0, 0, 5];
        int[] keep = [0, 0, 0, 0, 0, 5];

        var rows = ProbabilityReport.Build(
            [Category.Sixes], counts, keep, rerollsLeft: 1, ScoreGoal.MaxPoints,
            monteCarloTrials: 50_000, seed: 77);

        var row = Assert.Single(rows);
        Assert.Equal(36, row.MaxScore);
        Assert.Equal(30, row.CurrentScore);
        Assert.Equal(1d / 6d, row.TreeProbability, 12);
        Assert.Equal(row.BestProbability, row.TreeProbability, 12);

        var mc = row.MonteCarlo!.Value;
        Assert.Equal(ScoreGoal.MaxPoints, mc.Goal);
        Assert.InRange(row.TreeProbability, mc.LowerBound, mc.UpperBound);
    }
}

public class MonteCarloTests
{
    private readonly OutcomeTreeSolver _solver = new();

    [Fact]
    public void SimuleringenRammerDenEksakteVærdi()
    {
        // Estimatet skal ligge inden for fire standardfejl af den eksakte værdi.
        // Vi bruger ikke 95 %-intervallet her: det rammer per definition forbi i ca.
        // 1 af 20 tilfælde, og med 20 slag ville testen så fejle i godt hver tredje
        // kørsel. Fire standardfejl svarer til ca. 1 fejl ud af 16.000.
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category);
            var exact = solution.ProbabilityFromScratch(YatzyRules.RollsPerTurn);
            var result = MonteCarloEstimator.EstimateFromScratch(solution, YatzyRules.RollsPerTurn, 200_000, seed: 20240607);

            var deviation = System.Math.Abs(result.Estimate - exact);
            var tolerance = 4 * result.StandardError + 1e-4;

            Assert.True(
                deviation <= tolerance,
                $"{Categories.DanishName(category)}: simulering {result.Estimate:P4}, " +
                $"eksakt {exact:P4}, afvigelse {deviation:P4} > {tolerance:P4}");
        }
    }

    [Fact]
    public void KonfidensintervallerneDækkerNæstenAlleSlag()
    {
        // Et 95 %-interval skal dække den eksakte værdi for de allerfleste slag.
        // Vi tillader et par afvigere, netop fordi intervallet er 95 % og ikke 100 %.
        var covered = 0;
        foreach (var category in Categories.All)
        {
            var solution = _solver.Solve(category);
            var exact = solution.ProbabilityFromScratch(YatzyRules.RollsPerTurn);
            var result = MonteCarloEstimator.EstimateFromScratch(solution, YatzyRules.RollsPerTurn, 200_000, seed: 20240607);

            if (exact >= result.LowerBound && exact <= result.UpperBound)
            {
                covered++;
            }
        }

        Assert.True(covered >= Categories.All.Length - 3, $"kun {covered} af {Categories.All.Length} slag var dækket");
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
        var result = MonteCarloEstimator.Build(Category.Yatzy, ScoreGoal.AnyPoints, trials: 1_000, hits: 0, elapsedMilliseconds: 0);
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
