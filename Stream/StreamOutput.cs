namespace MaichessAnticheatService.Stream;

// What one consumed match event produced: at most one advisory live signal
// and, on MatchEnded, the finished match to analyse. Both null for events that
// only advanced internal state.
internal sealed record StreamOutput(LiveSignal? Live, FinishedMatch? Finished)
{
    public static StreamOutput None { get; } = new(null, null);
}
