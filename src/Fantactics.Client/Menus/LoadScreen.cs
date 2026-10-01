using Fantactics.Client.Logic.Session;
using Fantactics.Core;
using Fantactics.Protocol.Connections;
using Godot;

namespace Fantactics.Client.Menus;

/// <summary>
/// Lists the match files in the saves folder (autosaves, quicksaves, branches, and Sim files), newest first, and
/// loads one as the chosen seat. Loading a file with an <c>llm</c> seat plays on that file with the LLM.
/// </summary>
public partial class LoadScreen : Control
{
    private IReadOnlyList<SaveSummary> _saves = [];
    private string _folder = "";

    [Export]
    private Label _folderLabel = null!;

    [Export]
    private ItemList _list = null!;

    [Export]
    private OptionButton _seat = null!;

    [Export]
    private Label _message = null!;

    [Export]
    private Button _back = null!;

    [Export]
    private Button _load = null!;

    /// <summary>Load <paramref name="path"/>, showing <paramref name="seat"/> (a <see cref="Seat"/>) first.</summary>
    [Signal]
    public delegate void LoadChosenEventHandler(string path, int seat);

    /// <summary>Go back to the main menu.</summary>
    [Signal]
    public delegate void BackPressedEventHandler();

    /// <summary>Gives the screen its saves. Call before adding it to the tree.</summary>
    /// <param name="saves">The files, newest first.</param>
    /// <param name="folder">The folder they're in.</param>
    public void Initialize(IReadOnlyList<SaveSummary> saves, string folder)
    {
        _saves = saves;
        _folder = folder;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        _folderLabel.Text = _folder;
        foreach (Seat seat in Enum.GetValues<Seat>())
        {
            _seat.AddItem($"Play as {seat}", (int)seat);
        }

        foreach (SaveSummary save in _saves)
        {
            int index = _list.AddItem($"{save.Modified:yyyy-MM-dd HH:mm}  {save.Text}");
            _list.SetItemDisabled(index, !save.Readable);
        }

        _list.ItemSelected += index => OnSelected((int)index);
        _list.ItemActivated += _ => Load();
        _back.Pressed += () => EmitSignal(SignalName.BackPressed);
        _load.Pressed += Load;
        _load.Disabled = true;
        _message.Text = _saves.Count == 0 ? "No match files here yet." : "";
    }

    /// <summary>Shows why a file couldn't load.</summary>
    public void ShowError(string message) => _message.Text = message;

    private void OnSelected(int index)
    {
        SaveSummary save = _saves[index];
        _load.Disabled = !save.Readable;
        Seat human = save.Seats
            .Where(pair => SeatController.Parse(pair.Value).Kind == SeatControllerKind.Human)
            .Select(pair => pair.Key)
            .DefaultIfEmpty(Seat.P1)
            .First();
        foreach (Seat seat in Enum.GetValues<Seat>())
        {
            _seat.SetItemDisabled(_seat.GetItemIndex((int)seat), !save.Seats.ContainsKey(seat));
        }

        _seat.Select(_seat.GetItemIndex((int)human));
        _message.Text = "";
    }

    private void Load()
    {
        int[] selected = _list.GetSelectedItems();
        if (selected is [int index] && _saves[index].Readable)
        {
            EmitSignal(SignalName.LoadChosen, _saves[index].Path, _seat.GetSelectedId());
        }
    }
}
