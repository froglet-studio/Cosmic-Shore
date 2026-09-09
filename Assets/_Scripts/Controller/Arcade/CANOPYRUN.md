# Canopy Run — the Gibbon race (`GameModes.CanopyRun = 45`)

Skim Race's domain crystal race, played by a vessel that SWINGS. First DOMAIN to collect the
crystal target (track waypoints × laps, auto-calculated) wins; golf-timed; solo is AI backfill.
Gibbon-only, 1–4 players, 1–3 domains, 4 intensities. Authored by
`Tools/Build/author_canopy_run_assets.py` (`--check`), which clones `MinigameSkimRace.unity` as text.

## 1. What differs from Skim Race (and nothing else does)

| | Skim Race | Canopy Run |
|---|---|---|
| Controller | `SkimRaceController` | `CanopyRunController : SkimRaceController` — adds NO behaviour; the scene's identity and where a Canopy-Run-only rule lands |
| Track | `SpawnableWaypointTrack` — a skimmable prism ribbon | `SpawnableCanopyTrack : SpawnableWaypointTrack` — the SAME waypoint sets grow BOUGHS (3-prism columns) alternately left/right of the line every 180u at ±80u, a prism HOOP (8 prisms, r 34) around every crystal spot, and a thin guide vine along the line. Subclass, not sibling: `CrystalCollisionTurnMonitor.optionalEnvironment` is typed to the base, so the guid swap keeps the auto-calculated target alive |
| Crystals | the CrystalManager's OWN anchor sets (81/81/46u off the waypoints; 14 anchors vs 28 waypoints at intensity 3) | RE-AUTHORED ONTO THE WAYPOINTS (one dataset, asserted), jitter 35 → 12 so a crystal always spawns inside its hoop (`hoopRadius ≥ jitter + crystal radius + margin`, asserted) |
| Roster | Squirrel + Gibbon | Gibbon only — the card's single `Vessels` entry locks the launcher, the server spawn clamp and the AI hull pick (`PickAIVesselType` reads the card; the scene's `vesselClass: 13` templates are hygiene) |
| Scoring rule | `SkimRaceScoringRule` | the same asset, reused (metric Crystals, golf) |
| Comeback | default | `ComebackRatePerScoreDeficit 0.5` — a quarter-of-target deficit (target 24 at intensity 1) buys 3 levels; the generator fails if it ever buys under one |

Mode-keyed platform sites that carry a CanopyRun row: `EndConditionOverridesSO`
(`canopyRunCrystalCount`, 0 = auto; the End Game Conditions window), `ElementalComebackSystem`
(CrystalsCollected), `UGSStatsManager.LowerIsBetter`, `MiniGameHUD` (the Skim Race objective
provider — the next crystal), `VesselImpactor` (track-impact SFX), `SkimRaceScoreTracker` (now
reports under `gameData.GameMode` instead of a hard-coded SkimRace), `EnumIntegrityTests` (44),
`ElementalComebackSystemTests`. Registered in `OrganicRematchGames`, `ProgressionConfig`
(always unlocked), `EditorBuildSettings` (after Skim Race). Not authored: a `ModePreview`
definition (allowed — `Resolve` returns null), a `GameToastConfig` (falls back to the shared one).

## 2. Why this geometry

The Gibbon's beat: latch-to-latch at 150–250 u/s on the swing-rate-floored line is 0.6–1.3 s,
so a bough every ~180u along the line (~360u same side) is one grab per bough at race speed; the
resting aim yaw at race speed lands an abeam anchor ~80u out, which is where the boughs sit.
Alternating sides makes the left-arm / right-arm rhythm — the thing the vessel pays TEMPO for —
the fastest way round. The hoop around each crystal is the fling target: the latched arm's
reticle shows where the hull will be 0.6 s after letting go, so "fling reticle inside the hoop"
is the release window made visible.

## 3. Collider budget (every prism super-shielded — one stellated octahedron each — by the
donor's `SegmentSpawner.superShieldTrackPrisms`)

| Intensity | line | bough | hoop | guide | total prisms | crystal target |
|---|---|---|---|---|---|---|
| 1 | ~4,308u | 72 | 64 | 108 | **244** | 24 |
| 2 | ~5,394u | 90 | 80 | 135 | **305** | 30 |
| 3 | ~9,755u | 162 | 224 | 244 | **630** | 56 |
| 4 | ~8,205u | 138 | 216 | 205 | **559** | 54 |

Two orders of magnitude under Peel the Cage (10–20k). The generator asserts each under 2,000.

## 4. Open items

- No `ModePreview_CanopyRun` (the arcade card shows "LEVEL PREVIEW NOT AVAILABLE").
- No `GameToastConfig_CanopyRun` (shared toasts only; no idle hints).
- The Maelstrom pool does not draw it (its card is 2–4 players / 2+ domains; add deliberately).
- Card art is Skim Race's.

## 5. In-editor verification (no editor in the authoring session)

1. `python3 Tools/Build/author_canopy_run_assets.py --check` → OK.
2. Open `MinigameCanopyRun.unity`: the `Game` object's controller is `CanopyRunController` with
   the Skim Race fields intact (segmentSpawner, rule → SkimRaceScoringRule); `SpawnableTrack`
   carries `SpawnableCanopyTrack` with the canopy header; the CrystalManager's four position sets
   equal the track's four waypoint sets; `anchorJitterRadius` 12.
3. Arcade: the "Canopy Run" card appears (Skim Race art), roster shows only the Gibbon, 1–4
   players, intensity 1–4. Launch solo: 3 AI Gibbons spawn.
4. Countdown: the canopy builds under the connecting panel; boughs alternate sides; a hoop
   surrounds each crystal; the guide vine traces the line. Nothing pops (arena-ready gate).
5. Race: crystals count per domain; the HUD goal line reads "COLLECT CRYSTALS n/24"; finishing
   shows VICTORY + time; replay reloads the scene.
6. AI Gibbons cast, swing and release around the course (no orbiting: `breakOrbits` 0).
