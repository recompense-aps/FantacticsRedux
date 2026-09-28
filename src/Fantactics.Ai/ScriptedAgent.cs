using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Players;

namespace Fantactics.Ai;

/// <summary>Plays a fixed queue of commands, for tests.</summary>
/// <param name="commands">Commands to play, in order.</param>
public sealed class ScriptedAgent(IEnumerable<ICommand> commands) : IPlayerAgent
{
    private readonly Queue<ICommand> _commands = new(commands);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The script ran out of commands.</exception>
    public ICommand Decide(PlayerView view, Decision decision, LegalActions legal) =>
        _commands.TryDequeue(out ICommand? command)
            ? command
            : throw new InvalidOperationException($"Script exhausted at {decision}.");
}
