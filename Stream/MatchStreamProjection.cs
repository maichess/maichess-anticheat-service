using Maichess.Events.V1;
using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Stream;

// Pure fold of match.events.v1 into MatchTimingState. MatchCreated seeds the
// state (players + creation timestamp as the first think-time anchor); each
// MoveApplied appends a PlyObservation for a human mover (think time =
// applied_at_ms minus the previous anchor) and advances the anchor for either
// mover; MatchEnded marks the state finished. Events for unseeded matches
// (consumer joined mid-game) and irrelevant payloads leave the state as-is.
//
// The premove parameter carries MoveApplied's premove bit. It is passed
// explicitly because the service compiles against PlatformProtos 0.6.0, which
// predates the field; once the consumer is on >= 0.7.0 the shell passes
// MoveApplied.Premove (see CONTRACT_NOTES.md).
internal static class MatchStreamProjection
{
    public static MatchTimingState? Apply(MatchTimingState? state, MatchEvent evt, bool premove = false)
    {
        return evt.PayloadCase switch
        {
            MatchEvent.PayloadOneofCase.MatchCreated => Seed(evt),
            MatchEvent.PayloadOneofCase.MoveApplied when state is not null => ApplyMove(state, evt, premove),
            MatchEvent.PayloadOneofCase.MatchEnded when state is not null => state with { Ended = true },
            _ => state,
        };
    }

    private static MatchTimingState Seed(MatchEvent evt) =>
        new(
            evt.AggregateId,
            UserIdOrNull(evt.MatchCreated.White),
            UserIdOrNull(evt.MatchCreated.Black),
            evt.OccurredAt,
            [],
            false);

    private static MatchTimingState ApplyMove(MatchTimingState state, MatchEvent evt, bool premove)
    {
        MoveApplied move = evt.MoveApplied;
        string? moverId = UserIdOrNull(move.Player);
        if (moverId is null)
        {
            // A bot moved: no observation, but the clock anchor still advances so
            // the human's next think time is measured from this move.
            return state with { LastMoveAtMs = move.AppliedAtMs };
        }

        long thinkTime = Math.Max(0, move.AppliedAtMs - state.LastMoveAtMs);
        PlyObservation observation = new(move.Index, moverId, move.MoveUci, thinkTime, premove);
        return state with
        {
            LastMoveAtMs = move.AppliedAtMs,
            Observations = [.. state.Observations, observation],
        };
    }

    private static string? UserIdOrNull(Player? player) =>
        player?.IdentityCase == Player.IdentityOneofCase.UserId ? player.UserId : null;
}
