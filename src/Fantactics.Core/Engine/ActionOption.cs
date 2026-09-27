using Fantactics.Core.Commands;

namespace Fantactics.Core.Engine;

/// <summary>A complete, legal action command for the acting unit.</summary>
/// <param name="Command">The command to submit.</param>
/// <param name="Preview">Exact damage outcome, for attacks.</param>
public sealed record ActionOption(ICommand Command, AttackPreview? Preview);
