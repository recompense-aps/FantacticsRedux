namespace Fantactics.Client.Logic.Board;

/// <summary>One entry in the unit info panel: a status, ability, or trait.</summary>
/// <param name="Title">Its name and numbers, e.g. <c>Pinning Shot · attack range · Cooldown 2 · ready</c>.</param>
/// <param name="Description">What it does in one line, or empty when there's no text for it.</param>
public sealed record InfoLine(string Title, string Description);
