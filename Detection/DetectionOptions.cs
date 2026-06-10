namespace MaichessAnticheatService.Detection;

// All detector thresholds in one bindable bag (config section "Detection").
// Defaults are deliberate, documented heuristics — tune in config, not code:
//  - BookPlies: opening-book plies carry no signal (theory moves match engines).
//  - CorrelationBaseline: weighted engine-match rate a strong honest player
//    reaches; only the excess above it counts as suspicion.
//  - PremoveCorrelationWeight: pre-moved plies still count toward correlation,
//    but at reduced weight (they are usually forced/obvious replies).
//  - TimingCvThreshold: a coefficient of variation of think times below this is
//    "machine-steady"; honest humans vary much more.
//  - FlagThreshold/MinGames/CaseWindow: a case is flagged when the mean combined
//    score over the last CaseWindow games crosses FlagThreshold with at least
//    MinGames analysed — one strong game never flags on its own.
internal sealed record DetectionOptions
{
    public int BookPlies { get; init; } = 8;

    public double CorrelationBaseline { get; init; } = 0.55;

    public double PremoveCorrelationWeight { get; init; } = 0.5;

    public int TimingMinSamples { get; init; } = 10;

    public double TimingCvThreshold { get; init; } = 0.45;

    public double GameCorrelationWeight { get; init; } = 0.7;

    public double GameStatisticalWeight { get; init; } = 0.3;

    public double AccuracySpikeDelta { get; init; } = 0.25;

    public double AccuracySpikeBump { get; init; } = 0.1;

    public double FlagThreshold { get; init; } = 0.7;

    public int MinGames { get; init; } = 3;

    public int CaseWindow { get; init; } = 10;

    public int LiveMinPlies { get; init; } = 12;

    public double LiveAdvisoryThreshold { get; init; } = 0.7;

    public int EngineLines { get; init; } = 3;
}
