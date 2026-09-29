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
    /// Parses both seats' race lists (see <see cref="ParseAllowed"/>) into a match setup's allowed races; <c>null</c>
    /// when both seats may draft every race.
    /// </summary>
    /// <exception cref="SimException">A race is unknown.</exception>
    public static ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? ParseAllowed(
        string? p1,
        string? p2,
        IEnumerable<string> knownRaces) =>
        BySeat(ParseAllowed(p1, knownRaces), ParseAllowed(p2, knownRaces));

    /// <summary>
    /// A match setup's allowed races from each seat's list (<c>null</c> for any); <c>null</c> when both are.
    /// </summary>
    public static ImmutableSortedDictionary<Seat, ImmutableSortedSet<string>>? BySeat(
        ImmutableSortedSet<string>? p1,
        ImmutableSortedSet<string>? p2)
    {
        var bySeat = ImmutableSortedDictionary.CreateBuilder<Seat, ImmutableSortedSet<string>>();
        if (p1 is not null)
        {
            bySeat[Seat.P1] = p1;
        }

        if (p2 is not null)
        {
            bySeat[Seat.P2] = p2;
        }

        return bySeat.Count == 0 ? null : bySeat.ToImmutable();
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
