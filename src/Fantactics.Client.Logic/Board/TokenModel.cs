using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Geometry;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>What a unit token shows.</summary>
/// <param name="Id">The unit, in the viewing seat's ids.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Type">Its type.</param>
/// <param name="Tile">Where it stands.</param>
/// <param name="Hp">Current HP.</param>
/// <param name="MaxHp">Full HP.</param>
/// <param name="Mine">Whether it belongs to the viewing seat.</param>
/// <param name="Held">Whether it held this turn.</param>
/// <param name="Braced">Whether it is Braced this turn.</param>
/// <param name="Statuses">Its statuses.</param>
/// <param name="Acting">Whether it is the unit whose action slot is up.</param>
public sealed record TokenModel(
    int Id,
    Seat Owner,
    string Type,
    Point Tile,
    int Hp,
    int MaxHp,
    bool Mine,
    bool Held,
    bool Braced,
    ImmutableArray<StatusKind> Statuses,
    bool Acting);
