namespace Yatzy.Core.Rules;

/// <summary>
/// De 15 slag i spillet. Rækkefølgen svarer til rækkefølgen på en klassisk yatzy-blok.
/// </summary>
public enum Category
{
    Ones = 0,
    Twos = 1,
    Threes = 2,
    Fours = 3,
    Fives = 4,
    Sixes = 5,
    OnePair = 6,
    TwoPairs = 7,
    ThreeOfAKind = 8,
    FourOfAKind = 9,
    SmallStraight = 10,
    LargeStraight = 11,
    FullHouse = 12,
    Chance = 13,
    Yatzy = 14,
}

public static class Categories
{
    /// <summary>Alle 15 slag i blok-rækkefølge.</summary>
    public static readonly Category[] All =
    [
        Category.Ones, Category.Twos, Category.Threes, Category.Fours, Category.Fives, Category.Sixes,
        Category.OnePair, Category.TwoPairs, Category.ThreeOfAKind, Category.FourOfAKind,
        Category.SmallStraight, Category.LargeStraight, Category.FullHouse, Category.Chance, Category.Yatzy,
    ];

    /// <summary>De seks øverste slag (enere ... seksere), som tæller med i bonussen.</summary>
    public static readonly Category[] Upper =
    [
        Category.Ones, Category.Twos, Category.Threes, Category.Fours, Category.Fives, Category.Sixes,
    ];

    public static bool IsUpper(Category category) => (int)category <= (int)Category.Sixes;

    /// <summary>Øjenværdien (1-6) for et af de øverste slag.</summary>
    public static int UpperFace(Category category) =>
        IsUpper(category)
            ? (int)category + 1
            : throw new ArgumentOutOfRangeException(nameof(category), category, "Slaget er ikke et af de øverste slag.");

    /// <summary>Dansk navn til brugerfladen.</summary>
    public static string DanishName(Category category) => category switch
    {
        Category.Ones => "Enere",
        Category.Twos => "Toere",
        Category.Threes => "Treere",
        Category.Fours => "Firere",
        Category.Fives => "Femmere",
        Category.Sixes => "Seksere",
        Category.OnePair => "Et par",
        Category.TwoPairs => "To par",
        Category.ThreeOfAKind => "Tre ens",
        Category.FourOfAKind => "Fire ens",
        Category.SmallStraight => "Lille straight",
        Category.LargeStraight => "Stor straight",
        Category.FullHouse => "Fuldt hus",
        Category.Chance => "Chance",
        Category.Yatzy => "Yatzy",
        _ => category.ToString(),
    };

    /// <summary>Kort regelbeskrivelse til brugerfladen.</summary>
    public static string Description(Category category) => category switch
    {
        Category.Ones => "Summen af alle 1'ere",
        Category.Twos => "Summen af alle 2'ere",
        Category.Threes => "Summen af alle 3'ere",
        Category.Fours => "Summen af alle 4'ere",
        Category.Fives => "Summen af alle 5'ere",
        Category.Sixes => "Summen af alle 6'ere",
        Category.OnePair => "To ens - summen af de to terninger",
        Category.TwoPairs => "To par med forskellig øjenværdi - summen af de fire terninger",
        Category.ThreeOfAKind => "Tre ens - summen af de tre terninger",
        Category.FourOfAKind => "Fire ens - summen af de fire terninger",
        Category.SmallStraight => "1-2-3-4-5 blandt de seks terninger - giver 15",
        Category.LargeStraight => "2-3-4-5-6 blandt de seks terninger - giver 20",
        Category.FullHouse => "Tre ens + et par med anden øjenværdi - summen af de fem terninger",
        Category.Chance => "Summen af alle seks terninger",
        Category.Yatzy => "Seks ens - giver 100",
        _ => string.Empty,
    };

    /// <summary>Den højst mulige score i slaget (bruges bl.a. af statistikken).</summary>
    public static int MaxScore(Category category) => category switch
    {
        Category.Ones => 6,
        Category.Twos => 12,
        Category.Threes => 18,
        Category.Fours => 24,
        Category.Fives => 30,
        Category.Sixes => 36,
        Category.OnePair => 12,
        Category.TwoPairs => 22,
        Category.ThreeOfAKind => 18,
        Category.FourOfAKind => 24,
        Category.SmallStraight => 15,
        Category.LargeStraight => 20,
        Category.FullHouse => 28,
        Category.Chance => 36,
        Category.Yatzy => 100,
        _ => 0,
    };
}
