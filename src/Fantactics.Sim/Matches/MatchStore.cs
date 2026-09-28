using System.Text.Json;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Files;

namespace Fantactics.Sim.Matches;

/// <summary>
/// Loads and saves match files for the CLI. The file is the match (Simulation §6.1): every call loads it, replays it,
/// changes it, and saves it atomically while holding its lock (<see cref="MatchFiles"/>, shared with the client), so
/// several players, including the Godot client, can act on one match concurrently.
/// </summary>
/// <param name="rules">Rules to replay with.</param>
public sealed class MatchStore(RulesConfig rules)
{
    /// <summary>The rules matches are played with.</summary>
    public RulesConfig Rules => rules;

    /// <summary>Loads and resumes a match file.</summary>
    /// <exception cref="SimException">The file is missing, malformed, or no longer replays.</exception>
    public MatchSession Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SimException($"No match file at '{path}'. Create one with 'new'.");
        }

        try
        {
            return MatchSession.FromRecord(rules, MatchFiles.Read(path, rules));
        }
        catch (JsonException ex)
        {
            throw new SimException($"'{path}' is not a valid match file: {ex.Message}");
        }
    }

    /// <summary>Writes the match atomically.</summary>
    public void Save(string path, MatchSession session) => MatchFiles.Write(path, session.ToRecord());

    /// <summary>Takes the match's lock file, waiting while another process holds it.</summary>
    /// <returns>A handle that releases (and deletes) the lock when disposed.</returns>
    /// <exception cref="SimException">The lock couldn't be taken in time.</exception>
    public IDisposable Lock(string path)
    {
        try
        {
            return MatchFiles.Lock(path);
        }
        catch (TimeoutException ex)
        {
            throw new SimException(ex.Message);
        }
    }
}
