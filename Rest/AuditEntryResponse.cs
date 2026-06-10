using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// One audit row in the case detail (DTO record used only by the excluded
// endpoint adapter).
[ExcludeFromCodeCoverage]
internal sealed record AuditEntryResponse(string Action, string Actor, string Reason, long AtMs);
