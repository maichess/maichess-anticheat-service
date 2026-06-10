namespace MaichessAnticheatService.Detection;

// Combined per-game verdict for one player: the blended score, both component
// scores, and evidence pointers (ply indexes into the match's move list —
// references into match-db, never copies of game data).
internal sealed record GameVerdict(
    string MatchId,
    double Score,
    double Correlation,
    double Statistical,
    IReadOnlyList<int> SuspiciousPlies,
    long AnalyzedAtMs);
