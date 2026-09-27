namespace Fantactics.Sim.Views;

/// <summary>One event, flattened to a uniform shape for TOON tables (Simulation §6.2).</summary>
/// <param name="Seq">Command sequence number.</param>
/// <param name="Turn">Turn.</param>
/// <param name="Type">Event kind, e.g. <c>move</c>, <c>attack</c>, <c>died</c>.</param>
/// <param name="Actor">Acting unit handle or seat; empty if none.</param>
/// <param name="Target">Target unit handle; empty if none.</param>
/// <param name="Detail">Short free-text detail.</param>
public sealed record EventLine(int Seq, int Turn, string Type, string Actor, string Target, string Detail);
