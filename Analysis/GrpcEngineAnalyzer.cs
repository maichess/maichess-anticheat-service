using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using Maichess.Engine.V1;
using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Analysis;

// IEngineAnalyzer over engine-service AnalyzePosition: streams progressively
// deeper evaluations and keeps the final (deepest) update's lines. Each call
// is followed by the configured delay — the rate limit that caps the Engine
// load this service generates. Excluded from coverage: requires the live
// engine service.
[ExcludeFromCodeCoverage]
internal sealed class GrpcEngineAnalyzer(
    Bots.BotsClient client,
    AnalysisOptions analysisOptions,
    DetectionOptions detectionOptions) : IEngineAnalyzer
{
    public async Task<IReadOnlyList<EngineLine>> AnalyzeAsync(string fen, CancellationToken ct)
    {
        AnalyzePositionRequest request = new()
        {
            Fen = fen,
            BotId = analysisOptions.BotId,
            LineCount = (uint)detectionOptions.EngineLines,
        };

        AnalysisUpdate? last = null;
        using AsyncServerStreamingCall<AnalysisUpdate> call = client.AnalyzePosition(request, cancellationToken: ct);
        while (await call.ResponseStream.MoveNext(ct))
        {
            last = call.ResponseStream.Current;
        }

        if (analysisOptions.DelayMs > 0)
        {
            await Task.Delay(analysisOptions.DelayMs, ct);
        }

        return last is null
            ? []
            : [.. last.Lines
                .Where(line => line.Moves.Count > 0)
                .Select(line => new EngineLine((int)line.Rank, line.EvaluationCp, line.Moves[0]))];
    }
}
