using System.Collections.Immutable;

namespace Fantactics.Core.Events;

/// <summary>The action order for this turn was revealed (GameDesign §4.1).</summary>
/// <param name="Order">Units in the order they act.</param>
public sealed record InitiativeOrdered(ImmutableArray<int> Order) : GameEvent;
