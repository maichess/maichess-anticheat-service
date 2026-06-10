namespace MaichessAnticheatService.Detection;

// One principal variation from the engine's analysis of the position before a
// ply: its rank (1 = best), evaluation, and the first move of the line — the
// move the engine would play. Mapped from engine-service AnalyzePosition.
internal sealed record EngineLine(int Rank, int EvaluationCp, string MoveUci);
