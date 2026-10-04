using System.Collections.Immutable;
using Fantactics.Client.Common;
using Fantactics.Client.Logic.Board;
using Fantactics.Core.Geometry;
using Godot;

namespace Fantactics.Client.Match.Board;

/// <summary>Draws tile highlights and order arrows above the terrain and below the units.</summary>
public partial class BoardOverlay : Node2D
{
    private static readonly Color _objective = new(1, 0.84f, 0, 0.9f);
    private static readonly Color _reachable = new(1, 1, 1, 0.25f);
    private static readonly Color _arrival = new(0.55f, 0.95f, 1, 0.3f);
    private static readonly Color _arrivalEdge = new(0.55f, 0.95f, 1, 0.9f);
    private static readonly Color _path = new(1, 1, 0.4f, 0.9f);
    private static readonly Color _target = new(1, 0.2f, 0.2f, 0.9f);
    private static readonly Color _selected = new(1, 1, 0, 1);
    private static readonly Color _arrow = new(1, 1, 1, 0.8f);
    private static readonly Color _threatMove = new(1, 0.55f, 0.1f, 0.3f);
    private static readonly Color _threatAttack = new(1, 0.1f, 0.1f, 0.22f);
    private static readonly Color _threatAttackEdge = new(1, 0.15f, 0.15f, 0.75f);

    private ImmutableDictionary<Point, TileMark> _marks = ImmutableDictionary<Point, TileMark>.Empty;
    private ImmutableArray<OrderArrow> _arrows = [];

    /// <inheritdoc />
    public override void _Draw()
    {
        int size = GodotConversions.TileSize;
        foreach ((Point tile, TileMark mark) in _marks)
        {
            Rect2 rect = new(tile.X * size, tile.Y * size, size, size);
            if ((mark & TileMark.ThreatMove) != 0)
            {
                DrawRect(rect, _threatMove);
            }

            if ((mark & TileMark.ThreatAttack) != 0)
            {
                DrawRect(rect, _threatAttack);
                DrawRect(rect.Grow(-2), _threatAttackEdge, false, 1);
            }

            if ((mark & TileMark.Reachable) != 0)
            {
                DrawRect(rect, _reachable);
            }

            if ((mark & TileMark.Arrival) != 0)
            {
                DrawRect(rect, _arrival);
                DrawRect(rect.Grow(-1), _arrivalEdge, false, 1);
            }

            if ((mark & TileMark.Objective) != 0)
            {
                DrawRect(rect.Grow(-2), _objective, false, 2);
            }

            if ((mark & TileMark.Target) != 0)
            {
                DrawRect(rect.Grow(-1), _target, false, 2);
            }

            if ((mark & TileMark.Selected) != 0)
            {
                DrawRect(rect.Grow(-1), _selected, false, 1);
            }

            if ((mark & TileMark.Path) != 0)
            {
                DrawCircle(tile.TileCenter(), 3, _path);
            }
        }

        foreach (OrderArrow arrow in _arrows)
        {
            if (arrow.Deploy)
            {
                Point tile = arrow.Points[0];
                DrawRect(new Rect2(tile.X * size + 4, tile.Y * size + 4, size - 8, size - 8), _arrow, false, 2);
                continue;
            }

            Vector2[] points = new Vector2[arrow.Points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = arrow.Points[i].TileCenter();
            }

            DrawPolyline(points, _arrow, 2);
            DrawCircle(points[^1], 4, _arrow);
        }
    }

    /// <summary>Shows the model's highlights and arrows.</summary>
    public void Render(BoardModel model)
    {
        _marks = model.Marks;
        _arrows = model.Arrows;
        QueueRedraw();
    }
}
