namespace MaichessAnticheatService.Analysis;

// Reads a finished game from match-db by id (via its DatabaseService
// instance). Null when the match does not exist.
internal interface IMatchReader
{
    Task<MatchGame?> GetAsync(string matchId, CancellationToken ct);
}
