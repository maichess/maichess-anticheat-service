namespace MaichessAnticheatService.Analysis;

// Engine-load controls for the post-game pass (config section "Analysis").
// BotId selects the engine used for correlation; DelayMs is the pause between
// AnalyzePosition calls — analysis is deliberately slow and sequential so a
// burst of finished games never starves live bot-move calculation.
internal sealed record AnalysisOptions
{
    public string BotId { get; init; } = "blitz";

    public int DelayMs { get; init; } = 500;
}
