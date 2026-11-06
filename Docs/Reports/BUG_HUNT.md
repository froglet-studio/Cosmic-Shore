# Bug Hunt — where it stands (2026-11-06)

**Owner:** Yash. **Branch:** fixes land on `Ys-bleeding-edge` first and reach `bleeding-edge` with each merge (#1541
merged 2026-10-25). **Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) (issue states
refreshed 2026-11-06 06:30Z, run log `runs/2026-11-06`). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Six people send errors from Unity**, each to their own `bugledger/<user>` branch: Garrett (`gradi`), Shombith,
  Andrew (`owner`), jtgjones (`mrglo`), Yash (`yskha`), Caleb (`belac`).
- **On the board: 343 open, 9 being checked after a fix, 79 resolved** (the ledger itself: 360 issues, from 667
  branches). No code fixes on 2026-11-06.
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
_Generated 2026-11-06 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-11-06 `dbece2c81` bugledger: 18 open error(s) from belac's play tests, 12 report file(s) — on another branch
- 2026-11-05 `863379c4b` bugledger: 194 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-05 `2da0774ff` bugledger: 16 open error(s) from belac's play tests, 12 report file(s) — on another branch
- 2026-11-05 `1c6f5e55e` bugledger: 123 open error(s) from Shombith's play tests, 6 report file(s) — on another branch
- 2026-11-05 `63351a720` bugledger: 130 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-05 `61e7d46dd` bugledger: E-24ec0fbf4b fixed in 65602ce4d7 — on another branch
- 2026-11-05 `bc5c4c4a8` bugledger: E-7e57c6fa76 fixed in 65602ce4d7 — on another branch
- 2026-11-04 `76358976f` bugledger: 67 open error(s) from Owner's play tests, 12 report file(s) — on another branch
- 2026-11-04 `da747495e` bugledger: 15 open error(s) from belac's play tests, 8 report file(s) — on another branch
- 2026-11-04 `5ce0f231d` bugledger: 15 open error(s) from belac's play tests, 11 report file(s) — on another branch
- 2026-11-03 `7746a8e5f` bugledger: 122 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-03 `85632efc3` bugledger: 51 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-03 `f428b0b95` bugledger: 66 open error(s) from Owner's play tests, 12 report file(s) — on another branch
- 2026-11-03 `92bb5f4e6` bugledger: E-6bac11910c fixed in c089b6fc5c — on another branch
- 2026-11-03 `8d20e17db` bugledger: 15 open error(s) from belac's play tests, 4 report file(s) — on another branch
- 2026-11-03 `fe4daea63` bugledger: 49 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-02 `af86dcd20` bugledger: 64 open error(s) from Owner's play tests, 12 report file(s) — on another branch
- 2026-11-02 `457a4489a` bugledger: 122 open error(s) from Shombith's play tests, 12 report file(s) — on another branch
- 2026-11-02 `dfb7d6d82` bugledger: 48 open error(s) from yskha's play tests, 10 report file(s) — on another branch
- 2026-11-02 `e641e7237` bugledger: 48 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-02 `3a1456af0` bugledger: 47 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-02 `f9411906f` bugledger: 119 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-02 `c0eb51c4e` bugledger: 22 open error(s) from mrglo's play tests, 2 report file(s) — on another branch
- 2026-11-02 `d7bc285b3` bugledger: 14 open error(s) from belac's play tests, 4 report file(s) — on another branch
- 2026-11-02 `85ed49b00` docs(ship): B36 boot-reorder ship pass - refactor rows, two bug-ledger traps — on Ys-bleeding-edge, bleeding-edge

### Also in flight on other branches
- `bugledger/belac` (Caleb, 2026-11-05): bugledger: 18 open error(s) from belac's play tests, 12 report file(s)
- `bugledger/gradi` (Garrett, 2026-11-05): bugledger: 194 open error(s) from gradi's play tests, 12 report file(s)
- `bugledger/shombith` (Shombith, 2026-11-05): bugledger: 123 open error(s) from Shombith's play tests, 6 report file(s)
- `bugledger/owner` (Andrew, 2026-11-05): bugledger: E-24ec0fbf4b fixed in 65602ce4d7
- `bugledger/yskha` (Yash, 2026-11-04): bugledger: 51 open error(s) from yskha's play tests, 12 report file(s)
- `bugledger/mrglo` (jtgjones, 2026-11-02): bugledger: 22 open error(s) from mrglo's play tests, 2 report file(s)
- `bugledger-android/yskha` (Yash, 2026-11-02): devicelogs: 2 item(s) from yskha's phone (adb ingest)
- `claude/ai-one-branch` (unattributed, 2026-10-29): docs(ai): ai is the one AI branch; no personal integration branches
- `claude/gracious-johnson-s1lkvt` (unattributed, 2026-10-26): docs(branches): only Yash's sessions build on Ys-bleeding-edge; everyone else on bleeding-edge

### Open tickets (Froglet Tasks)
- T-20261102-ac92 [open] content: Give the Squirrel, Goby and Thresher boost loop an FMOD parameter: their ProximityBoostAudioController boostLoopEvent is event:/SFX/Oneshots/Gameplay sfx/Skim, which has no 'Boost Amount' parameter, so the loop plays but its intensity never follows the boost. (who: anyone with FMOD Studio)
- T-20261024-0212 [open] test: Re-capture Bloomrush after the prism creation queue (Ys-bleeding-edge): Release, Third Eye closed, Arcade > Bloomrush, play past the 90 s auto-capture, stop Play. Compare with the 10-20 captures (CPU busy 130-221 ms, Prism.CreateBlockCoroutine x56k/frame, CoroutinesDelayedCalls 139 ms). (who: anyone)
<!-- team_report:end -->
