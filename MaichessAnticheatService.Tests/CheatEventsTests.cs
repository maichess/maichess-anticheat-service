using Maichess.Events.V1;
using MaichessAnticheatService.Cases;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class CheatEventsTests
{
    private static CaseDocument Case(long sequence = 3) =>
        new("c1", "u1", CaseStatus.Flagged, 0.83, sequence, 1, [], 100, 200, 200);

    [Fact]
    public void PlayerFlaggedEnvelopeCarriesCaseFacts()
    {
        CheatEvent evt = CheatEvents.PlayerFlagged(Case(), "summary text", "evt-1", 5000);

        Assert.Equal("evt-1", evt.EventId);
        Assert.Equal("cheat.PlayerFlagged", evt.EventType);
        Assert.Equal("u1", evt.AggregateId);
        Assert.Equal(3, evt.Sequence);
        Assert.Equal(5000, evt.OccurredAt);
        Assert.Equal("evt-1", evt.CorrelationId);
        Assert.Equal(string.Empty, evt.CausationId);
        Assert.Equal("anticheat-service", evt.Producer);
        Assert.Equal("u1", evt.PlayerFlagged.UserId);
        Assert.Equal("c1", evt.PlayerFlagged.CaseId);
        Assert.Equal(0.83, evt.PlayerFlagged.Score);
        Assert.Equal("summary text", evt.PlayerFlagged.Summary);
        Assert.Equal(Detector.Combined, evt.PlayerFlagged.Detector);
    }

    [Fact]
    public void PlayerUnflaggedEnvelopeCarriesTheActor()
    {
        CheatEvent evt = CheatEvents.PlayerUnflagged(Case(), "dev-9", "false positive", "evt-2", 6000);

        Assert.Equal("cheat.PlayerUnflagged", evt.EventType);
        Assert.Equal("dev-9", evt.PlayerUnflagged.UnflaggedBy);
        Assert.Equal("false positive", evt.PlayerUnflagged.Reason);
        Assert.Equal("c1", evt.PlayerUnflagged.CaseId);
    }

    [Fact]
    public void LiveSuspicionEnvelopeCarriesTheMatchPointer()
    {
        CheatEvent evt = CheatEvents.LiveSuspicionRaised(Case(), "m4", 17, 0.71, "evt-3", 7000);

        Assert.Equal("cheat.LiveSuspicionRaised", evt.EventType);
        Assert.Equal("m4", evt.LiveSuspicionRaised.MatchId);
        Assert.Equal(17, evt.LiveSuspicionRaised.Ply);
        Assert.Equal(0.71, evt.LiveSuspicionRaised.Score);
    }
}
