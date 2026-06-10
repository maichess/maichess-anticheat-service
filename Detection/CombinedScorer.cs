namespace MaichessAnticheatService.Detection;

// Blends the two detectors into a per-game score, folds game scores into a
// per-case (per-user) score over a sliding window, and decides flagging.
//
// The case score is the mean combined score of the last CaseWindow analysed
// games, plus an accuracy-spike bump when the newest game's engine correlation
// jumps far above the player's own earlier average — a sudden jump in accuracy
// is itself a signal even when the absolute level is borderline.
internal static class CombinedScorer
{
    public static double GameScore(double correlation, double statistical, DetectionOptions options) =>
        Math.Clamp(
            (options.GameCorrelationWeight * correlation) + (options.GameStatisticalWeight * statistical),
            0,
            1);

    public static double CaseScore(IReadOnlyList<GameVerdict> verdicts, DetectionOptions options)
    {
        if (verdicts.Count == 0)
        {
            return 0;
        }

        List<GameVerdict> recent = [.. verdicts.Skip(Math.Max(0, verdicts.Count - options.CaseWindow))];
        double score = recent.Average(v => v.Score);

        if (recent.Count >= 2)
        {
            double priorCorrelation = recent.Take(recent.Count - 1).Average(v => v.Correlation);
            if (recent[^1].Correlation - priorCorrelation >= options.AccuracySpikeDelta)
            {
                score = Math.Clamp(score + options.AccuracySpikeBump, 0, 1);
            }
        }

        return score;
    }

    public static bool ShouldFlag(double caseScore, int gamesAnalyzed, DetectionOptions options) =>
        gamesAnalyzed >= options.MinGames && caseScore >= options.FlagThreshold;
}
