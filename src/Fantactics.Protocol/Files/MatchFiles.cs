using System.Text.Json;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;

namespace Fantactics.Protocol.Files;

/// <summary>
/// Reads and writes match files: a <see cref="MatchRecord"/> as JSON, shared by the Sim CLI and the client
/// (Simulation §6.1, TechnicalDesign §4). Writers take the file's lock and save atomically, so several processes can
/// play one match.
/// </summary>
public static class MatchFiles
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _lockRetry = TimeSpan.FromMilliseconds(50);

    /// <summary>Reads a match file.</summary>
    /// <exception cref="FileNotFoundException">There's no file at <paramref name="path"/>.</exception>
    /// <exception cref="JsonException">The file isn't a valid match record.</exception>
    public static MatchRecord Read(string path, RulesConfig rules) =>
        MatchRecord.FromJson(File.ReadAllText(path), rules);

    /// <summary>Writes the record atomically: a temp file next to it, then a rename over the original.</summary>
    public static void Write(string path, MatchRecord record)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        string temp = fullPath + ".tmp";
        File.WriteAllText(temp, record.ToJson());
        File.Move(temp, fullPath, overwrite: true);
    }

    /// <summary>Takes the match's lock file, waiting while another process holds it.</summary>
    /// <returns>A handle that releases (and deletes) the lock when disposed.</returns>
    /// <exception cref="TimeoutException">The lock couldn't be taken in time.</exception>
    public static IDisposable Lock(string path)
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
                throw new TimeoutException($"Timed out waiting for the lock on '{path}'.");
            }
        }
    }
}
