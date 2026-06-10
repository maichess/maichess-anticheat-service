namespace MaichessAnticheatService.Detection;

// One ply as the detector sees it. ThinkTimeMs is derived from the stream
// (MoveApplied.applied_at_ms deltas); a non-positive value means "timing
// unknown" (e.g. the stream state was cold) and the ply is exempt from timing
// features. Premove marks a client-asserted pre-move: legitimate near-zero
// think time — excluded from timing analysis and downweighted in correlation.
internal sealed record PlyObservation(
    int Ply,
    string UserId,
    string MoveUci,
    long ThinkTimeMs,
    bool Premove);
