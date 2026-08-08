using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Tests;

public class YatzyRulesTests
{
    private static int Score(Category category, params int[] dice) => YatzyRules.ScoreDice(category, dice);

    // Hånden 1-1-1-2-2-6 giver 3 i enere, 4 i toere, ingenting i treere/firere/femmere og 6 i seksere.
    [Theory]
    [InlineData(Category.Ones, 3)]
    [InlineData(Category.Twos, 4)]
    [InlineData(Category.Threes, 0)]
    [InlineData(Category.Fours, 0)]
    [InlineData(Category.Fives, 0)]
    [InlineData(Category.Sixes, 6)]
    public void ØversteSlagGiverSummenAfØjenværdien(Category category, int expected) =>
        Assert.Equal(expected, Score(category, 1, 1, 1, 2, 2, 6));

    [Fact]
    public void EtParTagerDetHøjestePar()
    {
        Assert.Equal(12, Score(Category.OnePair, 2, 2, 5, 5, 6, 6));
        Assert.Equal(0, Score(Category.OnePair, 1, 2, 3, 4, 5, 6));
    }

    [Fact]
    public void ToParTagerDeToHøjestePar()
    {
        Assert.Equal(22, Score(Category.TwoPairs, 2, 2, 5, 5, 6, 6));
        // Fire ens tæller kun som ét par - der skal to forskellige øjenværdier til.
        Assert.Equal(0, Score(Category.TwoPairs, 4, 4, 4, 4, 1, 3));
        Assert.Equal(20, Score(Category.TwoPairs, 4, 4, 4, 4, 6, 6));
    }

    [Fact]
    public void TreParKræverTreForskelligeØjenværdier()
    {
        Assert.Equal(24, Score(Category.ThreePairs, 2, 2, 4, 4, 6, 6));
        Assert.Equal(30, Score(Category.ThreePairs, 4, 4, 5, 5, 6, 6));
        // 4+2 er kun to forskellige øjenværdier, altså ikke tre par.
        Assert.Equal(0, Score(Category.ThreePairs, 3, 3, 3, 3, 5, 5));
        Assert.Equal(0, Score(Category.ThreePairs, 1, 2, 3, 4, 5, 5));
    }

    [Fact]
    public void TreOgFireEns()
    {
        Assert.Equal(15, Score(Category.ThreeOfAKind, 5, 5, 5, 1, 2, 3));
        Assert.Equal(0, Score(Category.ThreeOfAKind, 5, 5, 1, 1, 2, 3));
        Assert.Equal(24, Score(Category.FourOfAKind, 6, 6, 6, 6, 1, 2));
        Assert.Equal(0, Score(Category.FourOfAKind, 6, 6, 6, 1, 1, 2));
        Assert.Equal(25, Score(Category.FiveOfAKind, 5, 5, 5, 5, 5, 2));
        Assert.Equal(30, Score(Category.FiveOfAKind, 6, 6, 6, 6, 6, 6));
        Assert.Equal(0, Score(Category.FiveOfAKind, 6, 6, 6, 6, 1, 1));
    }

    [Fact]
    public void StraightsKræverDeFemRigtigeØjenværdier()
    {
        Assert.Equal(15, Score(Category.SmallStraight, 1, 2, 3, 4, 5, 5));
        Assert.Equal(15, Score(Category.SmallStraight, 1, 2, 3, 4, 5, 6));
        Assert.Equal(0, Score(Category.SmallStraight, 1, 2, 3, 4, 6, 6));

        Assert.Equal(20, Score(Category.LargeStraight, 2, 3, 4, 5, 6, 6));
        Assert.Equal(20, Score(Category.LargeStraight, 1, 2, 3, 4, 5, 6));
        Assert.Equal(0, Score(Category.LargeStraight, 1, 2, 3, 4, 5, 5));

        // Fuld straight kræver alle seks øjenværdier - altså præcis én af hver.
        Assert.Equal(21, Score(Category.FullStraight, 1, 2, 3, 4, 5, 6));
        Assert.Equal(0, Score(Category.FullStraight, 1, 2, 3, 4, 5, 5));
        Assert.Equal(0, Score(Category.FullStraight, 2, 3, 4, 5, 6, 6));
    }

