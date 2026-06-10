using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class EngineCorrelationDetectorTests
{
    private static PlyObservation Obs(int ply, string move = "e2e4", bool premove = false) =>
        new(ply, "u1", move, 1000, premove);

    private static IReadOnlyList<EngineLine> Lines(string top, string second = "a2a3", string third = "b2b3") =>
        [new EngineLine(1, 50, top), new EngineLine(2, 30, second), new EngineLine(3, 10, third)];

    [Fact]
    public void NoPliesScoresZero()
    {
        CorrelationResult result = EngineCorrelationDetector.Score([], TestOptions.Default());

        Assert.Equal(0, result.Score);
        Assert.Empty(result.SuspiciousPlies);
        Assert.Equal(0, result.SampledPlies);
    }

    [Fact]
    public void BookPliesAreSkipped()
    {
        DetectionOptions options = TestOptions.Default() with { BookPlies = 4 };
        AnalyzedPly[] plies = [new(Obs(0), Lines("e2e4")), new(Obs(3), Lines("e2e4"))];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, options);

        Assert.Equal(0, result.SampledPlies);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void EnginelessPliesAreSkipped()
    {
        AnalyzedPly[] plies = [new(Obs(5), [])];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, TestOptions.Default());

        Assert.Equal(0, result.SampledPlies);
    }

    [Fact]
    public void PerfectTopMatchingScoresOneAndMarksPliesSuspicious()
    {
        AnalyzedPly[] plies = [new(Obs(4), Lines("e2e4")), new(Obs(5), Lines("e2e4"))];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, TestOptions.Default());

        Assert.Equal(1, result.Score);
        Assert.Equal([4, 5], result.SuspiciousPlies);
        Assert.Equal(2, result.SampledPlies);
    }

    [Fact]
    public void RateBelowBaselineClampsToZero()
    {
        // One top-3 match out of two plies: rate 0.125 < baseline 0.5.
        AnalyzedPly[] plies =
        [
            new(Obs(4, "b2b3"), Lines("e2e4")),
            new(Obs(5, "h2h4"), Lines("e2e4")),
        ];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, TestOptions.Default());

        Assert.Equal(0, result.Score);
        Assert.Empty(result.SuspiciousPlies);
    }

    [Fact]
    public void SecondAndThirdLineMatchesCountPartially()
    {
        // top-2 (0.5) + top-3 (0.25) over two plies: rate 0.375, below baseline.
        AnalyzedPly[] plies =
        [
            new(Obs(4, "a2a3"), Lines("e2e4")),
            new(Obs(5, "b2b3"), Lines("e2e4")),
        ];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, TestOptions.Default());

        Assert.Equal(0, result.Score);
        Assert.Equal(2, result.SampledPlies);
    }

    [Fact]
    public void PremovedPliesAreDownweighted()
    {
        // Non-premove miss (weight 1, value 0) + premoved top-1 (weight 0.5,
        // value 1): rate = 0.5/1.5 = 1/3 — lower than the 0.5 an unweighted
        // average would give. Premoved top-1 still lands in suspicious plies.
        DetectionOptions options = TestOptions.Default() with { CorrelationBaseline = 0.25 };
        AnalyzedPly[] plies =
        [
            new(Obs(4, "h2h4"), Lines("e2e4")),
            new(Obs(5, "e2e4", premove: true), Lines("e2e4")),
        ];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, options);

        Assert.Equal((1.0 / 3 - 0.25) / 0.75, result.Score, 10);
        Assert.Equal([5], result.SuspiciousPlies);
    }

    [Fact]
    public void MixedMatchingScoresBetweenZeroAndOne()
    {
        // 3 top-1 + 1 miss: rate 0.75 -> (0.75-0.5)/0.5 = 0.5.
        AnalyzedPly[] plies =
        [
            new(Obs(4), Lines("e2e4")),
            new(Obs(5), Lines("e2e4")),
            new(Obs(6), Lines("e2e4")),
            new(Obs(7, "h2h4"), Lines("e2e4")),
        ];

        CorrelationResult result = EngineCorrelationDetector.Score(plies, TestOptions.Default());

        Assert.Equal(0.5, result.Score, 10);
    }
}
