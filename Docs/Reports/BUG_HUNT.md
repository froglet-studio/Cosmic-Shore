# Bug Hunt — where it stands (2026-10-28)

**Owner:** Yash. **Branch:** fixes land on `Ys-bleeding-edge` first and reach `bleeding-edge` with each merge (#1541
merged 2026-10-25). **Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) (issue states
refreshed 2026-10-28 05:00Z, run log `runs/2026-10-28`). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Six people send errors from Unity**, each to their own `bugledger/<user>` branch: Garrett (`gradi`), Shombith,
  Andrew (`owner`), jtgjones (`mrglo`), Yash (`yskha`), Caleb (`belac`).
- **On the board: 221 open, 16 being checked after a fix, 79 resolved** (the ledger itself: 256 issues, 227 open,
  29 validating, from 566 branches). The 2026-10-28 sweep added 26 new issues and removed 1 no branch carries.
- **Fixed on 2026-10-28:** the CameraSettingsSO inspector crash (E-79fe6514ac, `4cd26c01e9`): the M15 field refactor
  pointed five `FindProperty` calls at the new accessors instead of the serialized fields; and the Vivox GUID clash
  (E-8a0a0c4bb4, `ee34be4d33`): `ElementPipsConfigSO.cs.meta` carried the hand-typed GUID
  `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6`, which Vivox's visionOS folder also uses.
- **New, for Shombith's UI:** a Hangar NullReferenceException when a Daily Toy reward lands while the upgrades page
  shows an element with no ability entry (E-111d87fec0, `HangarUpgradesPage.DrawFocused` line 304 reads
  `entry.AbilityLabel` with `entry` null).
