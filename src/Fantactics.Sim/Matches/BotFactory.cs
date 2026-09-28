using Fantactics.Ai;
using Fantactics.Ai.Profiles;
using Fantactics.Core.Rules;

namespace Fantactics.Sim.Matches;

/// <summary>Creates built-in bots by name (see <see cref="BotLibrary"/>).</summary>
public static class BotFactory
{
    /// <summary>Names of the available bots; profiles also take <c>@difficulty</c>.</summary>
    public static IReadOnlyList<string> Names => BotLibrary.Names;

    /// <summary>Whether <paramref name="spec"/> names a bot, e.g. <c>captain</c> or <c>captain@easy</c>.</summary>
    public static bool IsKnown(string spec) => BotLibrary.IsKnown(spec);

    /// <summary>A usage hint listing the bots and difficulties.</summary>
    public static string Usage =>
        $"bot:<{string.Join("|", Names)}>[@{string.Join("|", BotLibrary.Difficulties)}]";

    /// <summary>Creates a bot.</summary>
    /// <param name="spec">Bot name from <see cref="Names"/>, optionally with <c>@difficulty</c>.</param>
    /// <param name="rules">Rules the match uses.</param>
    /// <param name="seed">Seed for any randomness in the bot.</param>
    /// <exception cref="SimException">The name is unknown.</exception>
    public static IPlayerAgent Create(string spec, RulesConfig rules, int seed) =>
        IsKnown(spec)
            ? BotLibrary.Create(spec, rules, seed)
            : throw new SimException($"Unknown bot '{spec}'. Use {Usage}.");
}
