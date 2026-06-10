using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class StatisticalDetectorTests
{
    private static PlyObservation Obs(int ply, long thinkMs, bool premove = false) =>
        new(ply, "u1", "e2e4", thinkMs, premove);

    [Fact]
    public void TooFewSamplesAbstains()
    {
        PlyObservation[] observations = [Obs(4, 1000), Obs(6, 1000)];

        Assert.Equal(0, StatisticalDetector.Score(observations, TestOptions.Default()));
    }

    [Fact]
    public void MachineSteadyTimingScoresHigh()
    {
        PlyObservation[] observations = [Obs(4, 1000), Obs(6, 1000), Obs(8, 1000), Obs(10, 1000)];

        Assert.Equal(1, StatisticalDetector.Score(observations, TestOptions.Default()));
    }

    [Fact]
    public void HumanlyVariedTimingScoresZero()
    {
        PlyObservation[] observations = [Obs(4, 200), Obs(6, 9000), Obs(8, 700), Obs(10, 30000)];

        Assert.Equal(0, StatisticalDetector.Score(observations, TestOptions.Default()));
    }

    [Fact]
    public void FastPremovedPliesDoNotRaiseTimingSuspicion()
    {
        // The mandated regression: a heavy pre-mover with near-zero pre-move
        // times must not look machine-steady. With pre-moves wrongly included
        // the sample would be dominated by ~0ms moves; excluded, only the four
        // varied honest thinks remain and the score stays 0.
        PlyObservation[] observations =
        [
            Obs(4, 10, premove: true),
            Obs(5, 12, premove: true),
            Obs(6, 9, premove: true),
            Obs(7, 11, premove: true),
            Obs(8, 8, premove: true),
            Obs(9, 300),
            Obs(10, 12000),
            Obs(11, 900),
            Obs(12, 25000),
        ];

        Assert.Equal(0, StatisticalDetector.Score(observations, TestOptions.Default()));
    }

    [Fact]
    public void BookAndUnknownTimingPliesAreExcluded()
    {
        // Two book plies + one unknown-timing ply leave two usable samples,
        // below the min-sample bar -> abstain even though those two are steady.
        PlyObservation[] observations =
        [
            Obs(0, 1000),
            Obs(1, 1000),
            Obs(4, 0),
            Obs(6, 1000),
            Obs(8, 1000),
        ];

        Assert.Equal(0, StatisticalDetector.Score(observations, TestOptions.Default()));
    }

    [Fact]
    public void ModerateConsistencyScoresBetweenZeroAndOne()
    {
        // cv of {800,1000,1200} = sqrt(80000/3)/1000 ≈ 0.1633 ->
        // (0.5-0.1633)/0.5 ≈ 0.6734.
        PlyObservation[] observations = [Obs(4, 800), Obs(6, 1000), Obs(8, 1200)];

        double score = StatisticalDetector.Score(observations, TestOptions.Default());

        Assert.InRange(score, 0.67, 0.68);
    }
}
