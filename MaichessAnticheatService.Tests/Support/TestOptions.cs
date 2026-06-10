using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Tests.Support;

// Small thresholds so tests construct minimal inputs; individual tests
// override what they probe.
internal static class TestOptions
{
    public static DetectionOptions Default() => new()
    {
        BookPlies = 2,
        CorrelationBaseline = 0.5,
        PremoveCorrelationWeight = 0.5,
        TimingMinSamples = 3,
        TimingCvThreshold = 0.5,
        GameCorrelationWeight = 0.7,
        GameStatisticalWeight = 0.3,
        AccuracySpikeDelta = 0.25,
        AccuracySpikeBump = 0.1,
        FlagThreshold = 0.7,
        MinGames = 2,
        CaseWindow = 3,
        LiveMinPlies = 3,
        LiveAdvisoryThreshold = 0.5,
        EngineLines = 3,
    };
}
