namespace Fantactics.Sim.Views;

/// <summary>One visible unit.</summary>
/// <param name="Id">Handle: uppercase for the viewer's units, lowercase for the enemy's.</param>
/// <param name="Side">
/// <c>you</c> or <c>enemy</c>; with more than two seats, the owner and relation, e.g. <c>P3 enemy</c> or
/// <c>P3 ally</c>.
/// </param>
/// <param name="Type">Unit type.</param>
/// <param name="X">Column.</param>
/// <param name="Y">Row.</param>
/// <param name="Tile">Terrain under the unit.</param>
/// <param name="Hp">Current HP.</param>
/// <param name="MaxHp">Maximum HP.</param>
/// <param name="Atk">Attack.</param>
/// <param name="Def">Defense.</param>
/// <param name="Mov">Movement points this turn (after Slowed/Rooted).</param>
/// <param name="Rng">Attack range, e.g. <c>2-3</c>.</param>
/// <param name="Init">Initiative this turn (effective, once the action order is set).</param>
/// <param name="Status">Statuses and turn flags, space-separated (e.g. <c>rooted:3 held</c>); empty if none.</param>
public sealed record UnitRow(
    string Id,
    string Side,
    string Type,
    int X,
    int Y,
    string Tile,
    int Hp,
    int MaxHp,
    int Atk,
    int Def,
    int Mov,
    string Rng,
    int Init,
    string Status);
