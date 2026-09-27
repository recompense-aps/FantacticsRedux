using System.Collections.Immutable;

namespace Fantactics.Core.Commands;

/// <summary>A player's hidden draft (GameDesign §4.4).</summary>
/// <param name="Starting">Unit types deployed at the start; total Cost at most the starting cap.</param>
/// <param name="Reserve">Unit types held back as reserves.</param>
public sealed record SubmitDraft(ImmutableArray<string> Starting, ImmutableArray<string> Reserve) : ICommand;
