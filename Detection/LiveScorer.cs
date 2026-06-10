namespace MaichessAnticheatService.Detection;

// Iteration-2 incremental scorer: folds one ply at a time into a per-(match,
// player) running state and raises a single advisory suspicion signal mid-game
// when enough usable plies show machine-steady timing. Advisory only — the
// authoritative verdict always comes from the post-game pass; consumers of the
// resulting LiveSuspicionRaised event must never set the durable flag from it.
//
// Pre-move handling mirrors StatisticalDetector: pre-moved, book, and
// unknown-timing plies never enter the sample.
internal static class LiveScorer
{
    public static (LivePlayerState State, double? Advisory) Observe(
        LivePlayerState state,
        PlyObservation observation,
        DetectionOptions options)
    {
        if (observation.Premove || observation.Ply < options.BookPlies || observation.ThinkTimeMs <= 0)
        {
            return (state, null);
        }

        // Welford's online update keeps the fold O(1) per ply.
        int samples = state.Samples + 1;
        double delta = observation.ThinkTimeMs - state.Mean;
        double mean = state.Mean + (delta / samples);
        double m2 = state.M2 + (delta * (observation.ThinkTimeMs - mean));
        LivePlayerState next = state with { Samples = samples, Mean = mean, M2 = m2 };

        if (next.Raised || next.Samples < options.LiveMinPlies)
        {
            return (next, null);
        }

        double cv = Math.Sqrt(next.M2 / next.Samples) / next.Mean;
        double score = Math.Clamp((options.TimingCvThreshold - cv) / options.TimingCvThreshold, 0, 1);
        if (score < options.LiveAdvisoryThreshold)
        {
            return (next, null);
        }

        return (next with { Raised = true }, score);
    }
}
