namespace Fantactics.Sim.Views;

/// <summary>A tile one of the viewer's units can move to this turn.</summary>
/// <param name="Unit">Unit handle.</param>
/// <param name="X">Column.</param>
/// <param name="Y">Row.</param>
/// <param name="Cost">Movement spent on the cheapest path.</param>
public sealed record ReachRow(string Unit, int X, int Y, int Cost);
