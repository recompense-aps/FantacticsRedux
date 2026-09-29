namespace Fantactics.Sim.Views;

/// <summary>A unit type available in the draft.</summary>
/// <param name="Type">Unit type.</param>
/// <param name="Race">The unit's race.</param>
/// <param name="Cost">Draft cost.</param>
/// <param name="Hp">HP.</param>
/// <param name="Atk">Attack.</param>
/// <param name="Def">Defense.</param>
/// <param name="Mov">Movement.</param>
/// <param name="Rng">Range.</param>
/// <param name="Init">Initiative.</param>
/// <param name="Unique">At most one per army.</param>
/// <param name="Classes">Class tags, space-separated (RacesAndUnits §2.4).</param>
/// <param name="Traits">Traits (including race traits) and abilities, space-separated.</param>
public sealed record DraftRow(
    string Type,
    string Race,
    int Cost,
    int Hp,
    int Atk,
    int Def,
    int Mov,
    string Rng,
    int Init,
    bool Unique,
    string Classes,
    string Traits);
