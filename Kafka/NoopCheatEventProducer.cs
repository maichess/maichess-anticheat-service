using System.Diagnostics.CodeAnalysis;
using Maichess.Events.V1;
using MaichessAnticheatService.Cases;

namespace MaichessAnticheatService.Kafka;

// Stand-in producer when Kafka is disabled (local dev without the broker):
// flag changes still persist to anticheat-db, they just don't propagate.
[ExcludeFromCodeCoverage]
internal sealed class NoopCheatEventProducer : ICheatEventProducer
{
    public Task ProduceAsync(CheatEvent cheatEvent, CancellationToken ct) => Task.CompletedTask;
}
