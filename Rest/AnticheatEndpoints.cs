using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using MaichessAnticheatService.Cases;
using Microsoft.AspNetCore.Mvc;

namespace MaichessAnticheatService.Rest;

// Thin HTTP adapter over CaseService — the Dev-only surface from
// rest/anticheat.md (overview, case detail, unflag). Every endpoint requires a
// valid JWT and the persisted dev_mode profile flag (resolved via IDevGate).
// Excluded from coverage (REST endpoint handler per the project convention);
// the case lifecycle behaviour lives in the tested CaseService.
[ExcludeFromCodeCoverage]
internal static class AnticheatEndpoints
{
    internal static IEndpointRouteBuilder MapAnticheatEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/anticheat").RequireAuthorization();
        group.MapGet("/cases", ListCases);
        group.MapGet("/cases/{caseId}", GetCase);
        group.MapPost("/cases/{caseId}/unflag", Unflag);
        return routes;
    }

    private static async Task<IResult> ListCases(
        ClaimsPrincipal principal,
        CaseService cases,
        IDevGate devGate,
        CancellationToken ct,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        if (await RequireDevAsync(principal, devGate, ct) is { } denied)
        {
            return denied;
        }

        CaseStatus? statusFilter = null;
        if (status is not null)
        {
            if (!TryParseStatus(status, out CaseStatus parsed))
            {
                return Results.BadRequest(new ErrorResponse("unknown status"));
            }

            statusFilter = parsed;
        }

        IReadOnlyList<CaseDocument> result = await cases.ListCasesAsync(
            statusFilter,
            Math.Clamp(limit, 1, 200),
            Math.Max(0, offset),
            ct);
        return Results.Ok(new CaseListResponse([.. result.Select(ToSummary)]));
    }

    private static async Task<IResult> GetCase(
        string caseId,
        ClaimsPrincipal principal,
        CaseService cases,
        IDevGate devGate,
        CancellationToken ct)
    {
        if (await RequireDevAsync(principal, devGate, ct) is { } denied)
        {
            return denied;
        }

        CaseDocument? caseDocument = await cases.GetCaseAsync(caseId, ct);
        if (caseDocument is null)
        {
            return Results.NotFound();
        }

        IReadOnlyList<AuditEntry> audit = await cases.ListAuditAsync(caseId, ct);
        return Results.Ok(new CaseDetailResponse(
            caseDocument.Id,
            caseDocument.UserId,
            StatusString(caseDocument.Status),
            caseDocument.Score,
            caseDocument.FlaggedAtMs,
            [.. caseDocument.Games.Select(g => new CaseGameResponse(
                g.MatchId, g.Score, g.Correlation, g.Statistical, g.SuspiciousPlies, g.AnalyzedAtMs))],
            [.. audit.Select(a => new AuditEntryResponse(a.Action, a.Actor, a.Reason, a.AtMs))]));
    }

    private static async Task<IResult> Unflag(
        string caseId,
        UnflagRequest request,
        ClaimsPrincipal principal,
        CaseService cases,
        IDevGate devGate,
        CancellationToken ct)
    {
        if (await RequireDevAsync(principal, devGate, ct) is { } denied)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new ErrorResponse("reason is required"));
        }

        string devUserId = UserId(principal)!;
        UnflagResult result = await cases.UnflagAsync(caseId, devUserId, request.Reason, ct);
        return result switch
        {
            UnflagResult.Cleared => Results.NoContent(),
            UnflagResult.NotFound => Results.NotFound(),
            _ => Results.Conflict(new ErrorResponse("case is not flagged")),
        };
    }

    private static async Task<IResult?> RequireDevAsync(
        ClaimsPrincipal principal,
        IDevGate devGate,
        CancellationToken ct)
    {
        string? userId = UserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        return await devGate.IsDevAsync(userId, ct) ? null : Results.Forbid();
    }

    private static string? UserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

    private static CaseSummaryResponse ToSummary(CaseDocument c) =>
        new(
            c.Id,
            c.UserId,
            StatusString(c.Status),
            c.Score,
            c.Games.Count,
            c.LiveSignals,
            c.CreatedAtMs,
            c.UpdatedAtMs,
            c.FlaggedAtMs);

    private static string StatusString(CaseStatus status) =>
        status switch
        {
            CaseStatus.Open => "open",
            CaseStatus.Flagged => "flagged",
            _ => "cleared",
        };

    private static bool TryParseStatus(string value, out CaseStatus status)
    {
        switch (value)
        {
            case "open":
                status = CaseStatus.Open;
                return true;
            case "flagged":
                status = CaseStatus.Flagged;
                return true;
            case "cleared":
                status = CaseStatus.Cleared;
                return true;
            default:
                status = CaseStatus.Open;
                return false;
        }
    }
}
