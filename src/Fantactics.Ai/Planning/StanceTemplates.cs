using Fantactics.Ai.Profiles;

namespace Fantactics.Ai.Planning;

/// <summary>How each <see cref="Stance"/> reweights a style for building its candidate orders.</summary>
public static class StanceTemplates
{
    /// <summary>Every stance, in a fixed order.</summary>
    public static IReadOnlyList<Stance> All { get; } = Enum.GetValues<Stance>();

    /// <summary>The stances a style considers, each with its bonus.</summary>
    public static IReadOnlyList<(Stance Stance, double Bonus)> For(StyleWeights style) =>
        style.Stances.Count == 0
            ? [.. All.Select(stance => (stance, 0.0))]
            : [.. All
                .Where(stance => style.Stances.ContainsKey(Key(stance)))
                .Select(stance => (stance, style.Stances[Key(stance)]))];

    /// <summary>The weights used to build a stance's orders.</summary>
    public static StyleWeights Apply(Stance stance, StyleWeights style) => stance switch
    {
        Stance.AllIn => style with
        {
            Exposure = style.Exposure * 0.3,
            Advance = style.Advance * 2,
            Opportunity = style.Opportunity * 1.5,
            Hold = 0,
        },
        Stance.HoldLine => style with
        {
            Advance = style.Advance * 0.2,
            Hold = style.Hold * 3 + 0.5,
            Terrain = style.Terrain * 1.5,
        },
        Stance.ObjectivePush => style with
        {
            Objectives = style.Objectives * 2,
            Advance = style.Advance * 1.3,
        },
        Stance.FallBack => style with
        {
            Exposure = style.Exposure * 2,
            Advance = style.Advance * 0.3,
            Terrain = style.Terrain * 2,
        },
        Stance.Bait => style with
        {
            Exposure = style.Exposure * 2,
            Advance = style.Advance * 1.5,
            Opportunity = style.Opportunity * 0.5,
        },
        _ => style,
    };

    /// <summary>The name used for <paramref name="stance"/> in profile JSON, e.g. <c>allIn</c>.</summary>
    public static string Key(Stance stance)
    {
        string name = stance.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
