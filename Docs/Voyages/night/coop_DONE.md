# Ark escort co-op readiness (DONE, first pass)

Branch `claude/voyage-coop` (off `origin/claude/voyage-night`, c7001d212), commit d435c3102, pushed. Compile clean;
EditMode `ArkModel|ArkFall|ArkPacing|Escort|Voyage` 62/62 pass (`-batchmode -force-d3d11`). Paths under `Assets/_Scripts/`.

No two-client test was run. Multiplayer Play Mode is installed (`com.unity.multiplayer.playmode@2.0.2`), but a party
needs UGS sign-in, Lobby and Relay, and `Docs/MULTIPLAYER_START_HERE.md` §2 forbids testing against the live UGS project
without the owner's go-ahead. So this pass is code review plus EditMode tests. The MPPM test in "Hand test" below is
for the owner.

## The model (unchanged, now applied to the escort)

`Docs/Voyages/SLICE.md` §4.5 and §5: friends sail one course, and each machine runs its own Ark, corridor and fauna.
The host-owned networked Ark is V2. Until this pass the escort had no course at all: a friend's machine sailed nothing,
or a different escort if the friend opened the card. Also, the host's networked species in corridor worlds were
network-spawned onto the friends' machines.

## Fixed in this pass

1. **Corridor worlds are peer-local (`Cell.IsSatellite`).**
   - `CellLifeSpawnerBase.cs:177` (flora) and `:509` (fauna): every peer seeds every species in a satellite cell, synced or not. Before, a client seeded none of The Maw's `NetworkSynced` Wildlife Sharks, so its Maw had no sharks of its own.
   - `FaunaNetworkSync.cs:178-185`: the host never network-spawns a satellite cell's creature. Before, the host's corridor sharks appeared on friends' machines at the host's coordinates.
   - Every corridor creature is now its peer's own sim authority. Plate wear (`Fauna.BiteMass`) and `Fauna.Killed` run once per machine, on that machine's own world. There are no puppets to double count.
2. **The escort course.**
   - New `VoyageCourseKind.EscortSail` / `EscortEnd` (`Data/Voyages/VoyageCourse.cs`) carry the difficulty, a seed, the host's home pose, and the seat list (client ids in seat order).
   - The leader sends them (`ArkEscortRun.Coop.cs:107` `AnnounceSail`; `AnnounceEnd` is called from `FinishMetrics`).
   - Friends' harbours sail as followers (`VoyageHarbour.cs:219`): the same difficulty, and the corridor is seeded (`_cfg.Seed = _seed`).
   - Each world's hostile roll comes from the seed (`ArkEscortRun.cs` `_rng`; it was `TickCount` before).
   - `ArkwayRun.HomeOverride` (`ArkwayRun.cs:292`) gives every peer the host's origin and heading, so the worlds stand in the same places.
   - A friend can't start an escort of its own inside a host's session (`VoyageHarbour.SetSailEscort`). It ends when the host's does.
3. **Human seats; AI fills the rest.**
   - The leader keeps its own `MySeat`, and every other connected human takes the next free seat in client-id order (`ArkEscortRun.Coop.cs:38` `BeginCoop`).
   - The host spawns AI companions only for seats no human took (`ArkEscortRun.cs:256` `IsHumanSeat`).
   - A follower flies the slot of its seat in its own active squad (`WithSeat`).
   - With more friends than seats, the extras fly along without a seat.
4. **Pacification runs on every peer** (`ArkEscortRun.Metrics.cs:375`), because the corridor cells are local. Before, the server-only gate meant a friend's worlds never pacified.
5. **The report for parties.**
   - New fields `Seed`, `Following` and `HumanSeats`.
   - The party's other humans are sampled as seats (seat -1, by player name) for proximity and round stats.
   - Each machine writes its own report, because each runs its own escort.
6. **Pace determinism.** Ark speed is a pure function of position (destination, departure, profile); there is no accumulated state. New tests `ArkPaceIsAPureFunctionOfPosition` and `EscortCourseRoundTripsThroughJson`.

## Still open, by priority (file:line hooks)

