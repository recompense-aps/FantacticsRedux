using System.Text.Json;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;

namespace Fantactics.Sim.Matches;

/// <summary>
/// Reads and writes match files. The file is the match (Simulation §6.1): every call loads it, replays it, changes
/// it, and saves it atomically while holding a lock file, so two players can act on one match concurrently.
/// </summary>
/// <param name="rules">Rules to replay with.</param>
public sealed class MatchStore(RulesConfig rules)
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _lockRetry = TimeSpan.FromMilliseconds(50);

    /// <summary>The rules matches are played with.</summary>
    public RulesConfig Rules => rules;

    /// <summary>Loads and replays a match file.</summary>
    /// <exception cref="SimException">The file is missing, malformed, or no longer replays.</exception>
    public MatchSession Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SimException($"No match file at '{path}'. Create one with 'new'.");
        }

        MatchRecord record;
        try
        {
            record = MatchRecord.FromJson(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new SimException($"'{path}' is not a valid match file: {ex.Message}");
        }

        return MatchSession.FromRecord(rules, record);
    }

    /// <summary>Writes the match atomically: a temp file next to it, then a rename over the original.</summary>
    public void Save(string path, MatchSession session)
    {
        string fullPath = Path.GetFullPath(path);
        string temp = fullPath + ".tmp";
        File.WriteAllText(temp, session.ToRecord().ToJson());
        File.Move(temp, fullPath, overwrite: true);
    }

    /// <summary>Takes the match's lock file, waiting while another process holds it.</summary>
    /// <returns>A handle that releases (and deletes) the lock when disposed.</returns>
    /// <exception cref="SimException">The lock couldn't be taken in time.</exception>
    public IDisposable Lock(string path)
    {
        string lockPath = Path.GetFullPath(path) + ".lock";
        DateTime deadline = DateTime.UtcNow + _lockTimeout;
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(_lockRetry);
            }
            catch (IOException)
            {
                throw new SimException($"Timed out waiting for the lock on '{path}'.");
            }
        }
    }
}
