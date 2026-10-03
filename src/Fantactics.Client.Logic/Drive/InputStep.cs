using Fantactics.Core.Geometry;

namespace Fantactics.Client.Logic.Drive;

/// <summary>One thing a scripted player does on screen, the way a person would: a click or a key press.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Name">The button text, InputMap action, or screenshot name; empty for a tile click.</param>
/// <param name="Tile">The tile to click, for <see cref="InputStepKind.ClickTile"/>.</param>
/// <param name="Secondary">Right click (cancel) rather than left click (confirm), for a tile click.</param>
public sealed record InputStep(InputStepKind Kind, string Name, Point Tile = default, bool Secondary = false)
{
    /// <summary>Clicks the button whose text is exactly <paramref name="text"/>.</summary>
    public static InputStep Button(string text) => new(InputStepKind.ClickButton, text);

    /// <summary>Clicks <paramref name="tile"/> on the board.</summary>
    public static InputStep Click(Point tile, bool secondary = false) =>
        new(InputStepKind.ClickTile, "", tile, secondary);

    /// <summary>Presses the InputMap action <paramref name="action"/>.</summary>
    public static InputStep Press(string action) => new(InputStepKind.PressAction, action);

    /// <summary>Saves a screenshot called <paramref name="name"/>.</summary>
    public static InputStep Shot(string name) => new(InputStepKind.Screenshot, name);

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        InputStepKind.ClickTile => $"{(Secondary ? "right-click" : "click")} tile {Tile}",
        InputStepKind.ClickButton => $"click '{Name}'",
        InputStepKind.PressAction => $"press {Name}",
        _ => $"screenshot {Name}",
    };
}
