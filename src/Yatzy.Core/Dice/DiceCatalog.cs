using Yatzy.Core.Rules;

namespace Yatzy.Core.Dice;

/// <summary>
/// Forudberegnet katalog over alle mulige terningetilstande.
/// </summary>
/// <remarks>
/// <para>
/// Fordi rækkefølgen af terningerne er ligegyldig, beskrives en hånd som en tællevektor.
/// Der findes C(n+5,5) forskellige tællevektorer med n terninger, altså
/// 1, 6, 21, 56, 126, 252 og 462 for n = 0 ... 6. I alt 924 - mod 6^6 = 46.656
/// ordnede udfald. Det er den reduktion der gør både udfaldstræet og simuleringen hurtig.
/// </para>
/// <para>
/// Kataloget indeholder også overgangstabellen: for hver mulig "behold"-mængde
/// (924 stk.) listen over hvilke fulde hænder et omkast kan ende i, og med hvilken
/// sandsynlighed. Det er kanterne i udfaldstræet.
/// </para>
/// </remarks>
public sealed class DiceCatalog
{
    private const int Faces = YatzyRules.Faces;
    private const int DiceCount = YatzyRules.DiceCount;

    /// <summary>Delt instans - kataloget er uforanderligt og kan bruges fra flere tråde.</summary>
    public static DiceCatalog Instance { get; } = new();

    private readonly List<int[]>[] _bySize = new List<int[]>[DiceCount + 1];
    private readonly Dictionary<int, int>[] _indexBySize = new Dictionary<int, int>[DiceCount + 1];
    private readonly int[] _globalOffset = new int[DiceCount + 1];

    private readonly int[][] _keepCounts;
    private readonly int[] _keepSize;
    private readonly int[][] _transitionTargets;
    private readonly double[][] _transitionProbabilities;
    private readonly int[][] _subKeeps;
    private readonly double[] _freshRollProbabilities;
    private readonly long[] _freshRollCounts;

    private DiceCatalog()
    {
        for (var size = 0; size <= DiceCount; size++)
        {
            var list = new List<int[]>();
            Enumerate(new int[Faces], 0, size, list);
            _bySize[size] = list;

            var index = new Dictionary<int, int>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                index[Key(list[i])] = i;
            }

            _indexBySize[size] = index;
        }

        var offset = 0;
        for (var size = 0; size <= DiceCount; size++)
        {
            _globalOffset[size] = offset;
            offset += _bySize[size].Count;
        }

        KeepCount = offset;
        _keepCounts = new int[KeepCount][];
        _keepSize = new int[KeepCount];
        for (var size = 0; size <= DiceCount; size++)
        {
            var list = _bySize[size];
            for (var i = 0; i < list.Count; i++)
            {
                var id = _globalOffset[size] + i;
                _keepCounts[id] = list[i];
                _keepSize[id] = size;
            }
        }

        FullStateCount = _bySize[DiceCount].Count;

        _transitionTargets = new int[KeepCount][];
        _transitionProbabilities = new double[KeepCount][];
        BuildTransitions();

        _subKeeps = new int[FullStateCount][];
        BuildSubKeeps();

