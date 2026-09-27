using Fantactics.Core.State;

namespace Fantactics.Core.Records;

/// <summary>The result of replaying a match record.</summary>
/// <param name="State">Final state.</param>
/// <param name="DriftAtSeq">First command whose state hash differed from the record, or <c>null</c>.</param>
public sealed record ReplayResult(GameState State, int? DriftAtSeq);
