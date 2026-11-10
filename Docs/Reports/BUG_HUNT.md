# Bug Hunt — where it stands (2026-11-10)

**Owner:** Yash. **Branch:** fixes land on `Ys-bleeding-edge` first and reach `bleeding-edge` with each merge (#1541
merged 2026-10-25). **Board:** [Team Bug Board](https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno) (issue states
refreshed 2026-11-10, run log `runs/2026-11-10`). Mechanism: `Docs/DIAGNOSTICS.md` § "The Bug Ledger".

## 1. In one minute

- **Six people send errors from Unity**, each to their own `bugledger/<user>` branch: Garrett (`gradi`), Shombith,
  Andrew (`owner`), jtgjones (`mrglo`), Yash (`yskha`), Caleb (`belac`).
- **On the board: 391 open, 8 being checked after a fix, 79 resolved** (the ledger itself: 399 issues, from 708
  branches). The 2026-11-07, -08 and -09 sweeps were cut off by worker restarts before the board mirror; their
  findings are in their run logs and the 2026-11-10 sweep mirrored all of it (it also restored 18 board entries
  the 2026-11-06 mirror had written with partial data).
- **Waiting on a pull request: `Manta.asset` does not parse** (E-251530c87b, E-603d7f129d, E-c52c39070d; Garrett,
  Andrew and Yash, since 2026-11-08). #1804's Yastri description is an unquoted YAML value whose first line ends in
  a colon. The one-line quote fix (words unchanged) is on `claude/fix-manta-ability-map-yaml` (`580693b6b9`, cut
  from `bleeding-edge`, which takes changes only through a PR). Garrett's Manta.
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
_Generated 2026-11-10 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area bug-hunt --days 14`._

### Recent changes (25+)
- 2026-11-10 `168f61237` bugledger: E-9f5f369ac6 fixed in fa702c0db2 — on another branch
- 2026-11-10 `0eac56a76` bugledger: 68 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-10 `6cb75ac20` bugledger: 208 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-10 `c3c82b9ae` bugledger: 206 open error(s) from gradi's play tests, 6 report file(s) — on another branch
- 2026-11-10 `2f3f06fbf` bugledger: 127 open error(s) from Shombith's play tests, 12 report file(s) — on another branch
- 2026-11-10 `aaee3671f` bugledger: 27 open error(s) from belac's play tests, 12 report file(s) — on another branch
- 2026-11-10 `67eff5aac` bugledger: 65 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-09 `d31b003bd` bugledger: 22 open error(s) from belac's play tests, 8 report file(s) — on another branch
- 2026-11-09 `1e8ffc2df` bugledger: 21 open error(s) from belac's play tests, 4 report file(s) — on another branch
- 2026-11-09 `9f139d3ab` bugledger: 206 open error(s) from gradi's play tests, 12 report file(s) — on another branch
- 2026-11-09 `b8e770b94` bugledger: 55 open error(s) from Will's play tests, 4 report file(s) — on another branch
- 2026-11-09 `15148f20b` bugledger: 126 open error(s) from Shombith's play tests, 2 report file(s) — on another branch
- 2026-11-09 `3f6111bfe` bugledger: 21 open error(s) from belac's play tests, 9 report file(s) — on another branch
- 2026-11-09 `47dd29c43` bugledger: 54 open error(s) from Will's play tests, 1 report file(s) — on another branch
- 2026-11-09 `b982f93c0` bugledger: 53 open error(s) from Will's play tests, 5 report file(s) — on another branch
- 2026-11-09 `a8eb4f828` bugledger: 51 open error(s) from Will's play tests, 5 report file(s) — on another branch
- 2026-11-09 `3501d6d89` bugledger: 49 open error(s) from Will's play tests, 12 report file(s) — on another branch
- 2026-11-09 `23429b638` bugledger: 68 open error(s) from Owner's play tests, 12 report file(s) — on another branch
- 2026-11-08 `2f53bd755` bugledger: 202 open error(s) from gradi's play tests, 3 report file(s) — on another branch
- 2026-11-08 `48cb336a8` bugledger: 202 open error(s) from gradi's play tests, 4 report file(s) — on another branch
- 2026-11-08 `3502b556f` bugledger: 198 open error(s) from gradi's play tests, 7 report file(s) — on another branch
- 2026-11-08 `1459e7c44` bugledger: 67 open error(s) from Owner's play tests, 12 report file(s) — on another branch
- 2026-11-06 `73a30251a` bugledger: 58 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-06 `5a49fbc88` bugledger: 62 open error(s) from yskha's play tests, 12 report file(s) — on another branch
- 2026-11-06 `0d0a3bb98` bugledger: E-0de9ec6c70 fixed in 93765d6aa8 — on another branch

### Also in flight on other branches
- `bugledger/gradi` (Garrett, 2026-11-10): bugledger: E-9f5f369ac6 fixed in fa702c0db2
- `bugledger/yskha` (Yash, 2026-11-10): bugledger: 68 open error(s) from yskha's play tests, 12 report file(s)
- `bugledger/shombith` (Shombith, 2026-11-10): bugledger: 127 open error(s) from Shombith's play tests, 12 report file(s)
- `bugledger/belac` (Caleb, 2026-11-09): bugledger: 27 open error(s) from belac's play tests, 12 report file(s)
- `bugledger/will` (ownerbynight, 2026-11-09): bugledger: 55 open error(s) from Will's play tests, 4 report file(s)
- `bugledger/owner` (Andrew, 2026-11-09): bugledger: 68 open error(s) from Owner's play tests, 12 report file(s)
- `bugledger/mrglo` (jtgjones, 2026-11-02): bugledger: 22 open error(s) from mrglo's play tests, 2 report file(s)
- `bugledger-android/yskha` (Yash, 2026-11-02): devicelogs: 2 item(s) from yskha's phone (adb ingest)
- `claude/ai-one-branch` (unattributed, 2026-10-29): docs(ai): ai is the one AI branch; no personal integration branches

### Open tickets (Froglet Tasks)
- T-20261102-ac92 [open] content: Give the Squirrel, Goby and Thresher boost loop an FMOD parameter: their ProximityBoostAudioController boostLoopEvent is event:/SFX/Oneshots/Gameplay sfx/Skim, which has no 'Boost Amount' parameter, so the loop plays but its intensity never follows the boost. (who: anyone with FMOD Studio)
<!-- team_report:end -->
