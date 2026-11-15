# Snap - the Goby & Pistol's Charge game

`GameModes.Snap = 114`, hint toast `SnapHint = 189` (both reserved in `Docs/ElementalLoop/GAME_KIT_ROSTER.md`, row 24).
Authored by `Tools/Build/author_snap_assets.py` (shared pieces in `goby_games_lib.py`). Built 2026-11-15 from Garrett's
"make all the missing games you can" (the roster's step 0). **Authored headless, not yet run in the editor.**

## The loop, in the pair's own terms

A hunt through Kelpwood's ball of kelp. The **Goby** cruises and darts from strand to strand; its Charge card, **Lock**,
holds the best rival in its nose's cone. While that rival is on the **Pistol**'s screen, a vortex ring the Pistol fires
(**Snap**) homes on them at 240 deg/s, and a ring that reaches the rival's hull is a **ring hit** (Vortex Sting): 1 -> 3
petals from each of their elements with Charge, stolen onto the pair at Charge 5 (Vortex Steal). Every ring hit is one
point; the first DOMAIN to the target wins. The two halves have to work together: the Goby's lock and its route put the
rival in the Pistol's view; the Pistol picks the moment.

## What it owns, and what it reuses

| Decision | Snap |
|---|---|
| Metric | `CombatPoints` (8). `SnapScoringRuleSO.PointsForCombatHit` pays the ring's class (`MissileDirect`, the config's `ringHitClass`) 1 point and every other class 0. The breakdown is `MissileHitsLanded`, which `CombatHitScoring` already counts for that class. |
| Target | `EndConditionOverridesSO.snapHitTarget` (5, an estimate: the ring's 4 s cooldown and the lock-on-screen gate make hits rare), read by `SnapHitTurnMonitor` (`CombatPointTurnMonitorBase`). |
| Comeback | 0.8 per point of deficit: a quarter of the target behind (1.25 hits) buys one element level. |
| Arena | **Kelpwood's four cells, referenced, not forked.** The kelp gives a dart something to stop on (a perch), and its neutral mass is what the Goby AI routes the Pistol's islands through; the tadpole herds are fauna the Goby can lock when no rival is in its cone. |
| Vessel lock | The card's `Vessels` (the Goby class). |
| AI | **None of the mode's own.** An AI pair is its seat's crew (`GobyPistolDuo`): the Goby AI locks the nearest rival in range (`AiLockNearestRival`) and the Pistol AI snaps when the lock is on screen (`PistolAI.Think(..., LockedInView())`). Rampage's reason: the kit's own loop IS the game. |
| Min players / domains | 2 / 2: a ring never scores on a team-mate. |

Nothing about the pair changes (LOCKED, Shombith 2026-10-31). The scene is Kelpwood's, cloned through
`lib.committed_scene`: the controller, monitor and rule swapped, the donor's settings and arena-cell fields stripped
(this controller reads neither), and the four AI templates turned from Anglers into Gobies.

## Intensity

Kelpwood's ladder, as it is: 28 -> 88 strands, eight -> four herds, three -> one omni crystals, danger tips at I3 and
collars at I4. More kelp means more to perch on and more to hide behind; danger kelp punishes a careless dart.

## Verification status

Authored headless: the generator's `--check`, the reference compile and the static gates. **Not run in Unity.** What
only a person can judge: whether a rival is on the Pistol's screen often enough for hits to come at a fun rate (if not,
the target drops, or the arena shrinks), and whether the kelp is too dense for the Goby's darts.

## Known limitations

- The target is a guess. A first timed match should set it.
- A human flying the Goby half has the Pistol AI snapping for them; a human flying the Pistol has the Goby AI locking.
  Either way the human's half decides, which is the crew design.
