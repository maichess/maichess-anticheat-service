using Maichess.Events.V1;

namespace MaichessAnticheatService.Cases;

// Producer seam for cheat.events.v1 (keyed by the event's aggregate_id, i.e.
// the user id). Only the flag state rides the topic — evidence stays in
// anticheat-db.
internal interface ICheatEventProducer
{
    Task ProduceAsync(CheatEvent cheatEvent, CancellationToken ct);
}
