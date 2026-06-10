using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// One row of the dev overview (DTO record used only by the excluded endpoint
// adapter). Serialized snake_case per the service JSON options.
[ExcludeFromCodeCoverage]
internal sealed record CaseSummaryResponse(
    string CaseId,
    string UserId,
    string Status,
    double Score,
    int GamesAnalyzed,
    int LiveSignals,
    long CreatedAtMs,
    long UpdatedAtMs,
    long? FlaggedAtMs);
