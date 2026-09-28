using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Playback;

/// <summary>Two units meet in a clash.</summary>
/// <param name="UnitA">One unit.</param>
/// <param name="UnitB">The other.</param>
/// <param name="Tile">The contested tile, if they fought over one.</param>
public sealed record ClashStep(int UnitA, int UnitB, Point? Tile) : Step;
