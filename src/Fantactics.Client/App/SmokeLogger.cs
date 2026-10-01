using System.Collections.Concurrent;
using Godot;

namespace Fantactics.Client.App;

/// <summary>
/// Collects the errors Godot logs (engine and script errors, and unhandled C# exceptions in callbacks) so a smoke run
/// can fail on them instead of carrying on. Warnings are ignored. Godot may log from any thread.
/// </summary>
public partial class SmokeLogger : Logger
{
    private readonly ConcurrentQueue<string> _errors = new();

    /// <summary>Takes the oldest error logged so far.</summary>
    /// <returns>Whether there was one.</returns>
    public bool TryTake(out string error)
    {
        bool taken = _errors.TryDequeue(out string? next);
        error = next ?? "";
        return taken;
    }

    /// <inheritdoc />
    public override void _LogError(
        string function,
        string file,
        int line,
        string code,
        string rationale,
        bool editorNotify,
        int errorType,
        Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
    {
        if (errorType != (int)ErrorType.Warning)
        {
            string what = string.IsNullOrEmpty(rationale) ? code : $"{rationale} ({code})";
            _errors.Enqueue($"{what} at {file}:{line} in {function}");
        }
    }
}
