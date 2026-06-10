using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// One analysed game in the case detail (DTO record used only by the excluded
// endpoint adapter). suspicious_plies are evidence pointers into match-db.
[ExcludeFromCodeCoverage]
internal sealed record CaseGameResponse(
    string MatchId,
    double Score,
    double Correlation,
    double Statistical,
    IReadOnlyList<int> SuspiciousPlies,
    long AnalyzedAtMs);
