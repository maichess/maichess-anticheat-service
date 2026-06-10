using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class CombinedScorerTests
{
    private static GameVerdict Verdict(string matchId, double score, double correlation = 0.5) =>
        new(matchId, score, correlation, 0.2, [], 1000);

    [Fact]
    public void GameScoreBlendsByConfiguredWeights()
    {
        Assert.Equal(0.62, CombinedScorer.GameScore(0.8, 0.2, TestOptions.Default()), 10);
    }

    [Fact]
    public void GameScoreClampsAtOne()
    {
        DetectionOptions options = TestOptions.Default() with { GameCorrelationWeight = 2.0 };

        Assert.Equal(1, CombinedScorer.GameScore(1, 1, options));
    }

    [Fact]
    public void EmptyCaseScoresZero()
    {
        Assert.Equal(0, CombinedScorer.CaseScore([], TestOptions.Default()));
    }

    [Fact]
    public void CaseScoreAveragesRecentGames()
    {
        GameVerdict[] verdicts = [Verdict("m1", 0.4), Verdict("m2", 0.6)];

        Assert.Equal(0.5, CombinedScorer.CaseScore(verdicts, TestOptions.Default()), 10);
    }

    [Fact]
    public void OnlyTheConfiguredWindowCounts()
    {
        // Window 3: the 1.0-score game falls out once three newer games exist.
        GameVerdict[] verdicts =
        [
            Verdict("m1", 1.0),
            Verdict("m2", 0.3),
            Verdict("m3", 0.3),
            Verdict("m4", 0.3),
        ];

        Assert.Equal(0.3, CombinedScorer.CaseScore(verdicts, TestOptions.Default()), 10);
    }

    [Fact]
    public void AccuracySpikeBumpsTheScore()
    {
        // Newest correlation 0.9 vs prior average 0.3 -> spike >= 0.25 delta.
        GameVerdict[] verdicts =
        [
            Verdict("m1", 0.4, correlation: 0.3),
            Verdict("m2", 0.4, correlation: 0.9),
        ];

        Assert.Equal(0.5, CombinedScorer.CaseScore(verdicts, TestOptions.Default()), 10);
    }

    [Fact]
    public void AccuracySpikeBumpClampsAtOne()
    {
        GameVerdict[] verdicts =
        [
            Verdict("m1", 0.95, correlation: 0.3),
            Verdict("m2", 1.0, correlation: 0.9),
        ];

        Assert.Equal(1, CombinedScorer.CaseScore(verdicts, TestOptions.Default()));
    }

    [Fact]
    public void SteadyCorrelationGetsNoBump()
    {
        GameVerdict[] verdicts =
        [
            Verdict("m1", 0.4, correlation: 0.5),
            Verdict("m2", 0.4, correlation: 0.6),
        ];

        Assert.Equal(0.4, CombinedScorer.CaseScore(verdicts, TestOptions.Default()), 10);
    }

    [Fact]
    public void SingleGameNeverSpikes()
    {
        Assert.Equal(0.9, CombinedScorer.CaseScore([Verdict("m1", 0.9)], TestOptions.Default()), 10);
    }

    [Fact]
    public void FlagsOnlyWithEnoughGamesAboveThreshold()
    {
        DetectionOptions options = TestOptions.Default();

        Assert.False(CombinedScorer.ShouldFlag(0.9, 1, options));
        Assert.False(CombinedScorer.ShouldFlag(0.5, 5, options));
        Assert.True(CombinedScorer.ShouldFlag(0.7, 2, options));
    }
}
