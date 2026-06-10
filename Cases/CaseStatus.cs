namespace MaichessAnticheatService.Cases;

// Case lifecycle: Open (analysed, below threshold) -> Flagged (combined score
// crossed the threshold over enough games) -> Cleared (a dev removed the
// flag). A Cleared case keeps accumulating analyses and can be re-flagged.
internal enum CaseStatus
{
    Open,
    Flagged,
    Cleared,
}
