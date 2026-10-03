using Fantactics.Core;
using Godot;

namespace Fantactics.Client.Match.Curtain;

/// <summary>
/// The pass-device screen between hotseat players: it hides everything until the next player says they're ready, so
/// neither sees the other's hidden orders, draft, or reserve.
/// </summary>
public partial class HotseatCurtain : ColorRect
{
    [Export]
    private Label _title = null!;

    [Export]
    private Button _ready = null!;

    /// <summary>The next player is ready; the curtain has hidden itself.</summary>
    [Signal]
    public delegate void DismissedEventHandler();

    /// <summary>Whether the curtain is up.</summary>
    public bool IsUp => Visible;

    /// <summary>The ready button's text.</summary>
    public string ReadyText => _ready.Text;

    /// <inheritdoc />
    public override void _Ready()
    {
        Visible = false;
        _ready.Pressed += Dismiss;
    }

    /// <summary>Hides the screen until <paramref name="seat"/>'s player is ready.</summary>
    public void Raise(Seat seat)
    {
        _title.Text = $"Pass to {seat}";
        _ready.Text = $"I'm {seat}: show my board";
        Visible = true;
        _ready.GrabFocus();
    }

    private void Dismiss()
    {
        Visible = false;
        EmitSignal(SignalName.Dismissed);
    }
}
