# maichess-anticheat-service

Anti-cheat for the maichess platform: consumes `match.events.v1`, scores finished games
with a combined detector (engine correlation + pre-move-aware timing statistics), raises
advisory live signals during play, persists cases/evidence/audit in the `anticheat-db`
DatabaseService instance, and emits flag changes on `cheat.events.v1` for the user read
models (Match Maker matchmaking filter, match-manager user replica).

## Run

```bash
dotnet run            # REST on :8080 (dev profile), Kafka off by default
```

Configuration (see `appsettings.json`): `Services:{AnticheatDatabase,MatchDatabase,
EngineService,UserService}`, `Jwt:Key`, `Kafka:{Enabled,Bootstrap,SchemaRegistry}`,
`Analysis:{BotId,DelayMs}`, `Detection:*` thresholds.

## Test

```bash
dotnet test MaichessAnticheatService.Tests/MaichessAnticheatService.Tests.csproj \
  -p:CollectCoverage=true "-p:Include=[MaichessAnticheatService]*"
```

100% line/branch/method on non-excluded code is the bar.

## Mutation testing

```bash
dotnet tool restore                     # once per checkout
cd MaichessAnticheatService.Tests
dotnet stryker
```

Exclusions mirror the coverage exclusions (REST adapter, live gRPC/Kafka/DB glue).
