using Maichess.Events.V1;
using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Stream;

// The deterministic core of the stream side: holds the per-match timing states
// and per-(match, player) live-scorer states, routes each consumed MatchEvent
// through the pure folds, and reports what side effects the shell should
// perform (emit an advisory signal / analyse a finished match). Everything
// impure (Kafka, gRPC, persistence) stays in the consumer shell.
internal sealed class AnticheatStreamProcessor(DetectionOptions options)
{
    private readonly Dictionary<string, MatchTimingState> matches = [];
    private readonly Dictionary<(string MatchId, string UserId), LivePlayerState> live = [];

    public StreamOutput Handle(MatchEvent evt, bool premove = false)
    {
        matches.TryGetValue(evt.AggregateId, out MatchTimingState? state);
        MatchTimingState? next = MatchStreamProjection.Apply(state, evt, premove);
        if (next is null)
        {
            return StreamOutput.None;
        }

        if (next.Ended)
        {
            matches.Remove(evt.AggregateId);
            foreach ((string MatchId, string UserId) key in live.Keys.Where(k => k.MatchId == evt.AggregateId).ToList())
            {
                live.Remove(key);
            }

            return new StreamOutput(null, new FinishedMatch(next.MatchId, next.Observations));
        }

        matches[evt.AggregateId] = next;

        LiveSignal? signal = null;

        // A MoveApplied can only reach this point with a seeded prior state (an
        // unseeded one returns above), so state is non-null inside the check.
        if (evt.PayloadCase == MatchEvent.PayloadOneofCase.MoveApplied
            && next.Observations.Count > state!.Observations.Count)
        {
            PlyObservation observation = next.Observations[^1];
            (string, string) key = (evt.AggregateId, observation.UserId);
            LivePlayerState playerState = live.GetValueOrDefault(key, LivePlayerState.Empty);
            (LivePlayerState updated, double? advisory) = LiveScorer.Observe(playerState, observation, options);
            live[key] = updated;
            if (advisory is { } score)
            {
                signal = new LiveSignal(observation.UserId, evt.AggregateId, observation.Ply, score);
            }
        }

        return signal is null ? StreamOutput.None : new StreamOutput(signal, null);
    }
}
