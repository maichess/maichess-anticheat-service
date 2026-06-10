# Anti-Cheat Service

Analyses players' games for engine assistance, flags likely cheaters, and propagates the
flag so matchmaking can exclude flagged players. Detection is combined — engine
correlation (primary) + statistical timing features — in two iterations: a post-game
pass on `MatchEnded` (authoritative) and a live in-game stream scorer (advisory only).
Devs review evidence and clear false positives via the Dev REST surface.

## Contracts

- **REST:** `maichess-api-contracts/rest/anticheat.md` (Dev-only: case overview, case
  detail, unflag)
- **Events:** consumes `match.events.v1`, produces `cheat.events.v1`
  (`maichess-api-contracts/protos/events/v1/{match_events,cheat_events}.proto`,
  Protobuf on the wire)
- **gRPC clients only** — no gRPC server: Database (anticheat-db + match-db instances),
  Engine `Bots.AnalyzePosition`, User `Users.GetUser` (dev gate)
- **Generated stubs:** `Maichess.PlatformProtos` NuGet package

Implement against these contracts exactly. Document blockers in `CONTRACT_NOTES.md`.

## Stack

- **Runtime:** ASP.NET (net10.0), C#, nullable enabled (no AOT — Grpc.Net.Client)
- **Persistence:** `anticheat-db` DatabaseService instance via `Database.DatabaseClient`
  gRPC (`Services:AnticheatDatabase`) — collections `cases` + `audit`. **References,
  never duplicates, match-db**: evidence is `match_id` + ply indexes only; game data is
  read on demand from match-db (`Services:MatchDatabase`).
- **Kafka:** Confluent client, Protobuf serde (schema registry during the migration
  window), gated by `Kafka:Enabled`.

## Structure

```
Detection/   # Pure detectors: correlation, statistical (timing), combined scorer,
             # live (Welford) scorer, DetectionOptions thresholds
Stream/      # Pure fold of match.events into per-match timing state + the
             # AnticheatStreamProcessor decision core (live signals, finished matches)
Analysis/    # Post-game pass: GameAnalyzer + IEngineAnalyzer / IMatchReader seams
Cases/       # Case lifecycle (CaseService), cheat event builders, store seam
Data/        # DatabaseService-backed store + match reader (excluded glue)
Kafka/       # Consumer / producer / analysis-worker shells (excluded glue)
Rest/        # Dev endpoints + DTOs (excluded), IDevGate via user-service dev_mode
Program.cs   # DI wiring
```

## Key Design Decisions

- **Pre-moves are legitimate.** The premove bit (MoveSubmitted/MoveApplied `premove`,
  contracts >= 0.7.0) exempts a ply from all timing analysis and downweights it in
  correlation. Getting this wrong is the main false-positive source; it is tested
  explicitly (`StatisticalDetectorTests.FastPremovedPliesDoNotRaiseTimingSuspicion`,
  `LiveScorerTests.HeavyPremoverNeverTriggersALiveSignal`). The flag is client-asserted:
  it can only reduce *timing* suspicion, never correlation evidence.
- **Timing lives in the stream, not the doc.** Match docs store no per-move timestamps;
  think times are `MoveApplied.applied_at_ms` deltas accumulated by the stream processor.
  If that state is cold when a match ends, the post-game pass degrades to
  correlation-only.
- **Engine load is throttled**: the analysis worker is sequential and the engine analyzer
  sleeps `Analysis:DelayMs` between `AnalyzePosition` calls; book plies are never sent to
  the engine. The queue is bounded; overflow drops are logged (analysis is rebuildable by
  replaying `match.events.v1` with a fresh consumer group).
- **Flag = event, evidence = db.** Flag changes emit `PlayerFlagged`/`PlayerUnflagged` on
  compacted `cheat.events.v1` (keyed by user id) for the user read models; the evidence
  and audit trail stay in anticheat-db. `LiveSuspicionRaised` is advisory — consumers
  must never set the durable flag from it.
- **One advisory signal per (match, player)**, latched in the live scorer state; the
  post-game verdict reconciles it (the live signal is an audit entry + event only).
- **Dev gate** resolves `dev_mode` via user-service `Users.GetUser` per request (the JWT
  carries no dev claim).

## Code Style

Same strictness as search-service: `TreatWarningsAsErrors`, `AnalysisMode=All`,
StyleCop, one type per file, explicit types except where apparent, no comments unless
explaining a non-obvious constraint.

## Testing Requirements

- 100% line, branch, and method coverage on all included code is mandatory. Run
  `dotnet test MaichessAnticheatService.Tests/MaichessAnticheatService.Tests.csproj
  -p:CollectCoverage=true "-p:Include=[MaichessAnticheatService]*"`.
- Plain xUnit `[Fact]` tests; deterministic fakes in `Tests/Support`.
- Excluded from coverage (`[ExcludeFromCodeCoverage]`):
  - `Rest/AnticheatEndpoints` + REST DTO records (thin HTTP adapter)
  - `Rest/UserServiceDevGate` (live gRPC client)
  - `Data/DatabaseAnticheatStore`, `Data/DatabaseMatchReader` (live DatabaseService)
  - `Analysis/GrpcEngineAnalyzer` (live engine stream)
  - `Kafka/*` shells (live Kafka consumer/producer/worker; their decision logic lives in
    the tested `Stream/`, `Analysis/`, `Cases/` types)
- Coverlet excludes `Program.cs`, `*.g.cs`, `*.generated.cs` by file.

### Mutation testing

Stryker.NET is wired as a local dotnet tool (`.config/dotnet-tools.json`); config in
`MaichessAnticheatService.Tests/stryker-config.json` mirrors the coverage exclusions.
Run `dotnet tool restore` then `dotnet stryker` from the test project directory.
