# Launch Options

The Godot client reads options from its command line, so you can skip the menus and go straight into a match: a new one with chosen seats and draft limits, a save file, or a match shared with an LLM. Tests and screenshots use the same options. The parser is `LaunchArgs` (`src/Fantactics.Client.Logic/Launch/`); TechnicalDesign §2.5 and §4 cover the design behind it.

## Passing options

Game options go after a bare `--`. Godot's own options (`--path`, `--headless`) go before it.

```sh
GODOT="C:/Users/alex/code/godot/godot4.7.2/Godot_v4.7.2-stable_mono_win64_console.exe"
"$GODOT" --path src/Fantactics.Client -- --p2 bot:captain@easy
```

Use the `_console` binary from a shell so that `GD.Print` output and exit codes reach the terminal. To pass options when running from the editor, put them in **Project Settings → Editor → Run → Main Run Args** (`editor/run/main_run_args`), e.g. `-- --load playtests/quick.json`.

With no options the game opens the main menu. It goes straight into a match only when you give `--new`, `--p1` to `--p4`, `--load`, or `--autoplay`. Options that don't start a match (such as `--saves` or `--speed`) still apply to matches started from the menu.

An unknown option, a missing value, or a bad number prints an error and exits with code 1.

## Seat labels

`--p1` to `--p4` take the same seat labels as `fantactics-sim`. The label is stored in the match file.

