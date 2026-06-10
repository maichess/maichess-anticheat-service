using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Analysis;

// Iteration-1 post-game pass: replays a finished game through the engine and
// scores each human participant with the combined detector.
//
// The move list / FENs come from the match-db document (authoritative); the
// per-ply timing comes from the stream observations accumulated during play.
// When an observation for a ply is missing (cold stream state — e.g. the
// consumer restarted mid-game), a timing-less placeholder keeps the ply in the
// correlation sample while the timing detector ignores it, so the pass
// degrades to correlation-only instead of failing.
internal sealed class GameAnalyzer(IEngineAnalyzer engine, DetectionOptions options, Func<long> nowMs)
{
    public async Task<IReadOnlyList<PlayerVerdict>> AnalyzeAsync(
        MatchGame game,
        IReadOnlyList<PlyObservation> observations,
        CancellationToken ct)
    {
        var byPly = observations.ToDictionary(o => o.Ply);
        Dictionary<string, List<AnalyzedPly>> perPlayer = [];

        for (int ply = 0; ply < game.Moves.Count && ply < game.FenHistory.Count; ply++)
        {
            string? moverId = ply % 2 == 0 ? game.WhiteUserId : game.BlackUserId;
            if (moverId is null)
            {
                continue;
            }

            PlyObservation observation = byPly.TryGetValue(ply, out PlyObservation? seen)
                ? seen
                : new PlyObservation(ply, moverId, game.Moves[ply], 0, false);

            IReadOnlyList<EngineLine> lines = ply < options.BookPlies
                ? []
                : await engine.AnalyzeAsync(game.FenHistory[ply], ct);

            if (!perPlayer.TryGetValue(moverId, out List<AnalyzedPly>? plies))
            {
                plies = [];
                perPlayer[moverId] = plies;
            }

            plies.Add(new AnalyzedPly(observation, lines));
        }

        List<PlayerVerdict> verdicts = [];
        foreach ((string userId, List<AnalyzedPly> plies) in perPlayer)
        {
            CorrelationResult correlation = EngineCorrelationDetector.Score(plies, options);
            double statistical = StatisticalDetector.Score([.. plies.Select(p => p.Observation)], options);
            double score = CombinedScorer.GameScore(correlation.Score, statistical, options);
            verdicts.Add(new PlayerVerdict(
                userId,
                new GameVerdict(game.MatchId, score, correlation.Score, statistical, correlation.SuspiciousPlies, nowMs())));
        }

        return verdicts;
    }
}
