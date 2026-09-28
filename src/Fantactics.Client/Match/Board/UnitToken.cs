using Fantactics.Client.Common;
using Fantactics.Client.Logic.Board;
using Fantactics.Core;
using Fantactics.Core.State;
using Godot;

namespace Fantactics.Client.Match.Board;

/// <summary>A placeholder unit token: an owner-colored disc with the type's initials, an HP bar, and markers.</summary>
public partial class UnitToken : Node2D
{
    private static readonly Color _p1Color = new("3b82f6");
    private static readonly Color _p2Color = new("dc2626");

    private TokenModel? _model;

    /// <summary>The unit shown, in the viewing seat's ids.</summary>
    public int UnitId => _model?.Id ?? 0;

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_model is not TokenModel model)
        {
            return;
        }

        if (model.Acting)
        {
            DrawArc(Vector2.Zero, 14, 0, Mathf.Tau, 24, Colors.Yellow, 2);
        }

        Color body = model.Owner == Seat.P1 ? _p1Color : _p2Color;
        DrawCircle(Vector2.Zero, 11, body);
        DrawArc(Vector2.Zero, 11, 0, Mathf.Tau, 24, model.Mine ? Colors.White : Colors.Black, 1);
        string initials = model.Type.Length > 2 ? model.Type[..2] : model.Type;
        DrawString(ThemeDB.FallbackFont, new Vector2(-11, 4), initials, HorizontalAlignment.Center, 22, 10, Colors.White);

        float fraction = model.MaxHp > 0 ? Mathf.Clamp((float)model.Hp / model.MaxHp, 0, 1) : 1;
        DrawRect(new Rect2(-12, 12, 24, 3), Colors.Black);
        DrawRect(new Rect2(-12, 12, 24 * fraction, 3), fraction > 0.5f ? Colors.LimeGreen : fraction > 0.25f ? Colors.Orange : Colors.Red);

        if (model.Held)
        {
            DrawRect(new Rect2(8, -14, 6, 6), Colors.LightGray, model.Braced);
        }

        for (int i = 0; i < model.Statuses.Length; i++)
        {
            Color status = model.Statuses[i] == StatusKind.Rooted ? Colors.MediumPurple : Colors.Cyan;
            DrawCircle(new Vector2(-12 + i * 5, -12), 2, status);
        }
    }

    /// <summary>Shows <paramref name="model"/> at its tile.</summary>
    public void Show(TokenModel model)
    {
        _model = model;
        Position = model.Tile.TileCenter();
        Modulate = Colors.White;
        QueueRedraw();
    }

    /// <summary>Changes the HP shown, during playback.</summary>
    public void ShowHp(int hp)
    {
        if (_model is TokenModel model)
        {
            _model = model with { Hp = hp };
            QueueRedraw();
        }
    }
}
