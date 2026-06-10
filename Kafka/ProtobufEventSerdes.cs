using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Google.Protobuf;

namespace MaichessAnticheatService.Kafka;

// Confluent Protobuf serde factory for the maichess.events.v1 generated
// messages — the same pattern as match-manager's ProtobufEventSerdes. The
// serializer registers schemas with the registry (still present during the
// Avro->Protobuf transition; removed by the final migration step).
[ExcludeFromCodeCoverage]
internal static class ProtobufEventSerdes
{
    public static IAsyncSerializer<T> Serializer<T>(ISchemaRegistryClient registry)
        where T : class, IMessage<T>, new()
        => new ProtobufSerializer<T>(registry);

    public static IDeserializer<T> Deserializer<T>()
        where T : class, IMessage<T>, new()
        => new ProtobufDeserializer<T>().AsSyncOverAsync();
}
