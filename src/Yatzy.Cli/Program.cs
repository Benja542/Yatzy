using System.Globalization;
using Yatzy.Core.Game;
using Yatzy.Core.Probability;
using Yatzy.Core.Rules;

namespace Yatzy.Cli;

/// <summary>
/// Kommandolinjeværktøj til de tunge kørsler. Brugerfladen er Blazor-projektet
/// <c>Yatzy.Web</c>; her kan man køre simuleringer med mange flere gentagelser,
/// end det er rart at gøre i en browser.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "tabel";

        switch (command)
        {
            case "tabel":
                PrintProbabilityTable(GetLong(args, "--kast", 200_000), GetGoal(args));
                return 0;
            case "analytisk":
                PrintAnalytic(GetGoal(args));
                return 0;
            case "spil":
                SimulateGames(GetInt(args, "--spil", 2_000), GetInt(args, "--seed", 1), GetDouble(args, "--bonus", 2.0));
                return 0;
            case "hjaelp":
            case "--help":
            case "-h":
                PrintHelp();
                return 0;
            default:
                Console.Error.WriteLine($"Ukendt kommando: {command}");
                PrintHelp();
                return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Yatzy - 6 terninger, 20 slag, 3 kast pr. tur

            Brug:
              yatzy tabel [--kast N] [--maal maks|point]
                                         Sandsynlighed for hvert slag: analytisk, udfaldstræ og Monte Carlo
              yatzy analytisk [--maal maks|point]
                                         De analytiske udregninger og deres led, skrevet ud

              yatzy spil [--spil N] [--seed S] [--bonus B]
                                         Simulerer hele spil og opsummerer slutscoren
                                         --bonus styrer hvor hårdt computeren spiller efter bonussen

              --maal maks   (standard) sandsynligheden for at få maks point i slaget
              --maal point  sandsynligheden for at få point overhovedet
            """);
    }

    private static void PrintProbabilityTable(long trials, ScoreGoal goal)
    {
        Console.WriteLine(
            $"Sandsynlighed for at få {ScoreGoals.DanishName(goal)} i hvert slag. " +
            $"Monte Carlo: {trials:N0} simulerede ture pr. slag.");
        Console.WriteLine();
        Console.WriteLine(
            "{0,-16} {1,6} {2,12} {3,12} {4,12} {5,10} {6,12}",
            "Slag", "Maks", "Analytisk*", "Udfaldstræ", "Monte Carlo", "± 95 %", "Afvigelse");
        Console.WriteLine(new string('-', 88));

        var rows = ProbabilityReport.Build(
            Categories.All, counts: null, keepCounts: null,
            rerollsLeft: YatzyRules.RollsPerTurn, goal, trials, seed: 12345);

        foreach (var row in rows)
        {
            var mc = row.MonteCarlo!.Value;
            Console.WriteLine(
                "{0,-16} {1,6} {2,12} {3,12} {4,12} {5,10} {6,12}",
                Categories.DanishName(row.Category),
                row.MaxScore,
                Percent(row.Analytic.Value),
                Percent(row.TreeProbability),
                Percent(mc.Estimate),
                Percent(mc.MarginOfError),
                Percent(mc.Estimate - row.TreeProbability));
        }

        Console.WriteLine();
        Console.WriteLine("* Analytisk = sandsynligheden i ét enkelt kast med alle seks terninger.");
        Console.WriteLine("  Udfaldstræ og Monte Carlo = en hel tur med 3 kast og optimale omkast.");
    }

    private static void PrintAnalytic(ScoreGoal goal)
    {
        Console.WriteLine($"Mål: {ScoreGoals.DanishName(goal)}");
        Console.WriteLine();
        foreach (var result in AnalyticProbability.All(goal))
        {
            var bruteForce = AnalyticProbability.BruteForceCount(result.Category, goal);
            Console.WriteLine($"{Categories.DanishName(result.Category)} - {result.Method}");
            Console.WriteLine($"  {result.Formula}");
            foreach (var term in result.Terms)
            {
                Console.WriteLine($"    {term.Label,-10} {term.Explanation}");
            }

            Console.WriteLine(
                $"  P = {result.Probability} = {result.Value.ToString("P4", CultureInfo.InvariantCulture)}" +
                $"   [kontroltælling: {bruteForce:N0} {(bruteForce == result.Favourable ? "OK" : "AFVIGER!")}]");
            Console.WriteLine();
        }
    }

    private static void SimulateGames(int games, int seed, double bonusBias)
    {
        Console.WriteLine($"Simulerer {games:N0} hele spil (bonusvægt {bonusBias:N1}) ...");
        var simulation = new GameSimulation(seed, bonusBias);
        simulation.Run(games);
        var summary = simulation.Summarize();

        Console.WriteLine();
        Console.WriteLine($"Spil            : {summary.Games:N0}");
        Console.WriteLine($"Gennemsnit      : {summary.AverageTotal:N1} (± {1.96 * summary.StandardError:N1})");
        Console.WriteLine($"Median          : {summary.MedianTotal:N1}");
        Console.WriteLine($"Spredning       : {summary.StandardDeviation:N1}");
        Console.WriteLine($"Laveste/højeste : {summary.MinTotal} / {summary.MaxTotal}");
        Console.WriteLine($"Bonus opnået    : {summary.BonusRate:P1}");
        Console.WriteLine($"Tid             : {summary.ElapsedMilliseconds:N0} ms");
        Console.WriteLine();
        Console.WriteLine("{0,-16} {1,12} {2,12}", "Slag", "Gns. point", "Rammer");
        Console.WriteLine(new string('-', 42));
        foreach (var category in Categories.All)
        {
            Console.WriteLine(
                "{0,-16} {1,12} {2,12}",
                Categories.DanishName(category),
                summary.AverageScore[(int)category].ToString("N1", CultureInfo.InvariantCulture),
                Percent(summary.HitRate[(int)category]));
        }
    }

    private static ScoreGoal GetGoal(string[] args) =>
        TryGet(args, "--maal", out var value) && value.StartsWith("p", StringComparison.OrdinalIgnoreCase)
            ? ScoreGoal.AnyPoints
            : ScoreGoal.MaxPoints;

    private static string Percent(double value) => (100 * value).ToString("N3", CultureInfo.InvariantCulture) + " %";

    private static long GetLong(string[] args, string name, long fallback) =>
        TryGet(args, name, out var value) && long.TryParse(value, out var parsed) ? parsed : fallback;

    private static double GetDouble(string[] args, string name, double fallback) =>
        TryGet(args, name, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static int GetInt(string[] args, string name, int fallback) =>
        TryGet(args, name, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static bool TryGet(string[] args, string name, out string value)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                value = args[i + 1];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
