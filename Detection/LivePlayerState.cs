namespace MaichessAnticheatService.Detection;

// Running timing state for one (match, player) during play — Welford's online
// mean/variance over usable (non-premove, non-book, known-timing) think times.
// Raised latches once an advisory signal has been emitted so a match never
// produces more than one signal per player.
internal sealed record LivePlayerState(int Samples, double Mean, double M2, bool Raised)
{
    public static LivePlayerState Empty { get; } = new(0, 0, 0, false);
}
