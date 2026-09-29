using System.Collections.Immutable;
using Fantactics.Ai;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Tests;

/// <summary>Real mid-match states, taken from bot matches, for fuzzing the input builders.</summary>
internal static class States
{
    /// <summary>Riverford with the open draft and the given seat labels.</summary>
    public static MatchSetup Setup(ulong seed, string p1 = "human", string p2 = "human") => new(
        "riverford",
        seed,
        ImmutableSortedDictionary.CreateRange([KeyValuePair.Create(Seat.P1, p1), KeyValuePair.Create(Seat.P2, p2)]));

    /// <summary>Creates a bot, as hosts do.</summary>
    public static IPlayerAgent CreateBot(string spec, int seed) => BotFactory.Create(spec, RulesConfig.Default, seed);

    /// <summary>Every (state, seat that owes a decision) along a captain-vs-berserker match.</summary>
    public static IEnumerable<(GameState State, Seat Seat)> Along(ulong seed)
    {
        MatchHost host = new(RulesConfig.Default, Setup(seed));
        Dictionary<Seat, IPlayerAgent> bots = new()
        {
            [Seat.P1] = CreateBot("captain", (int)seed),
            [Seat.P2] = CreateBot("berserker", (int)seed + 1),
        };
        while (!host.IsOver)
        {
            Seat seat = GameEngine.PendingDecisions(host.State)[0].Seat;
            yield return (host.State, seat);
            host.SubmitEngine(seat, bots[seat].DecideFor(host.State, seat));
        }
    }
}
