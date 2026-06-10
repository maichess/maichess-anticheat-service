using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Stream;

// Per-match state accumulated from match.events.v1: the human participants
// (bots are not scored), the timestamp of the previous move (think-time
// anchor), and the per-ply observations gathered so far. This is the timing
// source for both iterations — the durable match doc stores no per-move
// timestamps, so timing only exists here.
internal sealed record MatchTimingState(
    string MatchId,
    string? WhiteUserId,
    string? BlackUserId,
    long LastMoveAtMs,
    IReadOnlyList<PlyObservation> Observations,
    bool Ended);
