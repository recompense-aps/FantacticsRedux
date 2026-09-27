using System.Text;
using Fantactics.Sim.Tournaments;
using Fantactics.Sim.Views;

namespace Fantactics.Sim.Output;

/// <summary>Renders result DTOs as aligned text for humans at a terminal.</summary>
public static class TextRenderer
{
    /// <summary>Renders any result DTO.</summary>
    public static string Render(object value) => value switch
    {
        SeatView view => View(view),
        LegalView legal => Legal(legal),
        ActResult act => Act(act),
        StatusView status => Status(status),
        LogView log => Events(log.Events),
        ErrorResult error => $"error ({error.Code}): {error.Message}",
        TournamentSummary summary => Tournament(summary),
        _ => value.ToString() ?? "",
    };

    private static string View(SeatView view)
    {
        StringBuilder text = new();
        text.AppendLine(
            $"Turn {view.Turn} · {view.Phase} · You: {view.You} ({view.Me.Race}) · Command {view.Me.Command} · "
            + $"Army {view.Me.Army} vs {view.Enemy.Army} · Destroyed {view.Me.Destroyed} vs {view.Enemy.Destroyed} · "
            + $"Tie priority {view.TiePriority}");
        text.AppendLine(view.Outcome is string outcome
            ? $"Match over: {outcome}"
            : view.Pending is PendingInfo pending
                ? $"You decide: {pending.Kind}{(pending.Unit is string unit ? $" for {unit}" : "")}"
                : $"Waiting for: {string.Join(", ", view.WaitingFor)}");

        if (view.Rows is { } rows)
        {
            text.AppendLine();
            text.Append(Map(rows));
            text.AppendLine(view.Legend);
        }

        text.AppendLine();
        text.Append(Table(
            ["ID", "Side", "Type", "Pos", "Tile", "HP", "Atk", "Def", "Mov", "Rng", "Init", "Status"],
            view.Units.Select(u => new[]
            {
                u.Id, u.Side, u.Type, $"{u.X},{u.Y}", u.Tile, $"{u.Hp}/{u.MaxHp}", $"{u.Atk}", $"{u.Def}", $"{u.Mov}",
                u.Rng, $"{u.Init}", u.Status,
            })));

        if (!view.Reserve.IsEmpty)
        {
            text.AppendLine();
            text.Append(Table(
                ["ID", "Type", "Where", "Cost", "Deploy"],
                view.Reserve.Select(r => new[] { r.Id, r.Type, r.Where, $"{r.Cost}", $"{r.DeployCost}" })));
        }

        if (view.Order is string order)
        {
            text.AppendLine().AppendLine($"Action order: {order}");
        }

        if (!view.Recent.IsEmpty)
        {
            text.AppendLine().AppendLine("Since your last command:").Append(Events(view.Recent));
        }

        return text.ToString().TrimEnd();
    }

    private static string Legal(LegalView legal)
    {
        StringBuilder text = new();
        text.AppendLine($"{legal.Kind}{(legal.Unit is string unit ? $" for {unit}" : "")} — {legal.Usage}");
        if (legal.Options is { } options)
        {
            text.Append(Table(
                ["Pick", "Action", "Target", "Tile", "Dmg", "Kills"],
                options.Select(o => new[]
                {
                    $"{o.Pick}", o.Action, o.Target, o.Tile, o.Dmg > 0 ? $"{o.Dmg}" : "", o.Kills ? "yes" : "",
                })));
        }

        if (legal.Reach is { } reach)
        {
            text.AppendLine($"Command {legal.Command}, at most {legal.MaxArrivals} arrivals.");
            foreach (IGrouping<string, ReachRow> group in reach.GroupBy(r => r.Unit))
            {
                text.AppendLine($"{group.Key}: {string.Join(" ", group.Select(r => $"{r.X},{r.Y}({r.Cost})"))}");
            }
        }

        if (legal.Deploys is { IsEmpty: false } deploys)
        {
            text.Append(Table(
                ["Reserve", "Type", "Cost", "Tiles"],
                deploys.Select(d => new[] { d.Unit, d.Type, $"{d.Cost}", d.Tiles })));
        }

        if (legal.Draftable is { } draftable)
        {
            text.AppendLine($"Budget {legal.Budget}, starting cap {legal.StartingCap}.");
            text.Append(Table(
                ["Type", "Cost", "HP", "Atk", "Def", "Mov", "Rng", "Init", "Unique", "Traits"],
                draftable.Select(d => new[]
                {
                    d.Type, $"{d.Cost}", $"{d.Hp}", $"{d.Atk}", $"{d.Def}", $"{d.Mov}", d.Rng, $"{d.Init}",
                    d.Unique ? "yes" : "", d.Traits,
                })));
        }

        if (legal.ToPlace is { } toPlace)
        {
            text.AppendLine($"Place: {string.Join(", ", toPlace)}");
            text.AppendLine($"Tiles: {legal.PlaceTiles}");
        }

        return text.ToString().TrimEnd();
    }

