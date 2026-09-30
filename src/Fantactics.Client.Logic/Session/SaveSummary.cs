using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core;

namespace Fantactics.Client.Logic.Session;

/// <summary>What the load screen shows about a match file, read without replaying it.</summary>
/// <param name="Path">The file.</param>
/// <param name="Modified">When it was last written.</param>
/// <param name="Seats">Who plays each seat (the record's labels); empty if the file isn't a match file.</param>
/// <param name="Position">Where the match stands, e.g. <c>turn 4 Movement</c> or <c>12 commands</c>.</param>
public sealed record SaveSummary(
    string Path,
    DateTime Modified,
    ImmutableSortedDictionary<Seat, string> Seats,
    string Position)
{
    /// <summary>Whether the file looked like a match file.</summary>
    public bool Readable => !Seats.IsEmpty;

    /// <summary>One line, e.g. <c>quick.json: turn 4 Movement, human vs bot:captain@easy</c>.</summary>
    public string Text => Readable
        ? $"{System.IO.Path.GetFileName(Path)}: {Position}, {string.Join(" vs ", Seats.Values)}"
        : $"{System.IO.Path.GetFileName(Path)}: not a match file";

    /// <summary>Reads a file's summary; never throws for a bad file.</summary>
    public static SaveSummary Read(string path)
    {
        DateTime modified = File.Exists(path) ? File.GetLastWriteTime(path) : default;
        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = json.RootElement;
            ImmutableSortedDictionary<Seat, string> seats = root.GetProperty("setup").GetProperty("seats")
                .EnumerateObject()
                .ToImmutableSortedDictionary(
                    seat => Enum.Parse<Seat>(seat.Name, ignoreCase: true),
                    seat => seat.Value.GetString() ?? "");
            string position = root.TryGetProperty("snapshot", out JsonElement snapshot)
                ? $"turn {snapshot.GetProperty("turn").GetInt32()} {snapshot.GetProperty("phase").GetString()}"
                : $"{root.GetProperty("commands").GetArrayLength()} commands";
            return new SaveSummary(path, modified, seats, position);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
            or IOException or ArgumentException or UnauthorizedAccessException)
        {
            return new SaveSummary(path, modified, ImmutableSortedDictionary<Seat, string>.Empty, "");
        }
    }

    /// <summary>Every <c>.json</c> file in <paramref name="folder"/>, newest first.</summary>
    public static IReadOnlyList<SaveSummary> In(string folder) => Directory.Exists(folder)
        ? [.. Directory.GetFiles(folder, "*.json")
            .Select(Read)
            .OrderByDescending(save => save.Modified)]
        : [];
}
