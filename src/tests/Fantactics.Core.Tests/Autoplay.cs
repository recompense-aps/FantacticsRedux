using System.Collections.Immutable;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Core.Tests;

/// <summary>Plays a Riverford match on a <see cref="MatchHost"/> with simple seeded choices, for tests that need real mid-match states.</summary>
internal static class Autoplay
{
    /// <summary>Riverford with the open draft; P1 drafts Elves and P2 drafts Goblins (see <see cref="Choose"/>).</summary>
    public static MatchSetup Setup(ulong seed) => new(
        "riverford",
        seed,
        ImmutableSortedDictionary.CreateRange([
            KeyValuePair.Create(Seat.P1, "human"),
            KeyValuePair.Create(Seat.P2, "human")]));

    /// <summary>Starts a match and plays up to <paramref name="decisions"/> decisions (fewer if it ends).</summary>
    public static MatchHost Run(RulesConfig rules, ulong seed, int decisions)
    {
        MatchHost host = new(rules, Setup(seed));
        Continue(host, (int)seed, decisions);
        return host;
    }

    /// <summary>Plays up to <paramref name="decisions"/> more decisions on <paramref name="host"/>.</summary>
    public static void Continue(MatchHost host, int seed, int decisions)
    {
        Random random = new(seed);
        for (int step = 0; step < decisions && !host.IsOver; step++)
        {
            Seat seat = GameEngine.PendingDecisions(host.State)[0].Seat;
            LegalActions legal = host.Snapshot(seat).Legal
                ?? throw new InvalidOperationException($"{seat} owes a decision but has no options.");
            if (host.Submit(seat, Choose(legal, random)) is not null && host.Submit(seat, SubmitMoveOrders.HoldAll) is { } violation)
            {
                throw new InvalidOperationException(violation.Message);
            }
        }
    }

    private static ICommand Choose(LegalActions legal, Random random) => legal.Decision switch
    {
        DraftArmyDecision { Seat: Seat.P1 } =>
            new SubmitDraft(["Archer", "Archer", "Scout"], ["Ranger"]),
        DraftArmyDecision => new SubmitDraft(["Grunt", "Grunt", "Tank"], ["Rusher"]),
        PlaceStartingArmyDecision => Place(legal.Placement ?? throw new InvalidOperationException("No placement.")),
        SubmitMoveOrdersDecision => Move(legal.Moves ?? throw new InvalidOperationException("No moves."), random),
        _ => legal.Actions[random.Next(legal.Actions.Length)].Command,
    };

    private static PlaceStartingArmy Place(PlacementOptions options) =>
        new([.. options.UnitIds.Select((id, index) => new UnitPlacement(id, options.Tiles[index]))]);

    private static SubmitMoveOrders Move(MoveOptions options, Random random)
    {
        IEnumerable<MoveOrder> moves = options.Units
            .Where(unit => unit.Destinations.Length > 0)
            .Select(unit => new MoveOrder(unit.UnitId, unit.Destinations[random.Next(unit.Destinations.Length)].Path));
        return new SubmitMoveOrders([.. moves], []);
    }
}
