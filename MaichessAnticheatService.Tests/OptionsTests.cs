using MaichessAnticheatService.Analysis;
using MaichessAnticheatService.Detection;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void DetectionDefaultsMatchTheDocumentedHeuristics()
    {
        DetectionOptions options = new();

        Assert.Equal(8, options.BookPlies);
        Assert.Equal(0.55, options.CorrelationBaseline);
        Assert.Equal(0.5, options.PremoveCorrelationWeight);
        Assert.Equal(10, options.TimingMinSamples);
        Assert.Equal(0.45, options.TimingCvThreshold);
        Assert.Equal(0.7, options.GameCorrelationWeight);
        Assert.Equal(0.3, options.GameStatisticalWeight);
        Assert.Equal(0.25, options.AccuracySpikeDelta);
        Assert.Equal(0.1, options.AccuracySpikeBump);
        Assert.Equal(0.7, options.FlagThreshold);
        Assert.Equal(3, options.MinGames);
        Assert.Equal(10, options.CaseWindow);
        Assert.Equal(12, options.LiveMinPlies);
        Assert.Equal(0.7, options.LiveAdvisoryThreshold);
        Assert.Equal(3, options.EngineLines);
    }

    [Fact]
    public void AnalysisDefaultsThrottleTheEngine()
    {
        AnalysisOptions options = new();

        Assert.Equal("blitz", options.BotId);
        Assert.Equal(500, options.DelayMs);
    }

    [Fact]
    public void AnalysisOptionsAreOverridable()
    {
        AnalysisOptions options = new() { BotId = "bullet", DelayMs = 0 };

        Assert.Equal("bullet", options.BotId);
        Assert.Equal(0, options.DelayMs);
    }
}
