using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Game;

/// <summary>
/// Spillets tilstand: spillerne og deres blokke, terningerne på bordet, hvilke der er
/// låst, og hvor mange kast der er tilbage i turen.
/// </summary>
/// <remarks>
/// Spillerne skiftes til at tage en tur. Når en spiller har skrevet sit slag, går turen
/// videre til den næste, terningerne ryddes, og der er tre nye kast. Spillet er slut når
/// alle spillere har fyldt deres blok - altså efter 20 runder.
/// </remarks>
public sealed class GameEngine
{
    /// <summary>Højeste antal spillere.</summary>
    public const int MaxPlayers = 6;

    private readonly Random _random;
    private readonly int[] _dice = new int[YatzyRules.DiceCount];
    private readonly bool[] _held = new bool[YatzyRules.DiceCount];
    private readonly List<Player> _players = [];

    public GameEngine(int? seed = null)
        : this([DefaultName(0)], seed)
    {
    }

    public GameEngine(IEnumerable<string> playerNames, int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        SetPlayers(playerNames);
    }

    /// <summary>Spillerne i den rækkefølge de har tur.</summary>
    public IReadOnlyList<Player> Players => _players;

    /// <summary>Nummeret på den spiller der har tur.</summary>
    public int CurrentPlayerIndex { get; private set; }

    /// <summary>Den spiller der har tur.</summary>
    public Player CurrentPlayer => _players[CurrentPlayerIndex];

    /// <summary>Blokken for den spiller der har tur.</summary>
    public ScoreSheet Sheet => CurrentPlayer.Sheet;

    /// <summary>Spilles der med mere end én spiller?</summary>
    public bool IsMultiplayer => _players.Count > 1;

    /// <summary>Terningerne. 0 betyder "ikke kastet endnu".</summary>
    public IReadOnlyList<int> Dice => _dice;

    /// <summary>Hvilke terninger der er låst og ikke kastes om.</summary>
    public IReadOnlyList<bool> Held => _held;

    /// <summary>
    /// Rundenummer for den spiller der har tur, 1-20. Når blokken er fuld bliver den
    /// stående på 20 i stedet for at løbe videre til 21.
    /// </summary>
    public int Turn => System.Math.Min(
        YatzyRules.CategoryCount,
        YatzyRules.CategoryCount - Sheet.OpenCount + 1);

    /// <summary>Antal kast brugt i turen (0-3).</summary>
    public int RollsUsed { get; private set; }

    /// <summary>Antal kast tilbage i turen.</summary>
    public int RollsLeft => YatzyRules.RollsPerTurn - RollsUsed;

    /// <summary>
    /// Antal omkast tilbage - det tal sandsynlighedsberegningerne bruger.
    /// Før første kast er det 3 (alle tre kast er "omkast" af en tom hånd).
    /// </summary>
    public int RerollsLeft => RollsLeft;

    /// <summary>Er der kastet i denne tur?</summary>
    public bool HasRolled => RollsUsed > 0;

    /// <summary>Må der kastes?</summary>
    public bool CanRoll => !IsGameOver && RollsLeft > 0;

    /// <summary>Må der skrives? Man skal have kastet mindst én gang.</summary>
    public bool CanWrite => !IsGameOver && HasRolled;

    /// <summary>Er spillet slut - altså har alle spillere fyldt deres blok?</summary>
    public bool IsGameOver => _players.All(player => player.IsDone);

    /// <summary>Tællevektoren for de terninger der ligger på bordet.</summary>
    public int[] Counts => HasRolled ? YatzyRules.CountFaces(_dice) : new int[YatzyRules.Faces];

    /// <summary>Håndens id i <see cref="DiceCatalog"/> - eller -1 hvis der ikke er kastet.</summary>
    public int StateId => HasRolled ? DiceCatalog.Instance.FullStateIdFromDice(_dice) : -1;

    /// <summary>
    /// Slutstillingen: spillerne sorteret efter score, med delte placeringer ved lige score.
    /// </summary>
    public IReadOnlyList<Standing> Standings
    {
        get
        {
            var sorted = _players.OrderByDescending(player => player.Sheet.Total).ToList();
            var best = sorted.Count > 0 ? sorted[0].Sheet.Total : 0;
            var standings = new List<Standing>(sorted.Count);
            var rank = 0;
            var previousTotal = int.MinValue;

            for (var i = 0; i < sorted.Count; i++)
            {
                var total = sorted[i].Sheet.Total;
                if (total != previousTotal)
                {
                    rank = i + 1;
                    previousTotal = total;
                }

                standings.Add(new Standing(rank, sorted[i], total, total == best));
            }

            return standings;
        }
    }

