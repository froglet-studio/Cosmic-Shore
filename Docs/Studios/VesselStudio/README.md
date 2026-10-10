# Vessel Studio (web) — the pages

Pick a vessel, its studio opens. Plain HTML pages. These pages ARE the artifact: the build (`build_artifact.py`)
publishes them unchanged (each carries its own Sync panel), and **every surface opens that build** (/vessel-studio D33):
claude.ai, the live mirror, Amoebius's **VESSEL STUDIO** page (the checkout's branch built and served on 127.0.0.1) and
Unity's **FrogletTools ▸ Vessels ▸ Vessel Studio** (the studio home in Unity; a card opens its studio through Amoebius, from Unity's checkout and branch). Sync, Ask,
Requests and the decision log work in claude.ai and in Amoebius (its own backend, kept on that computer); on the mirror
they say so and link to the artifact. Plan: `../VESSEL_STUDIO_PLAN.md`.

| File | What |
|---|---|
| `index.html` | The hub: one bay per studio, where each platform stands, and the **studio agent** (Ask, and Development requests). |
| `squirrel.html` | **Squirrel Studio, round 2: AI sim lab** (2026-10-09). The racer on its shipped numbers, on the game's four Skim Race courses (I1–I4, read from `MinigameSkimRace.unity`). AI Squirrels at Easy / Medium / Hard (Hard is a simplified stand-in for `SkimRaceDriver`; Easy and Medium add `SkimRaceHandicap`'s two mistakes), your own hull flown by you or by the AI, chase / follow / free camera, 1–4× speed, auto-restart, "Show AI thinking", and a headless scorecard (every difficulty × course, three seeds). Six play-style types over its four element levels. |
| `studios.json` + `previews/` | **The catalog and the card** (`/vessel-studio` D34): every studio, and its hub card as text (`chip`, `summary`, `spec`, `cardActions`, `fleet`) and as a picture (`previews/<id>.png`, baked from the hub's live canvas by `.claude/skills/vessel-studio/bake_previews.cjs`). Amoebius's VESSEL STUDIO page and Unity's studio home draw the hub's cards from it; `parity_gate.py` fails when it and `index.html` differ. |
| `studio-look.js` | **The Vessel Studio look** (`/vessel-studio` D19): the Stoat's sky, stars, lights and drifting prism field, the crystal and the screen marker, as one file every studio loads. Listed under `shared` in `studios.json`. |
| `studio-ide.js` | **The Vessel Studio editor layout** (`/vessel-studio` D8): the game view in the middle, every panel a tab in the right or bottom dock, pop-out windows, splitters, no page scroll. Listed under `shared` in `studios.json`. |
| `ai_race_panel.js` | The **universal AI race config panel** (`StudioRacePanel.mount`): Course · Your hull (You / AI Easy / Medium / Hard) · one row per AI rival seat with its own level (Off / Easy / Medium / Hard, D20) · Camera (Chase / Follow / Free) · Speed · Show AI thinking · Auto-restart. The Squirrel and the Stoat both mount it, and every arcade-game studio after them does too (`/vessel-studio` D17, §3.4). Listed under `shared` in `studios.json`. |
| `sync.js` | The **Sync panel** (Refresh, console, merge then delete, shared decisions; a Claude session does the git work as jobs). Every page carries its tag (D30). It talks to `window.claude`, which the claude.ai viewer or Amoebius's studio server gives (D33). User doc: `SYNC_PANEL.md`. Live in the one artifact: https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa |
| `stoat.html` | The **Stoat Flight Studio** (AI levels and rivals, 2026-10-09: Easy / Medium / Hard as the Skim Race levels, up to three rival Stoats each with its own level and play style, the **Score AI levels** scorecard; round 15: the field trajectory; round 14: editor layout, course ladder, AI sim lab), copied from `../StoatFlightStudio.html` with a back link. That file stays the source: re-copy it here when it changes. Opened from Amoebius as `stoat.html#prisma`, it reads "Running on Amoebius". |

## Where it is published

- **The one artifact** (claude.ai, `web` in `studios.json`): https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa.
  The full studio, with Sync, Ask, Requests and shared decisions.
- **The live mirror** (`mirror` in `studios.json`): https://yskhan61.github.io/vessel-studio/ (GitHub Pages,
  repo `YsKhan61/vessel-studio`). The same `build_artifact.py` output as plain files: open the hub or
  `squirrel.html` / `stoat.html` by link in any browser, nothing to install, updated in place when it is
  republished (a minute or so; a browser that has the page cached may need a refresh). The features that need
  claude.ai or Amoebius say so there (the public page never calls a local Amoebius). A mirror, not a source: change the
  pages here, then republish. Amoebius's VESSEL STUDIO page links it under the card (**live mirror**).
- **Amoebius** (OPEN IN AMOEBIUS / OPEN IN BROWSER): the same build, made from the checkout's branch by Amoebius
  (`StudioBuild`, byte-identical to `build_artifact.py`), served on `http://127.0.0.1:<port>/<token>/` with the
  artifact's backend: Sync jobs run locally (`sync_job.py`, Python 3, the skill's branch limits), Ask goes to Amoebius's
  Claude Code, decisions and requests are stored on that computer. `Port/docs/LAUNCHER.md` has the details.
