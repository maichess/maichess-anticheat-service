using Maichess.Events.V1;
using MaichessAnticheatService.Cases;

namespace MaichessAnticheatService.Tests.Support;

// Captures every emitted cheat event for assertion.
internal sealed class FakeCheatEventProducer : ICheatEventProducer
{
    public List<CheatEvent> Produced { get; } = [];

    public Task ProduceAsync(CheatEvent cheatEvent, CancellationToken ct)
    {
        Produced.Add(cheatEvent);
        return Task.CompletedTask;
    }
}
