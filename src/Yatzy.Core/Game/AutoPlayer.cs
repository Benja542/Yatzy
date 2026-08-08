using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Game;

/// <summary>
/// Resultatet af ét simuleret spil.
/// </summary>
/// <param name="Scores">Scoren i hvert af de 20 slag, indekseret efter <see cref="Category"/>.</param>
/// <param name="UpperSum">Summen af den øverste del.</param>
/// <param name="Bonus">Bonus (0 eller 100).</param>
/// <param name="Total">Samlet score.</param>
public sealed record GameResult(int[] Scores, int UpperSum, int Bonus, int Total);

/// <summary>
/// En computerspiller. Den bruges til at simulere hele spil, så man kan se hvordan
/// sandsynlighederne slår igennem på slutscoren.
/// </summary>
/// <remarks>
/// <para>
/// Strategien er en to-trins expectimax inden for turen: for hver mulig
/// "behold"-mængde beregnes den forventede værdi af den bedste skrivning efter de
/// resterende omkast. Det er samme udfoldning af udfaldstræet som i
/// <see cref="Probability.OutcomeTreeSolver"/>, men med point i bladene i stedet for 0/1.
/// </para>
/// <para>
/// Værdien af en hånd justeres med et bonus-incitament: for de øverste slag tæller
/// point ud over "fire ens af øjenværdien" ekstra, fordi netop de point er dem der
/// bringer én over de 84 point. Det er en heuristik - ikke en optimal løsning af hele
/// spillet, som ville kræve et tilstandsrum på 2^20 blokke gange bonus-status.
/// </para>
/// </remarks>
public sealed class AutoPlayer
{
    /// <summary>Rækkefølgen slag ofres i, når intet giver point.</summary>
    private static readonly Category[] SacrificeOrder =
    [
        Category.Yatzy, Category.FullStraight, Category.Villa, Category.Tower,
        Category.FiveOfAKind, Category.ThreePairs, Category.LargeStraight, Category.SmallStraight,
        Category.FourOfAKind, Category.House, Category.Ones, Category.TwoPairs,
        Category.Twos, Category.ThreeOfAKind, Category.Threes, Category.OnePair,
        Category.Fours, Category.Fives, Category.Sixes, Category.Chance,
    ];

    private readonly DiceCatalog _catalog = DiceCatalog.Instance;
    private readonly double _bonusBias;

    private readonly double[] _leafValue;
    private readonly double[][] _stateValue;
    private readonly int[][] _bestKeep;

    /// <param name="bonusBias">
    /// Hvor hårdt der spilles efter bonussen. 0 = slet ikke. Standarden 2 er fundet ved
    /// at afprøve værdier i simuleringen: den hæver både bonusandelen og slutscoren.
    /// </param>
    public AutoPlayer(double bonusBias = 2.0)
    {
        _bonusBias = bonusBias;
        _leafValue = new double[_catalog.FullStateCount];
        _stateValue = new double[YatzyRules.RollsPerTurn][];
        _bestKeep = new int[YatzyRules.RollsPerTurn][];
        for (var r = 0; r < YatzyRules.RollsPerTurn; r++)
        {
            _stateValue[r] = new double[_catalog.FullStateCount];
            _bestKeep[r] = new int[_catalog.FullStateCount];
        }
    }

    /// <summary>Spiller ét helt spil på 20 runder.</summary>
    public GameResult PlayGame(Random random)
    {
        var sheet = new ScoreSheet();
        Span<int> counts = stackalloc int[YatzyRules.Faces];

        while (!sheet.IsComplete)
        {
            PlanTurn(sheet);

            counts.Clear();
            Roll(counts, YatzyRules.DiceCount, random);

            for (var rerollsLeft = YatzyRules.RollsPerTurn - 1; rerollsLeft >= 1; rerollsLeft--)
            {
                var stateId = _catalog.FullStateId(counts);
                var keepId = _bestKeep[rerollsLeft][stateId];
                var keep = _catalog.Keep(keepId);
                for (var f = 0; f < YatzyRules.Faces; f++)
                {
                    counts[f] = keep[f];
                }

                Roll(counts, YatzyRules.DiceCount - _catalog.KeepSize(keepId), random);
            }

            sheet.Write(ChooseCategory(counts, sheet), counts);
        }

        var scores = sheet.ToArray().Select(score => score ?? 0).ToArray();
        return new GameResult(scores, sheet.UpperSum, sheet.Bonus, sheet.Total);
    }