    [Fact]
    public void HusVælgerDenBedsteKombination()
    {
        Assert.Equal(28, Score(Category.House, 6, 6, 6, 5, 5, 1));
        // 3+3: tre seksere plus et par femmere betaler bedst.
        Assert.Equal(28, Score(Category.House, 5, 5, 5, 6, 6, 6));
        // 4+2 tæller også - tre af de fire ens plus parret.
        Assert.Equal(16, Score(Category.House, 2, 2, 2, 2, 5, 5));
        Assert.Equal(0, Score(Category.House, 3, 3, 3, 1, 2, 4));
        // Seks ens er ikke et hus - parret skal have en anden øjenværdi.
        Assert.Equal(0, Score(Category.House, 4, 4, 4, 4, 4, 4));
    }

    [Fact]
    public void VillaErTreEnsPlusTreEns()
    {
        Assert.Equal(33, Score(Category.Villa, 5, 5, 5, 6, 6, 6));
        Assert.Equal(9, Score(Category.Villa, 1, 1, 1, 2, 2, 2));
        // 4+2 rækker ikke - den anden gruppe skal være tre ens.
        Assert.Equal(0, Score(Category.Villa, 3, 3, 3, 3, 5, 5));
        Assert.Equal(0, Score(Category.Villa, 4, 4, 4, 4, 4, 4));
    }

    [Fact]
    public void TårnErFireEnsPlusEtPar()
    {
        Assert.Equal(34, Score(Category.Tower, 6, 6, 6, 6, 5, 5));
        Assert.Equal(16, Score(Category.Tower, 2, 2, 2, 2, 4, 4));
        // Fem ens plus én terning giver ikke et par til toppen af tårnet.
        Assert.Equal(0, Score(Category.Tower, 6, 6, 6, 6, 6, 3));
        Assert.Equal(0, Score(Category.Tower, 3, 3, 3, 5, 5, 5));
    }

    [Fact]
    public void ChanceOgYatzy()
    {
        Assert.Equal(21, Score(Category.Chance, 1, 2, 3, 4, 5, 6));
        Assert.Equal(100, Score(Category.Yatzy, 3, 3, 3, 3, 3, 3));
        Assert.Equal(0, Score(Category.Yatzy, 3, 3, 3, 3, 3, 4));
    }

    [Theory]
    [InlineData(83, 0)]
    [InlineData(84, 100)]
    [InlineData(120, 100)]
    public void BonusGivesVedMindst84(int upperSum, int expected) =>
        Assert.Equal(expected, YatzyRules.Bonus(upperSum));

    [Fact]
    public void IsHitFølgerScoren()
    {
        foreach (var category in Categories.All)
        {
            var counts = YatzyRules.CountFaces([1, 2, 3, 4, 5, 6]);
            Assert.Equal(YatzyRules.Score(category, counts) > 0, YatzyRules.IsHit(category, counts));
        }
    }

    [Fact]
    public void BlokkenHar20Slag()
    {
        Assert.Equal(20, YatzyRules.CategoryCount);
        Assert.Equal(YatzyRules.CategoryCount, Categories.All.Length);
        Assert.Equal(YatzyRules.CategoryCount, Categories.All.Distinct().Count());
        // Alle enum-værdier skal være med på blokken.
        Assert.Equal(Enum.GetValues<Category>().OrderBy(c => c), Categories.All.OrderBy(c => c));
    }

    [Fact]
    public void AlleSlagHarNavnOgBeskrivelse()
    {
        foreach (var category in Categories.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(Categories.DanishName(category)));
            Assert.False(string.IsNullOrWhiteSpace(Categories.Description(category)));
            Assert.True(Categories.MaxScore(category) > 0);
        }
    }

    [Fact]
    public void MaxScoreKanFaktiskOpnås()
    {
        // Den påståede maksimumscore skal kunne findes blandt alle 46.656 udfald.
        foreach (var category in Categories.All)
        {
            var best = 0;
            foreach (var state in DiceCatalog.Instance.StatesOfSize(YatzyRules.DiceCount))
            {
                best = System.Math.Max(best, YatzyRules.Score(category, state));
            }

            Assert.Equal(Categories.MaxScore(category), best);
        }
    }
}
