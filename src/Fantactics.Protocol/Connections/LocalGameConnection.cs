using Fantactics.Core;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;

namespace Fantactics.Protocol.Connections;

/// <summary>A human seat's connection to a <see cref="LocalMatch"/>.</summary>
public sealed class LocalGameConnection : IGameConnection
{
    private readonly LocalMatch _match;
    private SeatUpdate _current;

    internal LocalGameConnection(LocalMatch match, Seat seat, SeatUpdate initial)
    {
        _match = match;
        Seat = seat;
        _current = initial;
    }

    /// <inheritdoc />
    public event Action<SeatUpdate>? Updated;

    /// <inheritdoc />
    public Seat Seat { get; }

    /// <inheritdoc />
    public SeatUpdate Current => Volatile.Read(ref _current);

    /// <inheritdoc />
    public Task<RuleViolation?> SubmitAsync(ICommand command) => _match.SubmitAsync(Seat, command);

    /// <inheritdoc />
    public Task<RuleViolation?> QueueAsync(IUnitActionCommand command) => _match.QueueAsync(Seat, command);

    /// <inheritdoc />
    public Task SetAutoSkipAsync(bool enabled) => _match.SetAutoSkipAsync(Seat, enabled);

    internal void Receive(SeatUpdate update)
    {
        Volatile.Write(ref _current, update);
        Updated?.Invoke(update);
    }
}
