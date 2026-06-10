using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Stream;

// A match that just ended, with every timing observation accumulated during
// play. Handed to the post-game analysis worker (iteration 1); the worker
// fetches the authoritative game data from match-db by this id.
internal sealed record FinishedMatch(string MatchId, IReadOnlyList<PlyObservation> Observations);
