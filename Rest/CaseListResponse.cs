using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// GET /anticheat/cases body (DTO record used only by the excluded endpoint
// adapter).
[ExcludeFromCodeCoverage]
internal sealed record CaseListResponse(IReadOnlyList<CaseSummaryResponse> Cases);
