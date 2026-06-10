namespace MaichessAnticheatService.Cases;

// Persistence seam over the anticheat-db DatabaseService instance.
internal interface IAnticheatStore
{
    Task<CaseDocument?> GetCaseByUserAsync(string userId, CancellationToken ct);

    Task<CaseDocument?> GetCaseAsync(string caseId, CancellationToken ct);

    Task<IReadOnlyList<CaseDocument>> ListCasesAsync(CaseStatus? status, int limit, int offset, CancellationToken ct);

    Task UpsertCaseAsync(CaseDocument caseDocument, CancellationToken ct);

    Task AppendAuditAsync(AuditEntry entry, CancellationToken ct);

    Task<IReadOnlyList<AuditEntry>> ListAuditAsync(string caseId, CancellationToken ct);
}
