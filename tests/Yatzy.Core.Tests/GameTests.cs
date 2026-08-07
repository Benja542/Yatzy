using Yatzy.Core.Game;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Tests;

public class ScoreSheetTests
{
    [Fact]
    public void EtSlagKanKunSkrivesEnGang()
    {
        var sheet = new ScoreSheet();
        sheet.Write(Category.Chance, 21);
        Assert.Throws<InvalidOperationException>(() => sheet.Write(Category.Chance, 30));
    }

    [Fact]
    public void BonusLæggesTilVedMindst84()
    {
        var sheet = new ScoreSheet();
        foreach (var category in Categories.Upper)
        {
            sheet.Write(category, 4 * Categories.UpperFace(category)); // præcis 84
        }

        Assert.Equal(84, sheet.UpperSum);
        Assert.Equal(100, sheet.Bonus);
        Assert.Equal(184, sheet.Total);
        Assert.Equal(0, sheet.PointsToBonus);
    }

    [Fact]
    public void BonusUdeblliverLigeUnderGrænsen()
    {
        var sheet = new ScoreSheet();
        foreach (var category in Categories.Upper)
        {
            sheet.Write(category, 4 * Categories.UpperFace(category));
        }

        Assert.Equal(100, sheet.Bonus);

        var justBelow = new ScoreSheet();
        justBelow.Write(Category.Ones, 3);
        foreach (var category in Categories.Upper.Skip(1))
        {
            justBelow.Write(category, 4 * Categories.UpperFace(category));
        }

        Assert.Equal(83, justBelow.UpperSum);
        Assert.Equal(0, justBelow.Bonus);
        Assert.Equal(1, justBelow.PointsToBonus);
    }

    [Fact]
    public void BonusKanBliveUmulig()
    {
        var sheet = new ScoreSheet();
        sheet.Write(Category.Sixes, 0);
        sheet.Write(Category.Fives, 0);
        Assert.False(sheet.BonusStillPossible);
    }

    [Fact]
    public void BlokkenErFuldEfter15Slag()
    {
        var sheet = new ScoreSheet();
        foreach (var category in Categories.All)
        {
            Assert.False(sheet.IsComplete);
            sheet.Write(category, 0);
        }

        Assert.True(sheet.IsComplete);
        Assert.Equal(0, sheet.OpenCount);
    }
}

public class GameEngineTests
{
    [Fact]
    public void TreKastPrTur()
    {
        var game = new GameEngine(seed: 1);
        game.NewGame();

        Assert.Equal(3, game.RollsLeft);
        game.Roll();
        Assert.Equal(2, game.RollsLeft);
        game.Roll();
        game.Roll();
        Assert.Equal(0, game.RollsLeft);
        Assert.False(game.CanRoll);
        Assert.Throws<InvalidOperationException>(game.Roll);
    }

    [Fact]
    public void DerSkalKastesFørDerKanSkrives()
    {
        var game = new GameEngine(seed: 1);
        game.NewGame();
        Assert.Throws<InvalidOperationException>(() => game.Write(Category.Chance));
    }

    [Fact]
    public void LåsteTerningerBliverLiggende()
    {
        var game = new GameEngine(seed: 12345);
        game.NewGame();
        game.Roll();

        var kept = game.Dice[0];
        game.ToggleHold(0);
        game.Roll();

        Assert.Equal(kept, game.Dice.First(die => die == kept));
    }

    [Fact]
    public void EtSpilBestårAf15Ture()
    {
        var game = new GameEngine(seed: 7);
        game.NewGame();

        var turns = 0;
        while (!game.IsGameOver)
        {
            game.Roll();
            game.Write(game.Sheet.Open.First());
            turns++;
        }

        Assert.Equal(YatzyRules.CategoryCount, turns);
        Assert.True(game.Sheet.IsComplete);
    }

    [Fact]
    public void ApplyKeepLåserPræcisDeRigtigeTerninger()
    {
        var game = new GameEngine(seed: 3);
        game.NewGame();
        game.Roll();

        var counts = game.Counts;
        int[] keep = [counts[0] > 0 ? 1 : 0, 0, 0, 0, 0, 0];
        game.ApplyKeep(keep);

        Assert.Equal(keep[0], game.Dice.Where((_, i) => game.Held[i]).Count(die => die == 1));
        Assert.Equal(keep[0], game.Held.Count(held => held));
    }
}

public class AutoPlayerTests
{
    [Fact]
    public void ComputerenUdfylderHeleBlokken()
    {
        var player = new AutoPlayer();
        var result = player.PlayGame(new Random(4));

        Assert.Equal(YatzyRules.CategoryCount, result.Scores.Length);
        Assert.True(result.Total > 0);
        Assert.Equal(result.UpperSum + result.Bonus + result.Scores.Skip(6).Sum(), result.Total);
    }

    [Fact]
    public void SammeFrøGiverSammeSpil()
    {
        var first = new AutoPlayer().PlayGame(new Random(11));
        var second = new AutoPlayer().PlayGame(new Random(11));
        Assert.Equal(first.Total, second.Total);
        Assert.Equal(first.Scores, second.Scores);
    }

    [Fact]
    public void ComputerenSkriverYatzyNårDenHarDen()
    {
        var player = new AutoPlayer();
        var sheet = new ScoreSheet();
        int[] counts = [0, 0, 0, 0, 0, 6]; // seks seksere

        Assert.Equal(Category.Yatzy, player.ChooseCategory(counts, sheet));
    }

    [Fact]
    public void ComputerenOfrerEtSlagNårIntetGiverPoint()
    {
        var player = new AutoPlayer();
        var sheet = new ScoreSheet();
        foreach (var category in Categories.All.Where(c => c != Category.Yatzy && c != Category.Ones))
        {
            sheet.Write(category, 0);
        }

        // Hånden 2-3-4-5-6-6 giver hverken yatzy eller enere - så ofres yatzy først.
        int[] counts = [0, 1, 1, 1, 1, 2];
        Assert.Equal(Category.Yatzy, player.ChooseCategory(counts, sheet));
    }

    [Fact]
    public void SimuleringenGiverEnRimeligGennemsnitsscore()
    {
        var simulation = new GameSimulation(seed: 2024);
        simulation.Run(200);
        var summary = simulation.Summarize();

        Assert.Equal(200, summary.Games);
        Assert.InRange(summary.AverageTotal, 150d, 400d);
        Assert.True(summary.MinTotal <= summary.MedianTotal);
        Assert.True(summary.MedianTotal <= summary.MaxTotal);
        Assert.Equal(200, summary.Histogram.Sum(bucket => bucket.Count));
    }
}
