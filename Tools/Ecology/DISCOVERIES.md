# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.

## Builders and thieves

Direction D (`Tools/Ecology/builders/`). The Serpent wall's steal-and-assemble, generalised to creatures that STEAL
prisms and BUILD with them by stigmergy. Everything below is seeded and measured; negatives are kept.

### Round 1 (2026-10-02, ~05:00 UTC) - the substrate and four species

**What `common/` gained (additive, backward compatible).** `Arena.steal(i, dom)` (changes hands, never removes;
refuses shielded and super-shielded), `Arena.move_mass` (counted: the expensive part in game), `Arena.set_danger`,
`Arena.destroy` (an ACTIVE force - a vessel ability), `Arena.audit()` (created - live - eaten - destroyed, must be
0), domain/danger/trail ledgers, pilots that lay a conserved TRAIL (`Pilot.trail_every`), a `circuit` pilot that races
a fixed loop, and Recorder deltas so the viewer shows prisms MOVING and changing colour when stolen.

**A correction to the brief.** `WallAssembler` pulls an opponent's prism SLOWER, not faster:
`ownPullSpeed 4.0` vs `opponentPullSpeed 1.0` (and rotate 8.0 vs 3.0). It steals on SNAP, with `superSteal: true`, which
in `PrismTeamManager.Steal` still refuses super-shielded mass but takes plain SHIELDED mass. It also turns its bottom
mates DANGEROUS and its top mates SHIELDED on bond. The creatures here are stricter: shielded mass is never taken.

**Substrate** (`builders/core.py`): a struct-of-arrays colony that forages loose mass (4x preference for a pilot's
trail), steals on pickup, carries the prism (one position write per moving carrier per step), and deposits on a
construction lattice when a species' LOCAL rule says so. No species reads a blueprint.

**The rules hold** (`builders/test_rules.py`, 90 s, 30% of mass shielded, a pilot laying trail): all five species
OK - audit 0, nothing eaten or destroyed without a ramming pilot, no shielded prism changed domain, moved, or was
held. The negative control (a thief shown every shield as off) is CAUGHT: 33 shielded prisms taken.

#### Species 1 - nest weavers (3 seeds x wander/evader/hunter, 3 min)

| variant | rule | result |
|---|---|---|
| v0 shell | Bonabeau royal-chamber template Q(r) + cement, workers walk to their bearing on the shell | **negative**: the template wins; every seed builds the same complete sphere (shell_frac 1.0, one component; Jaccard between seeds 0.27-0.36) |
| v1 logistic | broad template, cement feedback, workers must walk HOME to the core with real mass (Ladley & Bullock's logistic constraint) | irregular, supply-facing structure: Jaccard 0.53, 1 main component + satellites, 56% player trail. In slab section it reads as a porous cloud, not walls |
| v2 wasp comb | Theraulaz-Bonabeau lattice-swarm rule table: stalk / comb / envelope bricks, "up" = the cell's outward radial | first attempt built **2 bricks in 5 min** (workers circulating in open volume never land next to a 2-brick nest); fixed by making laden workers WALK ON THE NEST (a random walk over built bricks). Then an off-by-one meant no comb could seed. Then the envelope ran away (334 of 415 bricks, slabs over the combs). Envelope held to a 1-site-thick skin at distance 2 from the combs, with a mouth under the stalk: **a stalk, 2 flat comb tiers, an envelope wrapping the side the material arrives from**. Jaccard 0.71 |

Raid (hunter pilot flies at the core, rams bricks and workers): 1.0-1.7 hits/min on the raider, telegraph 0.5 s,
~4.2 kills/min + the brood store (~4 crystals/min raided). The first defence stung at once (telegraph 0.2-0.3 s); a
shared ALARM escalation fixed it (`Colony.defend`: one alarm level rises 0.6/s while a pilot is inside the alarm
radius; below 0.6 the workers form a guard screen between core and pilot, at 0.6 they strike). Raider hits halved
(2.3 -> 1.0/min) and a fast raid beats the alarm clock - the counterplay is speed.

#### Species 2 - fortress menders (`run_fortress.py`, 3 seeds x 3 cutting passes after 150 s of building)

A ram-capable pilot flies straight through the shell; the cut leaves ~14-16 empty sites. Time to re-fill:

| mend rule | half healed (t50, median) | 90% healed (t90, median) | cuts fully healed (90%) | repair from trail |
|---|---|---|---|---|
| none (ordinary building) | 39.5 s | 90.4 s | 4/9 | 98% |
| gap rule only | 15.8 s | 43.6 s | 6/9 | 98% |
| alarm only | **6.8 s** | 41.1 s | 5/9 | 97% |
| both | **6.4 s** | 61.6 s | 6/9 | 98% |

ALARM (a destroyed prism's site releases a diffusing pheromone; laden workers climb it) is what makes the wound close
fast enough to WATCH - 6x faster to half. The GAP rule (deposit probability boosted by how surrounded a hole is) is
what finishes it. t90 is noisy at n = 9 (both > gap alone is inside noise). **98% of the material that closes a wound
is the cutting pilot's own trail** - the colony mends its wall with the weapon's wake.

#### Species 3 - trap builders (5 policies x 3 seeds, 3 min)

The colony learns lanes from trail prisms (a lane field with a 60 s memory + a flow field from consecutive trail
headings), strips the trail and strings webs of DANGER prisms across the lane (strands perpendicular to the flow,
a filament rule keeps holes).

| pilot | hits/min | |
|---|---|---|
| circuit (same loop every lap) | **35.8** | 97% of the web sits on a lane; a trap is visible for a median **19 s** before it is sprung |
| varied (same loop, line re-drawn each lap) | **0.56** | lane_counterplay = varied/circuit = **0.016** |
| wanderer | 0.11 | no lanes, ~16% of a smaller web on a lane |
| evader | 0.0 | |

This is the strongest "the creature learns where players fly" signal in the program so far, and it is too lethal as
tuned (35.8/min). It is a dose: worker count, `max_nb`, `lane_min` and the 0.5 s burn cooldown are the dials.

#### Species 4 - wearers (body from stolen mass)

Hearts steal prisms (69% from the pilot's trail) and wear them on a body lattice; bodies that touch FUSE (39 of 40
hearts into one creature in ~105 s). Past 600 volume the creature hunts: approach with intercept, REAR 1 s (body
contracts, intent rises), LUNGE. Wanderer **2.1 hits/min with a 1.15-1.3 s telegraph**, evader **0** (counterplay
0.0), hunter kills **13.3/min** and strips 1041 volume of prisms back to its own domain. Two negatives:
1. the attachment exponent alpha (nb^-alpha) changed packing (mean neighbours 12.8 -> 2.3) but **every silhouette
   was the same isotropic blob**, because sites were drawn from the whole frontier;
2. contact attachment (a prism sticks where it touched - DLA's arrival point) fixes it: a massed head with a long
   tail toward where it fed.

**Cost, measured (sim at 10 Hz).** Carried-prism writes: nest 90-110/s (48 workers), traps 120-380/s (60 workers).
Wearer body prisms move with their creature: **1,900-2,500 prism writes/s** for one 300-prism monster. That is
the expensive part, and it is why the port design (below, round 2) parents a body to one transform.
