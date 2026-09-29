# Fantactics

Turn-based fantasy tactics game. Godot 4.7.2 (Mono) with C# on .NET 8. Design docs are in `notes/design/` (start there); raw early brainstorms are in `notes/og/`.

## Layout

All code lives under `src/`; the repo root holds only docs and config.

- `src/Fantactics.sln`
- `src/Fantactics.Core`: game rules and state. Must not reference Godot or networking.
- `src/Fantactics.Protocol`: wire DTOs shared by client and server, `IGameConnection` (the client's only link to a match) with its in-process `LocalMatch`, and match files (`MatchFiles`, `SharedMatchFile` for LLM seats) shared with the Sim CLI. Core's `MatchHost` is the match wrapper behind it (TechnicalDesign §2.4–2.5).
- `src/Fantactics.Server`: ASP.NET Core + SignalR server.
- `src/Fantactics.Client`: Godot project (`project.godot` lives here). Scenes and nodes only; see Godot Conventions below.
- `src/Fantactics.Client.Logic`: the client's presentation logic without Godot (input builders, playback timeline, board model, session, launch options), tested by `src/tests/Fantactics.Client.Logic.Tests`.
- `src/Fantactics.Ai`: computer players (`TacticalAgent`, `RandomAgent`, implementing Core's `IPlayerAgent`) and `MatchRunner` for in-memory matches. References Core only. Bot profiles and difficulty presets are embedded JSON in `Profiles/Data/`; bots are named `profile[@difficulty]` (e.g. `captain@easy`).
- `src/Fantactics.Sim`: the `fantactics-sim` CLI (file-backed matches for LLM/human seats, bot tournaments). See `notes/design/Simulation.md` §6.
- `src/tests/Fantactics.Core.Tests`: xUnit rules tests for Core (built with `ScenarioBuilder`).
- `src/tests/Fantactics.Ai.Tests`: fuzz, determinism, replay, and legal-actions agreement tests.
- `src/tests/Fantactics.Sim.Tests`: CLI grammar tests and in-process CLI tests.

Core's rules data (unit stats, tunable numbers) is `src/Fantactics.Core/Rules/Data/mvp-rules.json`; maps are ASCII files in `src/Fantactics.Core/Maps/Data/`. Both are embedded resources. When a rule's behavior changes, bump `GameEngine.RulesVersion`.

## Godot

- Editor binary: `C:\Users\alex\code\godot\godot4.7.2\Godot_v4.7.2-stable_mono_win64.exe`
- Console binary (use this from the shell so output reaches stdout): `C:\Users\alex\code\godot\godot4.7.2\Godot_v4.7.2-stable_mono_win64_console.exe`

## Commands

- Build everything: `dotnet build src/Fantactics.sln`
- Test: `dotnet test src/Fantactics.sln`
- Run server: `dotnet run --project src/Fantactics.Server`
- Simulation CLI: `dotnet run --project src/Fantactics.Sim -- <command>` (e.g. `new --out playtests/m.json`, `run --p1 bot:captain --games 500 --threads 0`). To play a seat as an LLM, use the `play-fantactics` skill.
- Re-import Godot assets headlessly: `<console binary> --headless --path src/Fantactics.Client --import`
- Run the game: `<console binary> --path src/Fantactics.Client`, with launch options after `--` (TechnicalDesign §2.5), e.g. `-- --p2 bot:captain@easy`, `-- --load playtests/x.json --as P1`, `-- --p2 llm --out playtests/x.json`.
- Godot smoke test (bots play a match through the real scenes, exit 0): `<console binary> --headless --path src/Fantactics.Client -- --autoplay`
- Cross-process check (Godot plus a CLI player on one file): `<console binary> --headless --path src/Fantactics.Client -- --autoplay --p1 bot:captain@easy --p2 llm --out <file>`, then play P2 through `fantactics-sim` on `<file>`; Godot exits 0 when the match ends.
- Check layout without looking: `<console binary> --path src/Fantactics.Client -- --p2 bot:captain --seed 3 --screenshot <png>` (add `--debug` to include the debug panel), then view the PNG.
- Set up a test position: play or load a match, quicksave (F5) or take `autosave.json`, edit its `snapshot` by hand, and `--load` it (TechnicalDesign §4).

## C# Conventions

- **Prefer LINQ** (method syntax) over manual loops for querying and transforming collections. Exception: Godot per-frame callbacks (`_Process`, `_PhysicsProcess`, `_Draw`) and code profiled as hot use plain loops to avoid allocations.
- **Core state is immutable**: model game state with `record`s and immutable collections (`System.Collections.Immutable`); applying a command returns a new state instead of mutating.
- Private fields are `_camelCase`; everything else follows standard .NET naming (PascalCase types/members, camelCase locals/parameters, `I`-prefixed interfaces).
- Use `var` only when the type is obvious from the right-hand side (`new`, casts); otherwise write the type.
- Nullable reference types are enabled in every project; don't suppress warnings with `!` without a reason.
- File-scoped namespaces matching the folder path; one top-level type per file.
- Prefer switch expressions and pattern matching when producing a value; use `switch` statements only for side effects.
- Use expression bodies (`=>`) for single-expression properties, indexers, and methods; anything with statements gets a block body.
- Prefer collection expressions (`[1, 2, 3]`, `[]`), target-typed `new()` when the type is already stated, and primary constructors for dependency injection.

- **Comments:** NEVER leave planning, narration, or change-log comments above a type or member (e.g. `// Added this to handle X`, `// Step 1: ...`, `// New method for ...`). To explain what a type, method, property, etc. does, ALWAYS use XML doc comments (`/// <summary>`, `<param>`, `<returns>`, `<exception>`). Inline `//` comments are only for explaining a non-obvious *why* inside a method body.

## C# Formatting

Enforced by `src/.editorconfig` (formatter and IDE analyzers) and `src/Directory.Build.props` (`EnforceCodeStyleInBuild`); run `dotnet format src/Fantactics.sln` to apply.

- 4-space indentation; soft line limit of 120 characters.
- Allman braces: opening brace on its own line.
- Always use braces for `if`/`else`/`for`/`foreach`/`while` bodies, even single statements.
- LINQ chains of two or more calls put each call on its own line, starting with the dot and indented one level:
  ```csharp
  var threats = enemies
      .Where(e => e.CanReach(tile))
      .OrderByDescending(e => e.Attack)
      .ToList();
  ```
- Member order: constants/static members, fields, constructors, properties, Godot overrides (`_Ready`, `_Process`, ...), public methods, then private methods.

## Godot Conventions

- **No rules or match logic in nodes.** Nodes render `Fantactics.Client.Logic` models and forward input to them. Anything testable without a scene tree goes in Client.Logic.
- **Scenes for layout, code for behavior.** Each reusable piece is a `.tscn` with a same-named script at its root, in the same folder, grouped by feature (`App/`, `Match/Board/`, `Match/Hud/`, …). Layout and containers are set in the scene. Dynamic nodes (unit tokens, buttons built from options) are instanced from `PackedScene` exports or created in code.
- **Node references through `[Export]` fields** assigned in the scene (`[Export] private BoardView _board = null!;`: the `null!` is fine here because Godot assigns exports before `_Ready`). No `GetNode("a/b/c")` string paths; `%UniqueName` only where an export isn't practical.
- **Signals go up, calls go down.** Parents call methods on children. Children raise `[Signal] public delegate void XEventHandler(...)` and emit with `EmitSignal(SignalName.X, ...)`. Siblings never reference each other, and there's no global event bus. Plain C# events are for non-Node types (`IGameConnection`, `ClientSession`).
- **No autoloads** unless justified. `Main` owns the services and passes them down with an `Initialize(...)` method called before the node enters the tree (nodes can't use constructor injection).
- **Threading:** never touch a node off the main thread. Match updates and file-watcher callbacks arrive on worker threads; marshal them with `MainThread.Post` (`Callable.From(...).CallDeferred()`). Unsubscribe C# events in `_ExitTree`.
- Scripts are `partial` classes whose name matches the file name (Godot requires it); namespaces follow folders (`Fantactics.Client.Match.Board`). Don't name a class like its own namespace's last part (`MatchHud` in `Match/Hud`, not `Hud`).
- **Input goes through InputMap actions** defined in `project.godot` (`confirm`, `cancel`, `submit`, `wait_action`, `delay_action`, `ability_1`–`ability_9`, `skip_animation`, `quicksave`, `quickload`, `toggle_debug`, `queue_modifier`), never raw keycodes.
- **Animation uses `Tween`s**, with durations multiplied by the speed setting; instant speed plays nothing. After playback the board always snaps to the authoritative view.
- Core's `Point` converts to and from `Vector2I` only through `Common/GodotConversions`, which also holds the tile size (32).
- Scene files may be written as text (omit `uid=` attributes); then run the headless `--import` so Godot generates the `.uid` files.

## Conventions

- Do not commit `.godot/`, `bin/`, or `obj/`. `*.import` and `*.uid` files are tracked.
- Move or rename scripts and resources in the Godot editor, not the filesystem, so UIDs and references stay intact.
