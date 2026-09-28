using System.Collections.Immutable;
using Fantactics.Ai;
using Fantactics.Client.Logic.Launch;
using Fantactics.Client.Logic.Session;
using Fantactics.Client.Logic.Settings;
using Fantactics.Client.Match;
using Fantactics.Core;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Fantactics.Protocol.Files;
using Godot;

namespace Fantactics.Client.App;

/// <summary>
/// The root: reads the launch options and settings, starts or loads a match, and shows it. Until the menus exist,
/// no options means a quick match against <c>bot:captain@easy</c>. With <c>--autoplay</c> it plays bots to the end
/// and quits with exit code 0 (1 on failure, 2 on timeout), the headless smoke test.
/// </summary>
public partial class Main : Node
{
    private const string SettingsPath = "user://settings.json";
    private static readonly TimeSpan _autoplayTimeout = TimeSpan.FromMinutes(5);

    private SharedMatchFile? _sharedFile;

    [Export]
    private PackedScene _matchScene = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        LaunchArgs args;
        try
        {
            args = LaunchArgs.Parse(OS.GetCmdlineUserArgs());
        }
        catch (ArgumentException ex)
        {
            GD.PrintErr(ex.Message);
            GetTree().Quit(1);
            return;
        }

        RulesConfig rules = RulesConfig.Default;
        string settingsPath = ProjectSettings.GlobalizePath(SettingsPath);
        ClientSettings settings = ClientSettings.Load(settingsPath);
        SaveLocations saves = SaveLocations.Choose(
            args.Saves,
            ProjectSettings.GlobalizePath("res://"),
            ProjectSettings.GlobalizePath("user://saves"),
            OS.HasFeature("template"));

        ClientSession session;
        try
        {
            session = args.Load is string path ? Load(path, rules, args) : Start(args, rules, saves);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Couldn't start the match: {ex.Message}");
            GetTree().Quit(1);
            return;
        }

        if (session.Match.ResumeWarning is string warning)
        {
            GD.PushWarning(warning);
        }

        MatchScreen screen = _matchScene.Instantiate<MatchScreen>();
        screen.Initialize(
            session,
            args.Autoplay ? settings with { Speed = 0 } : settings with { Speed = args.Speed ?? settings.Speed },
            changed => changed.Save(settingsPath));
        screen.MatchFinished += result => Finished(result, args.Autoplay);
        screen.Failed += message => Failed(message, args.Autoplay);
        AddChild(screen);

        if (args.Screenshot is string screenshot)
        {
            GetTree().CreateTimer(4).Timeout += () =>
            {
                GetViewport().GetTexture().GetImage().SavePng(screenshot);
                GetTree().Quit(0);
            };
        }

        if (args.Autoplay)
        {
            GetTree().CreateTimer(_autoplayTimeout.TotalSeconds).Timeout += () =>
            {
                GD.PrintErr("Autoplay timed out.");
                GetTree().Quit(2);
            };
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => _sharedFile?.Dispose();

    private static IPlayerAgent CreateBot(string spec, int seed) => BotFactory.Create(spec, RulesConfig.Default, seed);

    private static string SeatLabel(string label, bool autoplay) =>
        autoplay && SeatController.Parse(label).Kind != SeatControllerKind.Bot ? "bot:captain@easy" : label;

    private ClientSession Start(LaunchArgs args, RulesConfig rules, SaveLocations saves)
    {
        MatchSetup setup = new(
            args.Map,
            ImmutableSortedDictionary.CreateRange([
                KeyValuePair.Create(Seat.P1, args.P1Race),
                KeyValuePair.Create(Seat.P2, args.P2Race)]),
            args.Seed ?? (ulong)Random.Shared.NextInt64(),
            ImmutableSortedDictionary.CreateRange([
                KeyValuePair.Create(Seat.P1, SeatLabel(args.P1, args.Autoplay)),
                KeyValuePair.Create(Seat.P2, SeatLabel(args.P2, args.Autoplay))]));
        MatchHost host = new(rules, setup);
        bool shared = args.Out is not null || setup.Seats.Values.Any(label => SeatController.Parse(label).Kind == SeatControllerKind.Llm);
        return Open(host, rules, args, shared ? args.Out ?? saves.NewMatchFile(DateTime.Now) : null);
    }

    private ClientSession Load(string path, RulesConfig rules, LaunchArgs args)
    {
        MatchHost host = MatchHost.Resume(rules, MatchFiles.Read(path, rules));
        foreach ((Seat seat, string label) in host.Setup.Seats)
        {
            host.SetSeatLabel(seat, SeatLabel(label, args.Autoplay));
        }

        return Open(host, rules, args, path);
    }

    /// <summary>Wraps the host in a match, shared through <paramref name="file"/> if given, and a session.</summary>
    private ClientSession Open(MatchHost host, RulesConfig rules, LaunchArgs args, string? file)
    {
        if (file is not null)
        {
            _sharedFile = new SharedMatchFile(file, rules);
            _sharedFile.Diverged += reason => GD.PushWarning(reason);
            GD.Print($"Match file: {Path.GetFullPath(file)}");
        }

        LocalMatch match = new(host, CreateBot, _sharedFile);
        _sharedFile?.Watch(match);
        return new ClientSession(match, rules, CreateBot, args.DraftAs, args.As);
    }

    private void Finished(string result, bool autoplay)
    {
        GD.Print(result);
        if (autoplay)
        {
            GetTree().Quit(0);
        }
    }

    private void Failed(string message, bool autoplay)
    {
        GD.PrintErr(message);
        if (autoplay)
        {
            GetTree().Quit(1);
        }
    }
}
