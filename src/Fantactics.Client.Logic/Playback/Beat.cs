using System.Collections.Immutable;

namespace Fantactics.Client.Logic.Playback;

/// <summary>Steps that play at the same time, e.g. every unit's step in one movement tick.</summary>
/// <param name="Steps">The steps.</param>
public sealed record Beat(ImmutableArray<Step> Steps);
