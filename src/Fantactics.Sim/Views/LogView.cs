using System.Collections.Immutable;

namespace Fantactics.Sim.Views;

/// <summary>An event history (the <c>log</c> and <c>replay</c> commands).</summary>
/// <param name="Events">Events in order.</param>
public sealed record LogView(ImmutableArray<EventLine> Events);
