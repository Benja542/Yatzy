using System.Numerics;
using Yatzy.Core.Math;
using Yatzy.Core.Rules;

namespace Yatzy.Core.Probability;

/// <summary>Ét led i en analytisk udregning - fx en partition eller et inklusion-eksklusions-led.</summary>
/// <param name="Label">Leddets navn, fx "4+2" eller "j = 2".</param>
/// <param name="Outcomes">Leddets bidrag i antal udfald (kan være negativt).</param>
/// <param name="Explanation">Hvordan leddet er regnet ud.</param>
public readonly record struct AnalyticTerm(string Label, long Outcomes, string Explanation);

/// <summary>
/// Den analytisk beregnede sandsynlighed for et slag i ét kast med seks terninger.
/// </summary>
/// <param name="Category">Slaget.</param>
/// <param name="Probability">Den eksakte sandsynlighed som uforkortelig brøk.</param>
/// <param name="Favourable">Antal gunstige udfald.</param>
/// <param name="Total">Antal mulige udfald (6^6 = 46.656).</param>
/// <param name="Method">Metoden der er brugt.</param>
/// <param name="Formula">Formlen skrevet ud.</param>
/// <param name="Terms">Leddene i udregningen.</param>
public sealed record AnalyticResult(
    Category Category,
    Fraction Probability,
    long Favourable,
    long Total,
    string Method,
    string Formula,
    IReadOnlyList<AnalyticTerm> Terms)
{
    public double Value => Probability.ToDouble();
}

/// <summary>
/// Beregner sandsynligheden for hvert slag <b>analytisk</b> - altså med kombinatorik
/// og eksakt brøkregning i stedet for simulering.
/// </summary>
/// <remarks>
/// <para>Tallene gælder ét enkelt kast med alle seks terninger (46.656 lige sandsynlige udfald).</para>
/// <para>Der bruges tre klassiske teknikker:</para>
/// <list type="bullet">
/// <item><description>
/// <b>Komplementærreglen</b> for de øverste slag: P(mindst én sekser) = 1 − (5/6)^6.
/// </description></item>
/// <item><description>
/// <b>Inklusion-eksklusion</b> for de tre straights: antallet af kast hvor alle de
/// krævede øjenværdier er til stede er Σ (−1)^j · C(m,j) · (6−j)^6, hvor m er antallet
/// af krævede øjenværdier (5 for lille og stor straight, 6 for fuld straight).
/// </description></item>
/// <item><description>
/// <b>Optælling over partitioner</b> for par, ens, hus, villa og tårn: hvert kast har en
/// "form" (fx 4+2 eller 2+2+1+1), og antallet af kast med en given form er
/// antallet af måder at fordele øjenværdier på gange multinomialkoefficienten.
/// </description></item>
/// </list>
/// </remarks>
public static class AnalyticProbability
{
    private const int Faces = YatzyRules.Faces;
    private const int DiceCount = YatzyRules.DiceCount;

    /// <summary>Antal mulige (ordnede) udfald af ét kast med seks terninger: 6^6 = 46.656.</summary>
    public static long TotalOutcomes { get; } = Pow(Faces, DiceCount);

    /// <summary>Beregner sandsynligheden for alle 20 slag.</summary>
    public static IReadOnlyList<AnalyticResult> All() => Categories.All.Select(Compute).ToList();

    /// <summary>Beregner sandsynligheden for ét slag.</summary>
    public static AnalyticResult Compute(Category category) => category switch
    {
        Category.Ones or Category.Twos or Category.Threes
            or Category.Fours or Category.Fives or Category.Sixes => Upper(category),
        Category.SmallStraight => Straight(category, 1, 5),
        Category.LargeStraight => Straight(category, 2, 6),
        Category.FullStraight => Straight(category, 1, 6),
        Category.Chance => Certain(category),
        _ => ByShape(category),
    };

