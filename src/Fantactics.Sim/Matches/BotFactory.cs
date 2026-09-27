using Fantactics.Ai;

namespace Fantactics.Sim.Matches;

/// <summary>Creates built-in bots by name.</summary>
public static class BotFactory
{
    /// <summary>Names of the available bots.</summary>
    public static IReadOnlyList<string> Names { get; } = ["random"];

    /// <summary>Creates a bot.</summary>
    /// <param name="name">Bot name from <see cref="Names"/>.</param>
    /// <param name="seed">Seed for any randomness in the bot.</param>
    /// <exception cref="SimException">The name is unknown.</exception>
    public static IPlayerAgent Create(string name, int seed) => name switch
    {
        "random" => new RandomAgent(seed),
        _ => throw new SimException($"Unknown bot '{name}'."),
    };
}
