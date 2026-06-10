using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Maichess.Events.V1;
using MaichessAnticheatService.Cases;

namespace MaichessAnticheatService.Kafka;

// ICheatEventProducer over cheat.events.v1 (compacted, keyed by user id so the
// latest flag state per user wins). Idempotent single producer; Protobuf on
// the wire from day one. Excluded from coverage: live-Kafka producer shell.
[ExcludeFromCodeCoverage]
internal sealed class KafkaCheatEventProducer : ICheatEventProducer, IDisposable
{
    public const string Topic = "cheat.events.v1";

    private readonly IProducer<string, CheatEvent> producer;

    public KafkaCheatEventProducer(string bootstrapServers, ISchemaRegistryClient registry)
    {
        ProducerConfig config = new()
        {
            BootstrapServers = bootstrapServers,
            EnableIdempotence = true,
        };
        producer = new ProducerBuilder<string, CheatEvent>(config)
            .SetValueSerializer(ProtobufEventSerdes.Serializer<CheatEvent>(registry))
            .Build();
    }

    public async Task ProduceAsync(CheatEvent cheatEvent, CancellationToken ct) =>
        await producer.ProduceAsync(
            Topic,
            new Message<string, CheatEvent> { Key = cheatEvent.AggregateId, Value = cheatEvent },
            ct);

    public void Dispose() => producer.Dispose();
}
