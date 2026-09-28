namespace Fantactics.Sim.Tournaments;

/// <summary>
/// How one side of a tournament played, averaged per game, so personalities can be compared at a glance.
/// </summary>
/// <param name="Seat">P1 or P2.</param>
/// <param name="Bot">The seat's bot and race.</param>
/// <param name="FirstContact">Mean turn the side first dealt damage, over games where it did.</param>
/// <param name="NoContact">Games where the side never dealt damage.</param>
/// <param name="FirstArrival">Mean turn the side's first reserve arrived, over games where one did.</param>
/// <param name="Objective">Mean objective points.</param>
/// <param name="Damage">Mean damage dealt.</param>
/// <param name="DamageTaken">Mean damage taken.</param>
/// <param name="Destroyed">Mean enemy value destroyed.</param>
public sealed record StyleFingerprint(
    string Seat,
    string Bot,
    double FirstContact,
    int NoContact,
    double FirstArrival,
    double Objective,
    double Damage,
    double DamageTaken,
    double Destroyed);
