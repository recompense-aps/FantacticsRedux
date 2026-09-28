namespace Fantactics.Core.Engine;

/// <summary>Public totals for one player (shown in the HUD, GameDesign §4.5).</summary>
/// <param name="Seat">The player.</param>
/// <param name="Race">Race identifier.</param>
/// <param name="Command">Unspent Command.</param>
/// <param name="DestroyedValue">Enemy value destroyed.</param>
/// <param name="ObjectivePoints">Points scored for holding objectives.</param>
/// <param name="ArmyValue">Value on the field plus reserve.</param>
/// <param name="ReserveValue">Value of the undeployed reserve (its composition stays hidden).</param>
/// <param name="OrdersLocked">Whether the player locked in hidden orders for the current phase.</param>
public sealed record PlayerSummary(
    Seat Seat,
    string Race,
    int Command,
    int DestroyedValue,
    int ObjectivePoints,
    int ArmyValue,
    int ReserveValue,
    bool OrdersLocked);
