namespace Yatzy.Core.Game;

/// <summary>
/// En spiller med sin egen kolonne på blokken.
/// </summary>
public sealed class Player
{
    public Player(string name) => Name = name;

    /// <summary>Spillerens navn. Kan ændres undervejs uden at påvirke blokken.</summary>
    public string Name { get; set; }

    /// <summary>Spillerens blok.</summary>
    public ScoreSheet Sheet { get; private set; } = new();

    /// <summary>Har spilleren skrevet alle 20 slag?</summary>
    public bool IsDone => Sheet.IsComplete;

    /// <summary>Rydder blokken - bruges når der startes et nyt spil.</summary>
    internal void Reset() => Sheet = new ScoreSheet();

    public override string ToString() => Name;
}

/// <summary>
/// En placering i slutstillingen.
/// </summary>
/// <param name="Rank">Placeringen, 1 og opefter. Spillere med samme score deler placering.</param>
/// <param name="Player">Spilleren.</param>
/// <param name="Total">Spillerens samlede score.</param>
/// <param name="IsWinner">Er spilleren (delt) vinder?</param>
public readonly record struct Standing(int Rank, Player Player, int Total, bool IsWinner);