| Label | Who plays |
|---|---|
| `human` | You, at this machine. With more than one `human` seat, you play hotseat with a curtain between turns. |
| `llm` | An LLM (e.g. Claude with the `play-fantactics` skill) through `fantactics-sim` on a shared match file. See [Playing against an LLM](#playing-against-an-llm). |
| `bot:<profile>[@<difficulty>]` | A computer player. Profiles: `captain` (balanced), `warden` (defensive), `berserker` (aggressive), `trickster` (baits, unpredictable), `bumble` (very weak), `random`. Difficulties: `novice`, `easy`, `normal`, `hard`, `expert`, `master`. Leave the difficulty out to use the profile's default. |

## Starting a new match

| Option | Default | Meaning |
|---|---|---|
| `--new` | | Start a new match. Implied by `--p1` to `--p4`. |
| `--p1 <label>` | `human` | Who plays P1. |
| `--p2 <label>` | `bot:captain@easy` | Who plays P2. |
| `--p3 <label>`, `--p4 <label>` | none | Who plays P3 and P4 (GameDesign §3). Only maps laid out for them take them (`crossroads` takes four); give `--p3` before `--p4`. |
| `--teams <list>` | everyone on their own | Each seat's team in seat order, e.g. `1,2,1,2` for P1+P3 against P2+P4. |
| `--map <name>` | `riverford` | The map (from `src/Fantactics.Core/Maps/Data/`): `riverford` (2 seats) or `crossroads` (2–4 seats). |
| `--seed <n>` | random | The rules seed. The same seed and the same commands give the same match. |
| `--draft-as <profile\|none>` | `none` | A bot profile that drafts and places for every `human` seat, so the match opens at turn 1. `none` means you use the draft and placement screens. |
| `--out <file>` | | Keep the match in this file (the Sim's match format), shared with `fantactics-sim`. `llm` seats need a file and get `match-<date>-<time>.json` in the saves folder when this is left out. |

### Draft limits (open draft, GameDesign §4.4)

Every seat may draft units of every race unless you limit it. Budget and cap options accept positive whole numbers. A per-seat option overrides the all-seats one; P3 and P4 use the all-seats values.

| Option | Default | Meaning |
|---|---|---|
| `--p1-races <list>` to `--p4-races <list>` | `any` | Comma-separated races the seat may draft, e.g. `Elves` or `Elves,Goblins`; `any` allows every race. |
| `--budget <n>` | rules' `draftBudget` (40) | Draft points for every seat. |
| `--p1-budget <n>`, `--p2-budget <n>` | `--budget` | One seat's draft points. |
| `--starting-cap <n>` | rules' `startingCap` (30) | The most the starting army may cost, for every seat. |
| `--p1-starting-cap <n>`, `--p2-starting-cap <n>` | `--starting-cap` | One seat's starting cap. |

## Loading a match

| Option | Meaning |
|---|---|
| `--load <file>` | Continue a match file: an autosave, a quicksave (`quick.json`), a branch (`<name>.b<seq>.json`), or a `fantactics-sim` match file. |
| `--as <P1\|P2\|P3\|P4>` | The seat to show first. Defaults to the first `human` seat. |

Loading replays the file's command log and checks it against its snapshot. If the snapshot was hand-edited, or the rules have changed so the log no longer replays, the match continues from the snapshot and a warning appears in the top right (TechnicalDesign §4). A file with an `llm` seat is played on that file, shared with the CLI. Any other file loads into memory: playing on autosaves to `autosave.json` and leaves the loaded file unchanged.

## Files and saves

| Option | Default | Meaning |
|---|---|---|
| `--saves <dir>` | `<repo>/playtests/` in development; `user://saves` in exported builds | Where autosaves, quicksaves, branches, and new LLM match files go, and what the Load screen lists. |

In a match, F5 quicksaves to `quick.json` and F9 loads it. The debug panel (F1) can branch from any command in the timeline.

## Presentation and testing

| Option | Meaning |
|---|---|
| `--speed <n>` | Animation speed multiplier for this run (`1`, `2`, `4`; `0` is instant). Without it, the game uses the Settings value (instant for `--autoplay` and `--drive`). |
| `--debug` | Open the debug panel (F1) at the start. |
| `--menu <main\|new\|load\|settings>` | Open this menu screen instead of the main menu. Pair it with `--screenshot` to check a menu's layout. |
| `--screenshot <png>` | Save a picture of the screen after about 4 seconds, then quit with code 0. Use it to check layout without looking. |
| `--autoplay` | The smoke test: human seats become `bot:captain@easy` (`llm` seats stay), animations are instant unless `--speed` is given, and the match plays to the end. Exit code 0 when it finishes, 1 on a failure, 2 after a 5-minute timeout. With no other options it starts a new bot-vs-bot match; with `--load` it finishes that file. |
| `--drive` | The input smoke test: human seats stay human and are played only through synthetic input, the way a person would: clicks on the real buttons and board tiles, and the keys bound to InputMap actions (Enter, Esc, W, D, 1–9). A `bot:captain@easy` picks each answer. With no match options it starts at the main menu and clicks **New match…** then **Start** (`--menu new` starts at the second); with `--p1`…`--p4` or `--load` it starts in that match. Hotseat curtains are clicked through. Animations are instant unless `--speed` is given; exit codes as for `--autoplay`, and it prints how many decisions it drove. It fails, naming the step, when a button it needs isn't there or a decision is still on screen 10 s after its input ran. Can't be combined with `--autoplay`. |
| `--shots <dir>` | With `--drive`, save a screenshot into `<dir>` the first time each screen comes up (main menu, new match, draft, placement, orders, action; numbered in order), and at every hotseat handoff three more: `curtain-NNN-P2` (the curtain), `lifted-NNN-P2` (just after it lifts, while that seat's update plays) and `shown-NNN-P2` (once it waits on input). Use these to check that the curtain hides each seat's hidden choices; add `--speed 1` to see the animations after a lift. Needs a window, so leave out `--headless`. |

Both smoke tests also fail (exit code 1) when Godot logs any error, such as an exception in a node script, or when a node's required `[Export]` (one not declared nullable) is still unassigned when it enters the tree.

## Recipes

```sh
# Draft by hand against an easy bot
-- --p2 bot:captain@easy

# Skip the draft: a bot drafts and places for you
-- --p2 bot:warden@hard --draft-as captain

# Hotseat on one screen
-- --p1 human --p2 human

# Elves only against Goblins only, with a bigger P2 budget, on a fixed seed
-- --p1-races Elves --p2-races Goblins --p2-budget 50 --seed 7

# Four players on Crossroads, two against two (you and P3 against two bots)
-- --map crossroads --p2 bot:captain --p3 bot:captain --p4 bot:captain --teams 1,2,1,2

# Four-bot smoke test, free-for-all
--headless --path src/Fantactics.Client -- --autoplay --map crossroads --p1 bot:captain@easy --p2 bot:captain@easy --p3 bot:captain@easy --p4 bot:captain@easy

# Continue the autosave (or any save) as P2
-- --load playtests/autosave.json --as P2

# Play against Claude: give Claude the file and ask it to play P2 with the play-fantactics skill
-- --p2 llm --out playtests/vs-claude.json

# Headless smoke test
--headless --path src/Fantactics.Client -- --autoplay

# Input smoke tests: from the main menu, and hotseat with curtains
--headless --path src/Fantactics.Client -- --drive
--headless --path src/Fantactics.Client -- --drive --p1 human --p2 human

# Drive with a window and keep screenshots of each screen
--path src/Fantactics.Client -- --drive --shots playtests/shots

# Check the draft screen's layout
--path src/Fantactics.Client -- --p2 bot:captain --seed 3 --screenshot shot.png
```

(Each line after `--` goes at the end of `"$GODOT" --path src/Fantactics.Client`. The last two show Godot's own options too.)

## Playing against an LLM

1. Start a match with an `llm` seat: `-- --p2 llm --out playtests/<name>.json`, **New match** with Player `llm`, `--load` a file with an `llm` seat, or switch a seat to `llm` in the debug panel (F1), which moves the match to a new file.
2. The game shows the file's path in the top right. Ask Claude to play that seat in that file with the `play-fantactics` skill.
3. Claude's moves animate as they land (the client checks the file every 500 ms). While you wait, the prompt line shows how long the LLM has been thinking.

Only one process should play each seat: the game plays `human` and `bot` seats, and the CLI plays `llm` seats. If the file stops matching the game's own copy of the match (e.g. it was edited or replaced), syncing stops with a warning and the match carries on in the game only.
