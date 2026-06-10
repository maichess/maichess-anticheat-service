using MaichessAnticheatService.Cases;

namespace MaichessAnticheatService.Tests.Support;

// In-memory IAnticheatStore mirroring the database semantics the case service
// relies on (upsert by id, lookup by user, audit append).
internal sealed class FakeAnticheatStore : IAnticheatStore
{
    public Dictionary<string, CaseDocument> Cases { get; } = [];

    public List<AuditEntry> Audit { get; } = [];

    public Task<CaseDocument?> GetCaseByUserAsync(string userId, CancellationToken ct) =>
        Task.FromResult(Cases.Values.FirstOrDefault(c => c.UserId == userId));

    public Task<CaseDocument?> GetCaseAsync(string caseId, CancellationToken ct) =>
        Task.FromResult(Cases.GetValueOrDefault(caseId));

    public Task<IReadOnlyList<CaseDocument>> ListCasesAsync(
        CaseStatus? status,
        int limit,
        int offset,
        CancellationToken ct)
    {
        IReadOnlyList<CaseDocument> result =
        [
            .. Cases.Values
                .Where(c => status is null || c.Status == status)
                .OrderByDescending(c => c.UpdatedAtMs)
                .Skip(offset)
                .Take(limit),
        ];
        return Task.FromResult(result);
    }

    public Task UpsertCaseAsync(CaseDocument caseDocument, CancellationToken ct)
    {
        Cases[caseDocument.Id] = caseDocument;
        return Task.CompletedTask;
    }

    public Task AppendAuditAsync(AuditEntry entry, CancellationToken ct)
    {
        Audit.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> ListAuditAsync(string caseId, CancellationToken ct)
    {
        IReadOnlyList<AuditEntry> result = [.. Audit.Where(a => a.CaseId == caseId).OrderBy(a => a.AtMs)];
        return Task.FromResult(result);
    }
}
