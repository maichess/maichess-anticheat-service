namespace MaichessAnticheatService.Cases;

// Immutable audit trail row ("audit" collection): every flag, unflag, and
// live-suspicion signal, with who caused it ("system" or a dev's user id).
internal sealed record AuditEntry(
    string Id,
    string CaseId,
    string UserId,
    string Action,
    string Actor,
    string Reason,
    double Score,
    long AtMs)
{
    public const string ActionFlagged = "flagged";
    public const string ActionUnflagged = "unflagged";
    public const string ActionLiveSuspicion = "live_suspicion";
    public const string SystemActor = "system";
}
