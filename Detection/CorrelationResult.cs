namespace MaichessAnticheatService.Detection;

// Engine-correlation verdict for one player in one game: the normalised
// suspicion score, the plies where the played move was the engine's top
// choice (evidence pointers into the match's move list), and how many plies
// actually contributed (book plies and engine-less plies are skipped).
internal sealed record CorrelationResult(double Score, IReadOnlyList<int> SuspiciousPlies, int SampledPlies);
