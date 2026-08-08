using Yatzy.Core.Rules;

namespace Yatzy.Core.Game;

/// <summary>
/// En yatzy-blok med de 15 slag. Et slag kan skrives én gang - også med 0 point,
/// hvis man må strege det ud.
/// </summary>
public sealed class ScoreSheet
{
    private readonly int?[] _scores = new int?[YatzyRules.CategoryCount];

    /// <summary>Scoren i et slag, eller <c>null</c> hvis slaget stadig er åbent.</summary>
    public int? this[Category category] => _scores[(int)category];

    /// <summary>Er slaget skrevet?</summary>
    public bool IsFilled(Category category) => _scores[(int)category].HasValue;

    /// <summary>De slag der stadig er åbne.</summary>
    public IEnumerable<Category> Open => Categories.All.Where(category => !IsFilled(category));

    /// <summary>Antal åbne slag - og dermed antal ture tilbage.</summary>
    public int OpenCount => YatzyRules.CategoryCount - _scores.Count(score => score.HasValue);

    /// <summary>Er blokken fuld?</summary>
    public bool IsComplete => OpenCount == 0;

    /// <summary>Skriver et slag ud fra en hånd.</summary>
    public int Write(Category category, ReadOnlySpan<int> counts)
    {
        var score = YatzyRules.Score(category, counts);
        Write(category, score);
        return score;
    }

    /// <summary>Skriver en score direkte i et slag.</summary>
    public void Write(Category category, int score)
    {
        if (IsFilled(category))
        {
            throw new InvalidOperationException($"{Categories.DanishName(category)} er allerede skrevet.");
        }

        _scores[(int)category] = score;
    }

    /// <summary>Summen af den øverste del (enere ... seksere).</summary>
    public int UpperSum => Categories.Upper.Sum(category => _scores[(int)category] ?? 0);

    /// <summary>Bonussen - 100 point hvis den øverste del er mindst 84.</summary>
    public int Bonus => YatzyRules.Bonus(UpperSum);

    /// <summary>Summen af den nederste del.</summary>
    public int LowerSum => Categories.All
        .Where(category => !Categories.IsUpper(category))
        .Sum(category => _scores[(int)category] ?? 0);

    /// <summary>Den samlede score inklusive bonus.</summary>
    public int Total => UpperSum + Bonus + LowerSum;

    /// <summary>Hvor mange point der mangler til bonus (0 hvis den er i hus).</summary>
    public int PointsToBonus => System.Math.Max(0, YatzyRules.BonusThreshold - UpperSum);

    /// <summary>Kan bonussen stadig nås med de åbne øverste slag?</summary>
    public bool BonusStillPossible
    {
        get
        {
            var potential = UpperSum;
            foreach (var category in Categories.Upper)
            {
                if (!IsFilled(category))
                {
                    potential += Categories.MaxScore(category);
                }
            }

            return potential >= YatzyRules.BonusThreshold;
        }
    }

    public ScoreSheet Clone()
    {
        var copy = new ScoreSheet();
        Array.Copy(_scores, copy._scores, _scores.Length);
        return copy;
    }

    /// <summary>Alle scorer som array indekseret efter <see cref="Category"/>.</summary>
    public int?[] ToArray() => (int?[])_scores.Clone();
}