    private static AnalyticResult Upper(Category category)
    {
        var face = Categories.UpperFace(category);
        var missing = Pow(Faces - 1, DiceCount);
        var favourable = TotalOutcomes - missing;

        var terms = new List<AnalyticTerm>
        {
            new("Alle udfald", TotalOutcomes, $"6^{DiceCount} = {TotalOutcomes:N0}"),
            new($"Ingen {face}'ere", -missing, $"5^{DiceCount} = {missing:N0}"),
        };

        return new AnalyticResult(
            category,
            new Fraction(favourable, TotalOutcomes),
            favourable,
            TotalOutcomes,
            "Komplementærreglen",
            $"P = 1 − (5/6)^{DiceCount} = ({TotalOutcomes:N0} − {missing:N0}) / {TotalOutcomes:N0}",
            terms);
    }

    private static AnalyticResult Straight(Category category, int fromFace, int toFace)
    {
        var required = toFace - fromFace + 1;
        var terms = new List<AnalyticTerm>();
        long favourable = 0;

        for (var j = 0; j <= required; j++)
        {
            var sign = j % 2 == 0 ? 1 : -1;
            var contribution = sign * Binomial(required, j) * Pow(Faces - j, DiceCount);
            favourable += contribution;
            terms.Add(new AnalyticTerm(
                $"j = {j}",
                contribution,
                $"(−1)^{j} · C({required},{j}) · {Faces - j}^{DiceCount} = {contribution:N0}"));
        }

        return new AnalyticResult(
            category,
            new Fraction(favourable, TotalOutcomes),
            favourable,
            TotalOutcomes,
            "Inklusion-eksklusion",
            $"P = Σ(j=0..{required}) (−1)^j · C({required},j) · (6−j)^{DiceCount} / 6^{DiceCount} " +
            $"= {favourable:N0} / {TotalOutcomes:N0}",
            terms);
    }

    private static AnalyticResult Certain(Category category) =>
        new(category,
            Fraction.One,
            TotalOutcomes,
            TotalOutcomes,
            "Direkte",
            "Chance kan altid skrives - summen af seks terninger er mindst 6. P = 1",
            [new AnalyticTerm("Alle udfald", TotalOutcomes, $"6^{DiceCount} = {TotalOutcomes:N0}")]);

    /// <summary>
    /// Optælling over partitioner. En partition af 6 (fx 4+2) beskriver "formen" på et kast:
    /// hvor mange terninger der viser den hyppigste øjenværdi, den næsthyppigste osv.
    /// Antallet af kast med formen λ er
    /// <c>6! / ((6−k)! · ∏ m_j!) · 6! / ∏ λ_i!</c>, hvor k er antallet af dele og
    /// m_j er hvor mange dele der har samme størrelse.
    /// </summary>
    private static AnalyticResult ByShape(Category category)
    {
        var terms = new List<AnalyticTerm>();
        long favourable = 0;

        foreach (var shape in Partitions(DiceCount, DiceCount))
        {
            var outcomes = ShapeOutcomes(shape);
            if (!ShapeSatisfies(category, shape))
            {
                continue;
            }

            favourable += outcomes;
            terms.Add(new AnalyticTerm(
                string.Join("+", shape),
                outcomes,
                $"{FaceAssignments(shape):N0} øjenfordelinger · {Multinomial(shape):N0} rækkefølger = {outcomes:N0}"));
        }

        return new AnalyticResult(
            category,
            new Fraction(favourable, TotalOutcomes),
            favourable,
            TotalOutcomes,
            "Optælling over partitioner",
            $"P = ({string.Join(" + ", terms.Select(t => t.Outcomes.ToString("N0")))}) / {TotalOutcomes:N0} " +
            $"= {favourable:N0} / {TotalOutcomes:N0}",
            terms);
    }

