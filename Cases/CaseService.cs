using System.Globalization;
using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Cases;

// Case lifecycle: folds game verdicts and live signals into the per-user case,
// decides flag transitions, writes the audit trail, and emits the matching
// cheat events. Pure-deterministic apart from the injected id/clock factories
// (the MatchProjector convention), so every branch is unit-testable.
internal sealed class CaseService(
    IAnticheatStore store,
    ICheatEventProducer producer,
    DetectionOptions options,
    Func<string> newId,
    Func<long> nowMs)
{
    public async Task RecordVerdictAsync(string userId, GameVerdict verdict, CancellationToken ct)
    {
        CaseDocument caseDocument = await GetOrCreateAsync(userId, ct);
        if (caseDocument.Games.Any(g => g.MatchId == verdict.MatchId))
        {
            // Replay/redelivery of an already-analysed match must not skew the
            // score or re-trigger a flag.
            return;
        }

        List<CaseGame> games =
        [
            .. caseDocument.Games,
            new CaseGame(
                verdict.MatchId,
                verdict.Score,
                verdict.Correlation,
                verdict.Statistical,
                verdict.SuspiciousPlies,
                verdict.AnalyzedAtMs),
        ];
        double score = CombinedScorer.CaseScore(
            [.. games.Select(g => new GameVerdict(g.MatchId, g.Score, g.Correlation, g.Statistical, g.SuspiciousPlies, g.AnalyzedAtMs))],
            options);

        long now = nowMs();
        caseDocument = caseDocument with { Games = games, Score = score, UpdatedAtMs = now };

        if (caseDocument.Status != CaseStatus.Flagged && CombinedScorer.ShouldFlag(score, games.Count, options))
        {
            string summary = string.Create(
                CultureInfo.InvariantCulture,
                $"combined score {score:0.00} over {games.Count} games");
            caseDocument = caseDocument with
            {
                Status = CaseStatus.Flagged,
                FlaggedAtMs = now,
                Sequence = caseDocument.Sequence + 1,
            };
            await store.UpsertCaseAsync(caseDocument, ct);
            await store.AppendAuditAsync(
                new AuditEntry(newId(), caseDocument.Id, userId, AuditEntry.ActionFlagged, AuditEntry.SystemActor, summary, score, now),
                ct);
            await producer.ProduceAsync(CheatEvents.PlayerFlagged(caseDocument, summary, newId(), now), ct);
            return;
        }

        await store.UpsertCaseAsync(caseDocument, ct);
    }

    public async Task RecordLiveSuspicionAsync(string userId, string matchId, int ply, double score, CancellationToken ct)
    {
        CaseDocument caseDocument = await GetOrCreateAsync(userId, ct);
        long now = nowMs();
        caseDocument = caseDocument with
        {
            LiveSignals = caseDocument.LiveSignals + 1,
            UpdatedAtMs = now,
            Sequence = caseDocument.Sequence + 1,
        };
        string reason = string.Create(
            CultureInfo.InvariantCulture,
            $"match {matchId} ply {ply} score {score:0.00}");

        await store.UpsertCaseAsync(caseDocument, ct);
        await store.AppendAuditAsync(
            new AuditEntry(newId(), caseDocument.Id, userId, AuditEntry.ActionLiveSuspicion, AuditEntry.SystemActor, reason, score, now),
            ct);
        await producer.ProduceAsync(
            CheatEvents.LiveSuspicionRaised(caseDocument, matchId, ply, score, newId(), now),
            ct);
    }

    public async Task<UnflagResult> UnflagAsync(string caseId, string devUserId, string reason, CancellationToken ct)
    {
        CaseDocument? caseDocument = await store.GetCaseAsync(caseId, ct);
        if (caseDocument is null)
        {
            return UnflagResult.NotFound;
        }

        if (caseDocument.Status != CaseStatus.Flagged)
        {
            return UnflagResult.NotFlagged;
        }

        long now = nowMs();
        caseDocument = caseDocument with
        {
            Status = CaseStatus.Cleared,
            FlaggedAtMs = null,
            UpdatedAtMs = now,
            Sequence = caseDocument.Sequence + 1,
        };

        await store.UpsertCaseAsync(caseDocument, ct);
        await store.AppendAuditAsync(
            new AuditEntry(newId(), caseDocument.Id, caseDocument.UserId, AuditEntry.ActionUnflagged, devUserId, reason, caseDocument.Score, now),
            ct);
        await producer.ProduceAsync(
            CheatEvents.PlayerUnflagged(caseDocument, devUserId, reason, newId(), now),
            ct);
        return UnflagResult.Cleared;
    }

    public Task<IReadOnlyList<CaseDocument>> ListCasesAsync(CaseStatus? status, int limit, int offset, CancellationToken ct) =>
        store.ListCasesAsync(status, limit, offset, ct);

    public Task<CaseDocument?> GetCaseAsync(string caseId, CancellationToken ct) =>
        store.GetCaseAsync(caseId, ct);

    public Task<IReadOnlyList<AuditEntry>> ListAuditAsync(string caseId, CancellationToken ct) =>
        store.ListAuditAsync(caseId, ct);

    private async Task<CaseDocument> GetOrCreateAsync(string userId, CancellationToken ct)
    {
        CaseDocument? existing = await store.GetCaseByUserAsync(userId, ct);
        if (existing is not null)
        {
            return existing;
        }

        long now = nowMs();
        return new CaseDocument(newId(), userId, CaseStatus.Open, 0, 0, 0, [], now, now, null);
    }
}
