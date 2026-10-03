---
name: linear-issue
description: Work a Linear issue end to end (branch, plan for approval, implement, test, confirm, merge to main, comment, close). Use when asked to work on, pick up, start, or do a Linear issue such as ALE-7, or when invoked as /linear-issue <key>.
argument-hint: <issue key, e.g. ALE-7>
---

# Working a Linear issue

Issue: `$ARGUMENTS`. If no key was given, ask for one. Keys look like `ALE-<n>` (team **Alexgames**; see
`notes/Tracking.md`). Linear tools come from the `claude_ai_Linear` connector; load them with ToolSearch
(`select:mcp__claude_ai_Linear__get_issue,mcp__claude_ai_Linear__save_issue,mcp__claude_ai_Linear__save_comment,mcp__claude_ai_Linear__list_comments`)
before the first call.

There are two hard stops where you wait for the user: **plan approval** (step 3) and **merge confirmation**
(step 6). Never merge, comment, or close the issue without the second one.

## 1. Read the issue

- `get_issue` for the key. Read the title, description, project, labels, parent/sub-issues, relations, and
  `list_comments`. Follow the repo files and design-doc sections it names.
- If it's blocked by an open issue, or is already Done or Canceled, say so and ask before going on.
- Move it to **In Progress** (`save_issue` with `state: "In Progress"`).

## 2. Branch

- Require a clean working tree (`git status --porcelain`). If it isn't clean, stop and ask; don't stash or
  discard anything.
- `git checkout main`, then `git pull --ff-only` (skip the pull, with a note, if there's no network or remote).
- Create the branch from the issue's `gitBranchName` (e.g. `recompenseaps/ale-7-playtest-full-match-...`).
  It contains the key, and Linear links branches by that name. If the branch already exists, check it out
  and say that you're resuming it.

## 3. Plan (hard stop)

- Call `EnterPlanMode`. Explore the code read-only and write a plan that covers:
  - what changes, file by file
  - the tests you'll add or change (Core rules via `ScenarioBuilder`, Client.Logic tests, and so on)
  - how you'll verify it (see step 5)
  - doc or version changes: `GameEngine.RulesVersion` if rule behavior changes, `notes/LaunchOptions.md` if
    `LaunchArgs` changes, and design docs
  - open questions, if any
- Present it with `ExitPlanMode`. If the user tweaks it, revise and present it again. Don't write code until
  they approve.
- For playtest or research issues whose output is findings rather than code, the plan is how you'll run the
  session and where the findings go (usually sub-issues of the project's triage issue, or a comment).

## 4. Implement

- Follow CLAUDE.md conventions. Keep to the approved plan. If the plan turns out wrong, stop and explain the
  change before continuing.
- Commit on the branch in logical steps. Put the issue key in each message, e.g.
  `ALE-12: add unit info panel model`. End messages with the attribution line from the system reminder.
- Anything worth doing that's out of scope becomes a new Linear issue (same project, Backlog) instead of
  going into this branch. Mention it at the end.

## 5. Test

Always run:

- `dotnet build src/Fantactics.sln`
- `dotnet test src/Fantactics.sln`
- `dotnet format src/Fantactics.sln --verify-no-changes`

Also run these when relevant:

- The client (`src/Fantactics.Client`) was touched: the Godot smoke tests,
  `<console binary> --headless --path src/Fantactics.Client -- --autoplay` and `... -- --drive` (human input
  through the real buttons and keys). Run `--import` first if scenes or assets changed. For layout changes, take a
  `--screenshot` (or `--drive --shots <dir>`, windowed) and look at it.
- Bots or Core hot paths were touched: the Ai tests already cover determinism and legal-actions agreement.
  Add a short `fantactics-sim run` if strength or speed is part of the exit criterion.

Always check that the docs in `notes/` still match the code. Look at the full branch diff
(`git diff main...HEAD`) and read the relevant parts of:

- `notes/design/GameDesign.md` and `notes/design/RacesAndUnits.md`: rules, units, abilities, and numbers
  (compare with `mvp-rules.json` and the maps)
- `notes/design/TechnicalDesign.md`: architecture, the client link, client structure, and persistence
- `notes/design/Simulation.md`: bots, the CLI grammar, and tournaments
- `notes/LaunchOptions.md`: every `LaunchArgs` option
- `notes/Tracking.md`: projects, labels, and links, if the Linear structure changed

Update anything that's stale or missing in the same branch, and commit it with the issue key. If no doc needed
a change, say which docs you checked. Leave `notes/og/` alone; it's a historical record.

Fix failures and re-run until everything is green. Don't move to step 6 with a failing check. If something
can't be made to pass, stop and report it with the output.

## 6. Confirm (hard stop)

Summarize for the user:

- what changed (files, behavior), with the branch name and commits (`git log --oneline main..HEAD`)
- the check results: pass counts, smoke test exit code, and any screenshots
- the docs check: which `notes/` files you updated, or which you checked and left unchanged
- any follow-up issues you created, and anything you left out

Then ask with `AskUserQuestion` how to finish:

- **Merge and push** (Recommended when a remote exists): merge to main and push, so GitHub links work.
- **Merge locally only**
- **Not yet**: the user wants changes. Make them, re-run step 5, and ask again.

## 7. Merge

- `git checkout main`, `git pull --ff-only`, then `git merge --no-ff <branch>`. The merge message is
  `Merge <branch> (<KEY>: <issue title>)` plus the attribution line.
- If the merge conflicts, stop and ask. Don't resolve conflicts by guessing.
- Re-run `dotnet build` and `dotnet test` on main after the merge.
- Push if the user chose it, then delete the local branch (`git branch -d`). Delete the remote branch only if
  it was pushed.

## 8. Close out in Linear

- `save_comment` on the issue with:
  - a short summary of what was done
  - the merge commit hash, linked as
    `https://github.com/recompense-aps/FantacticsRedux/commit/<hash>` if it was pushed
  - the check results
  - the docs you updated in `notes/`
  - follow-up issues by key
- Move the issue to **Done** (`save_issue` with `state: "Done"`).
- If it has a parent whose sub-issues are now all Done, tell the user; don't close the parent yourself.
- Finish with one line giving the issue URL and the merge commit.
