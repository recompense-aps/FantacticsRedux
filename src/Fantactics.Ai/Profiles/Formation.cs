namespace Fantactics.Ai.Profiles;

/// <summary>How a bot places its starting army.</summary>
public enum Formation
{
    /// <summary>A compact group facing the objectives.</summary>
    Block,

    /// <summary>A line along the front of the deploy zone, spread over its full height, on the best cover.</summary>
    Line,

    /// <summary>Two groups toward the top and bottom of the map, ready to flank.</summary>
    Flanks,
}
