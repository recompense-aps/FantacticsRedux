namespace Fantactics.Client.Logic.Log;

/// <summary>One line of the player's log.</summary>
/// <param name="Turn">The turn it happened in.</param>
/// <param name="Text">What happened, e.g. <c>P2 Grunt is destroyed by P1 Archer</c>.</param>
/// <param name="Heading">Whether it starts a turn or phase rather than telling what happened.</param>
public sealed record LogLine(int Turn, string Text, bool Heading = false);
