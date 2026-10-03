using Fantactics.Ai;
using Fantactics.Client.Logic.Drive;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Match;
using Fantactics.Client.Match.Board;
using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Godot;

namespace Fantactics.Client.App;

/// <summary>
/// The scripted player behind <c>--drive</c>: it plays the human seats only through synthetic input (mouse clicks on
/// the real buttons and tiles, key presses for InputMap actions), following an <see cref="InputPlan"/> built from a
/// bot's choice for each decision. A button it can't find, or a decision that input doesn't answer in time, fails
/// the run. Main starts it (with the menu route, if any) and hands it each match screen it opens.
/// </summary>
public partial class InputDriver : Node
{
    /// <summary>The bot whose choices the scripted player acts out.</summary>
    private const string Profile = "captain@easy";

    /// <summary>Frames between steps, so each click's redraw lands before the next.</summary>
    private const int FramesPerStep = 3;

    /// <summary>Seconds to wait for a step's button to appear.</summary>
    private const double FindTimeout = 5;

    /// <summary>Seconds a decision may stay on screen after its plan has run.</summary>
    private const double AnswerTimeout = 10;

    private static readonly bool[] _downThenUp = [true, false];

    private readonly Queue<InputStep> _steps = new();
    private readonly HashSet<string> _shotsTaken = [];
    private readonly Dictionary<Seat, IPlayerAgent> _bots = [];
    private readonly SortedDictionary<string, int> _answeredCounts = [];
    private string? _shots;
    private MatchScreen? _screen;
    private ClientSession? _session;
    private SeatUpdate? _answered;
    private string _answeredName = "";
    private double _waited;
    private int _frames;

    /// <summary>The scripted player couldn't go on; <paramref name="message"/> says where it stopped.</summary>
    [Signal]
    public delegate void FailedEventHandler(string message);

    /// <summary>How many decisions of each kind input answered, e.g. <c>Drove 1 draft, 14 orders, …</c>.</summary>
    public string Summary => "Drove " + string.Join(", ", _answeredCounts.Select(kind => $"{kind.Value} {kind.Key}"));

    /// <inheritdoc />
    public override void _Ready() => SetProcess(false);

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (++_frames < FramesPerStep)
        {
            return;
        }

        if (_steps.Count > 0)
        {
            RunStep(delta * _frames);
        }
        else if (_screen is not null && IsInstanceValid(_screen))
        {
            Watch(_screen, delta * _frames);
        }

