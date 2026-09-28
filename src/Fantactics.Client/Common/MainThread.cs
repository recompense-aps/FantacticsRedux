using Godot;

namespace Fantactics.Client.Common;

/// <summary>Runs work on Godot's main thread. Match updates arrive on a worker thread and must never touch nodes there.</summary>
public static class MainThread
{
    /// <summary>Runs <paramref name="action"/> on the main thread during the next idle time. Safe from any thread.</summary>
    public static void Post(Action action) => Callable.From(action).CallDeferred();
}
