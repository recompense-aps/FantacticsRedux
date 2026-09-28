using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Playback;

/// <summary>A status starts or ends on a unit.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Status">The status.</param>
/// <param name="Applied">Whether it starts (otherwise it ends).</param>
public sealed record StatusStep(int UnitId, StatusKind Status, bool Applied) : Step;
