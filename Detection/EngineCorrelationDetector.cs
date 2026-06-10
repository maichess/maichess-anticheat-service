namespace MaichessAnticheatService.Detection;

// Primary detector: how closely a player's moves track the engine's choices.
// Each non-book ply contributes a weighted match value (top-1 = 1.0, top-2 =
// 0.5, top-3 = 0.25, otherwise 0); pre-moved plies contribute at reduced
// weight. The weighted match rate is then normalised against the honest-play
// baseline: only the excess above CorrelationBaseline scores, scaled to 0..1.
internal static class EngineCorrelationDetector
{
    public static CorrelationResult Score(IReadOnlyList<AnalyzedPly> plies, DetectionOptions options)
    {
        double numerator = 0;
        double denominator = 0;
        int sampled = 0;
        List<int> suspicious = [];

        foreach (AnalyzedPly ply in plies)
        {
            if (ply.Observation.Ply < options.BookPlies || ply.Lines.Count == 0)
            {
                continue;
            }

            double weight = ply.Observation.Premove ? options.PremoveCorrelationWeight : 1.0;
            double match = MatchValue(ply);
            numerator += weight * match;
            denominator += weight;
            sampled++;

            if (match >= 1.0)
            {
                suspicious.Add(ply.Observation.Ply);
            }
        }

        if (denominator == 0)
        {
            return new CorrelationResult(0, [], 0);
        }

        double rate = numerator / denominator;
        double score = Math.Clamp((rate - options.CorrelationBaseline) / (1 - options.CorrelationBaseline), 0, 1);
        return new CorrelationResult(score, suspicious, sampled);
    }

    private static double MatchValue(AnalyzedPly ply)
    {
        foreach (EngineLine line in ply.Lines)
        {
            if (line.MoveUci == ply.Observation.MoveUci)
            {
                return line.Rank switch
                {
                    1 => 1.0,
                    2 => 0.5,
                    _ => 0.25,
                };
            }
        }

        return 0;
    }
}
