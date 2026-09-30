using Fantactics.Ai;
using Fantactics.Ai.Profiles;
using Fantactics.Client.Logic.Launch;
using Fantactics.Client.Logic.Menus;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Logic.Settings;
using Fantactics.Client.Match;
using Fantactics.Client.Menus;
using Fantactics.Core;
using Fantactics.Core.Maps;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Godot;

namespace Fantactics.Client.App;

/// <summary>
/// The root: reads the launch options and settings, owns the services, and shows one screen at a time (the main
/// menu, the new-match and load screens, or a match), with the settings screen on top when open. Launch options that
/// name a match skip the menu (see <c>notes/LaunchOptions.md</c>). With <c>--autoplay</c> it plays bots to the end and
/// quits with exit code 0 (1 on failure, 2 on timeout), the headless smoke test.
/// </summary>
public partial class Main : Node
{
    private const string SettingsPath = "user://settings.json";
    private const string QuickMatchBot = "captain";
    private static readonly TimeSpan _autoplayTimeout = TimeSpan.FromMinutes(5);

    private readonly RulesConfig _rules = RulesConfig.Default;
    private LaunchArgs _args = LaunchArgs.None;
    private MatchOpener _opener = null!;
    private ClientSettings _settings = new();
    private OpenMatch? _open;
    private Node? _screen;

    [Export]
    private PackedScene _matchScene = null!;

    [Export]
    private PackedScene _mainMenuScene = null!;

    [Export]
    private PackedScene _newMatchScene = null!;

    [Export]
    private PackedScene _loadScene = null!;

    [Export]
    private SettingsScreen _settingsScreen = null!;

