using System.Text.Json.Serialization;

namespace Fantactics.Core.Commands;

/// <summary>
/// A player's intent, answering one pending decision. Serialized polymorphically with a <c>$type</c> discriminator
/// so match records and the network carry the same form (TechnicalDesign §2.2).
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(SubmitDraft), nameof(SubmitDraft))]
[JsonDerivedType(typeof(PlaceStartingArmy), nameof(PlaceStartingArmy))]
[JsonDerivedType(typeof(SubmitMoveOrders), nameof(SubmitMoveOrders))]
[JsonDerivedType(typeof(Attack), nameof(Attack))]
[JsonDerivedType(typeof(UseAbility), nameof(UseAbility))]
[JsonDerivedType(typeof(Wait), nameof(Wait))]
[JsonDerivedType(typeof(Delay), nameof(Delay))]
public interface ICommand
{
}
