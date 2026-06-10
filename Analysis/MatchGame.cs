namespace MaichessAnticheatService.Analysis;

// The slice of a finished match-db document the analyser needs: who played
// (null for a bot side), the move list, and the FEN before each move
// (FenHistory[i] is the position move i was played from). Read on demand by
// match id — anticheat-db never stores a copy of any of this.
internal sealed record MatchGame(
    string MatchId,
    string? WhiteUserId,
    string? BlackUserId,
    IReadOnlyList<string> Moves,
    IReadOnlyList<string> FenHistory);
