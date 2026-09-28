using Fantactics.Client.Common;
using Fantactics.Client.Logic.Board;
using Fantactics.Core;
using Fantactics.Core.Geometry;
using Fantactics.Core.Maps;
using Godot;

namespace Fantactics.Client.Match.Board;

/// <summary>
/// The board: terrain, highlights, unit tokens, and floating text. It renders a <see cref="BoardModel"/>, offers
/// small animation hooks to the event player, and reports clicks and hovers as tiles.
/// </summary>
public partial class BoardView : Node2D
{
    private readonly Dictionary<int, UnitToken> _tokens = [];
    private Point? _hovered;

    [Export]
    private TileMapLayer _terrain = null!;

    [Export]
    private BoardOverlay _overlay = null!;

    [Export]
    private Node2D _units = null!;

    [Export]
    private Node2D _effects = null!;

    [Export]
    private PackedScene _tokenScene = null!;

    /// <summary>A tile was clicked; <paramref name="secondary"/> for the cancel button (right click).</summary>
    [Signal]
    public delegate void TileClickedEventHandler(Vector2I tile, bool secondary);

    /// <summary>The pointer moved onto another tile.</summary>
    [Signal]
    public delegate void TileHoveredEventHandler(Vector2I tile);

    /// <summary>The map's size in pixels.</summary>
    public Vector2 PixelSize { get; private set; }

    /// <summary>The tile under the pointer, if it's on the map.</summary>
    public Point? Hovered => _hovered;

    /// <inheritdoc />
    public override void _Ready() => _terrain.TileSet = PlaceholderTiles.Create();

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
        {
            Point tile = GodotConversions.TileAt(GetLocalMousePosition());
            if (tile != _hovered)
            {
                _hovered = tile;
                EmitSignal(SignalName.TileHovered, tile.ToCell());
            }
        }
        else if (@event.IsActionPressed("confirm") || @event.IsActionPressed("cancel"))
        {
            if (@event is InputEventMouseButton)
            {
                Point tile = GodotConversions.TileAt(GetLocalMousePosition());
                EmitSignal(SignalName.TileClicked, tile.ToCell(), @event.IsActionPressed("cancel"));
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>Draws the terrain of <paramref name="map"/>.</summary>
    public void SetMap(GameMap map)
    {
        foreach (Point tile in map.AllPoints())
        {
            SetTerrain(tile, map[tile]);
        }

        PixelSize = new Vector2(map.Width, map.Height) * GodotConversions.TileSize;
    }

    /// <summary>Changes one tile's terrain.</summary>
    public void SetTerrain(Point tile, Terrain terrain) =>
        _terrain.SetCell(tile.ToCell(), PlaceholderTiles.SourceId, PlaceholderTiles.AtlasCoords(terrain));

    /// <summary>Shows exactly the model's tokens, highlights, and arrows.</summary>
    public void Render(BoardModel model)
    {
        _overlay.Render(model);
        HashSet<int> shown = [];
        foreach (TokenModel token in model.Tokens)
        {
            TokenFor(token.Id).Show(token);
            shown.Add(token.Id);
        }

        foreach (int gone in _tokens.Keys.Where(id => !shown.Contains(id)).ToList())
        {
            _tokens[gone].QueueFree();
            _tokens.Remove(gone);
        }
    }

    /// <summary>The token of a unit, if it's on the board.</summary>
    public UnitToken? Token(int unitId) => _tokens.GetValueOrDefault(unitId);

    /// <summary>Places a token for a unit that is appearing, during playback.</summary>
    public UnitToken Spawn(int unitId, Seat owner, string type, Point tile, bool mine, int maxHp)
    {
        UnitToken token = TokenFor(unitId);
        token.Show(new TokenModel(unitId, owner, type, tile, maxHp, maxHp, mine, false, false, [], false));
        return token;
    }

    /// <summary>Shows text rising and fading over a tile.</summary>
    public Tween FloatText(Point tile, string text, Color color, float seconds)
    {
        Label label = new()
        {
            Text = text,
            Modulate = color,
            Position = tile.TileCenter() + new Vector2(-16, -20),
            Size = new Vector2(32, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        _effects.AddChild(label);
        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", label.Position.Y - 12, seconds);
        tween.TweenProperty(label, "modulate:a", 0f, seconds);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
        return tween;
    }

    private UnitToken TokenFor(int unitId)
    {
        if (!_tokens.TryGetValue(unitId, out UnitToken? token))
        {
            token = _tokenScene.Instantiate<UnitToken>();
            _units.AddChild(token);
            _tokens[unitId] = token;
        }

        return token;
    }
}
