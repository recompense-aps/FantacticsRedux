using System.Text.Json.Serialization;

namespace Fantactics.Core.Engine;

/// <summary>Something the game is waiting for a seat to decide (Simulation §2).</summary>
/// <param name="Seat">The seat that must decide.</param>
[JsonPolymorphic]
[JsonDerivedType(typeof(DraftArmyDecision), "DraftArmy")]
[JsonDerivedType(typeof(PlaceStartingArmyDecision), "PlaceStartingArmy")]
[JsonDerivedType(typeof(SubmitMoveOrdersDecision), "SubmitMoveOrders")]
[JsonDerivedType(typeof(ChooseUnitActionDecision), "ChooseUnitAction")]
public abstract record Decision(Seat Seat);