    /// <summary>
    /// Forbereder turen: bladværdier for alle 462 hænder og derefter to niveauer
    /// af udfaldstræet. Skal kaldes når blokken har ændret sig.
    /// </summary>
    public void PlanTurn(ScoreSheet sheet)
    {
        // Bonus-incitamentet afhænger kun af blokken, ikke af hånden, så det slås op én gang.
        var chaseBonus = sheet.BonusStillPossible;
        for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
        {
            _leafValue[stateId] = BestWriteValue(stateId, sheet, chaseBonus);
            _stateValue[0][stateId] = _leafValue[stateId];
            _bestKeep[0][stateId] = _catalog.KeepId(_catalog.FullState(stateId));
        }

        var keepValue = new double[_catalog.KeepCount];
        for (var r = 1; r < YatzyRules.RollsPerTurn; r++)
        {
            var previous = _stateValue[r - 1];
            for (var keepId = 0; keepId < _catalog.KeepCount; keepId++)
            {
                var targets = _catalog.TransitionTargets(keepId);
                var probabilities = _catalog.TransitionProbabilities(keepId);
                var sum = 0d;
                for (var i = 0; i < targets.Length; i++)
                {
                    sum += probabilities[i] * previous[targets[i]];
                }

                keepValue[keepId] = sum;
            }

            for (var stateId = 0; stateId < _catalog.FullStateCount; stateId++)
            {
                var best = double.NegativeInfinity;
                var bestId = -1;
                foreach (var keepId in _catalog.SubKeeps(stateId))
                {
                    if (keepValue[keepId] > best)
                    {
                        best = keepValue[keepId];
                        bestId = keepId;
                    }
                }

                _stateValue[r][stateId] = best;
                _bestKeep[r][stateId] = bestId;
            }
        }
    }

    /// <summary>Den "behold"-mængde computerspilleren ville vælge. Kræver et forudgående <see cref="PlanTurn"/>.</summary>
    public int ChooseKeep(int stateId, int rerollsLeft) => _bestKeep[System.Math.Min(rerollsLeft, YatzyRules.RollsPerTurn - 1)][stateId];

    /// <summary>Det slag computerspilleren ville skrive hånden i.</summary>
    public Category ChooseCategory(ReadOnlySpan<int> counts, ScoreSheet sheet)
    {
        var best = Category.Chance;
        var bestValue = double.NegativeInfinity;
        var anyPoints = false;

        foreach (var category in Categories.All)
        {
            if (sheet.IsFilled(category))
            {
                continue;
            }

            var score = YatzyRules.Score(category, counts);
            if (score > 0)
            {
                anyPoints = true;
            }

            var value = AdjustedValue(category, score, sheet);
            if (value > bestValue)
            {
                bestValue = value;
                best = category;
            }
        }

        if (anyPoints)
        {
            return best;
        }

        foreach (var category in SacrificeOrder)
        {
            if (!sheet.IsFilled(category))
            {
                return category;
            }
        }

        return best;
    }

    /// <summary>Værdien af den bedste skrivning af en hånd. Slår scoren op i <see cref="ScoreTable"/>.</summary>
    private double BestWriteValue(int stateId, ScoreSheet sheet, bool chaseBonus)
    {
        var scores = ScoreTable.ForState(stateId);
        var best = double.NegativeInfinity;

        foreach (var category in Categories.All)
        {
            if (sheet.IsFilled(category))
            {
                continue;
            }

            var value = (double)scores[(int)category];
            if (chaseBonus && Categories.IsUpper(category))
            {
                value += _bonusBias * (value - 4 * Categories.UpperFace(category));
            }

            best = System.Math.Max(best, value);
        }

        return best == double.NegativeInfinity ? 0d : best;
    }

    /// <summary>Point plus bonus-incitament.</summary>
    private double AdjustedValue(Category category, int score, ScoreSheet sheet)
    {
        if (!Categories.IsUpper(category) || !sheet.BonusStillPossible)
        {
            return score;
        }

        // 84 point svarer til fire af hver øjenværdi. Point over/under det niveau
        // trækker bonussen tættere på eller længere væk.
        var face = Categories.UpperFace(category);
        return score + _bonusBias * (score - 4 * face);
    }

    private static void Roll(Span<int> counts, int dice, Random random)
    {
        for (var i = 0; i < dice; i++)
        {
            counts[random.Next(YatzyRules.Faces)]++;
        }
    }
}
