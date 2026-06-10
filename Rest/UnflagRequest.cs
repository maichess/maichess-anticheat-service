using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// POST /anticheat/cases/{id}/unflag body (DTO record used only by the
// excluded endpoint adapter).
[ExcludeFromCodeCoverage]
internal sealed record UnflagRequest(string? Reason);
