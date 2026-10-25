# The Cosmic Shore session deck

A Claude Code plugin that adds buttons to every Claude session working on this repository, so the
checks people ask for in every session take one press and no model turn. Source:
`.claude/skills/cosmic-shore-deck/` (the hooks module is `hooks/register.tsx`, tests
`hooks/deck.test.ts`). Asked for by Yash, 2026-10-15.

## What it adds

| Where | What |
|---|---|
| **Pane "Cosmic Shore"** (every surface: terminal, desktop, the Claude app on a phone) | Buttons **Team**, **Gates**, **Compile**, **Sync**, **Ship**, and the last result. A failed job shows **Fix with Claude**; Sync shows **Pull** and **Merge bleeding-edge** when there is something to take. Open it with `/deck`. |
| **Band above the prompt** (terminal and desktop only; the app draws no band) | `Deck  Open  Team  Gates  Sync`: each opens the pane and runs that job. |
| **Status line** | `<branch> ↑ahead ↓behind · N changed · bleeding-edge +M`, refreshed after every turn and every 2 minutes (no fetch; Sync and Team fetch). |
| **Slash commands** | `/deck` opens the pane. `/gates`, `/team`, `/sync` run the job and print the result in the transcript, so Claude reads it too. |

## What each button runs

| Button | Runs (from the repo root) | Model turn? |
|---|---|---|
| Team | `git fetch --prune origin`, then `python3 .claude/hooks/team_activity.py --peek 12`: every branch's pushes in the last 12 h, HOT where they touch files this session changed. Writes no hook state, so the prompt hook still reports each push once. | No |
| Gates | All at once: `check_console_logging`, `check_using_directives`, `check_conditional_compilation`, `check_ai_no_state_writes`, `check_switch_label_collisions`, `check_enum_member_references`, `check_self_referential_locals`, `team_tasks.py --self-test` (the team's hand-off tickets, Froglet Tasks) (about 25 s, set by the using-directives gate). PASS/FAIL per gate with its last line. | No |
| Compile | `bash Tools/Build/unity_refcompile/run.sh --config player-dev` (several minutes; the first run in a container fetches the reference assemblies). The last 28 lines. | No |
| Sync | `git fetch --prune origin`, then ahead/behind against `origin/<branch>`, uncommitted files, and how far `origin/bleeding-edge` is ahead. | No |
| Ship | Submits `/ship-quick`. | Yes |
| Fix with Claude | Submits the failed job's output and asks for the root cause and a fix. | Yes |
| Pull / Merge bleeding-edge | Submits a prompt to pull with `--no-rebase`, or to merge `origin/bleeding-edge` with a merge commit and push. | Yes |

One job runs at a time; pressing another while one runs shows a toast.

## How it loads

- **In a session that has this repository**: a plugin folder under the project's `.claude/skills/<name>/` is
  auto-loaded by Claude Code. If a session does not show the deck, run `/plugin` and check that
  `cosmic-shore-deck` is enabled.
- **From a terminal anywhere**: `/plugin install cosmic-shore-deck --marketplace froglet-studio/Cosmic-Shore`
  (the repository root's `.claude-plugin/marketplace.json` lists it).
- **While editing it in a session**: link the folder into that session's mods folder so it hot-reloads
  when the turn ends (`/plugin-authoring` says where that folder is).

## Changing it

- Add a gate: one row in `GATES` in `hooks/register.tsx`.
- Add a button: a job function `(h: Host) => Promise<Result>`, a row in `JOBS`, and a `Button` in the
  pane. The API refuses `$` passed to a function, so jobs reach the engine only through the `Host`
  closures built in `session.start`.
- Check before committing:
  `claude plugin validate .claude/skills/cosmic-shore-deck` and
  `claude plugin test .claude/skills/cosmic-shore-deck` (the tests mount the pane on terminal, desktop
  and mobile with a fake host).
- The plugin API is early access and changes between Claude Code releases. The declaration file the
  `/plugin-authoring` skill writes is the authority, not this page.
