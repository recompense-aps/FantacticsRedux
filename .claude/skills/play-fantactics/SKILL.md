---
name: play-fantactics
description: Play a seat in a Fantactics match through the fantactics-sim CLI (as P1 or P2, against a bot, a human, or another LLM). Use when asked to play, playtest, or self-play Fantactics, or to referee an LLM-vs-LLM match.
---

# Playing Fantactics with fantactics-sim

You are a player in a turn-based tactics match. The match lives in a JSON file; you only ever touch it through the
CLI. Design and rules: `notes/design/Simulation.md` (§6), `notes/design/GameDesign.md` (§4–5),
`notes/design/RacesAndUnits.md`.

## Rules of conduct

- **Never read, open, grep, or edit the match file** (or its `.lock`/`.tmp` siblings). It contains the opponent's
  hidden orders. Use only `status`, `view`, `legal`, `act`, and `log`. `replay` is for after the match ends.
- Play to win, within the rules. Don't probe the engine for exploits by submitting junk.
- Give every decision a short `--note` with your intent. Notes are private until the match ends.
- If a rule seems broken, confusing, or degenerate, say so in a note starting with `RULES?`, e.g.
  `--note "RULES? Scouts can body-block bridges forever"`.

## Setup

Build once, then call the built CLI (faster than `dotnet run`):

```sh
dotnet build src/Fantactics.Sim
SIM="dotnet src/Fantactics.Sim/bin/Debug/net8.0/fantactics-sim.dll"
```

Create a match if you weren't given one (defaults: P1 llm, P2 bot:random, map riverford). Both seats draft from
every race's units; the draft table shows each unit's race and classes. Race auras and abilities (War Cry, Mend) only
reach units of the same race. Limit a seat with `--p1-races Elves` (comma-separated). After both drafts, your view
shows how many units of each race the enemy drafted.

```sh
$SIM new --out playtests/match-1.json --p1 llm --p2 bot:random --seed 7
```

If you're playing against a person in the Godot client, they start the game with `-- --p2 llm --out playtests/<name>.json`
(or load a file with an `llm` seat, or switch a seat to `llm` in the debug panel, F1) and give you the file; the game shows
the path in its top-right notice. Their seat is labeled `human`. Play exactly as below:
`status --wait-for <your seat>` blocks while they think, and the client animates your moves as they land.

For a real opponent, use a bot profile instead of `bot:random`, optionally with a difficulty: `--p2 bot:captain` or `--p2 bot:captain@hard` (difficulties: novice, easy, normal, hard, expert, master). Personalities: `captain` (balanced), `warden` (defensive), `berserker` (aggressive), `trickster` (baits, unpredictable), `bumble` (very weak).

## The loop

Always pass `--format toon` (compact tables; about half the tokens of JSON).

1. `$SIM view <match> --as <seat> --format toon` once, to get oriented: map rows, your units, the enemy's units,
   your reserve, and what you owe.
2. `$SIM legal <match> --as <seat> --format toon` for the options of the pending decision.
3. `$SIM act <match> --as <seat> ... --note "..." --format toon`. The output lists what happened (including bot moves
   that followed), then your next view and options. Keep calling `act` from that output. Go back to `view`/`legal`
   only when you need the full map again.
4. Exit code 3 means it's not your decision: run `status --wait-for <seat>`, which blocks until it is.
   Exit code 2 means your input broke a rule or the grammar; read the message and try again.

## Input forms

| Decision | Form | Example |
|---|---|---|
| Draft | `--draft "starting types \| reserve types"`, from any race | `--draft "Druid Archer Archer Tank Grunt Scout \| Ranger Herbalist"` |
| Placement | `--orders` with `@` for every listed unit | `--orders "A@1,6 B@2,5 C@2,8"` |
| Moves | `--orders`: `X>x,y` move, `X>a,b>x,y` via waypoints, `X=hold`, `X@x,y` deploy a reserve unit | `--orders "D>7,2 F>3,9>5,8 G@0,4"` |
| Action | `--pick N` from the numbered options | `--pick 2` |

- Unit ids: UPPERCASE letters are yours (including reserve units), lowercase are the enemy's. Ids never change.
- Coordinates are `x,y`: x is the column and y the row, counted from the top-left, as in the map rows.
- Units without a move clause hold (Held gives +1 initiative; Braced units get +3 when an enemy moves into contact).
- Move orders are hidden and simultaneous. Enemies entering the same tile, or swapping tiles, clash to the death with
  basic strikes. Zone of control stops a unit that becomes adjacent to an enemy.
- Attack options show exact damage and whether they kill. Combat is deterministic.

## Winning (Deathmatch)

A player whose army value (units on the field plus undeployed reserve) falls below 25% of the draft budget at the end
of a turn loses (Rout). Objective tiles (`*` on the map) score 2 points at the end of each turn for the player who
holds more of them than the opponent. After turn 15, the higher score wins: destroyed enemy value plus objective
points. Sitting back is not safe: an opponent holding the objectives wins on points.

## Self-play (referee)

When asked to run LLM vs LLM:

1. `$SIM new --out playtests/<name>.json --p1 llm --p2 llm --seed N`.
2. Start two background subagents, one per seat, each told to read this skill, to use only `--as <its seat>`, and to
   never read `playtests/`. Each keeps its own context, so neither sees the other's plans.
3. Each subagent plays autonomously: `$SIM status <match> --wait-for <seat> --format toon` blocks until that seat
   owes a decision (or the match ends), then it acts, and repeats. Action slots alternate between seats many times per
   turn, so relaying every handoff through the referee would take hundreds of round trips.
4. When both report back, run `$SIM replay <match> --format text`, then summarize the game and every `RULES?` note.
