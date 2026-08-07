using Yatzy.Core.Dice;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Game;

/// <summary>
/// Spillets tilstand for én spiller: terningerne på bordet, hvilke der er låst,
/// hvor mange kast der er tilbage, og blokken.
/// </summary>
public sealed class GameEngine
{
    private readonly Random _random;
    private readonly int[] _dice = new int[YatzyRules.DiceCount];
    private readonly bool[] _held = new bool[YatzyRules.DiceCount];

    public GameEngine(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : new Random();

    /// <summary>Terningerne. 0 betyder "ikke kastet endnu".</summary>
    public IReadOnlyList<int> Dice => _dice;

    /// <summary>Hvilke terninger der er låst og ikke kastes om.</summary>
    public IReadOnlyList<bool> Held => _held;

    /// <summary>Blokken.</summary>
    public ScoreSheet Sheet { get; private set; } = new();

    /// <summary>Turnummer, 1-15.</summary>
    public int Turn => YatzyRules.CategoryCount - Sheet.OpenCount + 1;

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

    /// <summary>Er spillet slut?</summary>
    public bool IsGameOver => Sheet.IsComplete;

    /// <summary>Tællevektoren for de terninger der ligger på bordet.</summary>
    public int[] Counts => HasRolled ? YatzyRules.CountFaces(_dice) : new int[YatzyRules.Faces];

    /// <summary>Håndens id i <see cref="DiceCatalog"/> - eller -1 hvis der ikke er kastet.</summary>
    public int StateId => HasRolled ? DiceCatalog.Instance.FullStateIdFromDice(_dice) : -1;

    /// <summary>Starter et nyt spil.</summary>
    public void NewGame()
    {
        Sheet = new ScoreSheet();
        StartTurn();
    }

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

    /// <summary>Skriver den aktuelle hånd i et slag og går videre til næste tur.</summary>
    public int Write(Category category)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Du skal kaste mindst én gang før du kan skrive.");
        }

        var score = Sheet.Write(category, Counts);
        if (!IsGameOver)
        {
            StartTurn();
        }
        else
        {
            RollsUsed = YatzyRules.RollsPerTurn;
        }

        return score;
    }

    /// <summary>Scoren hvis den aktuelle hånd skrives i et slag.</summary>
    public int PotentialScore(Category category) => HasRolled ? YatzyRules.Score(category, Counts) : 0;

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
