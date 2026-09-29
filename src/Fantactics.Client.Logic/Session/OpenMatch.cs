using Fantactics.Protocol.Files;

namespace Fantactics.Client.Logic.Session;

/// <summary>A match the client has open: its session, and the file it's bound to when it's shared with the CLI.</summary>
/// <param name="Session">The session the screen shows.</param>
/// <param name="SharedFile">The shared match file, when an LLM seat (or <c>--out</c>) needs one; otherwise <c>null</c>.</param>
/// <param name="Name">A short name for saves made from it (branch files), usually the file it came from.</param>
public sealed record OpenMatch(ClientSession Session, SharedMatchFile? SharedFile, string Name) : IDisposable
{
    /// <summary>Stops watching the shared file, if any.</summary>
    public void Dispose() => SharedFile?.Dispose();
}
