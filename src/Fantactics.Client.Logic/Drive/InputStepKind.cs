namespace Fantactics.Client.Logic.Drive;

/// <summary>What an <see cref="InputStep"/> does.</summary>
public enum InputStepKind
{
    /// <summary>Click the visible, enabled button whose text is exactly <see cref="InputStep.Name"/>.</summary>
    ClickButton,

    /// <summary>Click a board tile with the left (confirm) or right (cancel) mouse button.</summary>
    ClickTile,

    /// <summary>Press and release the InputMap action <see cref="InputStep.Name"/>.</summary>
    PressAction,

    /// <summary>Save a picture of the screen named <see cref="InputStep.Name"/> (once per name in a run).</summary>
    Screenshot,
}
