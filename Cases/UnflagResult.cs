namespace MaichessAnticheatService.Cases;

// Outcome of a dev unflag request, mapped to REST statuses by the endpoint
// (Cleared -> 204, NotFound -> 404, NotFlagged -> 409).
internal enum UnflagResult
{
    Cleared,
    NotFound,
    NotFlagged,
}
