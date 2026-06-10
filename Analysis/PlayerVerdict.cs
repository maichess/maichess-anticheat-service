using MaichessAnticheatService.Detection;

namespace MaichessAnticheatService.Analysis;

// One analysed game from one human participant's perspective.
internal sealed record PlayerVerdict(string UserId, GameVerdict Verdict);
