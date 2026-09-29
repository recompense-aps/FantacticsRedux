using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>The pre-match draft (GameDesign §4.4).</summary>
internal static class DraftRules
{
    /// <summary>Throws a <see cref="RuleViolationException"/> unless the draft is legal for the seat.</summary>
    public static void Validate(GameState state, Seat seat, SubmitDraft draft)
    {
        PlayerState player = state.Players[seat];
        List<string> all = [.. draft.Starting, .. draft.Reserve];
        foreach (string type in all)
        {
            RuleViolationException.ThrowUnless(
                state.Rules.Units.TryGetValue(type, out UnitDefinition? definition),
                "unknown-unit",
                $"'{type}' is not a unit type.");
            RuleViolationException.ThrowUnless(
                player.MayDraft(definition.Race),
                "race-not-allowed",
                $"'{type}' is a {definition.Race} unit; this army may only draft "
                    + $"{string.Join(", ", player.AllowedRaces ?? [])}.");
        }

        RuleViolationException.ThrowUnless(
            !draft.Starting.IsEmpty,
            "empty-starting-army",
            "The starting army needs at least one unit.");

        int startingCost = draft.Starting.Sum(type => state.Rules.Units[type].Cost);
        RuleViolationException.ThrowUnless(
            startingCost <= player.StartingCapUnder(state.Rules),
            "over-starting-cap",
            $"The starting army costs {startingCost}; the cap is {player.StartingCapUnder(state.Rules)}.");

        int totalCost = all.Sum(type => state.Rules.Units[type].Cost);
        RuleViolationException.ThrowUnless(
            totalCost <= player.BudgetUnder(state.Rules),
            "over-budget",
            $"The draft costs {totalCost}; the budget is {player.BudgetUnder(state.Rules)}.");

        string? duplicateUnique = all
            .Where(type => state.Rules.Units[type].Unique)
            .GroupBy(type => type)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        RuleViolationException.ThrowUnless(
            duplicateUnique is null,
            "duplicate-unique",
            $"'{duplicateUnique}' is unique: at most one per army.");
    }

    /// <summary>Creates every drafted unit once both drafts are in, then moves on to placement.</summary>
    public static GameState Resolve(GameState state)
    {
        int nextId = state.NextUnitId;
        ImmutableSortedDictionary<int, Unit>.Builder units = state.Units.ToBuilder();
        foreach (Seat seat in SeatExtensions.All)
        {
            var draft = (SubmitDraft)state.PendingOrders[seat];
            IEnumerable<(string Type, UnitLocation Location)> drafted = draft.Starting
                .Select(type => (type, UnitLocation.Unplaced))
                .Concat(draft.Reserve.Select(type => (type, UnitLocation.Reserve)));
            foreach ((string type, UnitLocation location) in drafted)
            {
                units[nextId] = Unit.Create(nextId, seat, type, location, default(Point), state.Rules.Units[type].Hp);
                nextId++;
            }
        }

        ImmutableSortedDictionary<int, Unit> created = units.ToImmutable();
        ImmutableSortedDictionary<Seat, PlayerState> players = state.Players.Values.ToImmutableSortedDictionary(
            player => player.Seat,
            player =>
            {
                var draft = (SubmitDraft)state.PendingOrders[player.Seat];
                return player with { DraftedRaces = CountRaces(state.Rules, [.. draft.Starting, .. draft.Reserve]) };
            });
        return state with
        {
            Units = created,
            Players = players,
            Owners = state.Owners.SetItems(created.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Owner))),
            NextUnitId = nextId,
            PendingOrders = ImmutableSortedDictionary<Seat, ICommand>.Empty,
            Phase = Phase.Placement,
        };
    }

    /// <summary>Counts <paramref name="unitTypes"/> per race (the draft reveal, GameDesign §4.4).</summary>
    public static ImmutableSortedDictionary<string, int> CountRaces(RulesConfig rules, IEnumerable<string> unitTypes) =>
        unitTypes
            .GroupBy(type => rules.Units[type].Race)
            .ToImmutableSortedDictionary(group => group.Key, group => group.Count());
}
