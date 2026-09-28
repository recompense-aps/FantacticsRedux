using Fantactics.Core;
using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Playback;

/// <summary>A unit appears on the field (placed, arriving from reserve, or summoned).</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Type">Its type.</param>
/// <param name="Tile">Where it appears.</param>
public sealed record AppearStep(int UnitId, Seat Owner, string Type, Point Tile) : Step;