- **Checked on 2026-10-27:** Andrew's 'Border' / 'Gear' sightings came from a checkout older than `cb51edde09`; a
  fresh scan of every scene and prefab finds no orphaned component left, so both went to validating on his branch.
  Two more destroyed-mask errors arrived (Shombith E-4ae8006406 through `KitRectMask2D`, E-3233be5687 from a
  `ScrollRect` rebuild; Andrew E-9d5b82005a): same family as T-20261025-0c78 (#1554), Shombith's UI.
- **Weekly challenge board (E-c8e2f49696):** not a code bug. The UGS board `weekly_challenge` has no weekly reset, so
  old weeks' rows sit on it and the game hides them and says so once a session. Ticket T-20261026-0928 (#1585): a
  recurring weekly reset with archiving on, UTC Monday 00:00, set in UGS (Shombith's backend).
- **Fixed on 2026-10-26:** the "GameObject 'Border' / 'Gear' does not reference component" errors (3 testers, 46
  sightings): the palette pass (`60000fe9ee`) left five `KitRimMesh` / `KitAmbient` components orphaned in Menu_Main,
  ConnectingPanel and Maelstrom, so Unity re-attached them on every load; deleted in `cb51edde09`. The generator fix
  that stops a dropped component coming back is #1544 (Shombith's UI generators). Also `7d79733ad7` (spindle LOD
  batch read past the array's end) and three ledger fixes (`be56c98a1b`, `1ad13a8415`, `739e150cdb`).
- **Fixed on 2026-10-25:** 18 issues to validating (11 fixes handed on with `mark-fixed --branch`, 6 never marked, 1
  new), the client crystal-pickup domain error (`fb2466b822`; the pairing window itself is #1545), and per-machine
  performance baselines (`3454ba93e9`).
- **Waiting on others:** #1544 (UI generators), #1545 (client pairing window), the weekly_challenge board's weekly reset
  in UGS (E-c8e2f49696, #1585), a rate-limited party create (E-1a188c52a2), the destroyed-mask error
  (T-20261025-0c78, needs a capture).

## 2. What is blocking it

1. **Testers on old checkouts.** Each tester pulls `bleeding-edge`; their editor then takes the fixes and closes them
   after two clean play tests.
2. **Relay / transport errors are marked against the wrong fix.** E-0a098d80df, E-4d0d17e44b, E-e5ff2abb8c,
   E-e6054bc064, E-fda866d511 read "validating `24ba982c8`" on Yash's branch, but that commit is the Squirrel crystal
   morph. Their real causes are party B31-B33 (open), so the sweep did not hand that fix on.

## 3. Fixed so far (all in `bleeding-edge` unless noted)

| Error | Who hit it | Fix |
|---|---|---|
| Lobby `ObjectDisposedException` after Play | all testers | `2e8d5f63b` (party B34; the leak itself is B33, open) |
| Party "lobby not found" on join | Yash, Caleb, Garrett | `59e83e19d` |
| MPPM `ProjectDataStore` 411x | Garrett, jtgjones | MPPM 2.0.2 (`8bcdde930`) |
| Unparsable Rampage / Squirrel / Dolphin assets | Garrett, jtgjones | 2026-09-25 commits |
| Destroyed Player / VesselController, scene-close leaks | Garrett, jtgjones, Shombith | `f8af9fb5b` |
| Connecting panel after a cancelled load | Garrett, Shombith | `456375be0` (`Ys-bleeding-edge` only) |
| Squirrel crystal-morph PrismClock, Regatta "no course" | Yash | `24ba982c8`, `861cca3dd` |
| Urchin occlusion radius "clamped to 2.00", Urchin skimmer container unassigned | Garrett, Andrew, Shombith | `f3b155dd30` (marked 2026-10-25) |
| Rhino HUD / Serpent HUD "Coroutine couldn't be started" | Garrett, Shombith | `3fa268cd7f`, `6055707211` (marked 2026-10-25) |
| "Destroy may not be called from edit mode" (Multiplayer Tools) | four testers | MPPM tools 2.2.12 (`f8af9fb5b`, marked 2026-10-24) |
| "Destroy may not be called from edit mode" (CellMiniatureBuilder) | Shombith | `f5244d6819` (`Ys-bleeding-edge` only) |

## 4. Open, worst first

- **Manta Boost Activate FMOD loop** (E-cdf0c783c1): six testers. A content fix for Charles (T-20261015-4996).
- **Relay / transport failures** (E-0a098d80df, E-4d0d17e44b, E-e5ff2abb8c, E-e6054bc064, E-fda866d511, and the new
  "Relay: not connected" E-5fa838820d): four to five testers. They come after a host leaves or a session dies; party
  B31-B33 are the open causes.
- **Regressed on Garrett's machine** (the fix did not hold there, re-root-cause): the MPPM reflection error
  E-1f1ff22829 (414 sightings, fix `8bcdde930`) and destroyed `VesselController` E-17461650a8 (fix `f8af9fb5b`).
- **Skein "Turn started with no course"** (E-598aa7898c, Garrett, PlayMode): `861cca3dd` covers only the frame of
  leaving Play, so this stays open until a capture shows which case it is.
- **New this week:** Andrew's Urchin HUD (`vesselHUDController does not implement IVesselHUDController`, 21x), "No
  Player found to get domain!", two more PrismClock STRICT MODE effects (cellSwapSuction, shieldShatter), "Vessel
  selection DROPPED", duplicate package assemblies (Animation Rigging, Collab Proxy) on Andrew's machine.
- **Clean-play counting:** Yash's fixes still read 0 of 2 on his branch. The validation trail (`3484cb554`) rides on
  every send as `reports/<user>/ledger/validation.log`; read it before changing the counting again.

## 5. Performance (`perf_hunt.py status --refresh`, 2026-10-25)

Gates all OK. **Baselines are per machine since 2026-10-25** (`scene|label|user`; each tester's first valid capture of a
scenario is their baseline, and the 17 older baselines went to the tester whose capture they were). Only each
machine's NEWEST capture of a scenario counts. The flagged regressions fell from 184 to 35; most of the 184 were Caleb's
and Andrew's captures judged against Yash's faster machine. What still stands:
- Grizzly Charge (jtgjones, 10-25): `LateBehaviourUpdate` 0.40 -> 2.44 ms, CPU +35% (T-20261017-19f0).
- Garland (Yash, 10-26): `UGUI.Rendering.UpdateBatches` 0.82 -> 4.65 ms.
- Menu_Main S2 (Yash): `BlackHole.Tick` / `BlackHole.Field` ~0.01 -> 2 ms (T-20261017-9990).
- Regatta (Caleb, 10-16): GC per frame 47 -> 89 KB (T-20261017-2129).
- Andrew's Garland and Bloomrush: 19 lines against his own first capture; check one more capture before acting.

T-20261017-1809 closed: Caleb's next capture read its two markers within his baseline. Open capture tasks:
T-20261015-56dd, -3e0b, -3972, -1551, -7edd.

## 6. Latest activity (generated)

This block is refreshed by `python3 Tools/Build/team_report.py area bug-hunt --write`.

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-28 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-10-28 `0dd943120` bugledger: E-8a0a0c4bb4 fixed in ee34be4d33 — on another branch
- 2026-10-28 `03f509d4d` bugledger: E-79fe6514ac fixed in 4cd26c01e9 — on another branch
- 2026-10-28 `449c66143` bugledger: 22 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-28 `04f8a286a` bugledger: 95 open error(s) from gradi's play tests, 1 report file(s) — on another branch
- 2026-10-27 `5e57d2bba` bugledger: 44 open error(s) from belac's play tests, 10 report file(s) — on another branch
- 2026-10-27 `ddf97e8c5` bugledger: 95 open error(s) from Shombith's play tests, 2 report file(s) — on another branch
- 2026-10-27 `f63a131d5` bugledger: 20 open error(s) from yskha's play tests, 5 report file(s) — on another branch
- 2026-10-27 `75a59e07e` bugledger: 20 open error(s) from yskha's play tests, 9 report file(s) — on another branch
- 2026-10-27 `828cd1ce4` bugledger: 95 open error(s) from Shombith's play tests, 1 report file(s) — on another branch
- 2026-10-27 `895c11d29` bugledger: 94 open error(s) from Shombith's play tests, 1 report file(s) — on another branch
- 2026-10-27 `06937d838` bugledger: 19 open error(s) from yskha's play tests, 9 report file(s) — on another branch
- 2026-10-27 `0984c3d93` bugledger: 94 open error(s) from Shombith's play tests, 2 report file(s) — on another branch
- 2026-10-27 `577c23f1c` bugledger: 19 open error(s) from yskha's play tests, 5 report file(s) — on another branch
- 2026-10-27 `d8a75e45b` bugledger: 19 open error(s) from yskha's play tests, 11 report file(s) — on another branch
- 2026-10-27 `830410f5f` bugledger: 93 open error(s) from Shombith's play tests, 1 report file(s) — on another branch
- 2026-10-27 `352b1d85f` bugledger: 18 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-27 `a6900df29` bugledger: 18 open error(s) from yskha's play tests, 1 report file(s) — on another branch
- 2026-10-27 `fec361827` bugledger: 18 open error(s) from yskha's play tests, 7 report file(s) — on another branch
- 2026-10-27 `3a64de8e3` bugledger: 18 open error(s) from yskha's play tests, 3 report file(s) — on another branch
- 2026-10-27 `53afc4dc3` bugledger: 17 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-27 `91715e983` bugledger: 17 open error(s) from yskha's play tests, 9 report file(s) — on another branch
- 2026-10-27 `0fbfdf3e1` bugledger: 17 open error(s) from yskha's play tests, 5 report file(s) — on another branch
- 2026-10-27 `efdfef4f6` bugledger: 17 open error(s) from yskha's play tests, 3 report file(s) — on another branch
- 2026-10-27 `f40bcc049` bugledger: E-ed5bf7a724 fixed in cb51edde09 — on another branch
- 2026-10-27 `9c19d6909` bugledger: E-b44884c449 fixed in cb51edde09 — on another branch

### Also in flight on other branches
- `bugledger/shombith` (Shombith, 2026-10-28): bugledger: E-8a0a0c4bb4 fixed in ee34be4d33
- `bugledger/belac` (Caleb, 2026-10-28): bugledger: E-8a0a0c4bb4 fixed in ee34be4d33
- `bugledger/yskha` (Yash, 2026-10-28): bugledger: E-79fe6514ac fixed in 4cd26c01e9
- `bugledger/gradi` (Garrett, 2026-10-27): bugledger: 95 open error(s) from gradi's play tests, 1 report file(s)
- `bugledger/owner` (Andrew, 2026-10-27): bugledger: E-ed5bf7a724 fixed in cb51edde09
- `claude/gracious-johnson-s1lkvt` (unattributed, 2026-10-26): docs(branches): only Yash's sessions build on Ys-bleeding-edge; everyone else on bleeding-edge
- `bugledger/mrglo` (jtgjones, 2026-10-25): bugledger: 27 open error(s) from mrglo's play tests, 9 report file(s)

### Open tickets (Froglet Tasks)
- T-20261024-0212 [open] test: Re-capture Bloomrush after the prism creation queue (Ys-bleeding-edge): Release, Third Eye closed, Arcade > Bloomrush, play past the 90 s auto-capture, stop Play. Compare with the 10-20 captures (CPU busy 130-221 ms, Prism.CreateBlockCoroutine x56k/frame, CoroutinesDelayedCalls 139 ms). (who: anyone)
- T-20261021-b133 [open] decision: Decide which preset an older MobileLow phone starts on: first-run auto-detect still picks Very Low with 4x MSAA (Round 8); a hand pick of Low / Very Low now writes AA Off (who: anyone)
- T-20261021-fa97 [open] test: Measure the new Medium / Low quality presets on an older phone (Mali-G52 / Adreno 610 class): F7 diag GPU ms at High, Medium, Low in the same scene, and run the editor steps (sky stops animating on Medium; AA off, 80% render scale, softer bloom, a quarter of the cytoplasm shards on Low - still there and still pointing at the crystal) (who: anyone)
- T-20261020-09fe [open] decision: Pick the spindle LOD style and tune it in Unity. Resources/SpindleLodProfile > style: OVERT (default; the braid fans open on the prisms' own depth ramp, as far as danger prisms' faces travel, with a wave running root to tip - tune it on Resources/IncorporealDepth: strength 1, recedeStart/End 2.25/4.9 norm (~474/700 u), flowAmplitude 0.35, flowWaves 1.5, flowSpeed 0.6, pulseGlow 0.02 (round 6; was 0.8); and on SpindleLodProfile: chargeStrength 0.01, otherStrength 0.005 (round 6: deliberately negligible), overtThreadStart 10 norm (~1,000 u), overtFadeStart/End 15.625/22.5 norm, ~1,250/1,500 units) or COVERT (the braid converges into a thread nobody notices; threadGlow 1.35, minThreadPixels 1.25, farFadeStart/End 1300/1900 girths). The style switches live in Play. Preview without Unity: python3 Tools/Shaders/render_spindle_lod_preview.py OUT --style overt|covert [--strength 0.01]. (who: anyone)
- T-20261020-fb02 [open] test: Spindle LOD A/B in Unity Release: S2 Lattice (Cell Selector > Lattice, 8 min, auto-capture) once with Resources/SpindleLodProfile lodEnabled ON and once OFF; compare CPU/GPU ms, draws, and the renderers census (the capture's spindle LOD line). (who: anyone)
- T-20261018-20ac [open] test: On an Android development build: press Off on the diagnostics HUD (or hold four fingers 2 s). The HUD and Unity's red error console go away; Patch And Run a change and relaunch - they stay off; hold four fingers 2 s - the HUD returns (who: anyone)
- T-20261017-9990 [open] dev: Menu_Main with the Stoat flying: Driver.LateUpdate (BlackHoleRegistry or WormholeView) read 2.7 ms/frame average and 88 ms self in a spike frame (Yash, 2026-10-18; capture tainted by vsync + Third Eye, so rank only). The drivers now carry BlackHole.Tick/Field/Pairs/VesselPull/Waves/Warp/Lens and Wormhole.Tick/SelectAndRender markers, in diag by default: take a clean Release capture with the Stoat dropping a dipole, find the phase that spikes, fix it (who: anyone)
- T-20261017-2215 [open] test: Try the after-play confirmation and the Project Record in Unity (2026-10-17): stop Play after a run that leaves something new; the small window says what went to Claude (SENDING -> SENT) and closes by itself; Project record ▸ expands into Overview / Performance / Network / AI flights / Errors with Ys-bleeding-edge and bleeding-edge's performance status and your sends; also FrogletTools > Diagnostics > Project Record. Say what to change. (who: anyone)
- T-20261017-19f0 [open] dev: Performance regression in MinigameGrizzlyCharge|MinigameGrizzlyCharge (mrglo, 2026-10-16 15:42:28): marker LateBehaviourUpdate 0.3973 -> 0.6018 ms/frame (+51%) (who: anyone)
- T-20261017-c82c [open] test: Look at the instanced snow and the Squirrel's collect in Unity (2026-10-17 perf change): (1) Menu_Main and any arcade cell: the cytoplasm snow fills the cell at the same density as before and turns towards the crystal after a collect; look for transparent shards drawing in the wrong order where two overlap (they are one instanced draw now, unsorted inside it). (2) Skim Race or Regatta as the Squirrel: ten omni collects in a row, each lays its shielded ring and the crystal still morphs onto it and dissolves. (who: anyone)
- T-20261015-4996 [open] content: Boost Activate (event:/SFX/Oneshots/Gameplay sfx/Boost Activate) has a loop region but is fired as a one-shot, so the code refuses to play it: the Manta's boost is silent and logs one error per session (Bug Ledger E-cdf0c783c1). Remove the loop region, or tell engineering it is a held boost sound (who: anyone)
- T-20261015-0bc6 [open] test: Test the one-button bug flow in Unity: play until a red error appears (or Debug.LogError from any script), stop Play (it is sent by itself; the console says so); play and stop again with the same error (who: anyone)
- T-20261015-f106 [open] setup: Switch on the daily performance check: GitHub runs a scheduled workflow only from the repository's DEFAULT branch, so .github/workflows/perf-daily.yml (merged into Ys-bleeding-edge) fires once a day only after it reaches bleeding-edge / the default branch. Until then run it by hand: Actions > perf-daily > Run workflow on Ys-bleeding-edge. Check the first run's two artifacts (perf-sweep, perf-amoebius) read sensibly: gates all OK, the Amoebius Skim Race run 'within baseline'. (who: anyone)
- T-20261015-72e1 [open] dev: Amoebius: Mesh.SetVertices(List)/SetUVs(List<Vector4>)/SetTriangles(List) copy their lists into fresh arrays on every call (ToArray), and the renderer detects a changed mesh by array identity (SceneRenderer ReferenceEquals on RenderVertices / RenderUvWide / RenderSubmesh), so a per-frame mesh upload allocates the whole buffer each frame: CrystalChargeArcs.LateUpdate reads ~708 KB/frame in the Skim Race marker run while the game's own code is allocation-free (it reuses its writer lists). Unity's SetVertices(List) allocates nothing. Make the setters copy in place when the length matches, bump mesh.version, and have the renderer's dirty check key on version (or a per-buffer stamp) instead of array identity, so the engine's allocation numbers mean the game's. (who: anyone)
- T-20261015-8bae [open] decision: Assets/_Models/shield1.blend makes every machine without Blender log a red 'Blender could not be found' on import (Bug Ledger E-fd64b43840). Nothing in Assets references its guid. Delete it, or export it to .fbx? (who: anyone)
- T-20261015-3972 [open] test: S5 Wildlife Liberation, Release: play two minutes into a match (the editor captures S5 by itself), then stop: it goes to Claude by itself. It is the go/no-go for lever L8 (batched creature body re-sync): Fauna.BodySync must average 8 ms or more for L8 to be worth building. (who: anyone)
- T-20261015-3e0b [open] test: Skim Race I2 with 2 AI, Release: play 90 s (the editor captures SkimRace by itself), then stop: it goes to Claude by itself. This is the Burst check the 10-08 fix still owes (burst must read BURST and the Profiler must show no managed job time) and the test of the PoolRefill.Squirrel Prism ~9 KB/frame allocation lead from the Amoebius run. (who: anyone)
- T-20261015-56dd [open] test: First editor performance capture, S2 Lattice: Code Optimization = Release, Play, Cell Selector > Lattice, keep playing 8 minutes (the editor captures S2 by itself; the Scene view badge counts down), stop playing (it goes to Claude by itself) (the file is one JSON holding burst, diag, prof, renderers, cells and the console; pasting it into a Claude session also works). Close the Third Eye window and any preview first: an extra camera renders the world again and the capture says so. (who: anyone)
<!-- team_report:end -->
