namespace Fantactics.Client.Logic.Log;

/// <summary>One line of the player's log.</summary>
/// <param name="Turn">The turn it happened in.</param>
/// <param name="Text">What happened, e.g. <c>P2 Grunt is destroyed by P1 Archer</c>.</param>
public sealed record LogLine(int Turn, string Text);
