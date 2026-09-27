namespace Fantactics.Sim.Views;

/// <summary>A failed command.</summary>
/// <param name="Result">Always <c>error</c>.</param>
/// <param name="Code">Stable code, e.g. <c>out-of-range</c> or <c>bad-orders</c>.</param>
/// <param name="Message">What went wrong.</param>
public sealed record ErrorResult(string Result, string Code, string Message);
