using System.Collections.Immutable;

namespace Fantactics.Core.Commands;

/// <summary>A player's hidden orders for the movement phase.</summary>
/// <param name="Moves">Move orders; units without one hold.</param>
/// <param name="Deploys">Reserve arrivals.</param>
public sealed record SubmitMoveOrders(ImmutableArray<MoveOrder> Moves, ImmutableArray<DeployOrder> Deploys) : ICommand
{
    /// <summary>Orders that hold every unit and deploy nothing.</summary>
    public static SubmitMoveOrders HoldAll { get; } = new([], []);
}
