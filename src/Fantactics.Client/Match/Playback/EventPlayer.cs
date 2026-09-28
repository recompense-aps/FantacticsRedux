using System.Collections.Immutable;
using Fantactics.Client.Common;
using Fantactics.Client.Logic.Playback;
using Fantactics.Client.Match.Board;
using Fantactics.Client.Match.Hud;
using Fantactics.Core;
using Fantactics.Core.Rules;
using Godot;

namespace Fantactics.Client.Match.Playback;

/// <summary>
/// Plays an update's beats on the board with tweens, one beat at a time, scaled by the speed setting. It never
/// has to leave the board right: the match screen snaps the board to the update's view afterwards, so skipping
/// (<see cref="Skip"/>) simply stops early.
/// </summary>
public partial class EventPlayer : Node
{
    private bool _skipping;

    /// <summary>Whether beats are playing now.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Plays <paramref name="beats"/>.</summary>
    /// <param name="beats">What to play.</param>
    /// <param name="board">The board to animate.</param>
    /// <param name="hud">The HUD, for banners.</param>
    /// <param name="speed">Speed multiplier; 0 or less plays nothing.</param>
    /// <param name="viewer">The seat watching (its units are "mine").</param>
    /// <param name="rules">Rules, for unit stats.</param>
    public async Task Play(
        ImmutableArray<Beat> beats,
        BoardView board,
        MatchHud hud,
        double speed,
        Seat viewer,
        RulesConfig rules)
    {
        if (speed <= 0 || beats.IsEmpty)
        {
            return;
        }

        IsPlaying = true;
        _skipping = false;
        foreach (Beat beat in beats)
        {
            if (_skipping)
            {
                break;
            }

            float seconds = 0;
            foreach (Step step in beat.Steps)
            {
                seconds = Mathf.Max(seconds, PlayStep(step, board, hud, (float)(1 / speed), viewer, rules));
            }

            if (seconds > 0)
            {
                await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
            }
        }

        IsPlaying = false;
    }

    /// <summary>Stops playing the current update; the board then snaps to its end.</summary>
    public void Skip() => _skipping = true;

    /// <summary>Starts one step's animation.</summary>
    /// <returns>How long it takes, in seconds.</returns>
    private float PlayStep(Step step, BoardView board, MatchHud hud, float scale, Seat viewer, RulesConfig rules)
    {
        switch (step)
        {
            case MoveStep move when board.Token(move.UnitId) is UnitToken token:
                CreateTween().TweenProperty(token, "position", move.To.TileCenter(), 0.15f * scale);
                return 0.15f * scale;
            case StrikeStep strike when board.Token(strike.TargetId) is UnitToken target:
                target.ShowHp(strike.HpAfter);
                if (board.Token(strike.AttackerId) is UnitToken attacker)
                {
                    Vector2 home = attacker.Position;
                    Tween lunge = CreateTween();
                    lunge.TweenProperty(attacker, "position", home.Lerp(target.Position, 0.3f), 0.1f * scale);
                    lunge.TweenProperty(attacker, "position", home, 0.1f * scale);
                }

                board.FloatText(GodotConversions.TileAt(target.Position), $"-{strike.Damage}", Colors.OrangeRed, 0.4f * scale);
                return 0.3f * scale;
            case HealStep heal when board.Token(heal.UnitId) is UnitToken healed:
                healed.ShowHp(heal.HpAfter);
                board.FloatText(GodotConversions.TileAt(healed.Position), $"+{heal.Amount}", Colors.LimeGreen, 0.4f * scale);
                return 0.3f * scale;
            case DeathStep death when board.Token(death.UnitId) is UnitToken dying:
                CreateTween().TweenProperty(dying, "modulate:a", 0f, 0.3f * scale);
                return 0.3f * scale;
            case AppearStep appear:
                UnitToken arrived = board.Spawn(
                    appear.UnitId, appear.Owner, appear.Type, appear.Tile, appear.Owner == viewer, rules.Units[appear.Type].Hp);
                arrived.Scale = Vector2.Zero;
                CreateTween().TweenProperty(arrived, "scale", Vector2.One, 0.2f * scale);
                return 0.2f * scale;
            case StatusStep status when board.Token(status.UnitId) is UnitToken affected:
                string sign = status.Applied ? "" : "no longer ";
                board.FloatText(GodotConversions.TileAt(affected.Position), sign + status.Status, Colors.MediumPurple, 0.5f * scale);
                return 0.3f * scale;
            case AbilityStep ability when board.Token(ability.UnitId) is UnitToken caster:
                board.FloatText(ability.Target ?? GodotConversions.TileAt(caster.Position), ability.Ability, Colors.White, 0.5f * scale);
                return 0.35f * scale;
            case TileStep tile:
                board.SetTerrain(tile.Tile, tile.Terrain);
                return 0.2f * scale;
            case ClashStep clash when clash.Tile is not null || board.Token(clash.UnitA) is not null:
                board.FloatText(
                    clash.Tile ?? GodotConversions.TileAt(board.Token(clash.UnitA)?.Position ?? Vector2.Zero),
                    "Clash!",
                    Colors.Yellow,
                    0.5f * scale);
                return 0.3f * scale;
            case BannerStep banner:
                hud.ShowBanner(banner.Text, 0.9f * scale);
                return 0.9f * scale;
            default:
                return 0;
        }
    }
}
