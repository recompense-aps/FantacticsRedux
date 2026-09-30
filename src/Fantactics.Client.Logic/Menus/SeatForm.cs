using System.Collections.Immutable;

namespace Fantactics.Client.Logic.Menus;

/// <summary>One seat's settings in the new-match form.</summary>
/// <param name="Controller">Who plays it: <c>human</c>, <c>llm</c>, or <c>bot:&lt;spec&gt;</c>.</param>
/// <param name="Races">Races it may draft, or <c>null</c> for any.</param>
/// <param name="Budget">Its draft budget.</param>
/// <param name="StartingCap">The most its starting army may cost.</param>
public sealed record SeatForm(string Controller, ImmutableSortedSet<string>? Races, int Budget, int StartingCap);