    /// <summary>Opfylder en kastform (partition, faldende) slaget?</summary>
    private static bool ShapeSatisfies(Category category, int[] shape) => category switch
    {
        Category.OnePair => shape[0] >= 2,
        Category.TwoPairs => shape.Count(part => part >= 2) >= 2,
        Category.ThreePairs => shape.Count(part => part >= 2) >= 3,
        Category.ThreeOfAKind => shape[0] >= 3,
        Category.FourOfAKind => shape[0] >= 4,
        Category.FiveOfAKind => shape[0] >= 5,
        // Hus, villa og tårn kræver to grupper med forskellig øjenværdi. Formen er
        // sorteret faldende, så det er nok at se på de to største dele.
        Category.House => shape[0] >= 3 && shape.Length >= 2 && shape[1] >= 2,
        Category.Villa => shape[0] >= 3 && shape.Length >= 2 && shape[1] >= 3,
        Category.Tower => shape[0] >= 4 && shape.Length >= 2 && shape[1] >= 2,
        Category.Yatzy => shape[0] >= DiceCount,
        _ => throw new ArgumentOutOfRangeException(
            nameof(category), category, "Slaget afhænger ikke kun af kastets form."),
    };

    /// <summary>Antal udfald med en given form.</summary>
    private static long ShapeOutcomes(int[] shape) => FaceAssignments(shape) * Multinomial(shape);

    /// <summary>Antal måder at vælge hvilke øjenværdier der indgår i formen.</summary>
    private static long FaceAssignments(int[] shape)
    {
        var k = shape.Length;
        var result = Factorial(Faces) / Factorial(Faces - k);
        foreach (var multiplicity in shape.GroupBy(part => part).Select(g => g.Count()))
        {
            result /= Factorial(multiplicity);
        }

        return result;
    }

    /// <summary>Antal rækkefølger terningerne kan ligge i - multinomialkoefficienten.</summary>
    private static long Multinomial(int[] shape)
    {
        var result = Factorial(DiceCount);
        foreach (var part in shape)
        {
            result /= Factorial(part);
        }

        return result;
    }

    /// <summary>Alle partitioner af <paramref name="n"/> med dele på højst <paramref name="maxPart"/>, faldende.</summary>
    public static IEnumerable<int[]> Partitions(int n, int maxPart)
    {
        if (n == 0)
        {
            yield return [];
            yield break;
        }

        for (var part = System.Math.Min(n, maxPart); part >= 1; part--)
        {
            foreach (var rest in Partitions(n - part, part))
            {
                var result = new int[rest.Length + 1];
                result[0] = part;
                Array.Copy(rest, 0, result, 1, rest.Length);
                yield return result;
            }
        }
    }

    /// <summary>
    /// Kontroltælling: gennemløber alle 46.656 ordnede udfald og tæller hvor mange
    /// der giver point i slaget. Bruges til at efterprøve formlerne.
    /// </summary>
    public static long BruteForceCount(Category category)
    {
        Span<int> counts = stackalloc int[Faces];
        Span<int> dice = stackalloc int[DiceCount];
        long hits = 0;

        while (true)
        {
            counts.Clear();
            for (var i = 0; i < DiceCount; i++)
            {
                counts[dice[i]]++;
            }

            if (YatzyRules.IsHit(category, counts))
            {
                hits++;
            }

            var position = 0;
            while (position < DiceCount && ++dice[position] == Faces)
            {
                dice[position] = 0;
                position++;
            }

            if (position == DiceCount)
            {
                return hits;
            }
        }
    }

    private static long Binomial(int n, int k)
    {
        var result = BigInteger.One;
        for (var i = 0; i < k; i++)
        {
            result = result * (n - i) / (i + 1);
        }

        return (long)result;
    }

    private static long Factorial(int n)
    {
        long result = 1;
        for (var i = 2; i <= n; i++)
        {
            result *= i;
        }

        return result;
    }

    private static long Pow(int baseValue, int exponent)
    {
        long result = 1;
        for (var i = 0; i < exponent; i++)
        {
            result *= baseValue;
        }

        return result;
    }
}
