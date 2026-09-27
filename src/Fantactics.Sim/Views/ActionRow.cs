namespace Fantactics.Sim.Views;

/// <summary>One numbered action option; submit it with <c>act --pick N</c>.</summary>
/// <param name="Pick">Number to pass to <c>--pick</c>.</param>
/// <param name="Action">attack, ability name, wait, or delay.</param>
/// <param name="Target">Target unit handle; empty if none.</param>
/// <param name="Tile">Target tile as <c>x,y</c>; empty if none.</param>
/// <param name="Dmg">Exact damage for attacks; 0 otherwise.</param>
/// <param name="Kills">Whether an attack kills.</param>
public sealed record ActionRow(int Pick, string Action, string Target, string Tile, int Dmg, bool Kills);
