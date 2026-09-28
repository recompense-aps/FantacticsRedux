using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Hosting;
using Fantactics.Core.Records;
using Fantactics.Core.Rules;
using Fantactics.Protocol.Connections;

namespace Fantactics.Protocol.Files;

/// <summary>
/// A match file shared with other processes, typically an LLM playing a seat through the <c>fantactics-sim</c> CLI
/// (TechnicalDesign §2.5). Each operation on the <see cref="LocalMatch"/> takes the file's lock, applies commands the
/// others appended, and saves; <see cref="Watch"/> polls the file so their moves show up without a local operation.
/// If the file stops extending this match's record (someone rewound or replaced it), syncing stops and
/// <see cref="Diverged"/> is raised instead of guessing.
/// </summary>
/// <param name="path">The match file.</param>
/// <param name="rules">Rules to read it with.</param>
public sealed class SharedMatchFile(string path, RulesConfig rules) : IMatchSync, IDisposable
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    private Timer? _poll;
    private DateTime _lastSeenWrite;
    private int _refreshing;

    /// <summary>Raised once, with the reason, when the file no longer extends this match.</summary>
    public event Action<string>? Diverged;

    /// <summary>The match file.</summary>
    public string Path => path;

    /// <summary>Whether syncing stopped because the file diverged.</summary>
    public bool IsDiverged { get; private set; }

    /// <inheritdoc />
    public IDisposable Enter(MatchHost host)
    {
        if (IsDiverged)
        {
            return new Release(() => { });
        }

        IDisposable fileLock = MatchFiles.Lock(path);
        MatchRecord? theirs = File.Exists(path) ? MatchFiles.Read(path, rules) : null;
        if (theirs is not null && (!Extends(theirs, host) || !host.CatchUp(theirs.Commands.Skip(host.Commands.Count))))
        {
            fileLock.Dispose();
            Diverge($"'{path}' no longer continues this match; it was changed by something else.");
            return new Release(() => { });
        }

        foreach ((Seat seat, string label) in theirs?.Setup.Seats ?? ImmutableSortedDictionary<Seat, string>.Empty)
        {
            host.SetSeatLabel(seat, label);
        }

        // What the file holds now; the release only writes if the operation changed it (or there's no file yet).
        string onDisk = theirs is null ? "" : Contents(theirs.Commands.Length, theirs.Setup);
        return new Release(() =>
        {
            try
            {
                if (Contents(host.Commands.Count, host.Setup) != onDisk)
                {
                    MatchFiles.Write(path, host.ToRecord());
                }

                _lastSeenWrite = File.GetLastWriteTimeUtc(path);
            }
            finally
            {
                fileLock.Dispose();
            }
        });
    }

    /// <summary>Polls the file and refreshes <paramref name="match"/> when another process writes it.</summary>
    public void Watch(LocalMatch match)
    {
        _poll?.Dispose();
        _poll = new Timer(_ => Poll(match), null, _pollInterval, _pollInterval);
    }

    /// <summary>Stops watching.</summary>
    public void Dispose() => _poll?.Dispose();

    private static bool Extends(MatchRecord theirs, MatchHost host) =>
        theirs.Commands.Length >= host.Commands.Count
        && host.Commands
            .Zip(theirs.Commands)
            .All(pair => pair.First.Seq == pair.Second.Seq && pair.First.StateHashAfter == pair.Second.StateHashAfter);

    private static string Contents(int commands, MatchSetup setup) => $"{commands}|{string.Join(",", setup.Seats.Values)}";

    private void Poll(LocalMatch match)
    {
        if (IsDiverged || !File.Exists(path) || File.GetLastWriteTimeUtc(path) == _lastSeenWrite
            || Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            return;
        }

        match.RefreshAsync().ContinueWith(_ => Volatile.Write(ref _refreshing, 0), TaskScheduler.Default);
    }

    private void Diverge(string reason)
    {
        IsDiverged = true;
        _poll?.Dispose();
        Diverged?.Invoke(reason);
    }

    /// <summary>Runs an action when disposed.</summary>
    private sealed class Release(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
