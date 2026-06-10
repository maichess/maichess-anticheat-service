using MaichessAnticheatService.Analysis;
using MaichessAnticheatService.Detection;
using MaichessAnticheatService.Tests.Support;
using Xunit;

namespace MaichessAnticheatService.Tests;

public sealed class GameAnalyzerTests
{
    private static MatchGame Game(string? white = "w", string? black = "b", int plies = 6)
    {
        List<string> moves = [];
        List<string> fens = [];
        for (int i = 0; i < plies; i++)
        {
            moves.Add($"move{i}");
            fens.Add($"fen{i}");
        }

        fens.Add($"fen{plies}");
        return new MatchGame("m1", white, black, moves, fens);
    }

    private static GameAnalyzer Analyzer(FakeEngineAnalyzer engine, DetectionOptions? options = null) =>
        new(engine, options ?? TestOptions.Default(), () => 4242);

    [Fact]
    public async Task ScoresEachHumanParticipantSeparately()
    {
        FakeEngineAnalyzer engine = new();
        for (int i = 2; i < 6; i++)
        {
            // The engine's top line always matches white's move at even plies.
            engine.Set($"fen{i}", new EngineLine(1, 40, $"move{i - (i % 2)}"));
        }

        IReadOnlyList<PlayerVerdict> verdicts =
            await Analyzer(engine).AnalyzeAsync(Game(), [], CancellationToken.None);

        Assert.Equal(2, verdicts.Count);
        PlayerVerdict white = Assert.Single(verdicts, v => v.UserId == "w");
        PlayerVerdict black = Assert.Single(verdicts, v => v.UserId == "b");
        Assert.Equal(1, white.Verdict.Correlation);
        Assert.Equal(0, black.Verdict.Correlation);
        Assert.Equal("m1", white.Verdict.MatchId);
        Assert.Equal(4242, white.Verdict.AnalyzedAtMs);
    }

    [Fact]
    public async Task BotSidesAreNotScored()
    {
        FakeEngineAnalyzer engine = new();

        IReadOnlyList<PlayerVerdict> verdicts =
            await Analyzer(engine).AnalyzeAsync(Game(white: null), [], CancellationToken.None);

        PlayerVerdict verdict = Assert.Single(verdicts);
        Assert.Equal("b", verdict.UserId);
    }

    [Fact]
    public async Task BookPliesAreNeverSentToTheEngine()
    {
        FakeEngineAnalyzer engine = new();

        await Analyzer(engine).AnalyzeAsync(Game(), [], CancellationToken.None);

        Assert.Equal(["fen2", "fen3", "fen4", "fen5"], engine.AnalyzedFens);
    }

    [Fact]
    public async Task StreamObservationsSupplyTiming()
    {
        FakeEngineAnalyzer engine = new();
        DetectionOptions options = TestOptions.Default() with { TimingMinSamples = 2 };
        PlyObservation[] observations =
        [
            new(2, "w", "move2", 1000, false),
            new(4, "w", "move4", 1000, false),
        ];

        IReadOnlyList<PlayerVerdict> verdicts =
            await Analyzer(engine, options).AnalyzeAsync(Game(), observations, CancellationToken.None);

        PlayerVerdict white = Assert.Single(verdicts, v => v.UserId == "w");
        Assert.Equal(1, white.Verdict.Statistical);
    }

    [Fact]
    public async Task ColdStreamStateDegradesToCorrelationOnly()
    {
        // No observations at all: timing-less placeholders keep correlation
        // alive while the statistical detector abstains.
        FakeEngineAnalyzer engine = new();
        for (int i = 2; i < 6; i++)
        {
            engine.Set($"fen{i}", new EngineLine(1, 40, $"move{i}"));
        }

        IReadOnlyList<PlayerVerdict> verdicts =
            await Analyzer(engine).AnalyzeAsync(Game(), [], CancellationToken.None);

        PlayerVerdict white = Assert.Single(verdicts, v => v.UserId == "w");
        Assert.Equal(1, white.Verdict.Correlation);
        Assert.Equal(0, white.Verdict.Statistical);
        Assert.Equal(
            CombinedScorer.GameScore(1, 0, TestOptions.Default()),
            white.Verdict.Score,
            10);
    }

    [Fact]
    public async Task TruncatedFenHistoryStopsAtTheShorterList()
    {
        FakeEngineAnalyzer engine = new();
        MatchGame game = new("m1", "w", "b", ["move0", "move1", "move2"], ["fen0", "fen1"]);

        IReadOnlyList<PlayerVerdict> verdicts =
            await Analyzer(engine).AnalyzeAsync(game, [], CancellationToken.None);

        // Only plies 0 and 1 are visible; both are book plies -> no engine calls.
        Assert.Empty(engine.AnalyzedFens);
        Assert.Equal(2, verdicts.Count);
    }

    [Fact]
    public async Task SuspiciousPliesPointIntoTheMatchMoveList()
    {
        FakeEngineAnalyzer engine = new();
        engine.Set("fen2", new EngineLine(1, 40, "move2"));
        engine.Set("fen4", new EngineLine(1, 40, "move4"));

        IReadOnlyList<PlayerVerdict> verdicts =
            await Analyzer(engine).AnalyzeAsync(Game(), [], CancellationToken.None);

        PlayerVerdict white = Assert.Single(verdicts, v => v.UserId == "w");
        Assert.Equal([2, 4], white.Verdict.SuspiciousPlies);
    }
}
