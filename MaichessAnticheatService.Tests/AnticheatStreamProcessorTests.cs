using Maichess.Events.V1;
using MaichessAnticheatService.Stream;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class AnticheatStreamProcessorTests
{
    private static MatchEvent Created(string matchId = "m1") =>
        new()
        {
            EventId = $"{matchId}-created",
            EventType = "match.MatchCreated",
            AggregateId = matchId,
            OccurredAt = 1000,
            MatchCreated = new MatchCreated
            {
                White = new Player { UserId = "w" },
                Black = new Player { UserId = "b" },
            },
        };

    private static MatchEvent Move(string matchId, int index, long appliedAtMs) =>
        new()
        {
            EventId = $"{matchId}-{index}",
            EventType = "match.MoveApplied",
            AggregateId = matchId,
            OccurredAt = appliedAtMs,
            MoveApplied = new MoveApplied
            {
                MoveUci = "e2e4",
                Index = index,
                Player = new Player { UserId = index % 2 == 0 ? "w" : "b" },
                AppliedAtMs = appliedAtMs,
            },
        };

    private static MatchEvent BotMove(string matchId, int index, long appliedAtMs) =>
        new()
        {
            EventId = $"{matchId}-bot-{index}",
            EventType = "match.MoveApplied",
            AggregateId = matchId,
            OccurredAt = appliedAtMs,
            MoveApplied = new MoveApplied
            {
                MoveUci = "e7e5",
                Index = index,
                Player = new Player { BotId = "bot" },
                AppliedAtMs = appliedAtMs,
            },
        };

    private static MatchEvent Ended(string matchId = "m1") =>
        new()
        {
            EventId = $"{matchId}-ended",
            EventType = "match.MatchEnded",
            AggregateId = matchId,
            OccurredAt = 9000,
            MatchEnded = new MatchEnded { Status = MatchStatus.WhiteWon, EndReason = EndReason.Checkmate },
        };

    [Fact]
    public void EventsForUnknownMatchesProduceNothing()
    {
        AnticheatStreamProcessor processor = new(TestOptions.Default());

        Assert.Same(StreamOutput.None, processor.Handle(Move("ghost", 0, 1000)));
        Assert.Same(StreamOutput.None, processor.Handle(Ended("ghost")));
    }

    [Fact]
    public void MatchEndedEmitsTheAccumulatedObservations()
    {
        AnticheatStreamProcessor processor = new(TestOptions.Default());
        processor.Handle(Created());
        processor.Handle(Move("m1", 0, 2000));
        processor.Handle(Move("m1", 1, 3000));

        StreamOutput output = processor.Handle(Ended());

        Assert.NotNull(output.Finished);
        Assert.Equal("m1", output.Finished.MatchId);
        Assert.Equal(2, output.Finished.Observations.Count);
        Assert.Null(output.Live);

        // State is gone: replaying the end produces nothing.
        Assert.Same(StreamOutput.None, processor.Handle(Ended()));
    }

    [Fact]
    public void SteadyTimingRaisesOneLiveSignalPerPlayer()
    {
        // White's plies (book plies excluded by options BookPlies=2) are
        // machine-steady; LiveMinPlies=3 -> the signal fires on white's third
        // usable ply and never again.
        AnticheatStreamProcessor processor = new(TestOptions.Default());
        processor.Handle(Created());

        List<LiveSignal> signals = [];
        long at = 1000;
        for (int index = 2; index < 12; index++)
        {
            at += 1000;
            StreamOutput output = processor.Handle(Move("m1", index, at));
            if (output.Live is { } live)
            {
                signals.Add(live);
            }
        }

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, s => s.UserId == "w");
        Assert.Contains(signals, s => s.UserId == "b");
        Assert.All(signals, s => Assert.Equal("m1", s.MatchId));
        Assert.All(signals, s => Assert.Equal(1, s.Score));
    }

    [Fact]
    public void BotMovesNeverFeedTheLiveScorer()
    {
        AnticheatStreamProcessor processor = new(TestOptions.Default());
        processor.Handle(Created());

        for (int index = 2; index < 20; index++)
        {
            StreamOutput output = processor.Handle(BotMove("m1", index, 1000 + (index * 1000)));
            Assert.Same(StreamOutput.None, output);
        }
    }

    [Fact]
    public void NonMoveEventsOnlyAdvanceState()
    {
        AnticheatStreamProcessor processor = new(TestOptions.Default());

        Assert.Same(StreamOutput.None, processor.Handle(Created()));
    }

    [Fact]
    public void MatchEndedDropsLiveStateForThatMatchOnly()
    {
        AnticheatStreamProcessor processor = new(TestOptions.Default());
        processor.Handle(Created("m1"));
        processor.Handle(Created("m2"));
        processor.Handle(Move("m1", 2, 2000));
        processor.Handle(Move("m2", 2, 2000));

        StreamOutput ended = processor.Handle(Ended("m1"));
        Assert.NotNull(ended.Finished);

        // m2 keeps accumulating after m1's cleanup.
        StreamOutput m2End = processor.Handle(Ended("m2"));
        Assert.NotNull(m2End.Finished);
        Assert.Single(m2End.Finished.Observations);
    }
}
