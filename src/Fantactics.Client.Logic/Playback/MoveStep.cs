using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Playback;

/// <summary>A unit steps one tile.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="From">Where it was.</param>
/// <param name="To">Where it steps.</param>
public sealed record MoveStep(int UnitId, Point From, Point To) : Step;
