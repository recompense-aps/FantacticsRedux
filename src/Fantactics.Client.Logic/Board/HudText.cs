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
    /// Turn, phase, score, and Command from the seat's point of view. One against one, it also shows both armies'
    /// races once the drafts are revealed and the opponent's score; with more players, <see cref="Players"/> lists
    /// them.
    /// </summary>
    public static string Status(PlayerView view, RulesConfig rules)
    {
        PlayerSummary me = view.Players[view.Seat];
        if (view.Players.Count != 2)
        {
            return $"Turn {view.Turn}/{rules.TurnLimit} · {view.Phase} · {view.Seat} · "
                + $"Score {me.DestroyedValue + me.ObjectivePoints} · Command {me.Command}";
        }

        PlayerSummary them = view.Players[view.SoleOpponent()];
        string races = me.DraftedRaces is null || them.DraftedRaces is null
            ? ""
            : $"{Races(me.DraftedRaces)} vs {Races(them.DraftedRaces)} · ";
        return $"Turn {view.Turn}/{rules.TurnLimit} · {view.Phase} · {view.Seat} · {races}"
            + $"Score {me.DestroyedValue + me.ObjectivePoints}–{them.DestroyedValue + them.ObjectivePoints} · "
            + $"Command {me.Command}";
    }

    /// <summary>
    /// One line per player for the HUD's player list, in seat order: who they are to the viewer, their races once
    /// revealed, army value, and score, e.g. <c>P3 · enemy · Goblins 6 · Army 31 · Score 4</c>.
    /// </summary>
    public static IReadOnlyList<(Seat Seat, string Text)> Players(PlayerView view) =>
        view.Players.Values
            .Select(player => (player.Seat, string.Join(" · ", new[]
                {
                    player.Seat.ToString(),
                    player.Seat == view.Seat ? "you"
                        : player.Eliminated ? "out"
                        : view.AreEnemies(player.Seat, view.Seat) ? "enemy"
                        : "ally",
                    player.DraftedRaces is { } drafted ? Races(drafted) : "",
                    $"Army {player.ArmyValue}",
                    $"Score {player.DestroyedValue + player.ObjectivePoints}",
                }
                .Where(part => part.Length > 0))))
            .ToList();

    /// <summary>What the seat should do now, or who it's waiting for.</summary>
    /// <param name="update">The seat's latest update.</param>
    /// <param name="labelOf">Who plays a seat, for "waiting for" lines (e.g. <c>bot:captain</c>).</param>
    public static string Prompt(SeatUpdate update, Func<Seat, string> labelOf)
    {
        PlayerView view = update.View;
        if (view.Outcome is MatchOutcome outcome)
        {
            return $"{outcome.Headline()} ({outcome.Reason}).";
        }

        return update.Legal?.Decision switch
        {
            DraftArmyDecision => "Draft your army: pick starting units and reserves within your budget.",
            PlaceStartingArmyDecision => "Place your starting army: click a highlighted tile for each unit."
                + EnemyDrafts(view),
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

    /// <summary>What the enemy drafted, for the placement prompt; per seat when there's more than one enemy.</summary>
    private static string EnemyDrafts(PlayerView view)
    {
        List<PlayerSummary> enemies = view.Opponents()
            .Select(seat => view.Players[seat])
            .Where(enemy => enemy.DraftedRaces is not null)
            .ToList();
        return enemies switch
        {
            [] => "",
            [PlayerSummary enemy] => $" Enemy drafted {Races(enemy.DraftedRaces!)}.",
            _ => " Enemies drafted "
                + string.Join("; ", enemies.Select(e => $"{e.Seat}: {Races(e.DraftedRaces!)}"))
                + ".",
        };
    }

    private static string Races(ImmutableSortedDictionary<string, int> drafted) =>
        string.Join(", ", drafted.Select(pair => $"{pair.Key} {pair.Value}"));

    private static string TypeOf(PlayerView view, int unitId) =>
        view.Units.FirstOrDefault(u => u.Id == unitId)?.Type ?? $"Unit {unitId}";
}
