using System.Globalization;

namespace Yatzy.Web.Services;

/// <summary>
/// Talformatering på dansk. Appen er bygget med <c>InvariantGlobalization</c>, så den
/// ikke skal hente ICU-data ned i browseren; til gengæld skifter vi selv komma og
/// tusindtalsseparator, så tallene ser danske ud.
/// </summary>
public static class Format
{
    /// <summary>Et tal med et fast antal decimaler, fx 1.234,57.</summary>
    public static string Number(double value, int decimals = 2) =>
        Danish(value.ToString("N" + decimals, CultureInfo.InvariantCulture));

    /// <summary>Et heltal med tusindtalsseparator, fx 46.656.</summary>
    public static string Integer(long value) =>
        Danish(value.ToString("N0", CultureInfo.InvariantCulture));

    /// <summary>En sandsynlighed som procent, fx 43,72 %.</summary>
    public static string Percent(double value, int decimals = 2) =>
        double.IsNaN(value) ? "-" : Number(100 * value, decimals) + " %";

    /// <summary>En sandsynlighed som procent med fortegn, fx +0,05 %.</summary>
    public static string SignedPercent(double value, int decimals = 3)
    {
        if (double.IsNaN(value))
        {
            return "-";
        }

        var text = Percent(System.Math.Abs(value), decimals);
        return value < 0 ? "−" + text : "+" + text;
    }

    /// <summary>
    /// Meget små sandsynligheder skrives med flere decimaler, så fx yatzy ikke
    /// bare bliver til "0,00 %".
    /// </summary>
    public static string SmartPercent(double value)
    {
        if (double.IsNaN(value))
        {
            return "-";
        }

        if (value == 0d)
        {
            return "0 %";
        }

        var decimals = value switch
        {
            >= 0.01 => 2,
            >= 0.001 => 3,
            >= 0.0001 => 4,
            _ => 5,
        };

        return Percent(value, decimals);
    }

    /// <summary>Terningeøjne skrevet som "3-3-5-5-5-6".</summary>
    public static string Dice(IEnumerable<int> dice) => string.Join("-", dice);

    private const char Placeholder = '\u0001';

    private static string Danish(string invariant) =>
        invariant.Replace(',', Placeholder).Replace('.', ',').Replace(Placeholder, '.');
}
