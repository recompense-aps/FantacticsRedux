using System.Text.Json;
using Fantactics.Core;
using Fantactics.Core.Engine;
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
    /// <exception cref="MatchLoadException">
    /// The file is missing, isn't a match file this version reads, doesn't continue (it no longer replays and has no
    /// snapshot), or has no seat <paramref name="shown"/>; the message says which, naming the file.
    /// </exception>
    public OpenMatch Load(string path, Seat? shown = null, Func<string, string>? relabel = null)
    {
        MatchHost host = Resume(path);
        if (shown is Seat seatShown && !host.Setup.Seats.ContainsKey(seatShown))
        {
            throw Failed(path, $"it has no seat {seatShown}.");
        }

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

    private static MatchLoadException Failed(string path, string reason, Exception? inner = null) =>
        new($"Can't load {Path.GetFileName(path)}: {reason}", inner);

    /// <summary>A match file's record and where it continues from, every failure turned into a readable one.</summary>
    private MatchHost Resume(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw Failed(path, "the file no longer exists.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failed(path, $"the file couldn't be read. {ex.Message}", ex);
        }

        string? rulesVersion;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (MatchFileProblem.Of(document.RootElement) is string problem)
            {
                throw Failed(path, $"{problem}.");
            }

            rulesVersion = MatchFileProblem.RulesVersionOf(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw Failed(
                path,
                $"{MatchFileProblem.NotJson} (line {ex.LineNumber + 1}); it may be cut short or corrupted.",
                ex);
        }

        string otherRules = rulesVersion is not null && rulesVersion != GameEngine.RulesVersion
            ? $" It was saved with rules {rulesVersion}; this is {GameEngine.RulesVersion}."
            : "";
        MatchRecord record;
        try
        {
            record = MatchRecord.FromJson(json, rules);
        }
        catch (JsonException ex)
        {
            string detail = ex.Path is string field
                ? $"the value at {field} (line {ex.LineNumber + 1}) is invalid."
                : ex.Message;
            throw Failed(path, detail + otherRules, ex);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException
            or KeyNotFoundException)
        {
            throw Failed(path, $"part of it can't be read. {ex.Message}{otherRules}", ex);
        }

        try
        {
            return MatchHost.Resume(rules, record);
        }
        catch (MatchResumeException ex)
        {
            throw Failed(path, $"the match can't be continued. {ex.Message}", ex);
        }
        // A hand-edited snapshot can leave fields out (the serializer leaves them null) or contradict itself.
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException
            or NullReferenceException)
        {
            throw Failed(path, $"its saved position is invalid. {ex.Message}{otherRules}", ex);
        }
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