- **Parity**: `python3 .claude/skills/vessel-studio/parity_gate.py` fails when a page or an entry point would make one
  surface differ (a visible host label, a page reading the host itself, a raw WebGL renderer, a file:// entry point).

## Rules for every studio page

- **A reader of the shipped numbers, never their authority.** Each constant names the asset or class it was
  copied from (the Squirrel's are in `SHIP` and in its "Where the numbers come from" panel). When an asset
  changes, the page changes with it.
- **Say what is not modelled**, on the page.
- **Phone first:** a **Play on phone** mode with two thumb sticks and two trigger handles that feed the
  game's own dual-stick mix (`InputController`), the same as a gamepad.
- **Works outside the claude.ai viewer.** Pages carry their own `[hidden]` rule and keep state in
  `localStorage` only as a convenience. The studio agent needs a backend (claude.ai or Amoebius).
- **The same everywhere (D33).** Nothing on a page names the host; read it only with `VesselStudioTheme.host()`.
  Create the 3D renderer with `VesselStudioTheme.renderer(THREE, opts)`: without WebGL the stage says so and every tab
  and panel still loads.
- **Same look:** the dark cockpit palette and the Saira Condensed / IBM Plex faces in `index.html`.

## The studio agent

- **Ask** sends the question plus the vessel's spec (`SPEC` in `index.html`) to Claude through the claude.ai
  viewer's `sample` capability. It spends the viewer's own Claude usage and asks before the first call.
- **Development requests** are stored in the published artifact's database, collection `requests`
  (`vessel`, `kind`, `text`, `status` = open/done, `createdAt`, `by`, optional `reply`). A Claude Code session
  reads them with `ArtifactData` (`list`, collection `requests`) and marks them done; Amoebius's AGENT page gets
  the same text through **Copy as agent prompt**.
- The Stoat page's decision log uses the same database, collection `decisions`.

## Adding a vessel

1. Read its numbers from the assets (prefab, ability map in `Assets/Resources/ElementalAbilityMaps/`, its
   action SOs) and its `R_VesselActions/*.md` docs. Never invent a mechanic.
2. Copy `squirrel.html` as the template: replace `SHIP`, `ELEMENTS`, `TYPES`, the step function's mechanics
   and the "not modelled" list.
3. Add its bay to `index.html` and its spec to `SPEC`; take it off the "no studio yet" row.
4. Run the headless check (below), then republish the artifact and commit.

## Checking a page without a phone

A headless Chromium run (Playwright with the pre-installed `/opt/pw-browsers` headless shell) loads a page,
drives it through `window.__squirrelStudio` (`state`, `set`, `startRace`, `raceHeadless(course, difficulty, seed)`, `raceStats(...)`) and the keyboard, screenshots desktop and 844 × 390 phone play,
and fails on any console error or horizontal overflow at 400 px. If the CDN is unreachable from the test
machine, serve `three.min.js` (r128) to the page from a local copy.

### Squirrel scorecard, measured 2026-10-09 (median of seeds 11/22/33, seconds)

| Course | Easy | Medium | Hard | Game sim, Hard (`SKIM_RACE_AI.md` §14.5) |
|---|---|---|---|---|
| I1 | 73.6 | 73.6 | 69.3 | 64.3 |
| I2 | 94.2 | 97.3 | 90.8 | 79.3 |
| I3 | 238.9 | 239.7 | 229.1 | 183.4 |
| I4 | 108.2 | 102.4 | 90.8 | 149.1 |

Read it as relative. The studio pilot finishes every course at every level within ~25% of the game's Hard
on I1–I3 (it is faster on I4, where the game's pilot is conservative). Its difficulty gaps are smaller
than the game's (game: Medium +14–19%, Easy +23–54%). It plans only ~100 u ahead, so a late notice rarely
costs it, and it recovers from a missed crystal in ~2 s where the game's pilot loses ~10. The page says so.

## In Amoebius

VESSEL STUDIO ▸ **OPEN IN AMOEBIUS** builds the checkout's branch and opens the served page as its own window (Sync,
Ask, Requests and Decisions work there). **PLAY IN ENGINE** runs a studio's `engineMode` in
the game itself. Step-by-step checks: `../PRISMA_TEST_STEPS.md`.

### Stoat AI levels, measured 2026-10-09 (field trajectory, Balanced, 2 laps; median of seeds 11/23/37/51/67, seconds)

Hard is one deterministic run, so it has no spread. Medium and Easy are Hard plus the Skim Race mistakes
(`AI_LEVELS`: late notice, misjudged ring) and the lab-only pair judgement.

| Course | Easy | Medium | Hard | Medium vs Hard | Easy vs Hard |
|---|---|---|---|---|---|
| I1 | 82 | 77 | 56 | +38% | +46% |
| I2 | 89 | 79 | 59 | +34% | +51% |
| I3 | 79 | 68 | 52 | +31% | +52% |
| I4 | 70 (1 DNF of 5) | 62 | 49 | +27% | +43% |

- Before the path-watching field AI (`aiWarp`), Hard held the pair for a set time and came out at 70–89 s.
  Medium and Easy were faster than Hard on two of the four courses, because the outcome was chaotic.
- Holding the poles while they warp the path made Hard 49–59 s, warped 79–95% of the race.
- The game's own gaps (`SKIM_RACE_AI.md` §10) are Medium 14–19% and Easy 23–54% slower than Hard. The Stoat's
  Medium gap is wider than the game's: a missed ring costs a turn-back at warp speed. The Easy gap is in the
  game's range.
- In the page: AI Config ▸ **Score AI levels** (3 seeds, any play style).

