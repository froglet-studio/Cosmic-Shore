# Claude plugins and connectors for Cosmic Shore

> Added 2026-10-08. Paths are relative to the repository root. This file lists the Claude plugins and
> connectors that help build this game, says what each one is for HERE, and sets the rule a session
> follows so the human finds out about them instead of having to know they exist.

## The rule every session follows

1. **Before Unity performance, C# review, rendering (URP), UI, physics, audio-import or package work**, call
   `ListPlugins` for the plugins below. A plugin is enabled on the human's claude.ai account, not in the
   repo, so this is the only way to know.
2. **If a plugin that fits the task is not enabled**, render its install card once per session
   (`SearchPlugins` -> `SuggestPluginInstall`, ids in the table), say in one line what it would add, and
   carry on with the repo's own tools. Do not wait for it. A newly enabled plugin's skills appear in the
   NEXT session.
3. **If it is enabled**, use its skills for the task, and record what they found where the work is recorded
   (for performance: `Docs/PERFORMANCE_OPTIMIZATION.md`).
4. **The repo's measured rules win over a plugin's general advice.** `CLAUDE.md`,
   `Docs/claude/ANTI_PATTERNS.md`, `Docs/claude/SHADERS_AND_PERFORMANCE.md`, the perf record and the
   `Tools/Build/check_*.py` gates are written from this game's measurements. A plugin finding is a
   hypothesis until `diag` / `prof` measure it in Release ("Profile first", `CLAUDE.md`). A plugin that
   would add a package is flagged first, as for any new dependency.
5. **When the catalog changes**, update this file: search with `SearchPlugins` / `SearchMcpRegistry`
   (keywords: unity, performance, game, profiler, crash), and add or retire rows with the date.

## Plugins

| Plugin | Publisher | Install id | Skills that matter here | Status 2026-10-08 |
|---|---|---|---|---|
| **unity-perf** | Anthropic Directory, community tier (author "gqy"), v1.1.0 | `plugin_017a77bCxxukLBBYTKt4yMgf` | `unity-performance` (optimization), `csharp-zero-gc` (zero-GC engineering plans), `code-standards` (standards and review) | Available, not enabled. The performance restart below runs on it |
| **Unity** | Unity Technologies, official (Knowledge Work catalog), 0.1.6-beta | `plugin_014AH5hoCLbfMtN3eRfR7BYD` | `unity-cli`, `physics-3d-collision`, `optimize-text-mesh-pro`, `urp-postprocessing`, `validate-urp-render-graph-renderer-feature`, `ui-ugui`, `unity-package-management`, `optimize-audio` | Available, not enabled |

What the Unity plugin's skills are worth here:

- **`unity-cli`** goes with the `/verify-unity` rule (`CLAUDE.md`: every C# change passes the Unity CLI
  before it is committed). It drives an OPEN Unity Editor, so it works in a local Claude Code session on the
  machine that runs Unity, never in a cloud container (no editor there). Cloud sessions still file their C#
  changes in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **`physics-3d-collision`**: prism colliders, the collider-LOD budget and trigger costs
  (`Docs/SPATIAL_INDEX.md`; read the `/ecology` skill's collider-budget gate first).
- **`optimize-text-mesh-pro`**, **`ui-ugui`**: the HUD and menus. `UGUI.Rendering.UpdateBatches` is 0.6 ms a
  frame in Skim Race (diag, 2026-10-08).
- **`urp-postprocessing`**, **`validate-urp-render-graph-renderer-feature`**: the URP renderer and any custom
  renderer feature.
- **`unity-package-management`**: package moves. The multiplayer package versions are pinned
  (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`), and a new package is flagged before it is added.
- **`optimize-audio`**: Unity audio import settings only. The game's sounds go through FMOD
  (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`), so its mixer advice mostly does not apply.
- Not useful to this project: the 2D, sprite and tilemap skills, in-app purchases / LevelPlay, web
  optimization, BiRP-to-URP migration (the project is already URP), and Vivox unless voice chat is planned.

## Connectors

| Connector | Use here | Status 2026-10-08 |
|---|---|---|
| **GitHub** | PRs, CI, review threads | In every cloud session (the `mcp__github__*` tools) |
| **Sentry** | Players' errors and crashes, once the game ships the Sentry Unity SDK. Worth deciding before the Steam launch | Not installed. The game has no Sentry package (`Packages/manifest.json`) |
| **Figma** | UI design context, if the UI redesign's designs live in Figma (`Docs/UI_REDESIGN_TASKS.md`) | Not installed. No Figma file is referenced in the repo |
| Gmail, Google Calendar, Google Drive | Not used by the game's work | Connected on the account |

No Unity Editor connector is in the registry (searched 2026-10-08). The route to the editor is the Unity CLI
(`com.unity.pipeline`, `Docs/unity-cli-setup.md`) plus the Unity plugin's `unity-cli` skill.

## The performance restart (planned 2026-10-08)

The human asked to restart the performance review with **unity-perf**, one skill at a time. Each step reads
the current measurements, writes its findings into `Docs/PERFORMANCE_OPTIMIZATION.md`, and changes code only
where a Release `diag` / `prof` measures the gain (`Docs/PERFORMANCE_OPTIMIZATION.md` §4.7).

**Starting point** (branch `perf/performance-optimization` after the 2026-10-08 `Ys-bleeding-edge` merge):

- Skim Race I2, 2 AI, editor in Release (diag 2026-10-08 06:35, before the Burst fix was tested): 65.8 fps;
  frame avg 15.2 ms, p95 19.9; CPU busy 13.1 ms, GPU 3.7 ms; GC 30 KB a frame; `SkimRace.Pilot.Decide`
  avg 2.35 / p95 4.83 ms; `ShellContact.Query` 0.39 ms. The game's Burst jobs ran as managed code because
  two jobs called `MathF`; that is fixed and waits for the next capture (`Docs/SKIM_RACE_AI.md` §8.0k).
- `python3 Tools/Build/scan_perframe_allocations.py --gc-kb 30`: 4 large allocations in
  `Update`/`LateUpdate` (`ButterflyHullBuilder`, `ScarabHullBuilder`, `GunTransformer`, `SavePng`: each a
  `GetComponentsInChildren`), 8 in per-frame coroutines, 62 `yield return new` inside a loop. The scanner
  prints how many allocations 30 KB implies; size the target before fixing anything.

**Handoff:** `Docs/prompts/PERFORMANCE_PLUGIN_RESTART_PROMPT.md` (2026-10-08: still not enabled when the
session that planned this ended, so the restart moved to a new session).

**Order:**

1. **`/unity-performance`**: audit the hot paths, given the latest `diag` and `prof` JSON. Rank by measured
   milliseconds, not by how bad the code looks. §3.5 of the perf doc lists hypotheses already measured dead.
2. **`/csharp-zero-gc`**: plan the 30 KB a frame away, starting from the scanner's list and `prof`'s `topGc`.
   The existing gates (`check_mathf_params_alloc.py`, `scan_perframe_allocations.py`) stay the enforcement.
3. **`/code-standards`**: review the branch (`git diff origin/bleeding-edge...HEAD`) against its standards,
   reconciled with `CLAUDE.md` "Code Style" and `Docs/claude/ANTI_PATTERNS.md`. Where they disagree, this repo
   wins and the disagreement is noted here.
