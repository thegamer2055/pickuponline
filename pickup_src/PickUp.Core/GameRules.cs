
namespace PickUp.Core;

public enum GameSymbol
{
    Clover = 0,
    Moon = 1,
    Heart = 2,
    Star = 3,
    Horseshoe = 4,
    Crown = 5,

    // Alte Werte bleiben vorerst für mögliche Verweise im Projekt erhalten.
    // Sie dürfen nicht mehr als Würfelergebnis verwendet werden.
    PairJoker = 6,
    WildJoker = 7
}

public sealed record ScoreResult(
    int Points,
    string Combination,
    string? SymbolName = null,
    string JokerExplanation = "");

public sealed record RoundAward(
    int NewRoundScore,
    int Bonus,
    bool TargetReached);

public sealed record FieldScore(
    ScoreField Field,
    int Points,
    string Combination,
    string JokerExplanation = "");

public static class GameRules
{
    public const int RoundDurationSeconds = 210;
    public const int BonusPerRemainingSecond = 100;

    private static readonly int[] Targets =
    [100_000, 150_000, 175_000, 200_000, 225_000];

    private static readonly int[] Three =
        [5_000, 6_000, 7_000, 8_000, 9_000, 10_000];

    private static readonly int[] Four =
        [15_000, 20_000, 25_000, 30_000, 35_000, 40_000];

    private static readonly int[] Five =
        [50_000, 60_000, 70_000, 80_000, 90_000, 100_000];

    private static readonly string[] Names =
        ["Kleeblatt", "Mond", "Herz", "Stern", "Hufeisen", "Krone"];

    private static readonly IReadOnlyList<int> TargetView =
        Array.AsReadOnly(Targets);

    public static IReadOnlyList<int> RoundTargets => TargetView;



    public static int TargetForRound(int round)
    {
        if (round is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(round));

        return Targets[round - 1];
    }

    public static int RemainingSeconds(
        DateTimeOffset roundStartedAtUtc,
        DateTimeOffset nowUtc)
    {
        var elapsed = Math.Max(
            0,
            (nowUtc - roundStartedAtUtc).TotalSeconds);

        return Math.Clamp(
            RoundDurationSeconds - (int)Math.Floor(elapsed),
            0,
            RoundDurationSeconds);
    }

    public static int TimeBonus(int remainingSeconds)
    {
        return Math.Clamp(
            remainingSeconds,
            0,
            RoundDurationSeconds) * BonusPerRemainingSecond;
    }


    public static RoundAward ApplyRoundAward(
        int currentRoundScore,
        int awardedPoints,
        int round,
        int remainingSeconds)
    {
        if (currentRoundScore < 0 || awardedPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(currentRoundScore));

        var subtotal = checked(currentRoundScore + awardedPoints);
        var target = TargetForRound(round);

        // Kein weiterer Zeitbonus, wenn das Ziel bereits erreicht wurde.
        if (currentRoundScore >= target || subtotal < target)
            return new RoundAward(subtotal, 0, false);

        // Zeitbonus nur beim erstmaligen Erreichen des Rundenziels.
        var bonus = TimeBonus(remainingSeconds);

        return new RoundAward(
            checked(subtotal + bonus),
            bonus,
            true);
    }


    public static IReadOnlyList<FieldScore> AvailableFieldScores(
        IReadOnlyList<int> dice,
        IEnumerable<ScoreField> usedFields)
    {
        ValidateDice(dice);

        var used = usedFields.ToHashSet();
        var results = new List<FieldScore>();

        foreach (ScoreField field in Enum.GetValues(typeof(ScoreField)))
        {
            if (used.Contains(field))
                continue;

            var score = ScoreForField(dice, field);

            if (score.Points > 0)
                results.Add(score);
        }

        return results;
    }

    public static FieldScore ScoreForField(
        IReadOnlyList<int> dice,
        ScoreField field)
    {
        ValidateDice(dice);

        var naturalCounts = new int[6];

        foreach (var value in dice)
            naturalCounts[value]++;

        if (field == ScoreField.PairJoker ||
            field == ScoreField.WildJoker)
        {
            return ScoreJokerField(naturalCounts, field);
        }

        var result = EvaluateField(naturalCounts, field);

        return new FieldScore(
            field,
            result.Points,
            result.Combination);
    }


