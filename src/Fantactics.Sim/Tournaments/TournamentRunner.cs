using System.Collections.Immutable;
using Fantactics.Ai;
using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;
using Fantactics.Sim.Matches;

namespace Fantactics.Sim.Tournaments;

/// <summary>
/// Plays many bot-vs-bot matches in memory and aggregates the results. Matches run in parallel, one per thread;
/// each reduces to a result row and unit-stat totals as soon as it ends, so memory doesn't grow with game length.
/// Results depend only on the seeds, never on the thread count.
/// </summary>
/// <param name="rules">Rules to play with.</param>
public sealed class TournamentRunner(RulesConfig rules)
{
    private const double Z95 = 1.96;

    /// <summary>Plays the tournament.</summary>
    /// <param name="options">What to play.</param>
    /// <param name="variant">Rules to use instead of the default, e.g. from <c>run --rules</c>.</param>
    /// <returns>The summary and every match's result, in game order.</returns>
    public (TournamentSummary Summary, ImmutableArray<GameResult> Games) Run(
        TournamentOptions options,
        RulesConfig? variant = null)
    {
        RulesConfig effective = variant ?? rules;
        GameResult[] results = new GameResult[options.Games];
        Dictionary<(Seat Seat, string Type), UnitTypeStats> totals = [];
        ParallelOptions parallel = new() { MaxDegreeOfParallelism = options.Threads <= 0 ? -1 : options.Threads };
        Parallel.For(
            0,
            options.Games,
            parallel,
            () => new Dictionary<(Seat Seat, string Type), UnitTypeStats>(),
            (game, _, local) =>
            {
                results[game] = PlayOne(effective, options, game, local);
                return local;
            },
            local =>
            {
                lock (totals)
                {
                    foreach (((Seat Seat, string Type) key, UnitTypeStats stats) in local)
                    {
                        totals[key] = Add(totals.GetValueOrDefault(key), stats);
                    }
                }
            });

        ImmutableArray<GameResult> games = [.. results];
        return (Summarize(effective, options, games, totals), games);
    }

    private static TournamentSummary Summarize(
        RulesConfig rules,
        TournamentOptions options,
        ImmutableArray<GameResult> games,
        Dictionary<(Seat Seat, string Type), UnitTypeStats> totals)
    {
        ImmutableArray<UnitTypeStats> unitStats = totals
            .OrderBy(pair => pair.Key.Seat)
            .ThenBy(pair => pair.Key.Type, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToImmutableArray();
        ImmutableArray<EndReasonCount> endReasons = games
            .GroupBy(g => g.Winner == "draw" ? $"{g.Reason}: draw" : $"{g.Reason}: {g.Winner} wins")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new EndReasonCount(g.Key, g.Count()))
            .ToImmutableArray();

        double score = games.IsEmpty
            ? 0.5
            : games.Average(g => g.Winner switch
            {
                "P1" => 1.0,
                "P2" => 0.0,
                _ => 0.5,
            });
        (double low, double high) = WilsonInterval(score, games.Length);

        return new TournamentSummary(
            rules.Hash[..12],
            games.Length,
            $"bot:{options.P1Bot} {options.P1Race}",
            $"bot:{options.P2Bot} {options.P2Race}",
            games.Count(g => g.Winner == "P1"),
            games.Count(g => g.Winner == "P2"),
            games.Count(g => g.Winner == "draw"),
            Math.Round(score, 3),
            Math.Round(low, 3),
            Math.Round(high, 3),
            low > 0.5 || high < 0.5,
            games.IsEmpty ? 0 : Math.Round(games.Average(g => g.Turns), 2),
            endReasons,
            [
                Fingerprint(games, "P1", $"bot:{options.P1Bot} {options.P1Race}"),
                Fingerprint(games, "P2", $"bot:{options.P2Bot} {options.P2Race}"),
            ],
            unitStats);
    }

    private static StyleFingerprint Fingerprint(ImmutableArray<GameResult> games, string seat, string bot)
    {
        bool p1 = seat == "P1";
        List<int> contacts = games
            .Select(g => p1 ? g.P1FirstContact : g.P2FirstContact)
            .Where(turn => turn > 0)
            .ToList();
        List<int> arrivals = games
            .Select(g => p1 ? g.P1FirstArrival : g.P2FirstArrival)
            .Where(turn => turn > 0)
            .ToList();
        double Mean(IEnumerable<int> values) => values.Any() ? Math.Round(values.Average(), 2) : 0;
        return new StyleFingerprint(
            seat,
            bot,
            Mean(contacts),
            games.Length - contacts.Count,
            Mean(arrivals),
            Mean(games.Select(g => p1 ? g.P1Objective : g.P2Objective)),
            Mean(games.Select(g => p1 ? g.P1Damage : g.P2Damage)),
            Mean(games.Select(g => p1 ? g.P2Damage : g.P1Damage)),
            Mean(games.Select(g => p1 ? g.P1Destroyed : g.P2Destroyed)));
    }

