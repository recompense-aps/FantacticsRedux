using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fantactics.Ai;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;

namespace Fantactics.Sim.Tournaments;

/// <summary>Plays many bot-vs-bot matches in memory and aggregates the results.</summary>
/// <param name="rules">Rules to play with.</param>
public sealed class TournamentRunner(RulesConfig rules)
{
    /// <summary>Plays the tournament.</summary>
    /// <param name="options">What to play.</param>
    /// <param name="variant">Rules to use instead of the default, e.g. from <c>run --rules</c>.</param>
    /// <returns>The summary and every match's result, in game order.</returns>
    public (TournamentSummary Summary, ImmutableArray<GameResult> Games) Run(
        TournamentOptions options,
        RulesConfig? variant = null)
    {
        RulesConfig effective = variant ?? rules;
        ConcurrentBag<(GameResult Result, List<(Seat Seat, string Type, UnitTypeStats Delta)> Stats)> results = [];
        ParallelOptions parallel = new() { MaxDegreeOfParallelism = options.Parallel ? -1 : 1 };
        Parallel.For(0, options.Games, parallel, game => results.Add(PlayOne(effective, options, game)));

        ImmutableArray<GameResult> games = [.. results.Select(r => r.Result).OrderBy(r => r.Game)];
        ImmutableArray<UnitTypeStats> unitStats = results
            .SelectMany(r => r.Stats)
            .GroupBy(s => (s.Seat, s.Type))
            .OrderBy(g => g.Key.Seat)
            .ThenBy(g => g.Key.Type, StringComparer.Ordinal)
            .Select(g => new UnitTypeStats(
                g.Key.Seat.ToString(),
                g.Key.Type,
                g.Sum(s => s.Delta.Fielded),
                g.Sum(s => s.Delta.Damage),
                g.Sum(s => s.Delta.Kills),
                g.Sum(s => s.Delta.Deaths)))
            .ToImmutableArray();
        ImmutableArray<EndReasonCount> endReasons = games
            .GroupBy(g => g.Winner == "draw" ? $"{g.Reason}: draw" : $"{g.Reason}: {g.Winner} wins")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new EndReasonCount(g.Key, g.Count()))
            .ToImmutableArray();

        TournamentSummary summary = new(
            effective.Hash[..12],
            games.Length,
            $"bot:{options.P1Bot} {options.P1Race}",
            $"bot:{options.P2Bot} {options.P2Race}",
            games.Count(g => g.Winner == "P1"),
            games.Count(g => g.Winner == "P2"),
            games.Count(g => g.Winner == "draw"),
            games.IsEmpty ? 0 : games.Average(g => g.Turns),
            endReasons,
            unitStats);
        return (summary, games);
    }

    private static (GameResult Result, List<(Seat Seat, string Type, UnitTypeStats Delta)> Stats) PlayOne(
        RulesConfig rules,
        TournamentOptions options,
        int game)
    {
        ulong seed = options.Seed + (ulong)game;
        MatchSetup setup = new(
            options.Map,
            new Dictionary<Seat, string> { [Seat.P1] = options.P1Race, [Seat.P2] = options.P2Race }
                .ToImmutableSortedDictionary(),
            seed,
            new Dictionary<Seat, string> { [Seat.P1] = $"bot:{options.P1Bot}", [Seat.P2] = $"bot:{options.P2Bot}" }
                .ToImmutableSortedDictionary());
        Dictionary<Seat, IPlayerAgent> agents = new()
        {
            [Seat.P1] = BotFactory.Create(options.P1Bot, unchecked((int)(seed * 2))),
            [Seat.P2] = BotFactory.Create(options.P2Bot, unchecked((int)(seed * 2 + 1))),
        };

        Dictionary<int, (Seat Seat, string Type)> known = [];
        List<(Seat Seat, string Type, UnitTypeStats Delta)> stats = [];
        void Count(int unitId, int fielded = 0, int damage = 0, int kills = 0, int deaths = 0)
        {
            if (known.TryGetValue(unitId, out (Seat Seat, string Type) unit))
            {
                stats.Add((unit.Seat, unit.Type, new UnitTypeStats("", "", fielded, damage, kills, deaths)));
            }
        }

        MatchResult result = MatchRunner.Run(rules, setup, agents, (_, events) =>
        {
            foreach (GameEvent gameEvent in events)
            {
                switch (gameEvent)
                {
                    case UnitPlaced e:
                        known[e.UnitId] = (e.Owner, e.Type);
                        Count(e.UnitId, fielded: 1);
                        break;
                    case UnitArrived e:
                        known[e.UnitId] = (e.Owner, e.Type);
                        Count(e.UnitId, fielded: 1);
                        break;
                    case UnitSummoned e:
                        known[e.UnitId] = (e.Owner, e.Type);
                        Count(e.UnitId, fielded: 1);
                        break;
                    case UnitAttacked e:
                        Count(e.AttackerId, damage: e.Damage);
                        break;
                    case UnitDied e:
                        Count(e.KillerId, kills: 1);
                        Count(e.UnitId, deaths: 1);
                        break;
                }
            }
        });

        GameState final = result.FinalState;
        GameResult row = new(
            game,
            seed,
            result.Outcome.Winner?.ToString() ?? "draw",
            result.Outcome.Reason.ToString(),
            result.Turns,
            UnitRules.ArmyValue(final, Seat.P1),
            UnitRules.ArmyValue(final, Seat.P2),
            final.Players[Seat.P1].DestroyedValue,
            final.Players[Seat.P2].DestroyedValue,
            final.Players[Seat.P1].ObjectivePoints,
            final.Players[Seat.P2].ObjectivePoints);
        return (row, stats);
    }
}
