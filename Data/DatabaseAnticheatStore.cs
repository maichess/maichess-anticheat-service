using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maichess.Database.V1;
using MaichessAnticheatService.Cases;

namespace MaichessAnticheatService.Data;

// IAnticheatStore over the anticheat-db DatabaseService instance (project
// convention: generic Database gRPC CRUD, never a direct Mongo driver).
// Excluded from coverage: requires the live database service.
//
// The Database List filter is equality-only and unordered, so the dev-overview
// ordering ("most recently updated first") is applied to the fetched page.
[ExcludeFromCodeCoverage]
internal sealed class DatabaseAnticheatStore(Database.DatabaseClient client) : IAnticheatStore
{
    private const string Cases = "cases";
    private const string Audit = "audit";

    public async Task<CaseDocument?> GetCaseByUserAsync(string userId, CancellationToken ct)
    {
        Struct filter = new();
        filter.Fields["user_id"] = Value.ForString(userId);
        ListResponse response = await client.ListAsync(
            new ListRequest { Collection = Cases, Filter = filter, Limit = 1 },
            cancellationToken: ct);
        return response.Records.Count == 0 ? null : ToCase(response.Records[0]);
    }

    public async Task<CaseDocument?> GetCaseAsync(string caseId, CancellationToken ct)
    {
        try
        {
            GetResponse response = await client.GetAsync(
                new GetRequest { Collection = Cases, Id = caseId },
                cancellationToken: ct);
            return ToCase(response.Record);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CaseDocument>> ListCasesAsync(
        CaseStatus? status,
        int limit,
        int offset,
        CancellationToken ct)
    {
        ListRequest request = new() { Collection = Cases, Limit = limit, Offset = offset };
        if (status is { } s)
        {
            Struct filter = new();
            filter.Fields["status"] = Value.ForString(StatusToString(s));
            request.Filter = filter;
        }

        ListResponse response = await client.ListAsync(request, cancellationToken: ct);
        return [.. response.Records.Select(ToCase).OrderByDescending(c => c.UpdatedAtMs)];
    }

    public async Task UpsertCaseAsync(CaseDocument caseDocument, CancellationToken ct)
    {
        Struct fields = CaseToStruct(caseDocument);
        try
        {
            await client.UpdateAsync(
                new UpdateRequest { Collection = Cases, Id = caseDocument.Id, Fields = fields },
                cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            fields.Fields["id"] = Value.ForString(caseDocument.Id);
            await client.InsertAsync(
                new InsertRequest { Collection = Cases, Record = fields },
                cancellationToken: ct);
        }
    }

    public async Task AppendAuditAsync(AuditEntry entry, CancellationToken ct)
    {
        Struct record = new();
        record.Fields["id"] = Value.ForString(entry.Id);
        record.Fields["case_id"] = Value.ForString(entry.CaseId);
        record.Fields["user_id"] = Value.ForString(entry.UserId);
        record.Fields["action"] = Value.ForString(entry.Action);
        record.Fields["actor"] = Value.ForString(entry.Actor);
        record.Fields["reason"] = Value.ForString(entry.Reason);
        record.Fields["score"] = Value.ForNumber(entry.Score);
        record.Fields["at_ms"] = Value.ForNumber(entry.AtMs);
        await client.InsertAsync(
            new InsertRequest { Collection = Audit, Record = record },
            cancellationToken: ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> ListAuditAsync(string caseId, CancellationToken ct)
    {
        Struct filter = new();
        filter.Fields["case_id"] = Value.ForString(caseId);
        ListResponse response = await client.ListAsync(
            new ListRequest { Collection = Audit, Filter = filter },
            cancellationToken: ct);
        return [.. response.Records.Select(ToAudit).OrderBy(a => a.AtMs)];
    }

    private static Struct CaseToStruct(CaseDocument c)
    {
        Struct s = new();
        s.Fields["user_id"] = Value.ForString(c.UserId);
        s.Fields["status"] = Value.ForString(StatusToString(c.Status));
        s.Fields["score"] = Value.ForNumber(c.Score);
        s.Fields["sequence"] = Value.ForNumber(c.Sequence);
        s.Fields["live_signals"] = Value.ForNumber(c.LiveSignals);
        s.Fields["created_at_ms"] = Value.ForNumber(c.CreatedAtMs);
        s.Fields["updated_at_ms"] = Value.ForNumber(c.UpdatedAtMs);
        s.Fields["flagged_at_ms"] = c.FlaggedAtMs is { } f ? Value.ForNumber(f) : Value.ForNull();
        s.Fields["games"] = Value.ForList([.. c.Games.Select(GameToValue)]);
        return s;
    }

    private static Value GameToValue(CaseGame game)
    {
        Struct s = new();
        s.Fields["match_id"] = Value.ForString(game.MatchId);
        s.Fields["score"] = Value.ForNumber(game.Score);
        s.Fields["correlation"] = Value.ForNumber(game.Correlation);
        s.Fields["statistical"] = Value.ForNumber(game.Statistical);
        s.Fields["suspicious_plies"] = Value.ForList([.. game.SuspiciousPlies.Select(p => Value.ForNumber(p))]);
        s.Fields["analyzed_at_ms"] = Value.ForNumber(game.AnalyzedAtMs);
        return Value.ForStruct(s);
    }

    private static CaseDocument ToCase(Struct record)
    {
        bool hasFlaggedAt = record.Fields.TryGetValue("flagged_at_ms", out Value? flagged)
            && flagged.KindCase == Value.KindOneofCase.NumberValue;
        return new CaseDocument(
            Str(record, "id"),
            Str(record, "user_id"),
            StatusFromString(Str(record, "status")),
            Num(record, "score"),
            (long)Num(record, "sequence"),
            (int)Num(record, "live_signals"),
            [.. Games(record).Select(ToGame)],
            (long)Num(record, "created_at_ms"),
            (long)Num(record, "updated_at_ms"),
            hasFlaggedAt ? (long)flagged!.NumberValue : null);
    }

    private static CaseGame ToGame(Value value)
    {
        Struct s = value.StructValue;
        return new CaseGame(
            Str(s, "match_id"),
            Num(s, "score"),
            Num(s, "correlation"),
            Num(s, "statistical"),
            [.. SuspiciousPlies(s)],
            (long)Num(s, "analyzed_at_ms"));
    }

    private static IEnumerable<int> SuspiciousPlies(Struct game) =>
        game.Fields.TryGetValue("suspicious_plies", out Value? plies)
            && plies.KindCase == Value.KindOneofCase.ListValue
            ? plies.ListValue.Values.Select(v => (int)v.NumberValue)
            : [];

    private static RepeatedField<Value> Games(Struct record) =>
        record.Fields.TryGetValue("games", out Value? games)
            && games.KindCase == Value.KindOneofCase.ListValue
            ? games.ListValue.Values
            : [];

    private static AuditEntry ToAudit(Struct record) =>
        new(
            Str(record, "id"),
            Str(record, "case_id"),
            Str(record, "user_id"),
            Str(record, "action"),
            Str(record, "actor"),
            Str(record, "reason"),
            Num(record, "score"),
            (long)Num(record, "at_ms"));

    private static string StatusToString(CaseStatus status) =>
        status switch
        {
            CaseStatus.Open => "open",
            CaseStatus.Flagged => "flagged",
            _ => "cleared",
        };

    private static CaseStatus StatusFromString(string status) =>
        status switch
        {
            "flagged" => CaseStatus.Flagged,
            "cleared" => CaseStatus.Cleared,
            _ => CaseStatus.Open,
        };

    private static string Str(Struct s, string field) =>
        s.Fields.TryGetValue(field, out Value? value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : string.Empty;

    private static double Num(Struct s, string field) =>
        s.Fields.TryGetValue(field, out Value? value) && value.KindCase == Value.KindOneofCase.NumberValue
            ? value.NumberValue
            : 0;
}
