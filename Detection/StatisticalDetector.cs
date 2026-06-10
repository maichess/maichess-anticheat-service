namespace MaichessAnticheatService.Detection;

// Statistical detector: think-time consistency. Engine-assisted players show
// machine-steady move times (low coefficient of variation); honest humans vary
// a lot between forced replies and genuine thinks.
//
// Pre-move exclusion is the load-bearing rule here: a pre-moved ply has
// legitimate near-zero think time, so it is dropped from the sample entirely —
// otherwise heavy pre-movers would look like machines. Book plies and plies
// with unknown timing (ThinkTimeMs <= 0, e.g. cold stream state) are dropped
// too. Below TimingMinSamples usable plies the detector abstains (score 0).
internal static class StatisticalDetector
{
    public static double Score(IReadOnlyList<PlyObservation> observations, DetectionOptions options)
    {
        List<double> samples = [];
        foreach (PlyObservation observation in observations)
        {
            if (!observation.Premove && observation.Ply >= options.BookPlies && observation.ThinkTimeMs > 0)
            {
                samples.Add(observation.ThinkTimeMs);
            }
        }

        if (samples.Count < options.TimingMinSamples)
        {
            return 0;
        }

        double mean = samples.Average();
        double variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Count;
        double cv = Math.Sqrt(variance) / mean;

        return Math.Clamp((options.TimingCvThreshold - cv) / options.TimingCvThreshold, 0, 1);
    }
}
