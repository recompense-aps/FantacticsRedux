using System.Text.Json;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;

namespace Fantactics.Core.Tests;

/// <summary>Whole states survive a JSON round trip (save snapshots).</summary>
public class GameStateJsonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(40)]
    [InlineData(200)]
    public void StatesRoundTripToTheSameHash(int decisions)
    {
        GameState state = Autoplay.Run(RulesConfig.Default, seed: 7, decisions).State;

        GameState read = GameStateJson.Read(GameStateJson.Write(state), RulesConfig.Default);

        Assert.Equal(StateHash.Compute(state), StateHash.Compute(read));
        Assert.Same(RulesConfig.Default, read.Rules);
        Assert.Equal(GameEngine.PendingDecisions(state).AsEnumerable(), GameEngine.PendingDecisions(read));
    }

    [Fact]
    public void OutcomesSavedBeforeTeamsReadTheirSingleWinner()
    {
        MatchOutcome? won = JsonSerializer.Deserialize<MatchOutcome>(
            """{"winner":"P2","reason":"Rout"}""",
            CoreJson.Options);
        MatchOutcome? drawn = JsonSerializer.Deserialize<MatchOutcome>("""{"reason":"TurnLimit"}""", CoreJson.Options);

        Assert.Equal<Seat>([Seat.P2], won!.Winners);
        Assert.True(drawn!.IsDraw);
        Assert.DoesNotContain("\"winner\"", JsonSerializer.Serialize(won, CoreJson.Options));
    }
}