    /// <summary>Controllers a seat can have: <c>human</c>, <c>llm</c>, and every bot profile.</summary>
    private static IReadOnlyList<string> Controllers => ["human", "llm", .. BotFactory.Names.Select(name => $"bot:{name}")];

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
            _rules,
            CreateBot,
            SaveLocations.Choose(
                _args.Saves,
                ProjectSettings.GlobalizePath("res://"),
                ProjectSettings.GlobalizePath("user://saves"),
                OS.HasFeature("template")));
        _settingsScreen.Changed += () => ApplySettings(_settingsScreen.Settings);

        if (!_args.SkipsMenu)
        {
            switch (_args.Menu)
            {
                case "new":
                    ShowNewMatch();
                    break;
                case "load":
                    ShowLoad();
                    break;
                default:
                    ShowMainMenu();
                    if (_args.Menu == "settings")
                    {
                        OpenSettings();
                    }

                    break;
            }
        }
        else if (OpenMatch(() => _args.Load is string path
            ? _opener.Load(path, _args.As, label => SeatLabel(label, _args.Autoplay))
            : NewFromArgs()) is string error)
        {
            GD.PrintErr($"Couldn't open the match: {error}");
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

    private static ulong RandomSeed() => (ulong)Random.Shared.NextInt64();

    /// <summary>The match the launch options describe (<c>--new</c>, <c>--p1</c>, …).</summary>
    private OpenMatch NewFromArgs()
    {
        NewMatchForm form = _args.Form(_rules);
        foreach ((Seat seat, SeatForm settings) in form.Seats)
        {
            form = form.WithSeat(seat, s => s with { Controller = SeatLabel(settings.Controller, _args.Autoplay) });
        }

        return _opener.New(form.ToSetup(_rules, RandomSeed()), form.DraftAs, form.Out);
    }

    /// <summary>Replaces the current screen (and closes the current match, if any).</summary>
    private void Swap(Node next, OpenMatch? open = null)
    {
        _screen?.QueueFree();
        _open?.Dispose();
        _open = open;
        _screen = next;
        AddChild(next);
    }

    private void ShowMainMenu()
    {
        MainMenu menu = _mainMenuScene.Instantiate<MainMenu>();
        menu.QuickMatchPressed += () => ShowError(menu, OpenMatch(() => _opener.New(
            NewMatchForm.Defaults(_rules).ToSetup(_rules, RandomSeed()),
            QuickMatchBot)));
        menu.NewMatchPressed += ShowNewMatch;
        menu.ContinuePressed += () => ShowError(menu, OpenMatch(() => _opener.Load(_opener.Saves.Autosave)));
        menu.LoadPressed += ShowLoad;
        menu.SettingsPressed += OpenSettings;
        menu.QuitPressed += () => GetTree().Quit();
        Swap(menu);
        menu.ShowSaves(
            File.Exists(_opener.Saves.Autosave) ? SaveSummary.Read(_opener.Saves.Autosave).Text : null,
            _opener.Saves.Folder);
    }

    private void ShowNewMatch()
    {
        NewMatchScreen screen = _newMatchScene.Instantiate<NewMatchScreen>();
        screen.Initialize(
            NewMatchForm.Defaults(_rules),
            MapLibrary.Names,
            [.. _rules.Races.Keys],
            Controllers,
            BotLibrary.Difficulties,
            BotFactory.Names,
            form => form.Problems(_rules, BotFactory.IsKnown));
        screen.BackPressed += ShowMainMenu;
        screen.StartPressed += () =>
        {
            NewMatchForm form = screen.Form;
            if (OpenMatch(() => _opener.New(form.ToSetup(_rules, RandomSeed()), form.DraftAs, form.Out)) is string error)
            {
                screen.ShowError(error);
            }
        };
        Swap(screen);
    }

    private void ShowLoad()
    {
        LoadScreen screen = _loadScene.Instantiate<LoadScreen>();
        screen.Initialize(SaveSummary.In(_opener.Saves.Folder), _opener.Saves.Folder);
        screen.BackPressed += ShowMainMenu;
        screen.LoadChosen += (path, seat) =>
        {
            if (OpenMatch(() => _opener.Load(path, (Seat)seat)) is string error)
            {
                screen.ShowError(error);
            }
        };
        Swap(screen);
    }

    private void OpenSettings() => _settingsScreen.Open(_settings, _opener.Saves.Folder);

    private void ApplySettings(ClientSettings settings)
    {
        _settings = settings;
        settings.Save(ProjectSettings.GlobalizePath(SettingsPath));
        (_screen as MatchScreen)?.ApplySettings(settings);
    }

    private static void ShowError(MainMenu menu, string? error)
    {
        if (error is not null)
        {
            menu.ShowError(error);
        }
    }

    /// <summary>Opens a match and shows it in place of the current screen.</summary>
    /// <returns><c>null</c> if it opened; otherwise why not, and the current screen stays.</returns>
    private string? OpenMatch(Func<OpenMatch> open)
    {
        OpenMatch next;
        try
        {
            next = open();
        }
        catch (Exception ex) when (ex is MatchResumeException or IOException or System.Text.Json.JsonException
            or ArgumentException or UnauthorizedAccessException)
        {
            return ex.Message;
        }

        if (next.SharedFile is { } shared)
        {
            GD.Print($"Match file: {shared.Path}");
        }

        MatchScreen screen = _matchScene.Instantiate<MatchScreen>();
        screen.Initialize(
            next,
            _opener.Saves,
            _args.Autoplay ? _settings with { Speed = 0 } : _settings with { Speed = _args.Speed ?? _settings.Speed },
            ApplySettings,
            Controllers);
        screen.MatchFinished += Finished;
        screen.Failed += Failed;
        screen.LoadRequested += path => Report(OpenMatch(() => _opener.Load(path, next.Session.Shown)));
        screen.BranchRequested += seq => Report(OpenMatch(() => _opener.Branch(next, seq)));
        screen.ShareRequested += () => Report(OpenMatch(() => _opener.Share(next)));
        screen.SettingsRequested += OpenSettings;
        screen.MainMenuRequested += ShowMainMenu;
        screen.QuitRequested += () => GetTree().Quit();
        Swap(screen, next);
        if (_args.Debug)
        {
            screen.ToggleDebug();
        }

        return null;
    }

    private void Report(string? error)
    {
        if (error is not null)
        {
            GD.PrintErr($"Couldn't open the match: {error}");
        }
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
