using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maichess.Database.V1;
using MaichessAnticheatService.Analysis;

namespace MaichessAnticheatService.Data;

// IMatchReader over the match-db DatabaseService instance: reads the finished
// game by id on demand — anticheat-db never copies game data. Field names are
// the match-manager document schema (white_user_id / black_user_id / moves /
// fen_history, snake_case). Excluded from coverage: requires the live service.
[ExcludeFromCodeCoverage]
internal sealed class DatabaseMatchReader(Database.DatabaseClient client) : IMatchReader
{
    public async Task<MatchGame?> GetAsync(string matchId, CancellationToken ct)
    {
        Struct record;
        try
        {
            GetResponse response = await client.GetAsync(
                new GetRequest { Collection = "matches", Id = matchId },
                cancellationToken: ct);
            record = response.Record;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }

        return new MatchGame(
            matchId,
            StrOrNull(record, "white_user_id"),
            StrOrNull(record, "black_user_id"),
            Strings(record, "moves"),
            Strings(record, "fen_history"));
    }

    private static string? StrOrNull(Struct s, string field) =>
        s.Fields.TryGetValue(field, out Value? value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : null;

    private static IReadOnlyList<string> Strings(Struct s, string field) =>
        s.Fields.TryGetValue(field, out Value? value) && value.KindCase == Value.KindOneofCase.ListValue
            ? [.. value.ListValue.Values.Select(v => v.StringValue)]
            : [];
}
