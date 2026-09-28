namespace Fantactics.Client.Logic.Playback;

/// <summary>A unit is healed.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Amount">HP restored.</param>
/// <param name="HpAfter">Its HP afterwards.</param>
public sealed record HealStep(int UnitId, int Amount, int HpAfter) : Step;
