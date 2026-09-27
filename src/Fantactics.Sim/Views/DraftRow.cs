namespace Fantactics.Sim.Views;

/// <summary>A unit type available in the draft.</summary>
/// <param name="Type">Unit type.</param>
/// <param name="Cost">Draft cost.</param>
/// <param name="Hp">HP.</param>
/// <param name="Atk">Attack.</param>
/// <param name="Def">Defense.</param>
/// <param name="Mov">Movement.</param>
/// <param name="Rng">Range.</param>
/// <param name="Init">Initiative.</param>
/// <param name="Unique">At most one per army.</param>
/// <param name="Traits">Traits and abilities, space-separated.</param>
public sealed record DraftRow(
    string Type,
    int Cost,
    int Hp,
    int Atk,
    int Def,
    int Mov,
    string Rng,
    int Init,
    bool Unique,
    string Traits);
