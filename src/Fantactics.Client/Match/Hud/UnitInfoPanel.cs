using Fantactics.Client.Common;
using Fantactics.Client.Logic.Board;
using Godot;

namespace Fantactics.Client.Match.Hud;

/// <summary>
/// Describes one unit: its name in its seat's color, tags, HP and stats, notes for this turn, then its statuses,
/// abilities, and traits, each with a one-line description. Hidden when there's no unit to show.
/// </summary>
public partial class UnitInfoPanel : PanelContainer
{
    [Export]
    private Label _name = null!;

    [Export]
    private Label _tags = null!;

    [Export]
    private Label _stats = null!;

    [Export]
    private Label _notes = null!;

    [Export]
    private VBoxContainer _lines = null!;

    /// <summary>Shows <paramref name="info"/>, or hides the panel for <c>null</c>.</summary>
    public void Describe(UnitInfo? info)
    {
        Visible = info is not null;
        if (info is null)
        {
            return;
        }

        _name.Text = info.Name;
        _name.AddThemeColorOverride("font_color", SeatColors.Of(info.Owner).Lightened(0.35f));
        _tags.Text = info.Tags;
        _stats.Text = $"HP {info.Hp}/{info.MaxHp}\n{info.Stats}";
        _notes.Text = info.Notes;
        _notes.Visible = info.Notes.Length > 0;

        foreach (Node child in _lines.GetChildren())
        {
            _lines.RemoveChild(child);
            child.QueueFree();
        }

        foreach (InfoLine line in info.Statuses.Concat(info.Abilities).Concat(info.Traits))
        {
            _lines.AddChild(new Label
            {
                Text = line.Title,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = "LogHeading",
            });
            if (line.Description.Length > 0)
            {
                _lines.AddChild(new Label
                {
                    Text = line.Description,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    ThemeTypeVariation = "DimCaption",
                });
            }
        }
    }
}
