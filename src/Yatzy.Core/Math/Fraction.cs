using System.Globalization;
using System.Numerics;

namespace Yatzy.Core.Math;

/// <summary>
/// Eksakt brøk (tæller/nævner) baseret på <see cref="BigInteger"/>.
/// Bruges til de analytisk beregnede sandsynligheder, så de kan vises
/// uden afrundingsfejl - fx 2520/46656 = 35/648.
/// </summary>
public readonly struct Fraction : IEquatable<Fraction>, IComparable<Fraction>
{
    public static readonly Fraction Zero = new(BigInteger.Zero, BigInteger.One);
    public static readonly Fraction One = new(BigInteger.One, BigInteger.One);

    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public Fraction(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException("Nævneren i en brøk må ikke være 0.");
        }

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        if (gcd > BigInteger.One)
        {
            numerator /= gcd;
            denominator /= gcd;
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    public static implicit operator Fraction(int value) => new(value, BigInteger.One);

    public static implicit operator Fraction(BigInteger value) => new(value, BigInteger.One);

    public static Fraction operator +(Fraction a, Fraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Fraction operator -(Fraction a, Fraction b) =>
        new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Fraction operator *(Fraction a, Fraction b) =>
        new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    public static Fraction operator /(Fraction a, Fraction b) =>
        new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);

    public static bool operator ==(Fraction a, Fraction b) => a.Equals(b);

    public static bool operator !=(Fraction a, Fraction b) => !a.Equals(b);

    public static bool operator <(Fraction a, Fraction b) => a.CompareTo(b) < 0;

    public static bool operator >(Fraction a, Fraction b) => a.CompareTo(b) > 0;

    public static bool operator <=(Fraction a, Fraction b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Fraction a, Fraction b) => a.CompareTo(b) >= 0;

    /// <summary>Brøken som kommatal. Beregnes med ekstra præcision før konvertering.</summary>
    public double ToDouble()
    {
        if (Numerator.IsZero)
        {
            return 0d;
        }

        // Skalér op med 10^25 for at bevare præcision når tæller/nævner er store.
        var scale = BigInteger.Pow(10, 25);
        var scaled = Numerator * scale / Denominator;
        return (double)scaled / System.Math.Pow(10, 25);
    }

    /// <summary>Brøken skrevet som "a/b" (eller blot "a" når nævneren er 1).</summary>
    public override string ToString() =>
        Denominator.IsOne
            ? Numerator.ToString(CultureInfo.InvariantCulture)
            : $"{Numerator.ToString(CultureInfo.InvariantCulture)}/{Denominator.ToString(CultureInfo.InvariantCulture)}";

    public bool Equals(Fraction other) => Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is Fraction other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public int CompareTo(Fraction other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
}