    private static string Act(ActResult act)
    {
        StringBuilder text = new();
        text.Append(Events(act.Events));
        if (act.Outcome is string outcome)
        {
            text.AppendLine().AppendLine($"Match over: {outcome}");
        }
        else if (act.View is SeatView view)
        {
            text.AppendLine().AppendLine(View(view with { Recent = [] }));
            if (act.Legal is LegalView legal)
            {
                text.AppendLine().AppendLine(Legal(legal));
            }
        }
        else
        {
            text.AppendLine().AppendLine($"Waiting for: {string.Join(", ", act.WaitingFor)}");
        }

        return text.ToString().TrimEnd();
    }

    private static string Status(StatusView status)
    {
        StringBuilder text = new();
        text.AppendLine(status.Outcome is string outcome
            ? $"Match over after turn {status.Turn}: {outcome}"
            : $"Turn {status.Turn} · {status.Phase} · {status.Commands} commands");
        text.Append(Table(
            ["Seat", "Player", "Race", "Owes"],
            status.Seats.Select(s => new[] { s.Seat, s.Player, s.Race, s.Owes })));
        return text.ToString().TrimEnd();
    }

    private static string Tournament(TournamentSummary summary)
    {
        StringBuilder text = new();
        text.AppendLine(
            $"{summary.Games} games · P1 {summary.P1} ({summary.P1Wins} wins) vs P2 {summary.P2} ({summary.P2Wins} wins)"
            + $" · {summary.Draws} draws · avg {summary.AverageTurns:F1} turns");
        text.Append(Table(
            ["End", "Count"],
            summary.EndReasons.Select(e => new[] { e.Reason, $"{e.Count}" })));
        text.AppendLine();
        text.Append(Table(
            ["Seat", "Type", "Fielded", "Damage", "Kills", "Deaths"],
            summary.UnitStats.Select(u => new[]
            {
                u.Seat, u.Type, $"{u.Fielded}", $"{u.Damage}", $"{u.Kills}", $"{u.Deaths}",
            })));
        return text.ToString().TrimEnd();
    }

    private static string Events(IEnumerable<EventLine> events) =>
        Table(
            ["Seq", "Turn", "Event", "Who", "Target", "Detail"],
            events.Select(e => new[] { $"{e.Seq}", $"{e.Turn}", e.Type, e.Actor, e.Target, e.Detail }));

    private static string Map(IReadOnlyList<string> rows)
    {
        int width = rows.Count == 0 ? 0 : rows[0].Length;
        StringBuilder text = new();
        text.AppendLine("    " + new string(Enumerable.Range(0, width).Select(x => x >= 10 ? (char)('0' + x / 10) : ' ').ToArray()));
        text.AppendLine("    " + new string(Enumerable.Range(0, width).Select(x => (char)('0' + x % 10)).ToArray()));
        foreach ((string row, int y) in rows.Select((row, y) => (row, y)))
        {
            text.AppendLine($"{y,3} {row}");
        }

        return text.ToString();
    }

    private static string Table(string[] headers, IEnumerable<string[]> rows)
    {
        List<string[]> all = [headers, .. rows];
        int[] widths = headers.Select((_, column) => all.Max(row => row[column].Length)).ToArray();
        StringBuilder text = new();
        foreach (string[] row in all)
        {
            text.AppendLine(string.Join("  ", row.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
        }

        return text.ToString();
    }
}
