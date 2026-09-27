using System.Collections.Immutable;
using Fantactics.Core.Events;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>The command was legal.</summary>
/// <param name="State">The new state.</param>
/// <param name="Events">What happened, in order.</param>
public sealed record Accepted(GameState State, ImmutableArray<GameEvent> Events) : ApplyResult;
