using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Protocol.Connections;

namespace Fantactics.Client.Logic.Session;

/// <summary>What the load screen shows about a match file, read without replaying it.</summary>
/// <param name="Path">The file.</param>
/// <param name="Modified">When it was last written.</param>
/// <param name="Seats">Who plays each seat (the record's labels); empty if the file isn't a match file.</param>
/// <param name="Position">Where the match stands, e.g. <c>turn 4 Movement</c> or <c>12 commands</c>.</param>
/// <param name="RulesVersion">The rules version that saved it, or <c>null</c> if it doesn't say.</param>
/// <param name="Problem">Why it can't be loaded, e.g. <c>not valid JSON</c>; <c>null</c> if it looks loadable.</param>
public sealed record SaveSummary(
    string Path,
    DateTime Modified,
    ImmutableSortedDictionary<Seat, string> Seats,
    string Position,
    string? RulesVersion = null,
    string? Problem = null)
{
    /// <summary>Whether the file looked like a match file this version can load.</summary>
    public bool Readable => Problem is null && !Seats.IsEmpty;

    /// <summary>
    /// One line, e.g. <c>quick.json: turn 4 Movement, human vs bot:captain@easy</c>, with the rules version when it
    /// isn't this one's, or <c>broken.json: not valid JSON</c>.
    /// </summary>
    public string Text => Readable
        ? $"{System.IO.Path.GetFileName(Path)}: {Position}, {string.Join(" vs ", Seats.Values)}{OtherRules}"
        : $"{System.IO.Path.GetFileName(Path)}: {Problem ?? MatchFileProblem.NotAMatch}";

    /// <summary>
    /// The seats to offer: one per seat, labeled with who plays it, led by "the seat to move" when two or more seats
    /// are human (hotseat), so the first seat that owes a decision is shown.
    /// </summary>
    public IReadOnlyList<SeatChoice> SeatChoices =>
    [
        .. Humans.Count() >= 2 ? [new SeatChoice("Play the seat to move", null)] : Array.Empty<SeatChoice>(),
        .. Seats.Select(pair => new SeatChoice($"Play as {pair.Key} ({pair.Value})", pair.Key)),
    ];

    /// <summary>
    /// Which of <see cref="SeatChoices"/> to select at first: the seat to move in hotseat, otherwise the human seat
    /// (or the first seat when nobody here plays).
    /// </summary>
    public int DefaultSeatChoice => Humans.Count() >= 2
        ? 0
        : Seats.Keys
            .Select((seat, index) => (seat, index))
            .Where(pair => Humans.Contains(pair.seat))
            .Select(pair => pair.index)
            .FirstOrDefault();

    private IEnumerable<Seat> Humans => Seats
        .Where(pair => SeatController.Parse(pair.Value).Kind == SeatControllerKind.Human)
        .Select(pair => pair.Key);

    private string OtherRules => RulesVersion is string version && version != GameEngine.RulesVersion
        ? $", rules {version}"
        : "";

    /// <summary>Reads a file's summary; never throws for a bad file.</summary>
    public static SaveSummary Read(string path)
    {
        DateTime modified = File.Exists(path) ? File.GetLastWriteTime(path) : default;
        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = json.RootElement;
            if (MatchFileProblem.Of(root) is string problem)
            {
                return Unreadable(path, modified, problem);
            }

            ImmutableSortedDictionary<Seat, string> seats = root.GetProperty("setup").GetProperty("seats")
                .EnumerateObject()
                .ToImmutableSortedDictionary(
                    seat => Enum.Parse<Seat>(seat.Name, ignoreCase: true),
                    seat => seat.Value.GetString() ?? "");
            string position = root.TryGetProperty("snapshot", out JsonElement snapshot)
                && snapshot.ValueKind == JsonValueKind.Object
                ? $"turn {snapshot.GetProperty("turn").GetInt32()} {snapshot.GetProperty("phase").GetString()}"
                : $"{root.GetProperty("commands").GetArrayLength()} commands";
            return new SaveSummary(path, modified, seats, position, MatchFileProblem.RulesVersionOf(root));
        }
        catch (JsonException)
        {
            return Unreadable(path, modified, MatchFileProblem.NotJson);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Unreadable(path, modified, "file not found");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unreadable(path, modified, "can't be read");
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Unreadable(path, modified, MatchFileProblem.NotAMatch);
        }
    }

    /// <summary>Every <c>.json</c> file in <paramref name="folder"/>, newest first.</summary>
    public static IReadOnlyList<SaveSummary> In(string folder) => Directory.Exists(folder)
        ? [.. Directory.GetFiles(folder, "*.json")
            .Select(Read)
            .OrderByDescending(save => save.Modified)]
        : [];

    private static SaveSummary Unreadable(string path, DateTime modified, string problem) =>
        new(path, modified, ImmutableSortedDictionary<Seat, string>.Empty, "", Problem: problem);
}
