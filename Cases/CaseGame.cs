namespace MaichessAnticheatService.Cases;

// One analysed game recorded on a case: scores plus evidence pointers
// (match_id + ply indexes) into match-db. No game data is copied.
internal sealed record CaseGame(
    string MatchId,
    double Score,
    double Correlation,
    double Statistical,
    IReadOnlyList<int> SuspiciousPlies,
    long AnalyzedAtMs);
