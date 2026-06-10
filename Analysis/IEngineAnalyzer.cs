using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Analysis;

// Engine view of one position: the top principal variations (rank, evaluation,
// first move). Backed by engine-service AnalyzePosition; the implementation
// owns rate limiting since post-game analysis drives Engine load.
internal interface IEngineAnalyzer
{
    Task<IReadOnlyList<EngineLine>> AnalyzeAsync(string fen, CancellationToken ct);
}
