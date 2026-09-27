# Fantactics

Turn-based fantasy tactics game. Godot 4.7.2 (Mono) with C# on .NET 8. Design docs are in `notes/design/` (start there); raw early brainstorms are in `notes/og/`.

## Layout

All code lives under `src/`; the repo root holds only docs and config.

- `src/Fantactics.sln`
- `src/Fantactics.Core`: game rules and state. Must not reference Godot or networking.
- `src/Fantactics.Protocol`: wire DTOs shared by client and server.
- `src/Fantactics.Server`: ASP.NET Core + SignalR server.
- `src/Fantactics.Client`: Godot project (`project.godot` lives here).
- `src/tests/Fantactics.Core.Tests`: xUnit tests for Core.

## Godot

- Editor binary: `C:\Users\alex\code\godot\godot4.7.2\Godot_v4.7.2-stable_mono_win64.exe`
- Console binary (use this from the shell so output reaches stdout): `C:\Users\alex\code\godot\godot4.7.2\Godot_v4.7.2-stable_mono_win64_console.exe`

## Commands

- Build everything: `dotnet build src/Fantactics.sln`
- Test: `dotnet test src/Fantactics.sln`
- Run server: `dotnet run --project src/Fantactics.Server`
- Re-import Godot assets headlessly: `<console binary> --headless --path src/Fantactics.Client --import`
- Run the game: `<console binary> --path src/Fantactics.Client`

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

## Conventions

- Do not commit `.godot/`, `bin/`, or `obj/`. `*.import` and `*.uid` files are tracked.
- Move or rename scripts and resources in the Godot editor, not the filesystem, so UIDs and references stay intact.
