# Local Unity session: skills and shared Editor (start prompt)

Paste everything below the line into a **new local** Claude Code session started in the Cosmic-Shore folder
(`B:\FrogletGames\Cosmic-Shore`, not its parent, or the repo's own skills will not load). Put your task on the last
line. Written 2026-11-02.

---

You are working on Cosmic Shore in a LOCAL session on my machine, with the Unity CLI available. Work on
`Ys-bleeding-edge`; merge, never rebase or force-push; never push to `bleeding-edge`; never open a pull request unless
I ask.

## 0. Start

1. Read `CLAUDE.md`, `Docs/claude/PLUGINS_AND_CONNECTORS.md` and `Docs/unity-cli-setup.md`.
2. Check your skill list for `unity-perf:unity-performance`, `unity:unity-cli` and the repo's `/ship`. Tell me in one
   line if any is missing. On 2026-11-02 all three were available.
3. Run `unity --help`. The installed version is the authority on commands and flags (it was `1.0.0-beta.13`).
4. Run `python3 Tools/Build/who_works_where.py query <paths>` before changing anything.

## 1. The Editor is shared

`B:\FrogletGames\Cosmic-Shore` is the open Editor's project, and other sessions may be testing in it. That checkout
was on `bleeding-edge` with uncommitted Editor changes on 2026-11-02. Do not switch its branch, pull in it or clean it
up without asking me. Do the work in a separate worktree when the Editor is in use.

Before Play mode, `unity test`, `unity recompile`, a multiplayer clone, a package change, or saving `.cs` files under
`Assets/` in that checkout:

1. Check for other sessions with `ListAgents` and `mcp__ccd_session_mgmt__list_sessions`, then run
   `unity command editor_status`. A busy session may be testing even if Play mode reads stopped, so read its recent
   activity with `list_events`.
2. If one is testing, message it with what you need and for how long, using `notify_when_idle: true`. Tell me you are
   queued and behind whom. Turns go first come, first served, and you never ask a running test to stop.
3. On your turn, tell the other live sessions, and tell them again when you finish.

## 2. Which skills to use

Use these when the work fits. Measured numbers and this repo's rules beat a plugin's general advice, and a plugin
finding is a hypothesis until a Release `diag` / `prof` measures it.

| Work | Skills |
|---|---|
| Performance | `/unity-perf:unity-performance`, then `/unity-perf:csharp-zero-gc`, then `/unity-perf:code-standards`; `/perf-hunt` for the measured fix loop |
| Editor and testing | `/unity:unity-cli` |
| Physics | `/unity:physics-3d-collision`, after the `/ecology` collider-budget gate |
| UI and text | `/unity:ui-ugui`, `/unity:optimize-text-mesh-pro` |
| Rendering (URP) | `/unity:urp-postprocessing`, `/unity:validate-urp-render-graph-renderer-feature` |
| Packages | `/unity:unity-package-management`; flag any new package to me first |
| Audio import | `/unity:optimize-audio`; the game's sounds go through FMOD, so its mixer advice mostly does not apply |
| Unity assets without the Editor | `/asset-surgery`, `/ship-tools` |
| Bugs and QA | `/bug-ledger`, `/bug-closeout`, `/qa-backlog` |
| Finishing a branch | `/ship`, `/ship-quick`, `/ship-deep`, `/to-bleeding-edge` |

Game-system work has its own skill: `/vessel`, `/vessel-ai`, `/vessel-model`, `/arcadegame`, `/arenagame`, `/ecology`,
`/flora`, `/fauna`. Load it before editing that system.

## 3. Verify

Every C# commit passes `bash Tools/Build/unity_refcompile/run.sh` and `bash .claude/skills/ship/gates.sh`, plus the
`unity` CLI when an Editor is open and it is your turn. If neither check ran, say so in the commit or PR and add an entry
to `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Never claim a check that did not run. Run tests yourself instead of handing
me steps.

## 4. Known on 2026-11-02

- The GitHub connector failed to connect ("Authorization header is badly formatted") and Discord failed too. Check them
  at the start. `git` and `gh` are the fallback for GitHub.
- The unity-perf restart in `Docs/claude/PLUGINS_AND_CONNECTORS.md` has no record of having run, and its numbers date
  from 2026-10-08. If I ask for it, follow `Docs/prompts/PERFORMANCE_PLUGIN_RESTART_PROMPT.md` and take fresh `diag` /
  `prof` numbers first.
- Multiplayer has its own hand-off: `Docs/prompts/MULTIPLAYER_LOCAL_SESSION_PROMPT.md`.

My task: <write the task here>
