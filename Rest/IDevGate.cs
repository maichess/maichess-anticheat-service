namespace MaichessAnticheatService.Rest;

// Server-side dev_mode check for the Dev-only REST surface. The JWT carries no
// dev claim, so the caller's profile is resolved on demand.
internal interface IDevGate
{
    Task<bool> IsDevAsync(string userId, CancellationToken ct);
}
