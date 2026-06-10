using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Confluent.Kafka;
using Maichess.Events.V1;
using MaichessAnticheatService.Cases;
using MaichessAnticheatService.Stream;

namespace MaichessAnticheatService.Kafka;

// Live-Kafka shell for the stream side: consumes match.events.v1, routes each
// event through the deterministic AnticheatStreamProcessor, and dispatches its
// outputs — advisory live signals to the case service, finished matches to the
// analysis channel. All decisions live in the tested processor; this class is
// only consume/dispatch plumbing. Excluded from coverage.
//
// NOTE (PlatformProtos 0.6.0): MoveApplied.premove does not exist in the
// pinned package yet; Handle() defaults the bit to false. Once the consumer is
// on contracts >= 0.7.0, pass evt.MoveApplied.Premove (see CONTRACT_NOTES.md).
[ExcludeFromCodeCoverage]
internal sealed class MatchEventConsumer(
    string bootstrapServers,
    AnticheatStreamProcessor processor,
    CaseService cases,
    ChannelWriter<FinishedMatch> analysisQueue,
    ILogger<MatchEventConsumer> logger) : BackgroundService
{
    public const string Topic = "match.events.v1";
    public const string GroupId = "anticheat-detector";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private void ConsumeLoop(CancellationToken ct)
    {
        ConsumerConfig config = new()
        {
            BootstrapServers = bootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };

        using IConsumer<string, MatchEvent> consumer = new ConsumerBuilder<string, MatchEvent>(config)
            .SetValueDeserializer(ProtobufEventSerdes.Deserializer<MatchEvent>())
            .Build();
        consumer.Subscribe(Topic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                ConsumeResult<string, MatchEvent>? result = consumer.Consume(ct);
                if (result?.Message?.Value is not { } evt)
                {
                    continue;
                }

                StreamOutput output = processor.Handle(evt);
                Dispatch(output, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogWarning(ex, "Failed to consume a match event; skipping");
            }
        }

        consumer.Close();
    }

    private void Dispatch(StreamOutput output, CancellationToken ct)
    {
        if (output.Live is { } live)
        {
            // Fire-and-forget by design: an advisory signal must never stall the
            // consume loop behind the database/producer round-trip.
            _ = RecordLiveAsync(live, ct);
        }

        if (output.Finished is { } finished && !analysisQueue.TryWrite(finished))
        {
            logger.LogWarning("Analysis queue full; dropping match {MatchId}", finished.MatchId);
        }
    }

    private async Task RecordLiveAsync(LiveSignal live, CancellationToken ct)
    {
        try
        {
            await cases.RecordLiveSuspicionAsync(live.UserId, live.MatchId, live.Ply, live.Score, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to record live suspicion for user {UserId}", live.UserId);
        }
    }
}
