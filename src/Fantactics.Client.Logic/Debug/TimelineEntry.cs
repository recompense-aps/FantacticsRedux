using Fantactics.Core;

namespace Fantactics.Client.Logic.Debug;

/// <summary>One command in the debug timeline; clicking it branches the match from right after it.</summary>
/// <param name="Seq">The command's sequence number.</param>
/// <param name="Turn">The turn it was given in.</param>
/// <param name="Seat">Who gave it.</param>
/// <param name="Text">A one-line description.</param>
public sealed record TimelineEntry(int Seq, int Turn, Seat Seat, string Text);