        _frames = 0;
    }

    /// <summary>Starts driving: runs <paramref name="steps"/> (the menu route) first, if any.</summary>
    /// <param name="steps">Steps to run before any match, e.g. <see cref="InputPlan.Menu"/>.</param>
    /// <param name="shots">Where to save screenshots, or <c>null</c> for none.</param>
    public void Start(IReadOnlyList<InputStep> steps, string? shots)
    {
        _shots = shots;
        if (_shots is not null && DisplayServer.GetName() == "headless")
        {
            GD.Print("--shots needs a window; no screenshots in a headless run.");
            _shots = null;
        }

        foreach (InputStep step in steps)
        {
            _steps.Enqueue(step);
        }

        SetProcess(true);
    }

    /// <summary>Plays the human seats of the match on <paramref name="screen"/>.</summary>
    public void Drive(MatchScreen screen, ClientSession session)
    {
        _screen = screen;
        _session = session;
        _answered = null;
        _bots.Clear();
    }

    /// <summary>Runs the next step, or waits for its button to appear.</summary>
    private void RunStep(double delta)
    {
        InputStep step = _steps.Peek();
        Button? button = step.Kind == InputStepKind.ClickButton ? FindButton(step.Name) : null;
        if (step.Kind == InputStepKind.ClickButton && button is null)
        {
            _waited += delta;
            if (_waited > FindTimeout)
            {
                Fail($"No visible, enabled button '{step.Name}' ({_answeredName}).");
            }

            return;
        }

        // Dequeue before acting: a click can open a match, which calls Drive while this step is still running.
        _steps.Dequeue();
        _waited = 0;
        switch (step.Kind)
        {
            case InputStepKind.ClickButton when button is not null:
                Click(button.GetGlobalTransformWithCanvas() * (button.Size / 2), MouseButton.Left);
                break;
            case InputStepKind.ClickTile:
                if (_screen is null || FindBoard(_screen) is not BoardView board)
                {
                    Fail($"No board to {step}.");
                    return;
                }

                Click(board.ScreenPositionOf(step.Tile), step.Secondary ? MouseButton.Right : MouseButton.Left);
                break;
            case InputStepKind.PressAction:
                Press(step.Name);
                break;
            case InputStepKind.Screenshot:
                Screenshot(step.Name);
                break;
        }
    }

    /// <summary>Plans the next decision once the screen waits on input, and fails if one goes unanswered.</summary>
    private void Watch(MatchScreen screen, double delta)
    {
        if (screen.CurtainButton is string ready)
        {
            _steps.Enqueue(InputStep.Shot("curtain"));
            _steps.Enqueue(InputStep.Button(ready));
            return;
        }

        if (_answered is not null && ReferenceEquals(screen.Current, _answered))
        {
            _waited += delta;
            if (_waited > AnswerTimeout)
            {
                Fail($"The {_answeredName} wasn't submitted after its input ran. The prompt says: {screen.PromptLine}");
            }

            return;
        }

        _answered = null;
        _waited = 0;
        if (screen.AwaitingInput is not { Legal: LegalActions legal } update || _session is null)
        {
            return;
        }

        if (!_bots.TryGetValue(update.View.Seat, out IPlayerAgent? bot))
        {
            bot = BotFactory.Create(Profile, _session.Rules, (int)update.View.Seat + 1);
            _bots[update.View.Seat] = bot;
        }

        ICommand choice = bot.Decide(update.View, legal.Decision, legal);
        _answered = update;
        string decision = InputPlan.DecisionName(update);
        _answeredName = $"{update.View.Seat} {decision}";
        _answeredCounts[decision] = _answeredCounts.GetValueOrDefault(decision) + 1;
        foreach (InputStep step in InputPlan.For(update, choice))
        {
            _steps.Enqueue(step);
        }
    }

    /// <summary>The visible, enabled button with exactly this text, preferring one that isn't toggled on.</summary>
    private Button? FindButton(string text)
    {
        Button? pressed = null;
        Stack<Node> nodes = new();
        nodes.Push(GetTree().Root);
        while (nodes.Count > 0)
        {
            Node node = nodes.Pop();
            if (node is Button { Disabled: false } button && button.Text == text && button.IsVisibleInTree())
            {
                if (!button.ButtonPressed)
                {
                    return button;
                }

                pressed ??= button;
            }

            // Push children in reverse so the walk visits them in tree order.
            for (int i = node.GetChildCount() - 1; i >= 0; i--)
            {
                nodes.Push(node.GetChild(i));
            }
        }

        return pressed;
    }

    private static BoardView? FindBoard(Node root)
    {
        Stack<Node> nodes = new();
        nodes.Push(root);
        while (nodes.Count > 0)
        {
            Node node = nodes.Pop();
            if (node is BoardView board)
            {
                return board;
            }

            foreach (Node child in node.GetChildren())
            {
                nodes.Push(child);
            }
        }

        return null;
    }

    /// <summary>Moves the pointer to <paramref name="position"/> (viewport pixels) and clicks there.</summary>
    private void Click(Vector2 position, MouseButton button)
    {
        Viewport viewport = GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        foreach (bool down in _downThenUp)
        {
            viewport.PushInput(
                new InputEventMouseButton
                {
                    Position = position,
                    GlobalPosition = position,
                    ButtonIndex = button,
                    ButtonMask = down ? (MouseButtonMask)(1 << ((int)button - 1)) : 0,
                    Pressed = down,
                },
                true);
        }
    }

    /// <summary>Presses and releases the first key bound to <paramref name="action"/> in the InputMap.</summary>
    private void Press(string action)
    {
        if (!InputMap.HasAction(action))
        {
            Fail($"No InputMap action '{action}'.");
            return;
        }

        InputEventKey? key = null;
        foreach (InputEvent bound in InputMap.ActionGetEvents(action))
        {
            if (bound is InputEventKey found)
            {
                key = found;
                break;
            }
        }

        if (key is null)
        {
            Fail($"No key is bound to '{action}'.");
            return;
        }

        foreach (bool down in _downThenUp)
        {
            InputEventKey copy = (InputEventKey)key.Duplicate();
            copy.Pressed = down;
            GetViewport().PushInput(copy, true);
        }
    }

    private void Screenshot(string name)
    {
        if (_shots is null || !_shotsTaken.Add(name))
        {
            return;
        }

        Directory.CreateDirectory(_shots);
        string path = Path.Combine(_shots, $"{_shotsTaken.Count:00}-{name}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
    }

    private void Fail(string message)
    {
        SetProcess(false);
        _steps.Clear();
        EmitSignal(SignalName.Failed, message);
    }
}
