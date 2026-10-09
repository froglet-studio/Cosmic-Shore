# Vessel Studio (web) — the pages

Pick a vessel, its studio opens. Plain HTML pages, no build step, so the same folder opens in a desktop
browser, in a phone browser, from Amoebius's **STUDIOS** page on Windows, and from Unity through
**FrogletTools ▸ Vessels ▸ Vessel Studio** (which opens Amoebius there). Plan: `../VESSEL_STUDIO_PLAN.md`.

| File | What |
|---|---|
| `index.html` | The hub: one bay per studio, where each platform stands, and the **studio agent** (Ask, and Development requests). |
| `squirrel.html` | **Squirrel Studio, round 2: AI sim lab** (2026-10-09). The racer on its shipped numbers, on the game's four Skim Race courses (I1–I4, read from `MinigameSkimRace.unity`). AI Squirrels at Easy / Medium / Hard (Hard is a simplified stand-in for `SkimRaceDriver`; Easy and Medium add `SkimRaceHandicap`'s two mistakes), your own hull flown by you or by the AI, chase / follow / free camera, 1–4× speed, auto-restart, "Show AI thinking", and a headless scorecard (every difficulty × course, three seeds). Six play-style types over its four element levels. |
| `studio-look.js` | **The Vessel Studio look** (`/vessel-studio` D19): the Stoat's sky, stars, lights and drifting prism field, the crystal and the screen marker, as one file every studio loads. Listed under `shared` in `studios.json`. |
| `studio-ide.js` | **The Vessel Studio editor layout** (`/vessel-studio` D8): the game view in the middle, every panel a tab in the right or bottom dock, pop-out windows, splitters, no page scroll. Listed under `shared` in `studios.json`. |
| `sync.js` | The **Sync panel** (Refresh, console, merge then delete, shared decisions; a Claude session does the git work as jobs). Not referenced by the pages in the repo: `.claude/skills/vessel-studio/build_artifact.py` and Refresh inject it at publish time. User doc: `SYNC_PANEL.md`. Live in the one artifact: https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa |
| `stoat.html` | The **Stoat Flight Studio** (round 15: the field trajectory; round 14: editor layout, course ladder, AI sim lab), copied from `../StoatFlightStudio.html` with a back link. That file stays the source: re-copy it here when it changes. Opened from Amoebius as `stoat.html#prisma`, it reads "Running on Amoebius". |

## Rules for every studio page

- **A reader of the shipped numbers, never their authority.** Each constant names the asset or class it was
  copied from (the Squirrel's are in `SHIP` and in its "Where the numbers come from" panel). When an asset
  changes, the page changes with it.
- **Say what is not modelled**, on the page.
- **Phone first:** a **Play on phone** mode with two thumb sticks and two trigger handles that feed the
  game's own dual-stick mix (`InputController`), the same as a gamepad.
- **Works outside the claude.ai viewer.** Pages carry their own `[hidden]` rule and keep state in
  `localStorage` only as a convenience. The studio agent needs the viewer (see below).
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

STUDIOS ▸ **OPEN IN AMOEBIUS** opens a page as its own window. **PLAY IN ENGINE** runs a studio's `engineMode` in
the game itself. Step-by-step checks: `../PRISMA_TEST_STEPS.md`.
