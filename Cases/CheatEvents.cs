using Maichess.Events.V1;

namespace MaichessAnticheatService.Cases;

// Builders for the cheat.events.v1 envelopes this service emits. Envelope
// conventions mirror the match event loop: aggregate_id is the partition key
// (user id), sequence is per-aggregate monotonic (the case's counter),
// causation_id stays empty (no upstream event id to point at).
internal static class CheatEvents
{
    public const string Producer = "anticheat-service";

    public static CheatEvent PlayerFlagged(
        CaseDocument caseDocument,
        string summary,
        string eventId,
        long occurredAtMs) =>
        new()
        {
            EventId = eventId,
            EventType = "cheat.PlayerFlagged",
            AggregateId = caseDocument.UserId,
            Sequence = caseDocument.Sequence,
            OccurredAt = occurredAtMs,
            CorrelationId = eventId,
            CausationId = string.Empty,
            Producer = Producer,
            PlayerFlagged = new PlayerFlagged
            {
                UserId = caseDocument.UserId,
                CaseId = caseDocument.Id,
                Score = caseDocument.Score,
                Summary = summary,
                Detector = Detector.Combined,
            },
        };

    public static CheatEvent PlayerUnflagged(
        CaseDocument caseDocument,
        string unflaggedBy,
        string reason,
        string eventId,
        long occurredAtMs) =>
        new()
        {
            EventId = eventId,
            EventType = "cheat.PlayerUnflagged",
            AggregateId = caseDocument.UserId,
            Sequence = caseDocument.Sequence,
            OccurredAt = occurredAtMs,
            CorrelationId = eventId,
            CausationId = string.Empty,
            Producer = Producer,
            PlayerUnflagged = new PlayerUnflagged
            {
                UserId = caseDocument.UserId,
                CaseId = caseDocument.Id,
                UnflaggedBy = unflaggedBy,
                Reason = reason,
            },
        };

    public static CheatEvent LiveSuspicionRaised(
        CaseDocument caseDocument,
        string matchId,
        int ply,
        double score,
        string eventId,
        long occurredAtMs) =>
        new()
        {
            EventId = eventId,
            EventType = "cheat.LiveSuspicionRaised",
            AggregateId = caseDocument.UserId,
            Sequence = caseDocument.Sequence,
            OccurredAt = occurredAtMs,
            CorrelationId = eventId,
            CausationId = string.Empty,
            Producer = Producer,
            LiveSuspicionRaised = new LiveSuspicionRaised
            {
                UserId = caseDocument.UserId,
                MatchId = matchId,
                Ply = ply,
                Score = score,
            },
        };
}
