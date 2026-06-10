using System.Diagnostics.CodeAnalysis;

namespace MaichessAnticheatService.Rest;

// REST error body (DTO record used only by the excluded endpoint adapter).
[ExcludeFromCodeCoverage]
internal sealed record ErrorResponse(string Error);
