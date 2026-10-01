using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Maps;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;

namespace Fantactics.Client.Logic.Menus;

/// <summary>
/// A new match as the menu (or the launch options) describes it: the map, each seat's controller, races, budget and
/// starting cap, the seed, and whether a bot drafts and places for human seats. <see cref="ToSetup"/> makes the
/// record's setup, writing budgets and caps only where they differ from the rules'.
/// </summary>
/// <param name="Map">The map.</param>
/// <param name="Seats">Each seat's settings.</param>
/// <param name="Seed">Rules seed, or <c>null</c> for a random one.</param>
/// <param name="DraftAs">Bot profile that drafts and places for human seats (skip the draft), or <c>null</c>.</param>
/// <param name="Out">File to keep the match in, or <c>null</c> for the default (see <c>MatchOpener.New</c>).</param>
/// <param name="Teams">Each seat's team, or <c>null</c> for everyone on a team of their own.</param>
public sealed record NewMatchForm(
    string Map,
    ImmutableSortedDictionary<Seat, SeatForm> Seats,
    ulong? Seed,
    string? DraftAs,
    string? Out,
    ImmutableSortedDictionary<Seat, int>? Teams = null)
{
    /// <summary>A human P1 against <c>bot:captain@easy</c> on the first map, under the rules' budget and cap.</summary>
    public static NewMatchForm Defaults(RulesConfig rules) => new(
        MapLibrary.Names.Contains("riverford") ? "riverford" : MapLibrary.Names[0],
        ImmutableSortedDictionary.CreateRange([
            KeyValuePair.Create(Seat.P1, new SeatForm("human", null, rules.DraftBudget, rules.StartingCap)),
            KeyValuePair.Create(Seat.P2, new SeatForm("bot:captain@easy", null, rules.DraftBudget, rules.StartingCap))]),
        null,
        null,
        null);

    /// <summary>Why the match can't start; empty when it can.</summary>
    /// <param name="rules">The rules (for race names).</param>
    /// <param name="isBot">Whether a bot spec names a known bot.</param>
    public IReadOnlyList<string> Problems(RulesConfig rules, Func<string, bool> isBot)
    {
        List<string> problems = [];
        if (!MapLibrary.Names.Contains(Map))
        {
            problems.Add($"Unknown map '{Map}'.");
        }
        else if (MapLibrary.Load(Map) is { } map)
        {
            if (Seats.Count < 2 || Seats.Count > map.Seats)
            {
                problems.Add($"{Map} takes 2 to {map.Seats} players.");
            }

            problems.AddRange(Seats.Keys
                .Where(seat => !map.HasDeployZone(seat))
                .Select(seat => $"{Map} has no deploy zone for {seat}."));
        }

        if (Teams is { } teams
            && Seats.Keys.Select(seat => teams.GetValueOrDefault(seat, seat.OwnTeam())).Distinct().Count() < 2)
        {
            problems.Add("At least two teams must play.");
        }

        foreach ((Seat seat, SeatForm form) in Seats)
        {
            if (SeatController.Parse(form.Controller) is { Kind: SeatControllerKind.Bot, BotSpec: string spec } && !isBot(spec))
            {
                problems.Add($"{seat}: unknown bot '{spec}'.");
            }

            if (form.Races is { } races && (races.IsEmpty || races.Any(race => !rules.Races.ContainsKey(race))))
            {
                problems.Add(races.IsEmpty
                    ? $"{seat}: allow at least one race."
                    : $"{seat}: unknown race in {string.Join(", ", races)}.");
            }

            if (form.Budget <= 0 || form.StartingCap <= 0)
            {
                problems.Add($"{seat}: the budget and starting cap must be positive.");
            }
        }

        if (DraftAs is string profile && !isBot(profile))
        {
            problems.Add($"Unknown bot '{profile}' to draft with.");
        }

        return problems;
    }

    /// <summary>The match setup.</summary>
    /// <param name="rules">The rules (budgets and caps equal to theirs are left out).</param>
    /// <param name="randomSeed">The seed to use when <see cref="Seed"/> isn't set.</param>
    public MatchSetup ToSetup(RulesConfig rules, ulong randomSeed) => new(
        Map,
        Seed ?? randomSeed,
        Seats.ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value.Controller),
        OrNull(Seats
            .Where(pair => pair.Value.Races is not null)
            .ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value.Races ?? [])),
        OrNull(Seats
            .Where(pair => pair.Value.Budget != rules.DraftBudget)
            .ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value.Budget)),
        OrNull(Seats
            .Where(pair => pair.Value.StartingCap != rules.StartingCap)
            .ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value.StartingCap)),
        Teams);

    /// <summary>Changes one seat's settings.</summary>
    public NewMatchForm WithSeat(Seat seat, Func<SeatForm, SeatForm> change) => this with
    {
        Seats = Seats.SetItem(seat, change(Seats[seat])),
    };

    private static ImmutableSortedDictionary<Seat, T>? OrNull<T>(ImmutableSortedDictionary<Seat, T> bySeat) =>
        bySeat.IsEmpty ? null : bySeat;
}