    /// <summary>Standardnavnet til spiller nummer <paramref name="index"/> (0-baseret).</summary>
    public static string DefaultName(int index) => $"Spiller {index + 1}";

    /// <summary>Starter et nyt spil med de samme spillere.</summary>
    public void NewGame()
    {
        foreach (var player in _players)
        {
            player.Reset();
        }

        CurrentPlayerIndex = 0;
        StartTurn();
    }

    /// <summary>Starter et nyt spil med et nyt hold spillere.</summary>
    public void NewGame(IEnumerable<string> playerNames)
    {
        SetPlayers(playerNames);
        StartTurn();
    }

    /// <summary>Omdøber en spiller uden at forstyrre spillet.</summary>
    public void RenamePlayer(int index, string name) =>
        _players[index].Name = CleanName(name, index);

    /// <summary>Kaster de terninger der ikke er låst.</summary>
    public void Roll()
    {
        if (!CanRoll)
        {
            throw new InvalidOperationException("Der er ikke flere kast tilbage i denne tur.");
        }

        for (var i = 0; i < YatzyRules.DiceCount; i++)
        {
            if (!_held[i] || !HasRolled)
            {
                _dice[i] = _random.Next(1, YatzyRules.Faces + 1);
            }
        }

        RollsUsed++;
        SortDice();
    }

    /// <summary>Låser/låser en terning op.</summary>
    public void ToggleHold(int index)
    {
        if (!HasRolled)
        {
            return;
        }

        _held[index] = !_held[index];
    }

    /// <summary>Låser præcis de terninger der svarer til en "behold"-mængde (bruges af hjælpefunktionen).</summary>
    public void ApplyKeep(ReadOnlySpan<int> keepCounts)
    {
        Span<int> remaining = stackalloc int[YatzyRules.Faces];
        keepCounts.CopyTo(remaining);

        for (var i = 0; i < YatzyRules.DiceCount; i++)
        {
            var face = _dice[i];
            if (face >= 1 && remaining[face - 1] > 0)
            {
                remaining[face - 1]--;
                _held[i] = true;
            }
            else
            {
                _held[i] = false;
            }
        }
    }

    /// <summary>
    /// Skriver den aktuelle hånd i et slag på den nuværende spillers blok og giver
    /// turen videre til den næste spiller.
    /// </summary>
    public int Write(Category category)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Du skal kaste mindst én gang før du kan skrive.");
        }

        var score = Sheet.Write(category, Counts);
        NextPlayer();
        return score;
    }

    /// <summary>Scoren hvis den aktuelle hånd skrives i et slag.</summary>
    public int PotentialScore(Category category) => HasRolled ? YatzyRules.Score(category, Counts) : 0;

    private void NextPlayer()
    {
        if (IsGameOver)
        {
            // Alle blokke er fulde - der kastes ikke mere.
            RollsUsed = YatzyRules.RollsPerTurn;
            return;
        }

        // Normalt har alle spillere lige mange slag tilbage, men vi springer alligevel
        // fulde blokke over, så turskiftet aldrig kan ende hos en spiller der er færdig.
        do
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;
        }
        while (CurrentPlayer.IsDone);

        StartTurn();
    }

    private void SetPlayers(IEnumerable<string> playerNames)
    {
        var names = playerNames.ToList();
        if (names.Count == 0)
        {
            names.Add(DefaultName(0));
        }

        if (names.Count > MaxPlayers)
        {
            throw new ArgumentException($"Der kan højst være {MaxPlayers} spillere.", nameof(playerNames));
        }

        _players.Clear();
        for (var i = 0; i < names.Count; i++)
        {
            _players.Add(new Player(CleanName(names[i], i)));
        }

        CurrentPlayerIndex = 0;
    }

    private static string CleanName(string name, int index)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? DefaultName(index) : trimmed;
    }

    private void StartTurn()
    {
        RollsUsed = 0;
        Array.Clear(_dice);
        Array.Clear(_held);
    }

    private void SortDice()
    {
        // Terningerne vises sorteret, og låsningen følger med, så brugerfladen er rolig at se på.
        var order = Enumerable.Range(0, YatzyRules.DiceCount)
            .OrderBy(i => _dice[i])
            .ThenByDescending(i => _held[i])
            .ToArray();

        var dice = order.Select(i => _dice[i]).ToArray();
        var held = order.Select(i => _held[i]).ToArray();
        Array.Copy(dice, _dice, dice.Length);
        Array.Copy(held, _held, held.Length);
    }
}
