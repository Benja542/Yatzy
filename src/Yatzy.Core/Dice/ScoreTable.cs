using Yatzy.Core.Rules;

namespace Yatzy.Core.Dice;

/// <summary>
/// Scoren for hvert af de 15 slag i hver af de 462 mulige hænder - regnet ud én gang
/// og slået op siden.
/// </summary>
/// <remarks>
/// Tabellen fylder 462 · 15 = 6.930 tal, men sparer millioner af kald til
/// <see cref="YatzyRules.Score"/> når der simuleres hele spil. Det er den eneste grund
/// til at den findes.
/// </remarks>
public static class ScoreTable
{
    private static readonly int[][] Scores = Build();

    /// <summary>Scoren for et slag i en given hånd.</summary>
    public static int Score(int stateId, Category category) => Scores[stateId][(int)category];

    /// <summary>Alle 15 scorer for en hånd, indekseret efter <see cref="Category"/>.</summary>
    public static ReadOnlySpan<int> ForState(int stateId) => Scores[stateId];

    private static int[][] Build()
    {
        var catalog = DiceCatalog.Instance;
        var table = new int[catalog.FullStateCount][];

        for (var stateId = 0; stateId < catalog.FullStateCount; stateId++)
        {
            var counts = catalog.FullState(stateId);
            var row = new int[YatzyRules.CategoryCount];
            foreach (var category in Categories.All)
            {
                row[(int)category] = YatzyRules.Score(category, counts);
            }

            table[stateId] = row;
        }

        return table;
    }
}
