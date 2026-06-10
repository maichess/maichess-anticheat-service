namespace MaichessAnticheatService.Detection;

// A player's ply paired with the engine's view of the position it was played
// from. Input to the correlation detector.
internal sealed record AnalyzedPly(PlyObservation Observation, IReadOnlyList<EngineLine> Lines);
