using Maichess.Events.V1;
using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Stream;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class MatchStreamProjectionTests
{
    private static MatchEvent Created(long occurredAt = 1000, string? whiteUserId = "w", string? blackBotId = null) =>
        new()
        {
            EventId = "e1",
            EventType = "match.MatchCreated",
            AggregateId = "m1",
            Sequence = 1,
            OccurredAt = occurredAt,
            MatchCreated = new MatchCreated
            {
                White = whiteUserId is null ? new Player { BotId = "bot" } : new Player { UserId = whiteUserId },
                Black = blackBotId is null ? new Player { UserId = "b" } : new Player { BotId = blackBotId },
            },
        };

    private static MatchEvent Move(int index, long appliedAtMs, bool byBot = false, string move = "e2e4") =>
        new()
        {
            EventId = $"e{index + 2}",
            EventType = "match.MoveApplied",
            AggregateId = "m1",
            Sequence = index + 2,
            OccurredAt = appliedAtMs,
            MoveApplied = new MoveApplied
            {
                MoveUci = move,
                ResultingFen = "fen",
                Index = index,
                Player = byBot ? new Player { BotId = "bot" } : new Player { UserId = index % 2 == 0 ? "w" : "b" },
                AppliedAtMs = appliedAtMs,
            },
        };

    private static MatchEvent Ended() =>
        new()
        {
            EventId = "end",
            EventType = "match.MatchEnded",
            AggregateId = "m1",
            OccurredAt = 99,
            MatchEnded = new MatchEnded { Status = MatchStatus.WhiteWon, EndReason = EndReason.Checkmate },
        };

    [Fact]
    public void MatchCreatedSeedsPlayersAndAnchor()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created(occurredAt: 5000));

        Assert.NotNull(state);
        Assert.Equal("m1", state.MatchId);
        Assert.Equal("w", state.WhiteUserId);
        Assert.Equal("b", state.BlackUserId);
        Assert.Equal(5000, state.LastMoveAtMs);
        Assert.Empty(state.Observations);
        Assert.False(state.Ended);
    }

    [Fact]
    public void BotSidesSeedAsNull()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created(whiteUserId: null, blackBotId: "bot"));

        Assert.NotNull(state);
        Assert.Null(state.WhiteUserId);
        Assert.Null(state.BlackUserId);
    }

    [Fact]
    public void HumanMoveAppendsObservationWithThinkTime()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created(occurredAt: 1000));
        state = MatchStreamProjection.Apply(state, Move(0, 4000));

        Assert.NotNull(state);
        PlyObservation observation = Assert.Single(state.Observations);
        Assert.Equal(0, observation.Ply);
        Assert.Equal("w", observation.UserId);
        Assert.Equal("e2e4", observation.MoveUci);
        Assert.Equal(3000, observation.ThinkTimeMs);
        Assert.False(observation.Premove);
        Assert.Equal(4000, state.LastMoveAtMs);
    }

    [Fact]
    public void PremoveBitIsCarriedOntoTheObservation()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created());
        state = MatchStreamProjection.Apply(state, Move(0, 2000), premove: true);

        Assert.NotNull(state);
        Assert.True(state.Observations[0].Premove);
    }

    [Fact]
    public void BotMoveAdvancesTheAnchorWithoutObserving()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created(occurredAt: 1000));
        state = MatchStreamProjection.Apply(state, Move(0, 3000, byBot: true));

        Assert.NotNull(state);
        Assert.Empty(state.Observations);
        Assert.Equal(3000, state.LastMoveAtMs);
    }

    [Fact]
    public void OutOfOrderTimestampClampsThinkTimeToZero()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created(occurredAt: 5000));
        state = MatchStreamProjection.Apply(state, Move(0, 4000));

        Assert.NotNull(state);
        Assert.Equal(0, state.Observations[0].ThinkTimeMs);
    }

    [Fact]
    public void MatchEndedMarksTheStateEnded()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created());
        state = MatchStreamProjection.Apply(state, Ended());

        Assert.NotNull(state);
        Assert.True(state.Ended);
    }

    [Fact]
    public void EventsForUnseededMatchesLeaveTheStateNull()
    {
        Assert.Null(MatchStreamProjection.Apply(null, Move(0, 1000)));
        Assert.Null(MatchStreamProjection.Apply(null, Ended()));
    }

    [Fact]
    public void IrrelevantPayloadsLeaveTheStateUnchanged()
    {
        MatchTimingState? state = MatchStreamProjection.Apply(null, Created());
        MatchEvent submitted = new()
        {
            EventId = "s1",
            EventType = "match.MoveSubmitted",
            AggregateId = "m1",
            OccurredAt = 2000,
            MoveSubmitted = new MoveSubmitted { MoveUci = "e2e4", By = new Player { UserId = "w" } },
        };

        Assert.Same(state, MatchStreamProjection.Apply(state, submitted));
    }
}
