using Fantactics.Core.Commands;

namespace Fantactics.Core.Records;

/// <summary>One accepted command in a match record.</summary>
/// <param name="Seq">Position in the log, starting at 1.</param>
/// <param name="Seat">Who submitted it.</param>
/// <param name="Command">The command.</param>
/// <param name="StateHashAfter">State hash after applying it.</param>
/// <param name="Note">Optional reasoning from the player, for post-game review. Never shown to the opponent.</param>
public sealed record RecordedCommand(int Seq, Seat Seat, ICommand Command, string StateHashAfter, string? Note);
