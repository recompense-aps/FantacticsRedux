namespace Fantactics.Sim.Views;

/// <summary>One of the viewer's units off the map.</summary>
/// <param name="Id">Handle.</param>
/// <param name="Type">Unit type.</param>
/// <param name="Where">reserve or unplaced.</param>
/// <param name="Cost">Draft cost.</param>
/// <param name="DeployCost">Command needed to deploy it.</param>
public sealed record ReserveRow(string Id, string Type, string Where, int Cost, int DeployCost);