        _freshRollCounts = new long[FullStateCount];
        _freshRollProbabilities = new double[FullStateCount];
        var total = (double)Pow(Faces, DiceCount);
        for (var id = 0; id < FullStateCount; id++)
        {
            var arrangements = Arrangements(_bySize[DiceCount][id]);
            _freshRollCounts[id] = arrangements;
            _freshRollProbabilities[id] = arrangements / total;
        }
    }

    /// <summary>Antal forskellige hænder med alle 6 terninger (462).</summary>
    public int FullStateCount { get; }

    /// <summary>Antal forskellige "behold"-mængder på tværs af alle størrelser (924).</summary>
    public int KeepCount { get; }

    /// <summary>Alle tællevektorer med et givet antal terninger.</summary>
    public IReadOnlyList<int[]> StatesOfSize(int size) => _bySize[size];

    /// <summary>Tællevektoren for en fuld hånd.</summary>
    public int[] FullState(int stateId) => _bySize[DiceCount][stateId];

    /// <summary>Id for en fuld hånd (6 terninger).</summary>
    public int FullStateId(ReadOnlySpan<int> counts) => _indexBySize[DiceCount][Key(counts)];

    /// <summary>Id for en fuld hånd ud fra rå terningeøjne.</summary>
    public int FullStateIdFromDice(ReadOnlySpan<int> dice)
    {
        Span<int> counts = stackalloc int[Faces];
        YatzyRules.CountFaces(dice, counts);
        return FullStateId(counts);
    }

    /// <summary>Tællevektoren for en "behold"-mængde.</summary>
    public int[] Keep(int keepId) => _keepCounts[keepId];

    /// <summary>Antal terninger i en "behold"-mængde.</summary>
    public int KeepSize(int keepId) => _keepSize[keepId];

    /// <summary>Globalt id for en "behold"-mængde.</summary>
    public int KeepId(ReadOnlySpan<int> counts)
    {
        var size = 0;
        for (var i = 0; i < Faces; i++)
        {
            size += counts[i];
        }

        return _globalOffset[size] + _indexBySize[size][Key(counts)];
    }

    /// <summary>Id for den tomme "behold"-mængde (alle terninger kastes om).</summary>
    public int EmptyKeepId => _globalOffset[0];

    /// <summary>De fulde hænder et omkast fra en "behold"-mængde kan ende i.</summary>
    public int[] TransitionTargets(int keepId) => _transitionTargets[keepId];

    /// <summary>Sandsynlighederne der hører til <see cref="TransitionTargets"/>.</summary>
    public double[] TransitionProbabilities(int keepId) => _transitionProbabilities[keepId];

    /// <summary>Alle "behold"-mængder der kan dannes ud fra en fuld hånd (op til 64 stk.).</summary>
    public int[] SubKeeps(int stateId) => _subKeeps[stateId];

    /// <summary>Sandsynlighedsfordelingen for et friskt kast med alle 6 terninger.</summary>
    public double[] FreshRollProbabilities => _freshRollProbabilities;

    /// <summary>Antal ordnede udfald (ud af 46.656) der giver en bestemt fuld hånd.</summary>
    public long FreshRollCount(int stateId) => _freshRollCounts[stateId];

    /// <summary>Antal ordnede udfald der svarer til en tællevektor - multinomialkoefficienten.</summary>
    public static long Arrangements(ReadOnlySpan<int> counts)
    {
        var total = 0;
        foreach (var c in counts)
        {
            total += c;
        }

        var result = Factorial(total);
        foreach (var c in counts)
        {
            result /= Factorial(c);
        }

        return result;
    }

    /// <summary>Udfolder en tællevektor til rå terningeøjne i stigende rækkefølge.</summary>
    public static int[] ToDice(ReadOnlySpan<int> counts)
    {
        var total = 0;
        foreach (var c in counts)
        {
            total += c;
        }

        var dice = new int[total];
        var k = 0;
        for (var face = 1; face <= Faces; face++)
        {
            for (var i = 0; i < counts[face - 1]; i++)
            {
                dice[k++] = face;
            }
        }

        return dice;
    }

    private void BuildTransitions()
    {
        for (var keepId = 0; keepId < KeepCount; keepId++)
        {
            var keep = _keepCounts[keepId];
            var reroll = DiceCount - _keepSize[keepId];
            var outcomes = _bySize[reroll];
            var denominator = (double)Pow(Faces, reroll);

            var targets = new int[outcomes.Count];
            var probabilities = new double[outcomes.Count];
            var combined = new int[Faces];

            for (var i = 0; i < outcomes.Count; i++)
            {
                var outcome = outcomes[i];
                for (var f = 0; f < Faces; f++)
                {
                    combined[f] = keep[f] + outcome[f];
                }

                targets[i] = FullStateId(combined);
                probabilities[i] = Arrangements(outcome) / denominator;
            }

            _transitionTargets[keepId] = targets;
            _transitionProbabilities[keepId] = probabilities;
        }
    }

    private void BuildSubKeeps()
    {
        for (var stateId = 0; stateId < FullStateCount; stateId++)
        {
            var state = _bySize[DiceCount][stateId];
            var keeps = new List<int>();
            var current = new int[Faces];
            CollectSubKeeps(state, current, 0, keeps);
            _subKeeps[stateId] = keeps.ToArray();
        }
    }

    private void CollectSubKeeps(int[] state, int[] current, int face, List<int> keeps)
    {
        if (face == Faces)
        {
            keeps.Add(KeepId(current));
            return;
        }

        for (var c = 0; c <= state[face]; c++)
        {
            current[face] = c;
            CollectSubKeeps(state, current, face + 1, keeps);
        }

        current[face] = 0;
    }

    private static void Enumerate(int[] current, int face, int remaining, List<int[]> result)
    {
        if (face == Faces - 1)
        {
            current[face] = remaining;
            result.Add((int[])current.Clone());
            current[face] = 0;
            return;
        }

        for (var c = 0; c <= remaining; c++)
        {
            current[face] = c;
            Enumerate(current, face + 1, remaining - c, result);
        }

        current[face] = 0;
    }

    private static int Key(ReadOnlySpan<int> counts)
    {
        var key = 0;
        for (var i = 0; i < Faces; i++)
        {
            key |= counts[i] << (3 * i);
        }

        return key;
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
