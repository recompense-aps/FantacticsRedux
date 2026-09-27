using System.Text.Json.Serialization;

namespace Fantactics.Core.Events;

/// <summary>
/// A fact produced by applying a command. Events are fine-grained (one per movement tick or strike) so clients can
/// animate them and logs show exactly what happened (TechnicalDesign §2.2).
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(OrdersLocked), nameof(OrdersLocked))]
[JsonDerivedType(typeof(UnitPlaced), nameof(UnitPlaced))]
[JsonDerivedType(typeof(TurnStarted), nameof(TurnStarted))]
[JsonDerivedType(typeof(CommandGained), nameof(CommandGained))]
[JsonDerivedType(typeof(UnitArrived), nameof(UnitArrived))]
[JsonDerivedType(typeof(ArrivalCancelled), nameof(ArrivalCancelled))]
[JsonDerivedType(typeof(UnitStepped), nameof(UnitStepped))]
[JsonDerivedType(typeof(UnitStopped), nameof(UnitStopped))]
[JsonDerivedType(typeof(ClashMarked), nameof(ClashMarked))]
[JsonDerivedType(typeof(ClashAvoided), nameof(ClashAvoided))]
[JsonDerivedType(typeof(ClashResolved), nameof(ClashResolved))]
[JsonDerivedType(typeof(UnitAttacked), nameof(UnitAttacked))]
[JsonDerivedType(typeof(UnitHealed), nameof(UnitHealed))]
[JsonDerivedType(typeof(StatusApplied), nameof(StatusApplied))]
[JsonDerivedType(typeof(StatusRemoved), nameof(StatusRemoved))]
[JsonDerivedType(typeof(UnitDied), nameof(UnitDied))]
[JsonDerivedType(typeof(InitiativeOrdered), nameof(InitiativeOrdered))]
[JsonDerivedType(typeof(UnitWaited), nameof(UnitWaited))]
[JsonDerivedType(typeof(UnitDelayed), nameof(UnitDelayed))]
[JsonDerivedType(typeof(AbilityUsed), nameof(AbilityUsed))]
[JsonDerivedType(typeof(TileChanged), nameof(TileChanged))]
[JsonDerivedType(typeof(UnitSummoned), nameof(UnitSummoned))]
[JsonDerivedType(typeof(TurnEnded), nameof(TurnEnded))]
[JsonDerivedType(typeof(MatchEnded), nameof(MatchEnded))]
public abstract record GameEvent;
