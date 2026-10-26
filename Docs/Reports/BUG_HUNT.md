# Bug Hunt — where it stands (2026-10-26)

**Owner:** Yash. **Branch:** fixes land on `Ys-bleeding-edge` first and reach `bleeding-edge` with each merge (#1541
merged 2026-10-25). **Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) (issue states
refreshed 2026-10-26 12:05Z, run log `runs/2026-10-26`). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Six people send errors from Unity**, each to their own `bugledger/<user>` branch: Garrett (`gradi`), Shombith,
  Andrew (`owner`), jtgjones (`mrglo`), Yash (`yskha`), Caleb (`belac`).
- **On the board: 211 open, 29 being checked after a fix, 79 resolved** (was 270 open / 39 validating on 2026-10-25).
  Most of the drop is Garrett pulling overnight: 33 stale compile errors closed on his next load, and 7 fixes passed
  their two clean plays. Closes are read from each machine's validation trail until the sender's issue-folder clear
  (`be56c98a1b`) has run everywhere.
- **Fixed on 2026-10-26:** the "GameObject 'Border' / 'Gear' does not reference component" errors (3 testers, 46
  sightings): the palette pass (`60000fe9ee`) left five `KitRimMesh` / `KitAmbient` components orphaned in Menu_Main,
  ConnectingPanel and Maelstrom, so Unity re-attached them on every load; deleted in `cb51edde09`. The generator fix
  that stops a dropped component coming back is #1544 (Shombith's UI generators). Also `7d79733ad7` (spindle LOD
  batch read past the array's end) and three ledger fixes (`be56c98a1b`, `1ad13a8415`, `739e150cdb`).
- **Fixed on 2026-10-25:** 18 issues to validating (11 fixes handed on with `mark-fixed --branch`, 6 never marked, 1
  new), the client crystal-pickup domain error (`fb2466b822`; the pairing window itself is #1545), and per-machine
  performance baselines (`3454ba93e9`).
- **Waiting on others:** #1544 (UI generators), #1545 (client pairing window), the weekly_challenge board's weekly reset
  in UGS (E-c8e2f49696, a dashboard setting), a rate-limited party create (E-1a188c52a2), the destroyed-mask error
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
_Generated 2026-10-26 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-10-26 `be56c98a1` fix(bug-ledger): a send removes the issues this machine no longer has — on Ys-bleeding-edge
- 2026-10-26 `a5f17f4c7` bugledger: E-9ce3d19683 fixed in f84c3ce800 — on another branch
- 2026-10-26 `5820a3fa8` bugledger: E-b44884c449 fixed in cb51edde09 — on another branch
- 2026-10-26 `d259ce42b` bugledger: E-ed5bf7a724 fixed in cb51edde09 — on another branch
- 2026-10-26 `adc8a2649` bugledger: 15 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-25 `8e65aeff7` bugledger: 130 open error(s) from gradi's play tests, 1 report file(s) — on another branch
- 2026-10-25 `ef35e8601` bugledger: 14 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-25 `1ad13a841` fix(diagnostics): the crash detector and the Bug Ledger stay out of asset import workers — on Ys-bleeding-edge, bleeding-edge
- 2026-10-25 `69c4e83b1` bugledger: 10 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-25 `95fb7a111` feat(net): Load Time Insights records every online load by itself and ships it after Play — on Ys-bleeding-edge, bleeding-edge
- 2026-10-25 `9c4d76f96` bugledger: E-6e925c3b8b fixed in fb2466b822 — on another branch
- 2026-10-25 `739e150cd` fix(bug-ledger): a compile error closes once the scripts compile clean — on Ys-bleeding-edge, bleeding-edge
- 2026-10-25 `b7d4c31c3` bugledger: 133 open error(s) from gradi's play tests — on another branch
- 2026-10-25 `35004ec99` bugledger: 117 open error(s) from Shombith's play tests — on another branch
- 2026-10-25 `699ffc783` bugledger: 27 open error(s) from mrglo's play tests, 9 report file(s) — on another branch
- 2026-10-25 `c4a72e8d6` bugledger: 132 open error(s) from gradi's play tests — on another branch
- 2026-10-25 `8bfbd6129` bugledger: E-f3658978b8 fixed in 59b78edc39 — on another branch
- 2026-10-25 `af7f1fc77` bugledger: E-95af7aa88a fixed in 59b78edc39 — on another branch
- 2026-10-25 `2d0954bb1` bugledger: E-6b68d83637 fixed in 59b78edc39 — on another branch
- 2026-10-25 `087926af8` bugledger: E-01ed4e655c fixed in 59b78edc39 — on another branch
- 2026-10-25 `f8d31f363` bugledger: E-2ff5c04cef fixed in 59b78edc39 — on another branch
- 2026-10-25 `8fa128ad0` bugledger: E-6b14fd9bb5 fixed in eb9b53b453 — on another branch
- 2026-10-25 `5f97d7f45` bugledger: E-70af76123e fixed in eb9b53b453 — on another branch
- 2026-10-25 `b84082e1b` bugledger: E-f3e848936b fixed in eb9b53b453 — on another branch
- 2026-10-25 `827d62404` bugledger: E-9a61c468da fixed in eb9b53b453 — on another branch

### Also in flight on other branches
- `bugledger/gradi` (Garrett, 2026-10-26): bugledger: E-9ce3d19683 fixed in f84c3ce800
- `bugledger/yskha` (Yash, 2026-10-26): bugledger: E-b44884c449 fixed in cb51edde09
- `bugledger/shombith` (Shombith, 2026-10-26): bugledger: E-b44884c449 fixed in cb51edde09
- `bugledger/mrglo` (jtgjones, 2026-10-25): bugledger: 27 open error(s) from mrglo's play tests, 9 report file(s)
- `bugledger/owner` (Andrew, 2026-10-25): bugledger: E-908f18b36f fixed in 5ad9f467b1
- `bugledger/belac` (Caleb, 2026-10-25): bugledger: E-96c9656884 fixed in 8694975c6f

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
