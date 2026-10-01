using System.Collections.Immutable;
using Fantactics.Core;

namespace Fantactics.Sim.Views;

/// <summary>Short text for races in views (GameDesign §4.4).</summary>
public static class RaceText
{
    /// <summary>Units drafted per race, e.g. <c>Elves 5, Goblins 2</c>; empty before the draft reveal.</summary>
    public static string Drafted(ImmutableSortedDictionary<string, int>? drafted) =>
        drafted is null ? "" : string.Join(", ", drafted.Select(pair => $"{pair.Key} {pair.Value}"));

    /// <summary>Races a seat may draft: <c>any</c>, or a comma-separated list.</summary>
    public static string Allowed(ImmutableSortedSet<string>? allowed) =>
        allowed is null ? "any" : string.Join(", ", allowed);

    /// <summary>
    /// Parses each seat's race list (see <see cref="ParseAllowed(string?, IEnumerable{string})"/>) into a match setup's
    /// allowed races; <c>null</c> when every seat may draft every race.
    /// </summary>
    /// <exception cref="SimException">A race is unknown.</exception>
    public static ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? ParseAllowed(
        IReadOnlyDictionary<Seat, string?> lists,
        IEnumerable<string> knownRaces)
    {
        List<string> known = [.. knownRaces];
        return BySeat(lists.ToDictionary(pair => pair.Key, pair => ParseAllowed(pair.Value, known)));
    }

    /// <summary>
    /// A two-player match setup's allowed races from each seat's list (<c>null</c> for any); <c>null</c> when both
    /// are.
    /// </summary>
    public static ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? BySeat(
        ImmutableSortedSet<string>? p1,
        ImmutableSortedSet<string>? p2) =>
        BySeat(new Dictionary<Seat, ImmutableSortedSet<string>?> { [Seat.P1] = p1, [Seat.P2] = p2 });

    /// <summary>
    /// A match setup's allowed races from each seat's list (<c>null</c> for any); <c>null</c> when every list is.
    /// </summary>
    public static ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? BySeat(
        IReadOnlyDictionary<Seat, ImmutableSortedSet<string>?> lists)
    {
        ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>> bySeat = lists
            .Where(pair => pair.Value is not null)
            .ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value!);
        return bySeat.IsEmpty ? null : bySeat;
    }

    /// <summary>
    /// Parses a comma-separated race list from the command line; <c>null</c>, empty, or <c>any</c> allows every race.
    /// </summary>
    /// <exception cref="SimException">A race is unknown.</exception>
    public static ImmutableSortedSet<string>? ParseAllowed(string? value, IEnumerable<string> knownRaces)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        ImmutableSortedSet<string> known = [.. knownRaces];
        ImmutableSortedSet<string> races = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToImmutableSortedSet();
        string? unknown = races.FirstOrDefault(race => !known.Contains(race));
        return unknown is null
            ? races
            : throw new SimException($"Unknown race '{unknown}'. Races: {string.Join(", ", known)}.");
    }
}
