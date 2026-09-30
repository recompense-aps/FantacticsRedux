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
using Fantactics.Sim.Views;

namespace Fantactics.Sim.Tournaments;

/// <summary>
/// Plays many bot-vs-bot matches in memory and aggregates the results. Matches run in parallel, one per thread;
/// each reduces to a result row plus unit and clash totals as soon as it ends, so memory doesn't grow with game length.
/// Results depend only on the seeds, never on the thread count.
/// </summary>
/// <param name="rules">Rules to play with.</param>
public sealed class TournamentRunner(RulesConfig rules)
{
    private const double Z95 = 1.96;

    /// <summary>The rules tournaments play unless a variant is given.</summary>
    public RulesConfig Rules => rules;

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
        Dictionary<(string A, string B), ClashStats> clashTotals = [];
        ParallelOptions parallel = new() { MaxDegreeOfParallelism = options.Threads <= 0 ? -1 : options.Threads };
        Parallel.For(
            0,
            options.Games,
            parallel,
            () => (Units: new Dictionary<(Seat Seat, string Type), UnitTypeStats>(),
                Clashes: new Dictionary<(string A, string B), ClashStats>()),
            (game, _, local) =>
            {
                results[game] = PlayOne(effective, options, game, local.Units, local.Clashes);
                return local;
            },
            local =>
            {
                lock (totals)
                {
                    foreach (((Seat Seat, string Type) key, UnitTypeStats stats) in local.Units)
                    {
                        totals[key] = Add(totals.GetValueOrDefault(key), stats);
                    }

                    foreach (((string A, string B) key, ClashStats stats) in local.Clashes)
                    {
                        clashTotals[key] = Add(clashTotals.GetValueOrDefault(key), stats);
                    }
                }
            });

