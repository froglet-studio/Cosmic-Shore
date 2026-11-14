# Bug Hunt — where it stands (2026-11-14)

**Owner:** Yash. **Branch:** fixes land on `Ys-bleeding-edge` first and reach `bleeding-edge` with each merge (#1541
merged 2026-10-25). **Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) (issue states
refreshed 2026-11-10 (board) and 2026-11-14 (ledger), run log `runs/2026-11-10`). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Six people send errors from Unity**, each to their own `bugledger/<user>` branch: Garrett (`gradi`), Shombith,
  Andrew (`owner`), jtgjones (`mrglo`), Yash (`yskha`), Caleb (`belac`).
- **Ledger on 2026-11-14: 395 open, 10 being checked after a fix** (405 issues, from 745 branches; nothing new
  on 2026-11-14, only repeats of the Relay, UGS and Vivox issues). **The board
  has not been updated since 2026-11-10:** the 2026-11-11 to -14 sweeps run at 03:54 UTC, and their board
  writes (ArtifactData) waited for an approval nobody was there to give. The git side (collect, triage, mark-fixed,
  this report) still runs. To unblock the board, allow the Team Bug Board's database writes for this sweep, or
  mirror it by hand once a day.
- **Fixed on bleeding-edge, marked 2026-11-12:** `Manta.asset` parses again (E-251530c87b, E-603d7f129d,
  E-c52c39070d), fixed by Garrett's own `4c28f26f06` (2026-11-10). The repair branch
  `claude/fix-manta-ability-map-yaml` is no longer needed.
- **New 2026-11-12/13, not code:** Garrett's machine could not bind its local UDP port ("address already in use",
  E-55c19c8f2b, E-f18360ba1d, E-849115e303, E-6f8a396c25): a second editor or player was holding it. **For an
  owner:** the UI theme's live colour sweep reads a destroyed TMP sub-mesh material (E-0f0eb8a59f,
  `UIThemeLive.SwapSea`, `UIThemeLive.cs:431`; Caleb's machine, 3 sightings; Shombith's UI theme).
- **Fixed on bleeding-edge:** the Ark escort's MissingReferenceException, 1,295 sightings on Garrett's machine
  (E-9f5f369ac6): a pilot swap carried the escort's steering hook to another hull's AI and nothing cleared it when
  the escort's hull was destroyed. `fa702c0db2` (2026-11-08); marked fixed.
- **New, for their owners:** the Thresher's two Morningstar FMOD events are not in the built banks (E-b6c18a87a2,
  E-eee0bbbdf4; one is still only in FMOD's `.unsaved` folder: save and rebuild in FMOD Studio, aradia1's
  project). Most sightings now: Unity's `t.GetParent() == nullptr` assertion, 199 on Garrett's machine
  (E-73d255442d), not yet traced.
- **New on 2026-11-06 (74 issues), mostly from Garrett's machine, 15:05-15:31:** about 60 Windows video-decoder
  errors (0xc00d5212; the machine's codecs, not the videos, which have not changed since 09-15), and a burst of
  DI / "Authentication not in build profile" errors. Also: `UrchinAnimation.Update` throws a NullReferenceException
  before the animation is set up, 40 sightings on Caleb's machine (E-75a12f0559, Garrett's vessels: `VesselAnimation`
  keeps its initialised flag private, so the override cannot guard on it; ticket #1773). The chain-fire gun error
  (E-debe17fb43) is ticket #1774.
- **Fixed on 2026-11-06:** the party recovery's "Recovery failed (ArgumentNullException)" (E-0de9ec6c70) came back
  past the B38 guard on Caleb's Regatta: after Stop, UniTask resumed the flow while `Application.isPlaying` was still
  true. A requested Stop now counts as Play ended (`93765d6aa8`).
- **Fixed on 2026-11-05:** Astro League's AI crashed every tick on Andrew's machine (E-7e57c6fa76, E-24ec0fbf4b,
  `UnassignedReferenceException: teamSpawns`): the scene's `teamSpawns` list kept an empty third slot from the
  2026-09-15 resync while the mode has two teams, so a pilot clamped onto it. Removed in `65602ce4d7`.
- **New, not code:** two Cloud Save writes failed after four retries on Yash's machine (E-2716e55218,
  E-9b3f50bb34, the backend's side - Shombith's). One stackless "Destroying components immediately" warning
  (E-6bc7e07601) cannot be traced from the log alone.