1. **P0, unverified in a real party.** Run the MPPM hand test below. The risks: `ConnectedClientsIds` order vs the friend's expected seat, and the host's AI companions (networked vessels) hunting the host's fauna while they fly through each friend's local copy of the world.
2. **P0: Ark drift between peers.**
   - Each Ark starts moving when its own load veil drops (`ArkwayRun.cs:486` `SetUnderway(true)`). A friend whose world builds 10 s slower has its Ark up to `Crawl x 10 s` behind (about 140 u at crawl, up to 450 u at cruise).
   - Fix: the host sends `NetworkManager.ServerTime` at underway in a new `EscortUnderway` course, and followers fast-forward their Ark along the same path by the difference. That's safe because the pace has no state, though it needs an `Ark.Advance(seconds)`.
   - V2 is the host-owned networked Ark.
3. **P1: separate fates.** Each peer's Ark can fall or survive on its own (its own fauna). Decide whether the host's outcome is everyone's. If so, send the outcome in `EscortEnd` and make followers end with it (`ArkEscortRun.cs` `OnArkFallen` / `OnRouteEnded`, `VoyageHarbour.cs:219`).
4. **P1: the friend's seat and squad.** The seat comes from connection order, not the joined squad's own `MySeat` (`Data/Structs/SavedSquad.cs:29`, a joined copy holds 1 or 2). Better: followers report their `SavedSquad.Id` and `MySeat` to the host before it sails (a client-to-host named message), and the host honours a joined copy's seat. Hook: `BeginCoop`, leader branch.
5. **P1: difficulty and choice for a party.** Only the host's card sails (`UI/Views/VoyageJobsView.cs:133`). Friends see their own card and difficulty arrows. Gate the card's sail button on `VoyageCourseLink.IsLeader`, and show "following the host's escort at difficulty N" (`VoyageHarbour.EscortDifficulty`).
6. **P2: the host's AI on friends' worlds.** The AI wingmates are networked companions (`ArkEscortRun.cs:269` `RequestSpawnAiCompanion`). Their kills land on the host's world, while friends only see them fly. Their projectiles may still hit friends' local creatures and plates (replicated effects). Measure friendly fire per peer.
7. **P2: params and genes.** `-arkEscortParams` is the sim's only (`ArkEscortSim.cs:77`), and genes are per process (`EscortGeneRegistry`). That's fine solo. If a party ever sails tuned values, put the applied params in the `EscortSail` course and apply them on followers too (`ArkEscortRun.Metrics.cs` `UseRuntimeEscort`).
8. **P2: party reports.** Each machine writes its own report. For a party summary the host would collect followers' reports (a client-to-host message on `Finished`). Kills by remote humans don't reach the host's `RunSeats`, because they happen on the friend's own world.
9. **P3: leash and recall for humans.** `BringWingmates` (`ArkEscortRun.cs:501`) moves only AI seats; a human friend is only leashed by its own `ArkwayRun.TickLeash`, which is per peer and fine.

## Round 2 (commit cf7d2ef99): Ark sync, shared fate, host-only card

- **Ark drift (was P0):**
  - The host sends `EscortUnderway` with `NetworkManager.ServerTime` when its Ark gets under way (`ArkEscortRun.Coop.cs` `CoopOnUnderway`).
  - A follower compares that with its own departure time (`TrySyncArk`). If its world built later, it fast-forwards its Ark with `Ark.AdvanceCourse`: the stateless course stepped in 0.1 s slices, up to 5 s per frame, stopping at a core until the corridor retargets. If it set out earlier, it holds with `Ark.HoldCourse`.
  - The difference is written as `ArkSyncSeconds`.
  - Remaining drift is only what the corridor's 0.2 s retarget tick adds.
- **Shared fate (was P1):**
  - `EscortEnd` carries the host's `Result`, `FallenLeg` and `Overrun`, and followers end with it (`EndWithHost`).
  - A follower's own fall or arrival does not end it. It holds with banner "THE ARK IS DOWN - HOLDING FOR THE HOST" or "THROUGH - HOLDING FOR THE HOST" (`FollowerHolds`, `ArkwayRun.EndsOnArkFall` off), and records `LocalOutcome`.
  - It gives up after 90 s without the host's end, or immediately when the session drops, and then ends with its own outcome.
  - Breach lines never end a follower.
