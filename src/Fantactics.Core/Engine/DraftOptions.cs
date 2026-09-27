using System.Collections.Immutable;

namespace Fantactics.Core.Engine;

/// <summary>What a seat may draft.</summary>
/// <param name="Units">Unit types of the seat's race.</param>
/// <param name="Budget">Total draft budget.</param>
/// <param name="StartingCap">Maximum Cost of the starting army.</param>
public sealed record DraftOptions(ImmutableArray<DraftUnitOption> Units, int Budget, int StartingCap);
