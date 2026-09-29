using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Hosting;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>The HUD's status and prompt lines for a seat's update.</summary>
public static class HudText
{
    /// <summary>
    /// Turn, phase, both armies' races once the drafts are revealed, score, and Command, from the seat's point of view.
    /// </summary>
    public static string Status(PlayerView view, RulesConfig rules)
    {
        PlayerSummary me = view.Players[view.Seat];
        PlayerSummary them = view.Players[view.Seat.Opponent()];
        string races = me.DraftedRaces is null || them.DraftedRaces is null
            ? ""
            : $"{Races(me.DraftedRaces)} vs {Races(them.DraftedRaces)} · ";
        return $"Turn {view.Turn}/{rules.TurnLimit} · {view.Phase} · {view.Seat} · {races}"
            + $"Score {me.DestroyedValue + me.ObjectivePoints}–{them.DestroyedValue + them.ObjectivePoints} · "
            + $"Command {me.Command}";
    }

    /// <summary>What the seat should do now, or who it's waiting for.</summary>
    /// <param name="update">The seat's latest update.</param>
    /// <param name="labelOf">Who plays a seat, for "waiting for" lines (e.g. <c>bot:captain</c>).</param>
    public static string Prompt(SeatUpdate update, Func<Seat, string> labelOf)
    {
        PlayerView view = update.View;
        if (view.Outcome is MatchOutcome outcome)
        {
            return outcome.Winner is Seat winner ? $"{winner} wins ({outcome.Reason})." : $"Draw ({outcome.Reason}).";
        }

        return update.Legal?.Decision switch
        {
            DraftArmyDecision => "Draft your army.",
            PlaceStartingArmyDecision => "Place your starting army.",
            SubmitMoveOrdersDecision =>
                "Move orders: click a unit, then a tile. Right-click clears. Enter submits.",
            ChooseUnitActionDecision decision =>
                $"{TypeOf(view, decision.UnitId)} acts: click a red target, 1–9 for abilities, W to wait, D to delay.",
            _ => "Waiting for " + string.Join(" and ", view.PendingDecisions
                .Select(d => d.Seat)
                .Distinct()
                .Select(seat => $"{seat} ({labelOf(seat)})")) + ".",
        };
    }

    private static string Races(ImmutableSortedDictionary<string, int> drafted) =>
        string.Join(", ", drafted.Select(pair => $"{pair.Key} {pair.Value}"));

    private static string TypeOf(PlayerView view, int unitId) =>
        view.Units.FirstOrDefault(u => u.Id == unitId)?.Type ?? $"Unit {unitId}";
}
