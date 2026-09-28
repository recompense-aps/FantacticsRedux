using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Playback;

/// <summary>A unit uses an ability.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Ability">The ability.</param>
/// <param name="Target">Its target tile, if any.</param>
public sealed record AbilityStep(int UnitId, string Ability, Point? Target) : Step;
