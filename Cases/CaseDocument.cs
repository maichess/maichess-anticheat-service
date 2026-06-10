namespace MaichessAnticheatService.Cases;

// The per-user anti-cheat case persisted in anticheat-db ("cases" collection).
// Sequence is the per-user monotonic counter stamped on every cheat event the
// case emits (the topic is keyed by user id, so this orders a user's events).
internal sealed record CaseDocument(
    string Id,
    string UserId,
    CaseStatus Status,
    double Score,
    long Sequence,
    int LiveSignals,
    IReadOnlyList<CaseGame> Games,
    long CreatedAtMs,
    long UpdatedAtMs,
    long? FlaggedAtMs);
