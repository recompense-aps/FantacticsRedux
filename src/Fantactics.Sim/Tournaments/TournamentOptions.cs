namespace Fantactics.Sim.Tournaments;

/// <summary>What a tournament plays.</summary>
/// <param name="Map">Built-in map name.</param>
/// <param name="P1Race">P1's race.</param>
/// <param name="P2Race">P2's race.</param>
/// <param name="P1Bot">P1's bot name.</param>
/// <param name="P2Bot">P2's bot name.</param>
/// <param name="Games">Number of matches.</param>
/// <param name="Seed">Seed of the first match; match <c>i</c> uses <c>Seed + i</c>.</param>
/// <param name="Parallel">Whether to play matches on multiple threads.</param>
public sealed record TournamentOptions(
    string Map,
    string P1Race,
    string P2Race,
    string P1Bot,
    string P2Bot,
    int Games,
    ulong Seed,
    bool Parallel);
