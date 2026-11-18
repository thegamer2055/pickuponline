
using PickUp.Core;
using Xunit;

namespace PickUp.Tests;

public sealed class GameRulesTests
{
    [Fact]
    public void Scores_ThreeOfEachSymbol()
    {
        Assert.Equal(5_000, GameRules.BestScore([0, 0, 0, 1, 2]).Points);
        Assert.Equal(6_000, GameRules.BestScore([1, 1, 1, 0, 2]).Points);
        Assert.Equal(7_000, GameRules.BestScore([2, 2, 2, 0, 1]).Points);
        Assert.Equal(8_000, GameRules.BestScore([3, 3, 3, 0, 1]).Points);
        Assert.Equal(9_000, GameRules.BestScore([4, 4, 4, 0, 1]).Points);
        Assert.Equal(10_000, GameRules.BestScore([5, 5, 5, 0, 1]).Points);
    }

    [Fact]
    public void Scores_FourOfEachSymbol()
    {
        Assert.Equal(15_000, GameRules.BestScore([0, 0, 0, 0, 1]).Points);
        Assert.Equal(20_000, GameRules.BestScore([1, 1, 1, 1, 0]).Points);
        Assert.Equal(25_000, GameRules.BestScore([2, 2, 2, 2, 0]).Points);
        Assert.Equal(30_000, GameRules.BestScore([3, 3, 3, 3, 0]).Points);
        Assert.Equal(35_000, GameRules.BestScore([4, 4, 4, 4, 0]).Points);
        Assert.Equal(40_000, GameRules.BestScore([5, 5, 5, 5, 0]).Points);
    }

    [Fact]
    public void Scores_FiveOfEachSymbol()
    {
        Assert.Equal(50_000, GameRules.BestScore([0, 0, 0, 0, 0]).Points);
        Assert.Equal(60_000, GameRules.BestScore([1, 1, 1, 1, 1]).Points);
        Assert.Equal(70_000, GameRules.BestScore([2, 2, 2, 2, 2]).Points);
        Assert.Equal(80_000, GameRules.BestScore([3, 3, 3, 3, 3]).Points);
        Assert.Equal(90_000, GameRules.BestScore([4, 4, 4, 4, 4]).Points);
        Assert.Equal(100_000, GameRules.BestScore([5, 5, 5, 5, 5]).Points);
    }

    [Fact]
    public void Scores_FullHouseAndFiveDifferent()
    {
        Assert.Equal(45_000, GameRules.BestScore([2, 2, 2, 4, 4]).Points);
        Assert.Equal(37_500, GameRules.BestScore([0, 1, 2, 3, 4]).Points);
        Assert.Equal(0, GameRules.BestScore([0, 0, 1, 1, 2]).Points);
    }

    [Fact]
    public void JokerFields_AreSeparateAndCanBeUsedIndependently()
    {
        var dice = new[] { 0, 0, 0, 1, 2 };

        var available = GameRules.AvailableFieldScores(
            dice,
            Array.Empty<ScoreField>());

        Assert.Contains(available, x => x.Field == ScoreField.PairJoker);
        Assert.Contains(available, x => x.Field == ScoreField.WildJoker);

        var afterPairUsed = GameRules.AvailableFieldScores(
            dice,
            new[] { ScoreField.PairJoker });

        Assert.DoesNotContain(
            afterPairUsed,
            x => x.Field == ScoreField.PairJoker);

        Assert.Contains(
            afterPairUsed,
            x => x.Field == ScoreField.WildJoker);
    }

    [Fact]
    public void JokerFields_UseActualDiceWithoutReplacingSymbols()
    {
        // Vier Kleeblätter und ein Mond bleiben vier Kleeblätter.
        var dice = new[] { 0, 0, 0, 0, 1 };

        var pairScore = GameRules.ScoreForField(
            dice,
            ScoreField.PairJoker);

        var wildScore = GameRules.ScoreForField(
            dice,
            ScoreField.WildJoker);

        Assert.Equal(15_000, pairScore.Points);
        Assert.Equal(15_000, wildScore.Points);
    }

    [Fact]
    public void JokerFields_DoNotAwardSingleOrDoubleSymbolScores()
    {
        var dice = new[] { 0, 0, 1, 1, 2 };

        Assert.Equal(
            0,
            GameRules.ScoreForField(dice, ScoreField.PairJoker).Points);

        Assert.Equal(
            0,
            GameRules.ScoreForField(dice, ScoreField.WildJoker).Points);
    }

    [Fact]
    public void NaturalSymbolFields_RequireAtLeastThreeMatchingSymbols()
    {
        Assert.Equal(
            0,
            GameRules.ScoreForField(
                new[] { 0, 1, 2, 3, 4 },
                ScoreField.Clover).Points);

        Assert.Equal(
            0,
            GameRules.ScoreForField(
                new[] { 1, 1, 0, 2, 3 },
                ScoreField.Moon).Points);
    }

    [Fact]
    public void RoundTargets_AreCorrect()
    {
        Assert.Equal(
            new[] { 100_000, 150_000, 175_000, 200_000, 225_000 },
            GameRules.RoundTargets.ToArray());
    }

    [Fact]
    public void RemainingSecondsAndTimeBonus_AreCorrect()
    {
        var start = new DateTimeOffset(
            2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(210, GameRules.RemainingSeconds(start, start));
        Assert.Equal(180, GameRules.RemainingSeconds(start, start.AddSeconds(30)));
        Assert.Equal(1, GameRules.RemainingSeconds(start, start.AddSeconds(209.5)));
        Assert.Equal(0, GameRules.RemainingSeconds(start, start.AddSeconds(210)));

        Assert.Equal(21_000, GameRules.TimeBonus(210));
        Assert.Equal(18_000, GameRules.TimeBonus(180));
        Assert.Equal(0, GameRules.TimeBonus(0));
    }

    [Fact]
    public void ReachingTargetAddsRemainingTimeBonus()
    {
        var award = GameRules.ApplyRoundAward(190_000, 10_000, 1, 75);

        Assert.True(award.TargetReached);
        Assert.Equal(7_500, award.Bonus);
        Assert.Equal(207_500, award.NewRoundScore);
    }

    [Fact]
    public void NotReachingTargetDoesNotAwardTimeBonus()
    {
        var award = GameRules.ApplyRoundAward(100_000, 10_000, 1, 75);

        Assert.False(award.TargetReached);
        Assert.Equal(0, award.Bonus);
        Assert.Equal(110_000, award.NewRoundScore);
    }

    [Fact]
    public void FiveDifferent_AndBothJokers_AreAvailable()
    {
        int[] dice = [0, 1, 2, 3, 4];

        var scores = GameRules.AvailableFieldScores(
            dice,
            Array.Empty<ScoreField>());

        Assert.Contains(scores, x =>
            x.Field == ScoreField.FiveDifferent &&
            x.Points == 37_500);

        Assert.Contains(scores, x =>
            x.Field == ScoreField.PairJoker &&
            x.Points == 37_500);

        Assert.Contains(scores, x =>
            x.Field == ScoreField.WildJoker &&
            x.Points == 37_500);
    }
}