# Bug Hunt — where it stands (2026-10-25 sweep)

**Owner:** Yash. **Branch:** the tooling and every fix up to 2026-10-17 are on `bleeding-edge`; the connecting-panel
fix `456375be0` and the 2026-10-25 sweep's two commits (`b1c8f54b28`, `f5244d6819`) are on `Ys-bleeding-edge` only.
**Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) (refreshed 2026-10-25: all 309 issues, run
log `runs/2026-10-25`). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Six people send errors from Unity**, each to their own `bugledger/<user>` branch: Garrett (`gradi`, 158 issues),
  Shombith (142), Andrew (`owner`, 50), jtgjones (`mrglo`, 35), Yash (`yskha`, 22), Caleb (`belac`, 21).
- **309 distinct errors are on those branches**: 270 open, 39 being checked after a fix (was 289 / 11 on 2026-10-24).
  Of the open ones, 157 were last seen more than 30 days ago and 184 were seen only once.
- **The daily sweep works again.** The 2026-10-25 run had push access and moved 18 issues to validating:
  - 11 were already fixed on `bleeding-edge` but a fix had only reached some testers' branches (Shombith x9, Andrew x2).
    `bug_ledger.py mark-fixed --branch <user>` (`b1c8f54b28`) now hands an existing fix to the testers still open
    without resetting the others' clean runs.
  - 6 were fixed on `bleeding-edge` and never marked: the Urchin occlusion band and skimmer container (`f3b155dd30`),
    three Rhino HUD coroutines (`3fa268cd7f`), the Serpent pip coroutine (`6055707211`).
  - 1 new fix: the toy cell miniature's template cube in edit mode (`f5244d6819`, E-837357c332, Shombith).
- **Testers on old checkouts are the rest of the pile.** Andrew's validation log says "waiting for a pull"; Garrett's
  stacks come from an older project folder (`Star-Writer2`). Each tester pulling `bleeding-edge` (and
  `Ys-bleeding-edge` for the two newest fixes) lets their editor adopt the fixes and close them after two clean plays.

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

## 5. Performance (same sweep, `perf_hunt.py status --refresh` 2026-10-24)

Gates all OK. 99 regressions flagged, mostly Menu_Main and Regatta captures from Caleb, Andrew and Yash compared with
one baseline recorded on a different machine (Menu_Main 3.8 ms CPU against 10-30 ms). Per-machine baselines are needed
before these numbers mean regressions. Open capture tasks: T-20261015-56dd, -3e0b, -3972, -1551, -7edd.

## 6. Latest activity (generated)

This block is refreshed by `python3 Tools/Build/team_report.py area bug-hunt --write`.

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-25 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-10-25 `b08716005` bugledger: E-837357c332 fixed in f5244d6819 — on another branch
- 2026-10-25 `6a8c45703` bugledger: E-f1681b036f fixed in 6055707211 — on another branch
- 2026-10-25 `2b4472397` bugledger: E-2320339269 fixed in 3fa268cd7f — on another branch
- 2026-10-25 `adc36ede1` bugledger: E-ad12affa41 fixed in 3fa268cd7f — on another branch
- 2026-10-25 `3da0f2660` bugledger: E-7cc3e0223c fixed in 3fa268cd7f — on another branch
- 2026-10-25 `ed4b7357a` bugledger: E-5a15f5e924 fixed in f3b155dd30 — on another branch
- 2026-10-25 `fdc90d049` bugledger: E-5103c76904 fixed in f3b155dd30 — on another branch
- 2026-10-25 `ae61bbeb6` bugledger: E-edc6a81429 fixed in f8af9fb5b — on another branch
- 2026-10-25 `2d162ce4d` bugledger: E-c703a8f80b fixed in f8af9fb5b — on another branch
- 2026-10-25 `7a704f469` bugledger: E-1f1ff22829 fixed in 8bcdde930 — on another branch
- 2026-10-25 `469d6d12d` bugledger: E-4e84ce7ae2 fixed in a227e1e2b — on another branch
- 2026-10-25 `605ac33ca` bugledger: E-6020da4524 fixed in a227e1e2b — on another branch
- 2026-10-25 `599a0a159` bugledger: E-4065bcae2a fixed in a227e1e2b — on another branch
- 2026-10-25 `680934314` bugledger: E-5713660fbd fixed in 8df5d3359 — on another branch
- 2026-10-25 `e8d48fb72` bugledger: E-cb11787d50 fixed in d7e361701 — on another branch
- 2026-10-25 `d696e6517` bugledger: E-2f25b16a12 fixed in d7e361701 — on another branch
- 2026-10-25 `641715924` bugledger: E-c523697cae fixed in 2e8d5f63b — on another branch
- 2026-10-25 `ef095a66a` bugledger: E-be60130128 fixed in 59e83e19d — on another branch
- 2026-10-25 `b1c8f54b2` fix(bugledger): mark-fixed can hand an existing fix to the senders still open — on Ys-bleeding-edge
- 2026-10-25 `d6f9691a1` bugledger: 11 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-10-25 `e307e90be` bugledger: E-1f28d58e0c fixed in 1c7616624f — on another branch
- 2026-10-25 `04100d3a1` bugledger: E-6e925c3b8b fixed in db43442b71 — on another branch
- 2026-10-25 `99a6ed8fb` bugledger: E-775c564039 fixed in db43442b71 — on another branch
- 2026-10-25 `daa04a0d9` bugledger: E-0de9ec6c70 fixed in c5449e28a3 — on another branch
- 2026-10-25 `192510e90` bugledger: E-41b7610878 fixed in c5449e28a3 — on another branch