- **Host-only card (was P1):** the escort card's Set sail and difficulty stepper show only on the leader (`UI/Views/VoyageJobsView.cs`, `sail: VoyageCourseLink.IsLeader`). Once the host sails, a friend's card shows the escort at the host's difficulty (`VoyageHarbour.EscortDifficulty`).
- **Joined squad's own seat: not done.** It isn't simple: the host must learn each friend's `MySeat` before it assigns seats and spawns AI, which needs a friend-to-host named message. Seats stay in connection order.

## Owner test script (2 machines, or MPPM with 2 players; needs a UGS go-ahead or the stand-ins)

Setup: build or open `claude/voyage-coop` on both machines. Both players are signed in, in one party, and in freestyle.
Player A hosts. Each machine writes its escort log line (`[ArkEscort]` in the player log, or the sim JSON).

1. **Card.** On B (friend), open Voyages, then the Ark escort card. Expect no Set sail button and no `<` `>` arrows. On A
   (host), expect both.
2. **Sail together.** A picks difficulty 3 and presses Set sail. Expect:
   - B's load veil to rise within about 2 s of A's, without B pressing anything;
   - on A, 1 AI wingmate, not 2 (B holds a seat);
   - B to fly the hull in its own squad's slot 1 (Rogue One), the first seat after A's.
3. **Same world, same Ark.** When both veils drop, A and B each look at A's vessel and at the Ark. Expect the Ark within
   about 20 u on both screens (a hull length is 110 u), both Arks crawling into the same first world, and B's vessel
   beside A's Ark on A's screen.
   - If it fails: B's log line `ArkSyncSeconds` shows how far apart the two machines set out. A large value with the Ark still behind means the fast-forward didn't apply.
4. **Fauna are local.** In The Maw (world 5), each machine has its own sharks, and there are no "ghost" sharks on B that don't react to B.
5. **Shared fate, fall.** Let A's Ark fall (fly away from it in a hostile world, or difficulty 9). Expect A's "THE ARK
   FELL IN ..." banner, and the same banner on B within a second even if B's Ark still stands. Both escorts then end.
   B's report: `HostOutcome` = 2 (Fallen), `Following` true.
6. **Shared fate, B first.** At difficulty 9, if B's Ark falls first, B shows "THE ARK IS DOWN - HOLDING FOR THE HOST" and
   keeps flying. B ends when A's escort ends, with A's outcome.
7. **Host leaves.** Start a new escort, then A leaves freestyle. Expect B's escort to end within a second (abandoned).
8. **Session drop.** Start a new escort, then kill A's game. Expect B to continue alone (its own outcome stands), or to
   end at once if its Ark already fell.

Report back: both machines' `[ArkEscort]` lines and B's `ArkSyncSeconds`, `HostOutcome`, `LocalOutcome` and `HumanSeats`.

## Solo regression

`run_sim.ps1 -Plan "3:Sparrow,Squirrel,Dolphin" -Tag coop_solo -Proj D:\Voyage\wt-a`, run under the shared `sim.lock`. The
escort sailed as leader and **survived**: hull min 17%, 609 s under way, 2 wingmates, 2 worlds pacified, 3 plates to
friendly fire (`D:\VoyageLogs\coop_solo.jsonl`). The JSON shows `Seed` set, `Following` false and `HumanSeats` 1, so the
seeded corridor and home override don't break the solo path.

Round 2 solo regression (`coop_solo2`, lock-aware `run_sim.ps1`): d3 Sparrow+Squirrel+Dolphin **survived**, hull min
25%, 608 s under way, 2 worlds pacified. The leader path is unchanged by the sync and fate code: `Following` false and
`HostOutcome` / `LocalOutcome` None. Compile clean; EditMode 63/63 (`-batchmode -force-d3d11`).
Post-merge (origin/claude/voyage-night into coop, 209c1a2e7): compile clean, EditMode 63/63.
