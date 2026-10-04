namespace Fantactics.Client.Logic.Board;

/// <summary>Highlights on a tile; several can apply at once.</summary>
[Flags]
public enum TileMark
{
    /// <summary>No highlight.</summary>
    None = 0,

    /// <summary>An objective tile.</summary>
    Objective = 1,

    /// <summary>The selected unit can move or arrive here.</summary>
    Reachable = 2,

    /// <summary>On the previewed path.</summary>
    Path = 4,

    /// <summary>A click here attacks or targets an ability.</summary>
    Target = 8,

    /// <summary>The selected or acting unit stands here.</summary>
    Selected = 16,

    /// <summary>A reserve unit that can still deploy could arrive here.</summary>
    Arrival = 32,

    /// <summary>A shown enemy can move here next turn.</summary>
    ThreatMove = 64,

    /// <summary>A shown enemy could attack here next turn but can't move here.</summary>
    ThreatAttack = 128,
}
