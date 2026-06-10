using Maichess.Events.V1;
using MaichessAnticheatService.Cases;
using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class CaseServiceTests
{
    private readonly FakeAnticheatStore store = new();
    private readonly FakeCheatEventProducer producer = new();
    private int idCounter;
    private long clock = 1000;

    private CaseService Service(DetectionOptions? options = null) =>
        new(store, producer, options ?? TestOptions.Default(), () => $"id-{++idCounter}", () => clock);

    private static GameVerdict Verdict(string matchId, double score, double correlation = 0.5) =>
        new(matchId, score, correlation, 0.3, [10, 14], 999);

    [Fact]
    public async Task FirstVerdictCreatesAnOpenCase()
    {
        await Service().RecordVerdictAsync("u1", Verdict("m1", 0.4), CancellationToken.None);

        CaseDocument created = Assert.Single(store.Cases.Values);
        Assert.Equal("u1", created.UserId);
        Assert.Equal(CaseStatus.Open, created.Status);
        Assert.Equal(0.4, created.Score, 10);
        Assert.Single(created.Games);
        Assert.Null(created.FlaggedAtMs);
        Assert.Empty(producer.Produced);
        Assert.Empty(store.Audit);
    }

    [Fact]
    public async Task ReanalysingTheSameMatchIsIdempotent()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.9), CancellationToken.None);
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.9), CancellationToken.None);

        CaseDocument current = Assert.Single(store.Cases.Values);
        Assert.Single(current.Games);
    }

    [Fact]
    public async Task CrossingTheThresholdFlagsAuditsAndEmits()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.8), CancellationToken.None);
        clock = 2000;
        await service.RecordVerdictAsync("u1", Verdict("m2", 0.8), CancellationToken.None);

        CaseDocument flagged = Assert.Single(store.Cases.Values);
        Assert.Equal(CaseStatus.Flagged, flagged.Status);
        Assert.Equal(2000, flagged.FlaggedAtMs);
        Assert.Equal(1, flagged.Sequence);

        AuditEntry audit = Assert.Single(store.Audit);
        Assert.Equal(AuditEntry.ActionFlagged, audit.Action);
        Assert.Equal(AuditEntry.SystemActor, audit.Actor);
        Assert.Contains("over 2 games", audit.Reason, StringComparison.Ordinal);

        CheatEvent produced = Assert.Single(producer.Produced);
        Assert.Equal("cheat.PlayerFlagged", produced.EventType);
        Assert.Equal("u1", produced.AggregateId);
        Assert.Equal(flagged.Id, produced.PlayerFlagged.CaseId);
        Assert.Equal(Detector.Combined, produced.PlayerFlagged.Detector);
    }

    [Fact]
    public async Task AnAlreadyFlaggedCaseOnlyUpdatesItsScore()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.8), CancellationToken.None);
        await service.RecordVerdictAsync("u1", Verdict("m2", 0.8), CancellationToken.None);
        producer.Produced.Clear();
        store.Audit.Clear();

        await service.RecordVerdictAsync("u1", Verdict("m3", 0.9), CancellationToken.None);

        CaseDocument current = Assert.Single(store.Cases.Values);
        Assert.Equal(CaseStatus.Flagged, current.Status);
        Assert.Equal(3, current.Games.Count);
        Assert.Empty(producer.Produced);
        Assert.Empty(store.Audit);
    }

    [Fact]
    public async Task LiveSuspicionAuditsAndEmitsWithoutFlagging()
    {
        await Service().RecordLiveSuspicionAsync("u1", "m9", 22, 0.74, CancellationToken.None);

        CaseDocument current = Assert.Single(store.Cases.Values);
        Assert.Equal(CaseStatus.Open, current.Status);
        Assert.Equal(1, current.LiveSignals);
        Assert.Equal(1, current.Sequence);

        AuditEntry audit = Assert.Single(store.Audit);
        Assert.Equal(AuditEntry.ActionLiveSuspicion, audit.Action);
        Assert.Contains("m9", audit.Reason, StringComparison.Ordinal);
        Assert.Contains("22", audit.Reason, StringComparison.Ordinal);

        CheatEvent produced = Assert.Single(producer.Produced);
        Assert.Equal("cheat.LiveSuspicionRaised", produced.EventType);
        Assert.Equal("m9", produced.LiveSuspicionRaised.MatchId);
        Assert.Equal(22, produced.LiveSuspicionRaised.Ply);
    }

    [Fact]
    public async Task UnflagClearsAuditsAndEmits()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.8), CancellationToken.None);
        await service.RecordVerdictAsync("u1", Verdict("m2", 0.8), CancellationToken.None);
        string caseId = store.Cases.Values.Single().Id;
        producer.Produced.Clear();
        store.Audit.Clear();
        clock = 5000;

        UnflagResult result = await service.UnflagAsync(caseId, "dev-7", "false positive", CancellationToken.None);

        Assert.Equal(UnflagResult.Cleared, result);
        CaseDocument cleared = store.Cases[caseId];
        Assert.Equal(CaseStatus.Cleared, cleared.Status);
        Assert.Null(cleared.FlaggedAtMs);
        Assert.Equal(5000, cleared.UpdatedAtMs);

        AuditEntry audit = Assert.Single(store.Audit);
        Assert.Equal(AuditEntry.ActionUnflagged, audit.Action);
        Assert.Equal("dev-7", audit.Actor);
        Assert.Equal("false positive", audit.Reason);

        CheatEvent produced = Assert.Single(producer.Produced);
        Assert.Equal("cheat.PlayerUnflagged", produced.EventType);
        Assert.Equal("dev-7", produced.PlayerUnflagged.UnflaggedBy);
    }

    [Fact]
    public async Task UnflagUnknownCaseReturnsNotFound()
    {
        Assert.Equal(
            UnflagResult.NotFound,
            await Service().UnflagAsync("ghost", "dev-7", "r", CancellationToken.None));
    }

    [Fact]
    public async Task UnflagAnUnflaggedCaseConflicts()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.1), CancellationToken.None);
        string caseId = store.Cases.Values.Single().Id;

        Assert.Equal(
            UnflagResult.NotFlagged,
            await service.UnflagAsync(caseId, "dev-7", "r", CancellationToken.None));
        Assert.Empty(producer.Produced);
    }

    [Fact]
    public async Task AClearedCaseCanBeReflaggedByNewGames()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.8), CancellationToken.None);
        await service.RecordVerdictAsync("u1", Verdict("m2", 0.8), CancellationToken.None);
        string caseId = store.Cases.Values.Single().Id;
        await service.UnflagAsync(caseId, "dev-7", "benefit of the doubt", CancellationToken.None);
        producer.Produced.Clear();

        await service.RecordVerdictAsync("u1", Verdict("m3", 0.9), CancellationToken.None);

        Assert.Equal(CaseStatus.Flagged, store.Cases[caseId].Status);
        CheatEvent produced = Assert.Single(producer.Produced);
        Assert.Equal("cheat.PlayerFlagged", produced.EventType);
    }

    [Fact]
    public async Task ReadPassthroughsDelegateToTheStore()
    {
        CaseService service = Service();
        await service.RecordVerdictAsync("u1", Verdict("m1", 0.4), CancellationToken.None);
        string caseId = store.Cases.Values.Single().Id;

        Assert.Single(await service.ListCasesAsync(null, 10, 0, CancellationToken.None));
        Assert.Empty(await service.ListCasesAsync(CaseStatus.Flagged, 10, 0, CancellationToken.None));
        Assert.NotNull(await service.GetCaseAsync(caseId, CancellationToken.None));
        Assert.Empty(await service.ListAuditAsync(caseId, CancellationToken.None));
    }
}
