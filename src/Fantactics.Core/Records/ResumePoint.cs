using System.Collections.Immutable;
using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>Where a loaded match continues from (see <see cref="MatchResume"/>).</summary>
/// <param name="Start">The position the kept commands start from.</param>
/// <param name="Custom">Whether <paramref name="Start"/> is a saved position rather than the setup's starting state.</param>
/// <param name="Commands">The history that still replays; empty when play continues from the snapshot.</param>
/// <param name="Events">The events the kept commands produced, in order.</param>
/// <param name="State">The state after the kept commands: where play continues.</param>
/// <param name="Warning">Why the history was dropped, or <c>null</c> if it was kept.</param>
public sealed record ResumePoint(
    GameState Start,
    bool Custom,
    ImmutableArray<RecordedCommand> Commands,
    ImmutableArray<LoggedEvent> Events,
    GameState State,
    string? Warning);
