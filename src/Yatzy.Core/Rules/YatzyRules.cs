namespace Yatzy.Core.Rules;

/// <summary>
/// Reglerne for den variant der spilles her: <b>6 terninger</b>, <b>20 slag</b>
/// (maxi-yatzyblokken), <b>3 kast pr. tur</b> og traditionel pointgivning.
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

    /// <summary>Antal slag på blokken - og dermed antal runder i et spil.</summary>
    public const int CategoryCount = 20;

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

    /// <summary>Point for fuld straight (1-2-3-4-5-6).</summary>
    public const int FullStraightPoints = 21;

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
            Category.TwoPairs => PairsScore(counts, 2),
            Category.ThreePairs => PairsScore(counts, 3),
            Category.ThreeOfAKind => NOfAKindScore(counts, 3),
            Category.FourOfAKind => NOfAKindScore(counts, 4),
            Category.FiveOfAKind => NOfAKindScore(counts, 5),
            Category.SmallStraight => HasFaces(counts, 1, 5) ? SmallStraightPoints : 0,
            Category.LargeStraight => HasFaces(counts, 2, 6) ? LargeStraightPoints : 0,
            Category.FullStraight => HasFaces(counts, 1, 6) ? FullStraightPoints : 0,
            Category.House => TwoGroupsScore(counts, 3, 2),
            Category.Villa => TwoGroupsScore(counts, 3, 3),
            Category.Tower => TwoGroupsScore(counts, 4, 2),
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

    /// <summary>
    /// To eller tre par. Parrene skal have <b>forskellig</b> øjenværdi - fire ens tæller
    /// altså kun som ét par. De højeste par vælges.
    /// </summary>
    private static int PairsScore(ReadOnlySpan<int> counts, int pairs)
    {
        var sum = 0;
        var found = 0;

        for (var face = Faces; face >= 1 && found < pairs; face--)
        {
            if (counts[face - 1] >= 2)
            {
                sum += 2 * face;
                found++;
            }
        }

        return found == pairs ? sum : 0;
    }

    /// <summary>
    /// Slagene der består af to grupper med forskellig øjenværdi: hus (3+2),
    /// villa (3+3) og tårn (4+2). Med seks terninger kan der være flere gyldige
    /// opdelinger, så den bedst betalende vælges.
    /// </summary>
    private static int TwoGroupsScore(ReadOnlySpan<int> counts, int first, int second)
    {
        var best = 0;
        for (var a = 1; a <= Faces; a++)
        {
            if (counts[a - 1] < first)
            {
                continue;
            }

            for (var b = 1; b <= Faces; b++)
            {
                if (b == a || counts[b - 1] < second)
                {
                    continue;
                }

                best = System.Math.Max(best, first * a + second * b);
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
