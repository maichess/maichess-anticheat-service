namespace MaichessAnticheatService.Stream;

// An advisory in-game suspicion signal produced by the live scorer (iteration
// 2). Surfaced as a LiveSuspicionRaised cheat event + an audit entry on the
// player's case; never a durable flag by itself.
internal sealed record LiveSignal(string UserId, string MatchId, int Ply, double Score);