### Also in flight on other branches
- `bugledger/shombith` (Shombith, 2026-10-25): bugledger: E-837357c332 fixed in f5244d6819
- `bugledger/gradi` (Garrett, 2026-10-25): bugledger: E-f1681b036f fixed in 6055707211
- `bugledger/owner` (Andrew, 2026-10-25): bugledger: E-5a15f5e924 fixed in f3b155dd30
- `bugledger/yskha` (Yash, 2026-10-25): bugledger: 11 open error(s) from yskha's play tests, 12 report file(s)
- `bugledger/belac` (Caleb, 2026-10-25): bugledger: E-1f28d58e0c fixed in 1c7616624f
- `bugledger/mrglo` (jtgjones, 2026-10-24): bugledger: E-72dc400f89 fixed in f8af9fb5b

### Open tickets (Froglet Tasks)
- T-20261024-0212 [open] test: Re-capture Bloomrush after the prism creation queue (Ys-bleeding-edge): Release, Third Eye closed, Arcade > Bloomrush, play past the 90 s auto-capture, stop Play. Compare with the 10-20 captures (CPU busy 130-221 ms, Prism.CreateBlockCoroutine x56k/frame, CoroutinesDelayedCalls 139 ms). (who: anyone)
- T-20261024-803f [open] test: Re-capture Menu_Main after the QuestTrackView fix (Ys-bleeding-edge): Release, Third Eye closed, play Menu_Main (freestyle or the hub) past the 4-minute auto-capture and stop Play. Compare ClipperRegistry.Cull with the 10-19..23 captures (3.1-7.7 ms/frame; 0.26 ms on 10-16). (who: anyone)
- T-20261021-b133 [open] decision: Decide which preset an older MobileLow phone starts on: first-run auto-detect still picks Very Low with 4x MSAA (Round 8); a hand pick of Low / Very Low now writes AA Off (who: anyone)
- T-20261021-fa97 [open] test: Measure the new Medium / Low quality presets on an older phone (Mali-G52 / Adreno 610 class): F7 diag GPU ms at High, Medium, Low in the same scene, and run the editor steps (sky stops animating on Medium; AA off, 80% render scale, softer bloom, a quarter of the cytoplasm shards on Low - still there and still pointing at the crystal) (who: anyone)
- T-20261020-09fe [open] decision: Pick the spindle LOD style and tune it in Unity. Resources/SpindleLodProfile > style: OVERT (default; the braid fans open on the prisms' own depth ramp, as far as danger prisms' faces travel, with a wave running root to tip - tune it on Resources/IncorporealDepth: strength 1, recedeStart/End 2.25/4.9 norm (~474/700 u), flowAmplitude 0.35, flowWaves 1.5, flowSpeed 0.6, pulseGlow 0.02 (round 6; was 0.8); and on SpindleLodProfile: chargeStrength 0.01, otherStrength 0.005 (round 6: deliberately negligible), overtThreadStart 10 norm (~1,000 u), overtFadeStart/End 15.625/22.5 norm, ~1,250/1,500 units) or COVERT (the braid converges into a thread nobody notices; threadGlow 1.35, minThreadPixels 1.25, farFadeStart/End 1300/1900 girths). The style switches live in Play. Preview without Unity: python3 Tools/Shaders/render_spindle_lod_preview.py OUT --style overt|covert [--strength 0.01]. (who: anyone)
- T-20261020-fb02 [open] test: Spindle LOD A/B in Unity Release: S2 Lattice (Cell Selector > Lattice, 8 min, auto-capture) once with Resources/SpindleLodProfile lodEnabled ON and once OFF; compare CPU/GPU ms, draws, and the renderers census (the capture's spindle LOD line). (who: anyone)
- T-20261020-86b5 [open] test: Open cece/gallant-keller-6mr1u4 in Unity: let SpindleLodMidGraph / SpindleLodFarGraph import (no magenta), run the EditMode test SpindleLodTests (ShippedProfile_IsSaneAndMeasured checks the LOD meshes against the braid Unity imported), then Play Menu_Main > Cell Selector > Lattice and fly in and out of a plant: no pop as limbs cross ~140 / ~590 / ~2,660 units, and the HUD console 'renderers' line shows far limbs as instanced. If a graph imports magenta, untick lodEnabled on Resources/SpindleLodProfile and report it. (who: anyone)
- T-20261018-20ac [open] test: On an Android development build: press Off on the diagnostics HUD (or hold four fingers 2 s). The HUD and Unity's red error console go away; Patch And Run a change and relaunch - they stay off; hold four fingers 2 s - the HUD returns (who: anyone)
- T-20261017-7647 [open] test: Share a clean capture of the lag you see: Release code optimization, Third Eye window closed, vsync/frame cap off ('fps uncap'), Auto-publish on in the Bug Hunt & Performance badge; play the laggy scene past the badge's auto-capture countdown and stop Play. It lands on your bugledger branch by itself (Shombith's has no capture yet) (who: anyone)
- T-20261017-9990 [open] dev: Menu_Main with the Stoat flying: Driver.LateUpdate (BlackHoleRegistry or WormholeView) read 2.7 ms/frame average and 88 ms self in a spike frame (Yash, 2026-10-18; capture tainted by vsync + Third Eye, so rank only). The drivers now carry BlackHole.Tick/Field/Pairs/VesselPull/Waves/Warp/Lens and Wormhole.Tick/SelectAndRender markers, in diag by default: take a clean Release capture with the Stoat dropping a dipole, find the phase that spikes, fix it (who: anyone)
- T-20261017-3d13 [open] test: Re-capture Menu_Main with the Stoat for 10 minutes in a Release editor run, with the Third Eye CLOSED (it is a second camera and taints the frame time). The vessel-registry fix should keep frames flat; the diag now names each Driver.LateUpdate and the BlackHole.* rows. (who: anyone)
- T-20261017-2215 [open] test: Try the after-play confirmation and the Project Record in Unity (2026-10-17): stop Play after a run that leaves something new; the small window says what went to Claude (SENDING -> SENT) and closes by itself; Project record ▸ expands into Overview / Performance / Network / AI flights / Errors with Ys-bleeding-edge and bleeding-edge's performance status and your sends; also FrogletTools > Diagnostics > Project Record. Say what to change. (who: anyone)
- T-20261017-19f0 [open] dev: Performance regression in MinigameGrizzlyCharge|MinigameGrizzlyCharge (mrglo, 2026-10-16 15:42:28): marker LateBehaviourUpdate 0.3973 -> 0.6018 ms/frame (+51%) (who: anyone)
- T-20261017-2129 [open] dev: Performance regression in MinigameRegatta|MinigameRegatta (belac, 2026-10-16 15:13:50): gcKB 47.01902389526367 -> 89.48501586914062 (+90%) (who: anyone)
- T-20261017-1809 [open] dev: Performance regression in MinigameRegatta|MinigameRegatta (belac, 2026-10-16 13:10:56): marker Inl_UniversalRenderTotal 0.9318 -> 1.3824 ms/frame (+48%) (who: anyone)
- T-20261017-c82c [open] test: Look at the instanced snow and the Squirrel's collect in Unity (2026-10-17 perf change): (1) Menu_Main and any arcade cell: the cytoplasm snow fills the cell at the same density as before and turns towards the crystal after a collect; look for transparent shards drawing in the wrong order where two overlap (they are one instanced draw now, unsorted inside it). (2) Skim Race or Regatta as the Squirrel: ten omni collects in a row, each lays its shielded ring and the crystal still morphs onto it and dissolves. (who: anyone)
- T-20261015-1551 [open] test: Find why Regatta runs at 10 fps in the editor: in MinigameRegatta (Release code optimization, Deep Profile off), play two sessions of 90 s or more, one with the Third Eye CLOSED and one with it OPEN (the editor captures each by itself, labelled MinigameRegatta; the capture records the open cameras), stop Play each time (each capture goes to Claude by itself) (who: anyone)
- T-20261015-7edd [open] test: Confirm the lava lamp fps fix in Unity: Release, Third Eye closed, Play Menu_Main for 8 minutes (the editor captures S2 by itself), then stop: it goes to Claude by itself. Then fly close to an omni and a Charge crystal and check their light looks as before. (who: anyone)
- T-20261015-4996 [open] content: Boost Activate (event:/SFX/Oneshots/Gameplay sfx/Boost Activate) has a loop region but is fired as a one-shot, so the code refuses to play it: the Manta's boost is silent and logs one error per session (Bug Ledger E-cdf0c783c1). Remove the loop region, or tell engineering it is a held boost sound (who: anyone)
- T-20261015-0bc6 [open] test: Test the one-button bug flow in Unity: play until a red error appears (or Debug.LogError from any script), stop Play (it is sent by itself; the console says so); play and stop again with the same error (who: anyone)
- T-20261015-7d29 [open] test: Test the Bug Hunt & Performance badge in the Scene view: open Unity, check the card shows bugs and perf with no dialog on load, fold/unfold/hide it, press Release if shown, then Play Menu_Main past the badge's auto-capture countdown, stop, and check the console says it was sent to Claude by itself. (who: anyone)
- T-20261015-f106 [open] setup: Switch on the daily performance check: GitHub runs a scheduled workflow only from the repository's DEFAULT branch, so .github/workflows/perf-daily.yml (merged into Ys-bleeding-edge) fires once a day only after it reaches bleeding-edge / the default branch. Until then run it by hand: Actions > perf-daily > Run workflow on Ys-bleeding-edge. Check the first run's two artifacts (perf-sweep, perf-amoebius) read sensibly: gates all OK, the Amoebius Skim Race run 'within baseline'. (who: anyone)
- T-20261015-72e1 [open] dev: Amoebius: Mesh.SetVertices(List)/SetUVs(List<Vector4>)/SetTriangles(List) copy their lists into fresh arrays on every call (ToArray), and the renderer detects a changed mesh by array identity (SceneRenderer ReferenceEquals on RenderVertices / RenderUvWide / RenderSubmesh), so a per-frame mesh upload allocates the whole buffer each frame: CrystalChargeArcs.LateUpdate reads ~708 KB/frame in the Skim Race marker run while the game's own code is allocation-free (it reuses its writer lists). Unity's SetVertices(List) allocates nothing. Make the setters copy in place when the length matches, bump mesh.version, and have the renderer's dirty check key on version (or a per-buffer stamp) instead of array identity, so the engine's allocation numbers mean the game's. (who: anyone)
- T-20261015-8bae [open] decision: Assets/_Models/shield1.blend makes every machine without Blender log a red 'Blender could not be found' on import (Bug Ledger E-fd64b43840). Nothing in Assets references its guid. Delete it, or export it to .fbx? (who: anyone)
- T-20261015-3972 [open] test: S5 Wildlife Liberation, Release: play two minutes into a match (the editor captures S5 by itself), then stop: it goes to Claude by itself. It is the go/no-go for lever L8 (batched creature body re-sync): Fauna.BodySync must average 8 ms or more for L8 to be worth building. (who: anyone)
- T-20261015-3e0b [open] test: Skim Race I2 with 2 AI, Release: play 90 s (the editor captures SkimRace by itself), then stop: it goes to Claude by itself. This is the Burst check the 10-08 fix still owes (burst must read BURST and the Profiler must show no managed job time) and the test of the PoolRefill.Squirrel Prism ~9 KB/frame allocation lead from the Amoebius run. (who: anyone)
- T-20261015-56dd [open] test: First editor performance capture, S2 Lattice: Code Optimization = Release, Play, Cell Selector > Lattice, keep playing 8 minutes (the editor captures S2 by itself; the Scene view badge counts down), stop playing (it goes to Claude by itself) (the file is one JSON holding burst, diag, prof, renderers, cells and the console; pasting it into a Claude session also works). Close the Third Eye window and any preview first: an extra camera renders the world again and the capture says so. (who: anyone)
<!-- team_report:end -->
