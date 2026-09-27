namespace Fantactics.Sim.Views;

/// <summary>A reserve unit that can arrive this turn.</summary>
/// <param name="Unit">Unit handle.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Cost">Command it costs.</param>
/// <param name="Tiles">Legal arrival tiles as space-separated <c>x,y</c>.</param>
public sealed record DeployRow(string Unit, string Type, int Cost, string Tiles);
