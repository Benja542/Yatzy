namespace Yatzy.Core.Rules;

/// <summary>
/// Reglerne for den variant der spilles her: <b>6 terninger</b>, <b>15 slag</b>,
/// <b>3 kast pr. tur</b> og traditionel pointgivning.
/// </summary>
/// <remarks>
/// Terningerne repræsenteres altid som et "tællevektor"-array med 6 pladser,
/// hvor plads <c>i</c> er antallet af terninger der viser <c>i + 1</c> øjne.
/// Rækkefølgen af terningerne er uden betydning for pointgivningen, så
/// tællevektoren er den naturlige - og mindste - tilstandsbeskrivelse.
/// </remarks>
public static class YatzyRules
{
    /// <summary>Antal terninger der spilles med.</summary>
    public const int DiceCount = 6;

    /// <summary>Antal sider på en terning.</summary>
    public const int Faces = 6;

    /// <summary>Antal kast pr. tur (første kast + to omkast).</summary>
    public const int RollsPerTurn = 3;

    /// <summary>Antal slag på blokken - og dermed antal ture i et spil.</summary>
    public const int CategoryCount = 15;

    /// <summary>Point-grænsen i den øverste del for at få bonus (4 af hver øjenværdi).</summary>
    public const int BonusThreshold = 84;

    /// <summary>Bonussens størrelse.</summary>
    public const int BonusPoints = 100;

    /// <summary>Point for yatzy (seks ens).</summary>
    public const int YatzyPoints = 100;

    /// <summary>Point for lille straight (1-2-3-4-5).</summary>
    public const int SmallStraightPoints = 15;

    /// <summary>Point for stor straight (2-3-4-5-6).</summary>
    public const int LargeStraightPoints = 20;

    /// <summary>Beregner scoren for et slag ud fra en tællevektor. Giver 0 hvis slaget ikke er opfyldt.</summary>
    public static int Score(Category category, ReadOnlySpan<int> counts)
    {
        if (counts.Length != Faces)
        {
            throw new ArgumentException($"Tællevektoren skal have {Faces} pladser.", nameof(counts));
        }

        return category switch
        {
            Category.Ones or Category.Twos or Category.Threes
                or Category.Fours or Category.Fives or Category.Sixes
                => UpperScore(category, counts),
            Category.OnePair => NOfAKindScore(counts, 2),
            Category.TwoPairs => TwoPairsScore(counts),
            Category.ThreeOfAKind => NOfAKindScore(counts, 3),
            Category.FourOfAKind => NOfAKindScore(counts, 4),
            Category.SmallStraight => HasFaces(counts, 1, 5) ? SmallStraightPoints : 0,
            Category.LargeStraight => HasFaces(counts, 2, 6) ? LargeStraightPoints : 0,
            Category.FullHouse => FullHouseScore(counts),
            Category.Chance => PipSum(counts),
            Category.Yatzy => NOfAKindScore(counts, 6) > 0 ? YatzyPoints : 0,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Ukendt slag."),
        };
    }

    /// <summary>Beregner scoren for et slag ud fra de rå terningeøjne (1-6).</summary>
    public static int ScoreDice(Category category, ReadOnlySpan<int> dice)
    {
        Span<int> counts = stackalloc int[Faces];
        CountFaces(dice, counts);
        return Score(category, counts);
    }

    /// <summary>
    /// Er slaget "slået"? Dvs. giver kombinationen point (score &gt; 0).
    /// Det er denne hændelse alle sandsynlighederne i programmet handler om.
    /// </summary>
    public static bool IsHit(Category category, ReadOnlySpan<int> counts) => Score(category, counts) > 0;

    /// <summary>Summen af alle øjne.</summary>
    public static int PipSum(ReadOnlySpan<int> counts)
    {
        var sum = 0;
        for (var i = 0; i < Faces; i++)
        {
            sum += (i + 1) * counts[i];
        }

        return sum;
    }

    /// <summary>Bonus for den øverste del: 100 point hvis summen er mindst 84.</summary>
    public static int Bonus(int upperSum) => upperSum >= BonusThreshold ? BonusPoints : 0;

    /// <summary>Laver en tællevektor ud fra rå terningeøjne (1-6).</summary>
    public static void CountFaces(ReadOnlySpan<int> dice, Span<int> counts)
    {
        counts.Clear();
        foreach (var die in dice)
        {
            if (die < 1 || die > Faces)
            {
                throw new ArgumentOutOfRangeException(nameof(dice), die, "En terning skal vise mellem 1 og 6 øjne.");
            }

            counts[die - 1]++;
        }
    }

    /// <summary>Laver en tællevektor ud fra rå terningeøjne (1-6).</summary>
    public static int[] CountFaces(ReadOnlySpan<int> dice)
    {
        var counts = new int[Faces];
        CountFaces(dice, counts);
        return counts;
    }

    private static int UpperScore(Category category, ReadOnlySpan<int> counts)
    {
        var face = Categories.UpperFace(category);
        return counts[face - 1] * face;
    }

    /// <summary>Højeste øjenværdi med mindst <paramref name="n"/> ens, ganget med n. Ellers 0.</summary>
    private static int NOfAKindScore(ReadOnlySpan<int> counts, int n)
    {
        for (var face = Faces; face >= 1; face--)
        {
            if (counts[face - 1] >= n)
            {
                return n * face;
            }
        }

        return 0;
    }

    private static int TwoPairsScore(ReadOnlySpan<int> counts)
    {
        var first = 0;
        var second = 0;
        for (var face = Faces; face >= 1; face--)
        {
            if (counts[face - 1] < 2)
            {
                continue;
            }

            if (first == 0)
            {
                first = face;
            }
            else
            {
                second = face;
                break;
            }
        }

        return second == 0 ? 0 : 2 * (first + second);
    }

    /// <summary>
    /// Fuldt hus: tre ens plus et par med en anden øjenværdi. Med seks terninger kan
    /// der være flere muligheder (fx 3+3), så den bedst betalende kombination vælges.
    /// </summary>
    private static int FullHouseScore(ReadOnlySpan<int> counts)
    {
        var best = 0;
        for (var triple = 1; triple <= Faces; triple++)
        {
            if (counts[triple - 1] < 3)
            {
                continue;
            }

            for (var pair = 1; pair <= Faces; pair++)
            {
                if (pair == triple || counts[pair - 1] < 2)
                {
                    continue;
                }

                best = System.Math.Max(best, 3 * triple + 2 * pair);
            }
        }

        return best;
    }

    private static bool HasFaces(ReadOnlySpan<int> counts, int fromFace, int toFace)
    {
        for (var face = fromFace; face <= toFace; face++)
        {
            if (counts[face - 1] == 0)
            {
                return false;
            }
        }

        return true;
    }
}
