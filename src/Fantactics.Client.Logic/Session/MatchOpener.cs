using Fantactics.Core;
using Fantactics.Core.Hosting;
using Fantactics.Core.Players;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;
using Fantactics.Protocol.Files;

namespace Fantactics.Client.Logic.Session;

/// <summary>
/// Starts, loads, branches, and shares matches (TechnicalDesign §4). A match is bound to its file only when it must
/// be: an LLM seat plays through the file, or <c>--out</c> asked for one. Everything else plays in memory and
/// autosaves at the start of every turn, so loading a save (a quicksave, a branch) never overwrites it.
/// </summary>
/// <param name="rules">Rules to play with.</param>
/// <param name="botFactory">Creates bots from a spec and seed.</param>
/// <param name="saves">Where saves go.</param>
public sealed class MatchOpener(RulesConfig rules, Func<string, int, IPlayerAgent> botFactory, SaveLocations saves)
{
    /// <summary>Where saves go.</summary>
    public SaveLocations Saves => saves;

    /// <summary>Starts a new match.</summary>
    /// <param name="setup">Map, seed, seat labels, and allowed races.</param>
    /// <param name="draftAs">Bot profile that drafts and places for human seats, or <c>null</c>.</param>
    /// <param name="file">File to keep the match in (shared with the CLI); required for LLM seats, generated if missing.</param>
    public OpenMatch New(MatchSetup setup, string? draftAs, string? file = null) =>
        Open(new MatchHost(rules, setup), draftAs, null, file ?? (HasLlm(setup) ? saves.NewMatchFile(DateTime.Now) : null), "match");

    /// <summary>Loads a match file (a save, a branch, or a Sim match file).</summary>
    /// <param name="path">The file.</param>
    /// <param name="shown">The seat to show first.</param>
    /// <param name="relabel">Changes seat labels on load (e.g. bots for every seat in autoplay).</param>
    /// <exception cref="MatchResumeException">The file doesn't replay and has no snapshot.</exception>
    public OpenMatch Load(string path, Seat? shown = null, Func<string, string>? relabel = null)
    {
        MatchHost host = MatchHost.Resume(rules, MatchFiles.Read(path, rules));
        foreach ((Seat seat, string label) in host.Setup.Seats)
        {
            host.SetSeatLabel(seat, relabel?.Invoke(label) ?? label);
        }

        return Open(host, null, shown, HasLlm(host.Setup) ? path : null, Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>Saves <paramref name="current"/> cut after command <paramref name="seq"/> as a branch file, and loads it.</summary>
    /// <returns>The branch, showing the same seat.</returns>
    public OpenMatch Branch(OpenMatch current, int seq)
    {
        string file = SaveLocations.BranchFile(Path.Combine(saves.Folder, current.Name + ".json"), seq);
        MatchFiles.Write(file, current.Session.Match.ToRecord().Truncated(seq));
        return Load(file, current.Session.Shown);
    }

    /// <summary>Moves an in-memory match to a new shared file (e.g. a seat was just handed to an LLM), and loads it.</summary>
    public OpenMatch Share(OpenMatch current)
    {
        string file = saves.NewMatchFile(DateTime.Now);
        current.Session.SaveTo(file);
        return Load(file, current.Session.Shown);
    }

    private static bool HasLlm(MatchSetup setup) =>
        setup.Seats.Values.Any(label => SeatController.Parse(label).Kind == SeatControllerKind.Llm);

    private OpenMatch Open(MatchHost host, string? draftAs, Seat? shown, string? file, string name)
    {
        SharedMatchFile? shared = file is null ? null : new SharedMatchFile(file, rules);
        LocalMatch match = new(host, botFactory, shared);
        shared?.Watch(match);
        ClientSession session = new(match, rules, botFactory, draftAs, shown, shared is null ? saves.Autosave : null);
        return new OpenMatch(session, shared, file is null ? name : Path.GetFileNameWithoutExtension(file));
    }
}
