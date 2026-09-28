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
}
