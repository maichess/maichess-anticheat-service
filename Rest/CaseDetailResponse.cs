using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// GET /anticheat/cases/{id} body (DTO record used only by the excluded
// endpoint adapter).
[ExcludeFromCodeCoverage]
internal sealed record CaseDetailResponse(
    string CaseId,
    string UserId,
    string Status,
    double Score,
    long? FlaggedAtMs,
    IReadOnlyList<CaseGameResponse> Games,
    IReadOnlyList<AuditEntryResponse> Audit);
