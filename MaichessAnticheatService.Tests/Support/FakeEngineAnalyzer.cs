using MaichessAnticheatService.Analysis;
using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Tests.Support;

// Deterministic engine stub: returns the configured lines per FEN (empty when
// unconfigured) and records every analysed FEN so tests can assert engine
// load (e.g. that book plies are never analysed).
internal sealed class FakeEngineAnalyzer : IEngineAnalyzer
{
    private readonly Dictionary<string, IReadOnlyList<EngineLine>> lines = [];

    public List<string> AnalyzedFens { get; } = [];

    public void Set(string fen, params EngineLine[] engineLines) => lines[fen] = engineLines;

    public Task<IReadOnlyList<EngineLine>> AnalyzeAsync(string fen, CancellationToken ct)
    {
        AnalyzedFens.Add(fen);
        return Task.FromResult(lines.TryGetValue(fen, out IReadOnlyList<EngineLine>? configured) ? configured : []);
    }
}
