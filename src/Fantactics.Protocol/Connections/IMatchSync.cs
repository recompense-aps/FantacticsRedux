using Fantactics.Core.Hosting;

namespace Fantactics.Protocol.Connections;

/// <summary>
/// Keeps a <see cref="LocalMatch"/> in step with storage other processes also write (a shared match file). Every
/// operation on the match runs between <see cref="Enter"/> and disposing the handle it returns, under the match's lock.
/// </summary>
public interface IMatchSync
{
    /// <summary>
    /// Called before an operation: takes whatever external lock the storage needs and applies commands others
    /// appended (<see cref="MatchHost.CatchUp"/>). Disposing the handle saves the host's record and releases the lock.
    /// </summary>
    IDisposable Enter(MatchHost host);
}
