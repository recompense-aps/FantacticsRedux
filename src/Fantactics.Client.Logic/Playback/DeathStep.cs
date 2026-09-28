namespace Fantactics.Client.Logic.Playback;

/// <summary>A unit dies.</summary>
/// <param name="UnitId">The unit.</param>
public sealed record DeathStep(int UnitId) : Step;