- **2026-11-03:** handed on the Stoat ability map's YAML fix (E-6bac11910c, `c089b6fc5c`). Most sightings then:
  the chain-fire prism effect firing a gun whose projectile pool was destroyed (E-debe17fb43, Garrett's impacts).
- **Earlier on 2026-11-02:** 10 issues, none a code fix (machine environment, and four `[AppManager]` injection
  errors that fired together once on Garrett's machine). Tickets for the bugs in Shombith's and Garrett's areas:
  #1682-#1688.
- **Closed on 2026-11-01 (already fixed, handed to the testers):** the party-sync crash (E-c4b9286c3a, 3 testers:
  `PartyMemberService.SyncFromSession` read a session a join or leave had let go; `9195c61b78`, B39-B44), the
  Serpentine card's YAML (E-45021e05fc, `7837c09b05`) and the Waystation Prototype toast's YAML (E-2d33e20d83,
  `2df1a32366`). Garrett's copy of the Vivox GUID clash also went to validating (`ee34be4d33`).
- **New, for Shombith:** the new Port voice and chat feature (Vivox, #1594) logs a family of errors on Garrett's and
  Shombith's machines: `'server' is null or empty` at sign-in and an editor `403 Forbidden` fetching Vivox credentials
  (E-ca127debd0, E-ae8d2e4fa2, E-5c27bd1550 and others) - Vivox is not set up for this UGS project, a backend setting.
  Also: blocking a friend before the friends service started throws (E-5bea1a27c8; the profile card shows the Block
  button without checking `FriendsServiceFacade.IsInitialized`), a rejoined player's vessel is initialised twice
  (E-71cedd035d, the Rev 37 rejoin path in `ClientPlayerVesselInitializer.ClaimReclaimedSeatAsync`), and the toast
  pool hands out a destroyed item after a scene change (E-245165ae01).
- **New, for Garrett:** a Cell leaves `Cell0_RetiringWorld` objects behind when the scene closes (E-c703a8f80b,
  spawned from `CellConveyor.OnDestroy`), and an `ArkEscortPilot` is read after it is destroyed (E-9f5f369ac6).
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
_Generated 2026-11-14 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-11-14 `bce9ca4f0` bugledger: 67 open error(s) from Owner's play tests, 12 report file(s) — on another branch
- 2026-11-14 `b5fa8e40d` bugledger: 211 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-13 `7307c580e` bugledger: 35 open error(s) from belac's play tests, 12 report file(s) — on another branch
- 2026-11-13 `e95b42f7b` bugledger: 31 open error(s) from belac's play tests, 12 report file(s) — on another branch
- 2026-11-13 `13b107b78` bugledger: 26 open error(s) from belac's play tests, 9 report file(s) — on another branch
- 2026-11-13 `ae84d6171` bugledger: 26 open error(s) from belac's play tests, 8 report file(s) — on another branch
- 2026-11-13 `b67d08af8` bugledger: 26 open error(s) from belac's play tests, 4 report file(s) — on another branch
- 2026-11-12 `58e90f3a9` bugledger: 209 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-12 `ceb6a5b41` feat(elemental-loop): the one-minute fun card and per-rung bars — on bleeding-edge
- 2026-11-12 `d0244cf78` bugledger: 205 open error(s) from gradi's play tests — on another branch
- 2026-11-12 `8fd8ce479` bugledger: 124 open error(s) from Shombith's play tests — on another branch
- 2026-11-12 `73e7a7b44` test(proven-win): the loop exercised end to end - scene-mode example and a real candidate from the queue — on bleeding-edge
- 2026-11-12 `f0ea9d5b2` bugledger: 26 open error(s) from belac's play tests — on another branch
- 2026-11-12 `a73f1e1d7` feat(proven-win): scene bench stress copies and frozen-time screenshots; W5 recorded (held), N1b and SeaLines rejected — on bleeding-edge
- 2026-11-12 `0874fbe58` feat(proven-win): probe P3 (hidden subtrees on their own canvases) in the scene bench; its Menu_Main result is suspect and recorded as such — on bleeding-edge
- 2026-11-12 `60d23dd8c` feat(proven-win): the scene bench freezes a set of writers together; Menu_Main's canvas cost is not dirt-driven — on bleeding-edge
- 2026-11-12 `91a397eff` feat(proven-win): pick a scene-bench owner by path; Menu_Main's per-frame canvas cost is not driven by its dirtiers — on bleeding-edge
- 2026-11-12 `1ad362996` feat(proven-win): the scene bench can diagnose what re-dirties a canvas; Menu_Main's dirtiers queued — on bleeding-edge
- 2026-11-12 `ed393bb0f` feat(proven-win): the scene bench can measure inside a real scene; reject the doors Animator sleep (no gain in Menu_Main either) — on bleeding-edge
- 2026-11-12 `dc9415a40` feat(proven-win): an in-scene bench for 'this component stops running'; doors Animator measured, no gain on GameCanvas — on bleeding-edge
- 2026-11-12 `4796a3155` feat(proven-win): a phone version of the A/B bench (built, not yet run on a phone) — on bleeding-edge
- 2026-11-12 `31e5b4420` docs(proven-win): the bench clones include chains and runs the game's publishers; probe before trusting a no-gain — on bleeding-edge
- 2026-11-12 `8c43757bb` perf(graphics): the HyperSea sky skips its dust off the galactic band and its core outside the caps (W3, bit-identical) — on bleeding-edge
- 2026-11-12 `371eb6b35` perf(graphics): the spindle lace searches 6 Voronoi cells, not 9 (W2, 1.21-1.24x in real Unity) — on bleeding-edge
- 2026-11-12 `e346da021` perf(graphics): the Charge crystal body skips its silenced crawl, 3.06-3.32x in real Unity, bit-identical — on bleeding-edge

### Also in flight on other branches
- `bugledger/owner` (Andrew, 2026-11-13): bugledger: 67 open error(s) from Owner's play tests, 12 report file(s)
- `bugledger/gradi` (Garrett, 2026-11-13): bugledger: 211 open error(s) from gradi's play tests, 12 report file(s)
- `bugledger/belac` (Caleb, 2026-11-13): bugledger: 35 open error(s) from belac's play tests, 12 report file(s)
- `bugledger/shombith` (Shombith, 2026-11-13): bugledger: 124 open error(s) from Shombith's play tests
- `bugledger/yskha` (Yash, 2026-11-12): bugledger: E-c52c39070d fixed in 4c28f26f06
- `bugledger/will` (ownerbynight, 2026-11-12): bugledger: E-c52c39070d fixed in 4c28f26f06
- `bugledger/mrglo` (jtgjones, 2026-11-02): bugledger: 22 open error(s) from mrglo's play tests, 2 report file(s)
- `bugledger-android/yskha` (Yash, 2026-11-02): devicelogs: 2 item(s) from yskha's phone (adb ingest)

### Open tickets (Froglet Tasks)
- T-20261102-ac92 [open] content: Give the Squirrel, Goby and Thresher boost loop an FMOD parameter: their ProximityBoostAudioController boostLoopEvent is event:/SFX/Oneshots/Gameplay sfx/Skim, which has no 'Boost Amount' parameter, so the loop plays but its intensity never follows the boost. (who: anyone with FMOD Studio)
<!-- team_report:end -->
