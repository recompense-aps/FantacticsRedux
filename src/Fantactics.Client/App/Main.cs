using System.Collections.Immutable;
using Fantactics.Ai;
using Fantactics.Client.Logic.Launch;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Logic.Settings;
using Fantactics.Client.Match;
using Fantactics.Core;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Godot;

namespace Fantactics.Client.App;

/// <summary>
/// The root: reads the launch options and settings, opens a match, and shows it; it swaps in another match when the
/// screen asks (quickload, a save, a branch, moving to a shared file). Until the menus exist, no options means a quick
/// match against <c>bot:captain@easy</c>. With <c>--autoplay</c> it plays bots to the end and quits with exit code 0
/// (1 on failure, 2 on timeout), the headless smoke test.
/// </summary>
public partial class Main : Node
{
    private const string SettingsPath = "user://settings.json";
    private static readonly TimeSpan _autoplayTimeout = TimeSpan.FromMinutes(5);

    private LaunchArgs _args = LaunchArgs.None;
    private MatchOpener _opener = null!;
    private ClientSettings _settings = new();
    private OpenMatch? _open;
    private MatchScreen? _screen;

    [Export]
    private PackedScene _matchScene = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            _args = LaunchArgs.Parse(OS.GetCmdlineUserArgs());
        }
        catch (ArgumentException ex)
        {
            GD.PrintErr(ex.Message);
            GetTree().Quit(1);
            return;
        }

        _settings = ClientSettings.Load(ProjectSettings.GlobalizePath(SettingsPath));
        _opener = new MatchOpener(
            RulesConfig.Default,
            CreateBot,
            SaveLocations.Choose(
                _args.Saves,
                ProjectSettings.GlobalizePath("res://"),
                ProjectSettings.GlobalizePath("user://saves"),
                OS.HasFeature("template")));

        if (!Show(() => _args.Load is string path
            ? _opener.Load(path, _args.As, label => SeatLabel(label, _args.Autoplay))
            : _opener.New(Setup(_args), _args.DraftAs, _args.Out)))
        {
            GetTree().Quit(1);
            return;
        }

        if (_args.Screenshot is string screenshot)
        {
            GetTree().CreateTimer(4).Timeout += () =>
            {
                GetViewport().GetTexture().GetImage().SavePng(screenshot);
                GetTree().Quit(0);
            };
        }

        if (_args.Autoplay)
        {
            GetTree().CreateTimer(_autoplayTimeout.TotalSeconds).Timeout += () =>
            {
                GD.PrintErr("Autoplay timed out.");
                GetTree().Quit(2);
            };
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => _open?.Dispose();

    private static IPlayerAgent CreateBot(string spec, int seed) => BotFactory.Create(spec, RulesConfig.Default, seed);

    /// <summary>In autoplay, human seats become bots; LLM seats stay, so a CLI player can take part in a smoke run.</summary>
    private static string SeatLabel(string label, bool autoplay) =>
        autoplay && SeatController.Parse(label).Kind == SeatControllerKind.Human ? "bot:captain@easy" : label;

    private static MatchSetup Setup(LaunchArgs args) => new(
        args.Map,
        args.Seed ?? (ulong)Random.Shared.NextInt64(),
        ImmutableSortedDictionary.CreateRange([
            KeyValuePair.Create(Seat.P1, SeatLabel(args.P1, args.Autoplay)),
            KeyValuePair.Create(Seat.P2, SeatLabel(args.P2, args.Autoplay))]),
        args.AllowedRaces(),
        args.DraftBudgets(),
        args.StartingCaps());

    /// <summary>Opens a match and shows it in place of the current one.</summary>
    /// <returns>Whether it opened; if not, the current match stays.</returns>
    private bool Show(Func<OpenMatch> open)
    {
        OpenMatch next;
        try
        {
            next = open();
        }
        catch (Exception ex) when (ex is MatchResumeException or IOException or System.Text.Json.JsonException or ArgumentException)
        {
            GD.PrintErr($"Couldn't open the match: {ex.Message}");
            return false;
        }

        _screen?.QueueFree();
        _open?.Dispose();
        _open = next;
        if (next.SharedFile is { } shared)
        {
            GD.Print($"Match file: {shared.Path}");
        }

        MatchScreen screen = _matchScene.Instantiate<MatchScreen>();
        screen.Initialize(
            next,
            _opener.Saves,
            _args.Autoplay ? _settings with { Speed = 0 } : _settings with { Speed = _args.Speed ?? _settings.Speed },
            changed => changed.Save(ProjectSettings.GlobalizePath(SettingsPath)),
            ["human", "llm", .. BotFactory.Names.Select(name => $"bot:{name}")]);
        screen.MatchFinished += Finished;
        screen.Failed += Failed;
        screen.LoadRequested += path => Show(() => _opener.Load(path, next.Session.Shown));
        screen.BranchRequested += seq => Show(() => _opener.Branch(next, seq));
        screen.ShareRequested += () => Show(() => _opener.Share(next));
        _screen = screen;
        AddChild(screen);
        if (_args.Debug)
        {
            screen.ToggleDebug();
        }

        return true;
    }

    private void Finished(string result)
    {
        GD.Print(result);
        if (_args.Autoplay)
        {
            GetTree().Quit(0);
        }
    }

    private void Failed(string message)
    {
        GD.PrintErr(message);
        if (_args.Autoplay)
        {
            GetTree().Quit(1);
        }
    }
}
