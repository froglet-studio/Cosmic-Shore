# Broadside (Charge arena): night of 2026-11-23

Written by the Broadside thread for the morning report. Every number is from the real game (Amoebius, headless, trunk
910c2778a), each seat flown by its hull's shipped AI, 4v4 timed 240 s matches. 21 runs, about 560 matches.
Share = a hull's points over the match's mean per seat (1.0 = a fair share). One run swings a hull by up to 0.25, so
every verdict below is pooled over 3+ runs.

## The answer

- **Balanced roster today: the 8 hulls from last night (Stoat, Brittle Star, Meerkat, Angler, Jacks, Scarab, Orca,
  Grizzly) sit at 0.87-1.16 with shipped values, except the Orca at 1.33** (3 runs, 41-43 matches a hull, rest + L5).
  With the Orca's fast gun reined (heat 6 -> 3 s, cool 5 -> 10 s) the 9-hull field is 0.82-1.17 (3 runs) and the
  Orca 1.17. That gun change is NOT shipped: the Orca lane is occupied (claude/ms-ai-orca, AI training), so it needs
  your go or the occupant's.
- **Swordfish moved up with its own lever: sword hit latch 1.4 -> 0.5 s** (`VesselCombatHitBySword`, the Swordfish's
  asset alone; the faster-than-victim rule kept). 0.58 -> about 0.85 pooled over 6 roster runs (about 1.0 at rest,
  0.65-0.8 at L5). Shipped in the PR; not seated on the card yet (low edge at L5).
- **Two hulls only reach the band by changing their own Charge game's premise; cards posted:**
  - Swordfish without the faster-than-victim rule: overshoots to 1.07-1.34 at latch 0.5 (a latch near 0.8 s would
    likely seat it), but Riposte's premise ("only while the Swordfish is faster") goes. Recommended: keep the rule.
  - Rhino: the gore SPEED is its binding lever, not the latch. Gore speed 150 -> 50 u/s at rest plus gore latch
    1.4 -> 0.6 s gives about 0.87; 100 u/s gives 0.59; latch alone 0.17-0.36. At 50 u/s a cruising Rhino gores, so
    Bullrush no longer needs its Ramp Spool wind-up (BULLRUSH.md, RhinoReworkTests, author_bullrush --check quote it).
    Recommended: keep the wind-up, Rhino stays off.
- **Thresher reaches the band with reach alone: `vesselHitRadius` 4 -> 20-22** (1.0-1.13 pooled over 6 runs, no
  speed or prism change). Not shipped: the Thresher is Yash's lane. Your go or his.
