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

public class MultiplayerTests
{
    [Fact]
    public void EnSpillerErStandard()
    {
        var game = new GameEngine(seed: 1);
        Assert.Single(game.Players);
        Assert.False(game.IsMultiplayer);
        Assert.Equal("Spiller 1", game.Players[0].Name);
    }

    [Fact]
    public void TurenGårVidereTilNæsteSpillerEfterHverSkrivning()
    {
        var game = new GameEngine(["Anna", "Bo", "Cecilie"], seed: 1);

        Assert.Equal("Anna", game.CurrentPlayer.Name);
        game.Roll();
        game.Write(Category.Chance);

        Assert.Equal("Bo", game.CurrentPlayer.Name);
        Assert.Equal(0, game.RollsUsed);
        Assert.False(game.HasRolled);

        game.Roll();
        game.Write(Category.Chance);
        Assert.Equal("Cecilie", game.CurrentPlayer.Name);

        game.Roll();
        game.Write(Category.Chance);
        Assert.Equal("Anna", game.CurrentPlayer.Name);
    }

    [Fact]
    public void HverSpillerHarSinEgenBlok()
    {
        var game = new GameEngine(["Anna", "Bo"], seed: 2);

        game.Roll();
        var annasScore = game.PotentialScore(Category.Chance);
        game.Write(Category.Chance);

        Assert.Equal(annasScore, game.Players[0].Sheet[Category.Chance]);
        Assert.Null(game.Players[1].Sheet[Category.Chance]);
        // Bo har alle 20 slag åbne selvom Anna har skrevet et.
        Assert.Equal(YatzyRules.CategoryCount, game.Players[1].Sheet.OpenCount);
    }

    [Fact]
    public void RundenTællerPrSpiller()
    {
        var game = new GameEngine(["Anna", "Bo"], seed: 3);

        Assert.Equal(1, game.Turn);
        game.Roll();
        game.Write(Category.Chance);

        // Bo er stadig i runde 1 - han har ikke skrevet endnu.
        Assert.Equal(1, game.Turn);
        game.Roll();
        game.Write(Category.Chance);

        // Nu er begge færdige med runde 1, og Anna starter runde 2.
        Assert.Equal(2, game.Turn);
        Assert.Equal("Anna", game.CurrentPlayer.Name);
    }

    [Fact]
    public void SpilletErFørstSlutNårAlleBlokkeErFulde()
    {
        var game = new GameEngine(["Anna", "Bo"], seed: 4);

        var turns = 0;
        while (!game.IsGameOver)
        {
            game.Roll();
            game.Write(game.Sheet.Open.First());
            turns++;
        }

        Assert.Equal(2 * YatzyRules.CategoryCount, turns);
        Assert.All(game.Players, player => Assert.True(player.Sheet.IsComplete));
        Assert.False(game.CanRoll);
    }

    [Fact]
    public void StillingenSorteresEfterScoreOgDelerPladsVedLighed()
    {
        var game = new GameEngine(["Anna", "Bo", "Cecilie"], seed: 5);
        game.Players[0].Sheet.Write(Category.Chance, 30);
        game.Players[1].Sheet.Write(Category.Chance, 10);
        game.Players[2].Sheet.Write(Category.Chance, 30);

        var standings = game.Standings;

        Assert.Equal([1, 1, 3], standings.Select(s => s.Rank));
        Assert.Equal(2, standings.Count(s => s.IsWinner));
        Assert.Equal("Bo", standings[^1].Player.Name);
    }

    [Fact]
    public void NavneRyddesOpOgTommeFelterFårStandardnavn()
    {
        var game = new GameEngine(["  Anna  ", "", "   "], seed: 6);

        Assert.Equal("Anna", game.Players[0].Name);
        Assert.Equal("Spiller 2", game.Players[1].Name);
        Assert.Equal("Spiller 3", game.Players[2].Name);
    }

    [Fact]
    public void DerErEnØvreGrænseForAntalSpillere()
    {
        var names = Enumerable.Range(1, GameEngine.MaxPlayers + 1).Select(i => $"Spiller {i}");
        Assert.Throws<ArgumentException>(() => new GameEngine(names));
    }

    [Fact]
    public void NytSpilRydderAlleBlokke()
    {
        var game = new GameEngine(["Anna", "Bo"], seed: 7);
        game.Roll();
        game.Write(Category.Chance);

        game.NewGame();

        Assert.All(game.Players, player => Assert.Equal(YatzyRules.CategoryCount, player.Sheet.OpenCount));
        Assert.Equal("Anna", game.CurrentPlayer.Name);
        Assert.False(game.HasRolled);
    }

    [Fact]
    public void NytSpilKanSkifteHoldet()
    {
        var game = new GameEngine(seed: 8);
        game.NewGame(["Dorte", "Erik", "Frida"]);

        Assert.Equal(3, game.Players.Count);
        Assert.True(game.IsMultiplayer);
        Assert.Equal("Dorte", game.CurrentPlayer.Name);
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
    public void OfringsrækkefølgenDækkerAlleSlag()
    {
        // Hvis et slag mangler, kan computeren ende uden noget at skrive i.
        var player = new AutoPlayer();
        var sheet = new ScoreSheet();
        foreach (var category in Categories.All.Take(Categories.All.Length - 1))
        {
            sheet.Write(category, 0);
        }

        // Hånden 1-1-1-1-1-2 giver intet i det sidste slag (yatzy), så der skal ofres.
        int[] counts = [5, 1, 0, 0, 0, 0];
        Assert.Equal(Categories.All[^1], player.ChooseCategory(counts, sheet));
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
