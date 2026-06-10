using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using MaichessAnticheatService.Analysis;
using MaichessAnticheatService.Cases;
using MaichessAnticheatService.Stream;

namespace MaichessAnticheatService.Kafka;

// Iteration-1 worker: drains the finished-match queue one match at a time
// (sequential by design — together with the engine analyzer's per-call delay
// this caps the Engine load), fetches the game from match-db, analyses every
// human participant, and folds the verdicts into their cases. Excluded from
// coverage: BackgroundService shell over fully tested parts.
[ExcludeFromCodeCoverage]
internal sealed class AnalysisWorker(
    ChannelReader<FinishedMatch> queue,
    IMatchReader matches,
    GameAnalyzer analyzer,
    CaseService cases,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One poisonous match must not stop anti-cheat analysis for every other game")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (FinishedMatch finished in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                MatchGame? game = await matches.GetAsync(finished.MatchId, stoppingToken);
                if (game is null)
                {
                    logger.LogWarning("Finished match {MatchId} not found in match-db", finished.MatchId);
                    continue;
                }

                IReadOnlyList<PlayerVerdict> verdicts =
                    await analyzer.AnalyzeAsync(game, finished.Observations, stoppingToken);
                foreach (PlayerVerdict verdict in verdicts)
                {
                    await cases.RecordVerdictAsync(verdict.UserId, verdict.Verdict, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Post-game analysis failed for match {MatchId}", finished.MatchId);
            }
        }
    }
}
