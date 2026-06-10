using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class LiveScorerTests
{
    private static PlyObservation Obs(int ply, long thinkMs, bool premove = false) =>
        new(ply, "u1", "e2e4", thinkMs, premove);

    private static LivePlayerState Feed(LivePlayerState state, params PlyObservation[] observations)
    {
        foreach (PlyObservation observation in observations)
        {
            (state, _) = LiveScorer.Observe(state, observation, TestOptions.Default());
        }

        return state;
    }

    [Fact]
    public void PremovedBookAndUnknownTimingPliesNeverEnterTheSample()
    {
        LivePlayerState state = Feed(
            LivePlayerState.Empty,
            Obs(4, 10, premove: true),
            Obs(0, 1000),
            Obs(6, 0));

        Assert.Equal(0, state.Samples);
    }

    [Fact]
    public void SteadyTimingRaisesOneAdvisorySignal()
    {
        LivePlayerState state = Feed(LivePlayerState.Empty, Obs(4, 1000), Obs(6, 1000));

        (LivePlayerState raised, double? advisory) =
            LiveScorer.Observe(state, Obs(8, 1000), TestOptions.Default());

        Assert.Equal(1, advisory);
        Assert.True(raised.Raised);

        // Further steady plies never signal again for this (match, player).
        (LivePlayerState after, double? second) =
            LiveScorer.Observe(raised, Obs(10, 1000), TestOptions.Default());
        Assert.Null(second);
        Assert.Equal(4, after.Samples);
    }

    [Fact]
    public void NoSignalBelowTheMinimumSampleCount()
    {
        (LivePlayerState state, double? advisory) =
            LiveScorer.Observe(Feed(LivePlayerState.Empty, Obs(4, 1000)), Obs(6, 1000), TestOptions.Default());

        Assert.Null(advisory);
        Assert.Equal(2, state.Samples);
    }

    [Fact]
    public void VariedTimingStaysBelowTheAdvisoryThreshold()
    {
        LivePlayerState state = Feed(LivePlayerState.Empty, Obs(4, 200), Obs(6, 9000));

        (_, double? advisory) = LiveScorer.Observe(state, Obs(8, 30000), TestOptions.Default());

        Assert.Null(advisory);
    }

    [Fact]
    public void HeavyPremoverNeverTriggersALiveSignal()
    {
        // Pre-moves carry near-zero think time but are excluded ply by ply, so
        // even a long streak of them cannot reach the sample minimum.
        LivePlayerState state = LivePlayerState.Empty;
        double? advisory = null;
        for (int ply = 4; ply < 20; ply++)
        {
            (state, advisory) = LiveScorer.Observe(state, Obs(ply, 10, premove: true), TestOptions.Default());
            Assert.Null(advisory);
        }

        Assert.Equal(0, state.Samples);
    }
}
