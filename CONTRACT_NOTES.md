# Contract Notes — anticheat-service

## Pending: premove wire read needs PlatformProtos >= 0.7.0 (publish handoff)

`rest/anticheat.md`, the `premove` fields on `MoveSubmitted`/`MoveApplied`
(`protos/events/v1/match_events.proto`), and the REST `premove`/`allow_flagged` flags
were added to api-contracts by task 14, but the **published** package at implementation
time was 0.6.0 (pre-premove). Per the contract policy this service does not consume an
unpublished version, so:

- This service is pinned to `Maichess.PlatformProtos` **0.6.0**.
- The pure fold takes the premove bit as an explicit parameter
  (`MatchStreamProjection.Apply(state, evt, premove)`), fully tested for both values.
- The Kafka shell (`Kafka/MatchEventConsumer.Dispatch` -> `processor.Handle(evt)`)
  currently passes the default `false`.

**Follow-up once vNext (>= 0.7.0) is published** (one line here + match-manager work):

1. Bump `Maichess.PlatformProtos` to the new version in `MaichessAnticheatService.csproj`
   (and reconcile all other consumers per the conventions).
2. In `Kafka/MatchEventConsumer`, change `processor.Handle(evt)` to
   `processor.Handle(evt, evt.PayloadCase == MatchEvent.PayloadOneofCase.MoveApplied
   && evt.MoveApplied.Premove)`.
3. match-manager: accept `premove` on `POST /matches/{id}/moves`, set it on
   `MoveSubmitted` (`Kafka/MatchCommands.SubmitMove`), and carry it onto `MoveApplied`
   in `Kafka/MatchProjector` (stash alongside `PendingMoveUci` in `LiveMatchState`,
   the same mechanism that carries the UCI from MoveSubmitted to MoveApplied).

Until then every ply counts as non-premove, which can only *over*-report timing
suspicion for pre-movers; the authoritative flag still needs the combined score over
MinGames, and the threshold defaults keep correlation dominant (0.7 vs 0.3).

## Deviations / interpretations

- **`GET /anticheat/cases` ordering**: the Database contract's `List` is equality-filter
  only (no sort), so "most recently updated first" is applied to the *fetched page*, not
  globally. Acceptable for the dev overview's data volumes; noted here per the contract
  policy rather than silently extending the Database contract with sorting.
- **Statistical features**: the knowledge base lists "rating-vs-performance outliers" as
  a statistical input. User ratings are not available to this service without consuming
  `user.events.v1` (another read model); instead the case scorer implements the
  **accuracy-spike** feature (newest game's correlation far above the player's own prior
  average). Rating-vs-performance can be added later by feeding a rating replica into
  `StatisticalDetector` — the seam (`DetectionOptions` + per-game verdicts) already
  supports it. Recorded in `maichess-knowledge-base/knowledge/services/anticheat-service.md`.
- **Iteration-2 live scoring is timing-only** (no live engine calls): live engine
  correlation would multiply Engine load per ongoing game, while the task only requires
  an advisory early signal that the post-game pass reconciles. The live signal surfaces
  as a `LiveSuspicionRaised` event + a `live_signals` counter/audit entry on the case
  (visible in the Dev overview); read models must ignore it for the `flagged` bit.