        ImmutableArray<GameResult> games = [.. results];
        return (Summarize(effective, options, games, totals, clashTotals), games);
    }

    private static TournamentSummary Summarize(
        RulesConfig rules,
        TournamentOptions options,
        ImmutableArray<GameResult> games,
        Dictionary<(Seat Seat, string Type), UnitTypeStats> totals,
        Dictionary<(string A, string B), ClashStats> clashTotals)
    {
        ImmutableArray<UnitTypeStats> unitStats = totals
            .OrderBy(pair => pair.Key.Seat)
            .ThenBy(pair => pair.Key.Type, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToImmutableArray();
        ImmutableArray<ClashStats> clashes = clashTotals
            .OrderBy(pair => pair.Key.A, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.B, StringComparer.Ordinal)
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

        string p1Label = Label(options.P1Bot, options.P1Races, options.DraftBudgets, Seat.P1);
        string p2Label = Label(options.P2Bot, options.P2Races, options.DraftBudgets, Seat.P2);
        List<(string Races, double Score)> armies = games
            .SelectMany(g => new[]
            {
                (Races: g.P1Races, Score: Outcome(g.Winner, "P1")),
                (Races: g.P2Races, Score: Outcome(g.Winner, "P2")),
            })
            .ToList();
        return new TournamentSummary(
            rules.Hash[..12],
            games.Length,
            p1Label,
            p2Label,
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
                Fingerprint(games, "P1", p1Label),
                Fingerprint(games, "P2", p2Label),
            ],
            unitStats,
            clashes,
            Group(armies, army => Shape(army.Races)),
            Group(armies, army => army.Races));
    }

    /// <summary>The bot, plus its allowed races and draft budget when the tournament sets them.</summary>
    private static string Label(
        string bot,
        ImmutableSortedSet<string>? races,
        ImmutableSortedDictionary<Seat, int>? budgets,
        Seat seat)
    {
        string label = races is null ? $"bot:{bot}" : $"bot:{bot} {string.Join('+', races)}";
        return budgets?.TryGetValue(seat, out int budget) == true ? $"{label} {budget}pt" : label;
    }

    /// <summary>1 for a win, 0.5 for a draw, 0 for a loss.</summary>
    private static double Outcome(string winner, string seat) => winner switch
    {
        "draw" => 0.5,
        _ when winner == seat => 1.0,
        _ => 0.0,
    };

    /// <summary>An army's shape by how many races it drafted (RacesAndUnits §2.4).</summary>
    private static string Shape(string races) => races.Split('+').Length switch
    {
        1 => "mono",
        2 => "two-race",
        _ => "three+",
    };

    private static ImmutableArray<ArmyGroupStats> Group(
        List<(string Races, double Score)> armies,
        Func<(string Races, double Score), string> key) =>
        armies
            .GroupBy(key)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ArmyGroupStats(
                group.Key,
                group.Count(),
                group.Count(army => army.Score == 1.0),
                group.Count(army => army.Score == 0.5)))
            .ToImmutableArray();

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
            Picked = total.Picked + delta.Picked,
            PickedWins = total.PickedWins + delta.PickedWins,
        };

    private static ClashStats Add(ClashStats? total, ClashStats delta) => total is null
        ? delta
        : total with
        {
            Clashes = total.Clashes + delta.Clashes,
            AWins = total.AWins + delta.AWins,
            BWins = total.BWins + delta.BWins,
            Unresolved = total.Unresolved + delta.Unresolved,
        };

    private static IPlayerAgent CreateBot(string name, BotProfile? profile, RulesConfig rules, int seed) =>
        profile is null ? BotFactory.Create(name, rules, seed) : new TacticalAgent(profile, rules, seed);

    private static GameResult PlayOne(
        RulesConfig rules,
        TournamentOptions options,
        int game,
        Dictionary<(Seat Seat, string Type), UnitTypeStats> totals,
        Dictionary<(string A, string B), ClashStats> clashes)
    {
        ulong seed = options.Seed + (ulong)game;
        MatchSetup setup = new(
            options.Map,
            seed,
            new Dictionary<Seat, string> { [Seat.P1] = $"bot:{options.P1Bot}", [Seat.P2] = $"bot:{options.P2Bot}" }
                .ToImmutableSortedDictionary(),
            RaceText.BySeat(options.P1Races, options.P2Races),
            options.DraftBudgets,
            options.StartingCaps);
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

        void CountClash(ClashResolved clash)
        {
            if (!known.TryGetValue(clash.UnitA, out (Seat Seat, string Type) a)
                || !known.TryGetValue(clash.UnitB, out (Seat Seat, string Type) b))
            {
                return;
            }

            // Order the pair so each unordered pair of types has a single row.
            ((int Id, string Type) first, (int Id, string Type) second) =
                string.CompareOrdinal(a.Type, b.Type) <= 0
                    ? ((clash.UnitA, a.Type), (clash.UnitB, b.Type))
                    : ((clash.UnitB, b.Type), (clash.UnitA, a.Type));
            ClashStats delta = new(
                first.Type,
                second.Type,
                1,
                clash.WinnerId == first.Id ? 1 : 0,
                clash.WinnerId == second.Id ? 1 : 0,
                clash.WinnerId is null ? 1 : 0);
            (string, string) key = (first.Type, second.Type);
            clashes[key] = Add(clashes.GetValueOrDefault(key), delta);
        }

        ImmutableArray<(Seat Seat, string Type)>? drafted = null;
        MatchResult result = MatchRunner.Run(
            rules,
            setup,
            agents,
            (state, events) =>
            {
                // Every drafted unit exists, unplaced or in reserve, when placement starts.
                if (drafted is null && state.Phase == Phase.Placement)
                {
                    drafted = [.. state.Units.Values.Select(unit => (unit.Owner, unit.Type)).Distinct()];
                }

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
                        case ClashResolved e:
                            CountClash(e);
                            break;
                    }
                }
            },
            keepRecord: false);

        foreach ((Seat Seat, string Type) pick in drafted ?? [])
        {
            UnitTypeStats delta = new(
                pick.Seat.ToString(),
                pick.Type,
                0,
                0,
                0,
                0,
                Picked: 1,
                PickedWins: result.Outcome.Winner == pick.Seat ? 1 : 0);
            totals[pick] = Add(totals.GetValueOrDefault(pick), delta);
        }

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
            damage[Seat.P2],
            RaceMix(final.Players[Seat.P1]),
            RaceMix(final.Players[Seat.P2]));
    }

    private static string RaceMix(PlayerState player) => string.Join('+', player.DraftedRaces?.Keys ?? []);
}