- **Flagged, levers saturate below the band** (relative to the roster's mean, rest):
  - Squirrel ~0.58: joust speed 90 -> 45 u/s doubles its strikes (4 -> 9) and 20 u/s adds nothing; the cap is the
    SHARED joust asset's faster-than-victim rule (Stoat, Shrike, Brittle Star, Thresher use it too). Needs a
    Squirrel-only joust asset (a kit decision).
  - Sparrow ~0.55-0.77: rockets are AI-limited (it fires only when someone is ahead); a 10x faster rack refill
    (`ammoPerPrism` 0.01 -> 0.1) and cheaper rockets did not move its missile hits.
  - Manta ~0.52: one skim per bomb (`energyPerSkim` 0.34 -> 1) lifts debuffs 3.5 -> 5.8; the fuse (20 -> 3 s) does
    not bind. Manta lane is also occupied (Brood Rush integration branch).
- **Too strong, no new lever tried tonight:** Wrasse 3.27, Jewel Wasp 1.87 (full field). Untouched: Honeyguide,
  Shrike, Tiger Moth, Urchin, Dolphin, Goby, Serpent (as last night).

Seats: the card seats up to 9 (4v4 plus host). It ships 6 hulls today; Orca and Grizzly are judged in band but not on
the card. 8 balanced hulls fill a lobby without repeats once the Orca gun is settled.

## Final tables

Shipped values plus the sword latch (`sWS_*`, seeds 57 L5, 58 rest, 59 L5):

| hull | pooled | rest s58 | L5 s57 | L5 s59 |
|---|---|---|---|---|
| Orca | **1.33** | 0.94 | 1.40 | 1.60 |
| Brittle Star | 1.16 | 1.34 | 0.88 | 1.26 |
| Scarab | 1.02 | 0.81 | 0.90 | 1.30 |
| Stoat | 1.01 | 0.89 | 1.47 | 0.71 |
| Angler | 0.98 | 1.08 | 0.82 | 1.05 |
| Jacks | 0.95 | 0.98 | 0.95 | 0.93 |
| Meerkat | 0.93 | 0.87 | 1.14 | 0.80 |
| Grizzly | 0.87 | 1.08 | 0.76 | 0.77 |
| Swordfish | 0.77 | 1.02 | 0.69 | 0.60 |

Same roster, Orca gun reined (`hWH_*`, seeds 54 rest, 55 L5, 56 rest): Jacks 1.17, Orca 1.17, Brittle Star 1.11,
Meerkat 0.98, Angler 0.97, Scarab 0.95, Swordfish 0.93, Stoat 0.89, Grizzly 0.82.

## The lever log (all rest unless noted; field = the 8 + the six weak hulls, 2 seeds a round)

| hull | lever | result |
|---|---|---|
| Rhino | gore speed 150 -> 50 / 0 u/s | strikes 5.4 -> 10.3 / 12.1 |
| Rhino | + gore latch 1.4 -> 0.7 / 0.6 s | 15 strikes, 0.87-0.90 |
| Rhino | latch 0.4 s, shipped speed | 0.17 / 0.36 (does not bind) |
| Rhino | speed 100 -> 33 + latch 0.6 | 0.59 |
| Swordfish | requireFasterThanVictim off (latch 1.4) | no change |
| Swordfish | blade cooldown 5 -> 2.5 s | no change |
| Swordfish | sword latch 0.7 / 0.5 s | 8 -> 13-16 strikes, ~0.85 pooled |
| Swordfish | latch 0.5 + rule off | 19-21 strikes, 1.07-1.34 |
| Squirrel | joust speed 45 / 20 u/s | 9.6 / 8.3 strikes (saturated) |
| Thresher | vesselHitRadius 14 / 20 / 22 / 26 | 0.84 / 0.87 / 1.0-1.13 / 1.13 relative |
| Manta | fuse 6 / 3 s, energyPerSkim 1 | debuffs 3.5 -> 4.4 -> 5.8 (fuse does not bind) |
| Sparrow | ammoPerPrism x4 / x10, ammoCost 0.1 | missiles unchanged |
| Orca | fast gun cool 10 / 14 s, heat 3 / 2.5 s, rate 20 | bullets 178 -> 35-60; ~1.17-1.30 |
| Orca | bomb re-arm 20 -> 23 s | no measurable gain |
| Jacks | dangerShare 0.06 -> 0.045 | no change (does not bind below 0.06) |

What it taught: for contact hulls the speed gate binds first (Rhino gore speed, Squirrel joust speed), then the
per-victim latch binds once the hull lands often (Rhino, Swordfish). Last night's "latches never bind" held for hulls
whose hits spread across victims; a duelling contact hull re-strikes the same victim.

## Regatta

No lever shipped tonight touches speed, boost, trail or prism size (the sword latch is a vessel-contact window), so no
Regatta re-judge was needed.

## PR

PR #2347 (https://github.com/froglet-studio/Cosmic-Shore/pull/2347, branch claude/project-thread-ef9ick) carries the sword latch 0.5 s (asset, `broadside_balance.py`
NEW_WINDOWS, the author_broadside check that required the sword's window to exceed the joust's, RIPOSTE.md). The
Riposte duel (the Swordfish's own Charge game) feels it: a victim can be struck again after 0.5 s.

## Housekeeping

- Trunk 910c2778a does not build in Amoebius (`UIThemeLive.cs(272)`, `case Shadow` on a `UIBehaviour`): Port's
  `Shadow` needs `: UIBehaviour` (PackageComponents.cs:261). Kept local; the Amoebius lane is occupied.
- 3-6 matches per run were OOM-killed with 4 workers on a 15 GB box; the shares are from the valid matches.
- Run files: `full_rest_s40`, `wA_rest_s41`, `wWB_*`, `wWC_*` (lever rounds), `fW{D,E,F,G}_*` (11-hull roster), `hWH_*`, `sWS_*`;
  override sets in `ov/`.
