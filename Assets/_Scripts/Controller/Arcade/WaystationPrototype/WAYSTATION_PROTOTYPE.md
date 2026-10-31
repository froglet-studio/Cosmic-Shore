# WAYSTATION PROTOTYPE (GameModes 76)

The Tiger Moth's Time race, rebuilt around **short hops**, as a separate mode beside Waystation (58).

Shombith, 2026-10-31: "don't change Waystation; create another game called Waystation Prototype and do all these there;
if Garrett approves, then we replace."

So Waystation is byte-for-byte as Garrett left it. This mode reuses it without editing any of it:
- `WaystationPrototypeController` inherits `WaystationController`.
- `WaystationPrototypeCourseSource` calls `WaystationCourse.Generate`.
- The scene is a clone of `MinigameWaystation.unity`.

The design round, the numbers and the fun score are in `Docs/Studios/TIGER_MOTH_TIME.md`.

**Status:** authored headless in a cloud session. It has **not been run in the Unity editor**. The editor steps are in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Waystation Prototype").

## The loop, in the Tiger Moth's own terms

1. **Clusters.** You weave Waystation's coils of rings, exactly as in Waystation. Each cluster ends at its square exit
   gate, which leaves the next cluster under your belly once you roll.
2. **A Drop Tower in every gap but the last.** The tower is a staircase of short hops laid along the gap's drop line:
   - Each hop's ring hangs just past a **floor plate**, under the line you fly.
   - Fly over the ring and **tap the Drop**. The fall is incorporeal, so you pass through the plate, thread the ring
     and land flying along the fall line.
   - **Too short a hold** lands you on or in front of the plate.
   - **Too long a hold** leaves too little runway for the Drop to recharge and the next roll to finish, and the
     **pipe** standing across your new heading meets you.
   - The pattern is down, sideways (no roll), roll 180, down, sideways back. Every Drop is a 90-degree turn, and from
     Garrett's square exit gate this square wave is the only pattern that makes progress toward the cluster.
   - At I3 and I4 some sideways hops are **roll gates**: the ring is abeam, so you roll 90 degrees first.
3. **The last gap is the long drop.** One Garrett-style drop of 1000 u or more. It leaves the Tiger Moth's neutral
   wormhole pair, the team route for whoever follows. It comes last, so its 20 s recharge never costs a tower.

## What the mode owns, and what it reuses

| Piece | Where it comes from |
|---|---|
| Clusters, exit gates, ring order, scoring (`SwitchesThreaded`, golf), turn monitor, race beats | Waystation and the gate-race platform, unedited |
| Long-drop autopilot (belly aim, roll onto an abeam ring) | `WaystationController`, inherited; the scene raises its `aiFoldMinDistance` 420 -> 700 so it takes only the long gap |
| Drop Towers (rings) | `WaystationTower.Lay` / `Build` (pure), mirrored by `Tools/Build/waystation_prototype_course.py` |
| Tower walls (floor plates, pipes) | `WaystationTower.Walls` (pure, from the broadcast course), laid by `WaystationPrototypeController.OnCourseRaised` on every peer as environment prisms (`SpawnablePrism`, `Domains.Blue`), plain or danger by a hash of (gate, wall, tile); regrown one tile per plate every 1.5 s |
| Hop autopilot | `WaystationPrototypeController.TryOverrideAim` / `OverrideBellyTarget` / `TickHops`: flies the cruise line over the ring, rolls the belly onto its drop line, presses through the replicated path, releases at the window's centre (`WaystationTower.HoldForTravel`) |
| Arena | Waystation's: the shared barren Skim Race cell |
| Target | `EndConditionOverridesSO.waystationPrototypeClusterTarget` = 5 CLUSTERS. The finish line is every ring the course lays, towers included (`RaceGateTurnMonitor` reads the laid course; the source answers the same count, so it does not warn) |

## The vessel is not changed (LOCKED rule, Shombith 2026-10-31)

No vessel code or asset is touched. A wall is an ordinary environment prism, so a hull meets it the way any hull meets
that kind of prism.

What a Tiger Moth takes when its colliders come back on inside a wall tile, read from the code
(`VesselImpactor.cs:122-166`, `ButterflyImpactorDataContainer.asset`):
- Prisms are trigger boxes and the hull is a non-trigger sphere of radius 6. Re-enabling the collider raises
  `OnTriggerEnter` once, on the next physics step. Nothing guards against it and there is no `OnTriggerStay`.
- **A plain neutral tile** explodes. The throttle drops by ×(1 − min(0.1·volume, 0.5)) and eases back over 1 s, and
  boost is reset.
- **A danger tile** explodes. The throttle drops to ×0 and recovers over 3 s, and all four elements take a **permanent**
  burn, because Blue counts as a rival domain. Boost is reset.
- Flying into a tile costs the same as landing in one.

## Intensity ladder

The course, not the arena, is what changes:

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Rings per cluster (Waystation's) | 3 | 4 | 5 | 6 |
| Hop window (travel / hold) | 50..250 u / ±0.079 s | 66..234 u / ±0.065 s | 85..225 u / ±0.055 s | 102..218 u / ±0.047 s |
| Tower ring radius | 48 | 42 | 36 | 32 |
| Roll gates (share of free sideways hops) | 0 | 0 | 0.5 | 1 |
| Danger share of wall tiles | 0.10 | 0.20 | 0.35 | 0.50 |

## Gates

Run these:

```
python3 Tools/Build/waystation_prototype_course.py --check
python3 Tools/Build/waystation_prototype_course.py --self-test
python3 Tools/Build/waystation_prototype_course.py --compile
python3 Tools/Build/author_waystation_prototype_assets.py --check
```

- `--check` sweeps 200 seeds × 4 intensities. It checks window, runway, path, edges, mouths, shell, towers, long gap,
  stand-down, danger and radius.
- `--self-test` runs six negative controls, each of which must fail the check it targets.
- `--compile` builds and runs the shipped `WaystationTower.cs` and compares it with the model. On 2026-10-31 it agreed
  on 2267 gates (worst disagreement 0.0013 u) and on every wall's tile and danger counts.

## Known limitations

- **Never flown.** The hop autopilot, the walls in a real cell, the network copy of the walls and the feel of the
  hold are all unverified.
- **A few gaps get no tower.** 2 to 48 of every 600 gaps, most at I4, where a tower would leave the cell or cut a
  cluster. That gap is then flown or dropped Waystation's way.
- **Regrowth is per peer**, as in Serpent Time. After heavy contact, two peers can disagree by a few tiles for a moment.
- **The preview** shows the tower rings but not their walls, which only the match controller lays.
- **The danger hash** is the same function as Serpent Time's `SerpentTimeCluster.IsDangerSlab`, now also on
  bleeding-edge, but written out again in `WaystationTower.Hash01`. Share one hash in a later cleanup; changing it
  would change which tiles are danger, so the model and `--compile` must move with it.
- **Replacing Waystation** is Garrett's call. What it would take is listed in `Docs/Studios/TIGER_MOTH_TIME.md` §5.
