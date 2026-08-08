namespace Yatzy.Core.Rules;

/// <summary>
/// Hvad man går efter i et slag - altså hvilken hændelse sandsynligheden handler om.
/// </summary>
public enum ScoreGoal
{
    /// <summary>Slaget skal bare give point (score &gt; 0).</summary>
    AnyPoints = 0,

    /// <summary>
    /// Slaget skal give den højst mulige score, fx 22 i to par (6-6-5-5) eller
    /// 36 i seksere (seks seksere).
    /// </summary>
    MaxPoints = 1,
}

public static class ScoreGoals
{
    public static readonly ScoreGoal[] All = [ScoreGoal.AnyPoints, ScoreGoal.MaxPoints];

    public static string DanishName(ScoreGoal goal) => goal switch
    {
        ScoreGoal.AnyPoints => "mindst 1 point",
        ScoreGoal.MaxPoints => "maks point",
        _ => goal.ToString(),
    };

    /// <summary>
    /// Er de to mål ens for slaget? Det gælder de slag der har en fast pointværdi -
    /// straights og yatzy - hvor "giver point" og "giver maks point" er samme hændelse.
    /// </summary>
    public static bool AreEquivalent(Category category) => category is
        Category.SmallStraight or Category.LargeStraight or Category.FullStraight or Category.Yatzy;
}