    private static FieldScore ScoreJokerField(
        int[] naturalCounts,
        ScoreField field)
    {
        var combinationFields = new[]
        {
        ScoreField.Clover,
        ScoreField.Moon,
        ScoreField.Heart,
        ScoreField.Star,
        ScoreField.Horseshoe,
        ScoreField.Crown,
        ScoreField.FourOfKind,
        ScoreField.FiveOfKind,
        ScoreField.FullHouse,
        ScoreField.FiveDifferent
    };

        var bestPoints = 0;
        var bestCombination = "Einzelwürfelwertung";

        foreach (var combinationField in combinationFields)
        {
            var result = EvaluateField(naturalCounts, combinationField);

            if (result.Points > bestPoints)
            {
                bestPoints = result.Points;
                bestCombination = result.Combination;
            }
        }

        // Wenn keine reguläre Kombination möglich ist:
        // Punkte für jedes einzelne Symbol vergeben.
        if (bestPoints == 0)
        {
            int[] singlePoints = [1_000, 2_000, 3_000, 4_000, 5_000, 6_000];

            for (var i = 0; i < naturalCounts.Length; i++)
            {
                bestPoints += naturalCounts[i] * singlePoints[i];
            }

            bestCombination = "Einzelwürfelwertung";
        }

        var explanation = field == ScoreField.PairJoker
            ? "Paar-Joker"
            : "Freier Joker";

        return new FieldScore(
            field,
            bestPoints,
            bestCombination,
            explanation);
    }


    public static ScoreResult BestScore(IReadOnlyList<int> dice)
    {
        ValidateDice(dice);

        ScoreResult best = new(0, "Keine gültige Kombination");

        foreach (ScoreField field in Enum.GetValues(typeof(ScoreField)))
        {
            var result = ScoreForField(dice, field);

            if (result.Points > best.Points)
            {
                best = new ScoreResult(
                    result.Points,
                    result.Combination,
                    null,
                    result.JokerExplanation);
            }
        }

        return best;
    }

    private static ScoreResult EvaluateField(
        IReadOnlyList<int> counts,
        ScoreField field)
    {
        var total = counts.Sum();
        var sorted = counts.OrderByDescending(x => x).ToArray();

        switch (field)
        {
            case ScoreField.Clover:
            case ScoreField.Moon:
            case ScoreField.Heart:
            case ScoreField.Star:
            case ScoreField.Horseshoe:
            case ScoreField.Crown:
                {
                    var symbol = (int)field;
                    var count = counts[symbol];

                    if (count < 3)
                        return new(0, "Mindestens drei gleiche Symbole erforderlich");

                    var points = count switch
                    {
                        3 => Three[symbol],
                        4 => Four[symbol],
                        5 => Five[symbol],
                        _ => 0
                    };

                    return new(
                        points,
                        $"{count} × {Names[symbol]}",
                        Names[symbol]);
                }

            case ScoreField.FourOfKind:
                {
                    for (var i = 0; i < 6; i++)
                    {
                        if (counts[i] == 4)
                            return new(Four[i], $"4 × {Names[i]}");

                        if (counts[i] == 5)
                            return new(Five[i], $"5 × {Names[i]}");
                    }

                    return new(0, "Nicht gültig");
                }

            case ScoreField.FiveOfKind:
                {
                    for (var i = 0; i < 6; i++)
                    {
                        if (counts[i] == 5)
                            return new(Five[i], $"5 × {Names[i]}");
                    }

                    return new(0, "Nicht gültig");
                }

            case ScoreField.FullHouse:
                if (total == 5 && sorted[0] == 3 && sorted[1] == 2)
                    return new(45_000, "Full House (3 + 2)");

                return new(0, "Nicht gültig");

            case ScoreField.FiveDifferent:
                if (total == 5 &&
                    counts.Count(value => value == 1) == 5 &&
                    counts.Count(value => value == 0) == 1)
                {
                    return new(37_500, "5 verschiedene Symbole");
                }

                return new(0, "Nicht gültig");

            default:
                return new(0, "Unbekanntes Wertungsfeld");
        }
    }

    private static void ValidateDice(IReadOnlyList<int> dice)
    {
        ArgumentNullException.ThrowIfNull(dice);

        if (dice.Count != 5 || dice.Any(value => value is < 0 or > 5))
        {
            throw new ArgumentException(
                "Es müssen genau fünf natürliche Symbole (0 bis 5) übergeben werden.",
                nameof(dice));
        }
    }
}