    /// <summary>
    /// The 95% Wilson score interval for a rate over <paramref name="games"/> games. Unlike the plain normal
    /// interval, it doesn't collapse to zero width when one side wins every game.
    /// </summary>
    private static (double Low, double High) WilsonInterval(double rate, int games)
    {
        if (games == 0)
        {
            return (0, 1);
        }

        double z2 = Z95 * Z95;
        double denominator = 1 + z2 / games;
        double center = (rate + z2 / (2 * games)) / denominator;
        double half = Z95 * Math.Sqrt(rate * (1 - rate) / games + z2 / (4.0 * games * games)) / denominator;
        return (Math.Max(0, center - half), Math.Min(1, center + half));
    }

    private static UnitTypeStats Add(UnitTypeStats? total, UnitTypeStats delta) => total is null
        ? delta
        : total with
        {
            Fielded = total.Fielded + delta.Fielded,
            Damage = total.Damage + delta.Damage,
            Kills = total.Kills + delta.Kills,
            Deaths = total.Deaths + delta.Deaths,
        };

    private static IPlayerAgent CreateBot(string name, BotProfile? profile, RulesConfig rules, int seed) =>
        profile is null ? BotFactory.Create(name, rules, seed) : new TacticalAgent(profile, rules, seed);

    private static GameResult PlayOne(
        RulesConfig rules,
        TournamentOptions options,
        int game,
        Dictionary<(Seat Seat, string Type), UnitTypeStats> totals)
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
            [Seat.P1] = CreateBot(options.P1Bot, options.P1Profile, rules, unchecked((int)(seed * 2))),
            [Seat.P2] = CreateBot(options.P2Bot, options.P2Profile, rules, unchecked((int)(seed * 2 + 1))),
        };

        Dictionary<int, (Seat Seat, string Type)> known = [];
        Dictionary<Seat, int> firstContact = [];
        Dictionary<Seat, int> firstArrival = [];
        Dictionary<Seat, int> damage = new() { [Seat.P1] = 0, [Seat.P2] = 0 };
        int turn = 0;
        void Count(int unitId, int fielded = 0, int damage = 0, int kills = 0, int deaths = 0)
        {
            if (known.TryGetValue(unitId, out (Seat Seat, string Type) unit))
            {
                UnitTypeStats delta = new(unit.Seat.ToString(), unit.Type, fielded, damage, kills, deaths);
                totals[unit] = Add(totals.GetValueOrDefault(unit), delta);
            }
        }

        MatchResult result = MatchRunner.Run(
            rules,
            setup,
            agents,
            (_, events) =>
            {
                foreach (GameEvent gameEvent in events)
                {
                    switch (gameEvent)
                    {
                        case UnitPlaced e:
                            known[e.UnitId] = (e.Owner, e.Type);
                            Count(e.UnitId, fielded: 1);
                            break;
                        case TurnStarted e:
                            turn = e.Turn;
                            break;
                        case UnitArrived e:
                            known[e.UnitId] = (e.Owner, e.Type);
                            firstArrival.TryAdd(e.Owner, turn);
                            Count(e.UnitId, fielded: 1);
                            break;
                        case UnitSummoned e:
                            known[e.UnitId] = (e.Owner, e.Type);
                            Count(e.UnitId, fielded: 1);
                            break;
                        case UnitAttacked e:
                            Count(e.AttackerId, damage: e.Damage);
                            if (known.TryGetValue(e.AttackerId, out (Seat Seat, string Type) attacker))
                            {
                                firstContact.TryAdd(attacker.Seat, turn);
                                damage[attacker.Seat] += e.Damage;
                            }

                            break;
                        case UnitDied e:
                            Count(e.KillerId, kills: 1);
                            Count(e.UnitId, deaths: 1);
                            break;
                    }
                }
            },
            keepRecord: false);

        GameState final = result.FinalState;
        return new GameResult(
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
            final.Players[Seat.P2].ObjectivePoints,
            firstContact.GetValueOrDefault(Seat.P1),
            firstContact.GetValueOrDefault(Seat.P2),
            firstArrival.GetValueOrDefault(Seat.P1),
            firstArrival.GetValueOrDefault(Seat.P2),
            damage[Seat.P1],
            damage[Seat.P2]);
    }
}
