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

### Round 2 (~07:00 UTC) - tuning, scar tissue, moulting, one shared cell

**Trap dose-response** (`results/traps_sweep.json`; 18 configs x circuit/varied/wander x 2 seeds, 3 min). Hits on a
racing-line pilot scale with colony size and fall with the lane threshold; a pilot who varies its line stays near 0 at
every setting; the filament rule `max_nb` barely matters.

| workers | lane_min 1.2: circuit / varied | lane_min 3.0: circuit / varied |
|---|---|---|
| 15 | 14.2-17.2 / 0-0.17 | 3.8-5.7 / 0 |
| 30 | 24.2-29.5 / 0-0.33 | 8.3-10.8 / 0 |
| 60 | 34.5-37.5 / 0.5-0.83 | 12.0-15.2 / 0 |

`lane_min` is the commitment dial ("how many laps before the web goes up"). Picked for v2: 30 workers, max_nb 2,
lane_min 3. Its first full run had ~15 laden workers hauling prisms around with nowhere to build (120-170 carried-prism
writes/s with nothing built). Raising the pickup threshold to 0.3 x lane_min on the blurred scent **broke it: 0 built
anywhere** (the blurred scent never gets that high; recorded as a negative). What works is PARKING: a laden worker that
has not deposited for 20 s and smells no lane at all stops moving (a carrier at rest costs nothing). Parking only where
the lane field is low also failed: a parked worker never woke up, and building halved. **traps_v2**: racing line 6.4
hits/min, varied / wanderer / evader / hunter 0; carried-prism writes 20-30/s off-lane (was 120-160) and 65-100/s on a
lane; webs differ between seeds in the world frame (Jaccard 0.57 on a 100 u grid, same lanes).

**The fortress learns where it is attacked** (`run_scar.py`, 3 cuts along the SAME line, 3 seeds). SCAR = a slow memory
of alarm that widens the template where the wall was cut, so a wound heals thicker. Wall prisms within 20 u of the cut
line just before passes 1 / 2 / 3:

| | pass 1 | pass 2 | pass 3 | sites the 3rd pass cuts |
|---|---|---|---|---|
| no scar | 65 | 74 | 86 | 11-19 |
| scar 3.0 | 65 | 89 | **113** | 25-32 |

That is +31% wall on the attacked line by the third pass, from the same total mass (743-843 built either way): the
colony moves material to where it is attacked. Healing stays fast (t50 16.6 s, 8/9 cuts healed to 90%, 99% of the repair
material from the attacker's trail).

**Wearers v3 - satiation moult** (body cap 150: over the cap a body sheds its outermost prisms where they hang into a
static LAIR, and a new heart is born there - feeding pays out as population). The lair is real (106-266 static prisms,
mostly your trail, 1-3 births per run) and the threat holds (wanderer 2.56 hits/min, telegraph 1.23 s, evader 0). **But
the cost cut is weak: moving-prism writes fell only 8-28%**, because a body regrows to the cap and the births add more
bodies. The cap bounds the cost per creature; it does not lower it much at this population.

**Wasp comb at 10 min:** the envelope grew to 4x the comb (618 vs 141), because the space BETWEEN tiers is also "two
sites from a comb". One more local rule (no envelope with comb both above and below) holds it to a skirt under the
combs: envelope/comb 2.6 at 6 min, 3 tiers.

**One shared cell** (`run_mixed.py`: wasp comb + trap web + wearers; a racer, a wanderer and a hunter; 5 min, 2 seeds).
Audit 0 in both. All three are the cell's one colour, so they can never steal from each other; they compete only for
loose mass. The mass split was wasp 45-52%, wearers 41-43%, traps 5-15%. The traps' share depends on how much lane
the racer lays before the others strip it. Hits by kind on the racer: burn 8 / 25 (the web), sting 0 / 14 (it flew
through the nest), crush 0 / 2. The hunter took everything apart: all 40 wearers and most trap workers killed in 5 min.
The single hunter policy is too strong to be the only probe in a mixed cell.

### Round 3 (~08:00 UTC) - sieges, division of labour, and "is it the seed or the play?"

**You cannot steal a mending wall; you have to break it** (`run_tug.py`, 3 seeds x 3 passes). A raiding vessel that
STEALS the bricks it touches (a Squirrel/Urchin-style steal) instead of ramming them:

| raid | wall healed to 90% | t50 | t90 | wall mass destroyed |
|---|---|---|---|---|
| ram (destroy) | 7/9 | 17.8 s | 70.0 s | 290-440 |
| steal | **9/9** | **5.4 s** | **12.5 s** | 0 |

The stolen bricks fall loose right at the breach, in the pilot's colour, which is exactly what the menders forage for.
They re-steal them within seconds. Removing supply is the only siege that works. That is emergent, and it is good game
design: two vessel verbs with different counters.

**Defence competes with repair** (`run_defend_vs_mend.py`). The ram t50 drifted 6.4 -> 10.7 -> 17.8 s between rounds.
The alarm defence added in round 1 was the cause: it sends every idle worker at the intruder, so nobody is fetching
repair material.

| | t50 | t90 | healed | stings on a pass-through cutter (9 passes) |
|---|---|---|---|---|
| defence on (all idle workers) | 17.8 s | 70.0 s | 7/9 | 1 |
| defence off | 5.6 s | 38.5 s | 9/9 | 0 |
| **defender caste 30%** (response thresholds, Bonabeau, Theraulaz & Deneubourg 1996) | **5.6 s** | **41.3 s** | 8/9 | 1 |

The caste keeps repair at full speed and loses nothing against a cutter. **But it costs raid defence**: against a hunter
that keeps attacking the core, raider hits fall 1.11 -> 0.11/min and crystals taken 27 -> 19 (fewer workers to ram).
Escalating recruitment (the alarm keeps accumulating while an intruder stays) **did not change this at all**. The
hunter pilot passes through the alarm radius in ~1.4 s per pass and the alarm decays between passes, so nothing ever
accumulates. Recorded as a negative. The caste fraction is a design dial: menders vs stingers.

**Structures are shaped by the play, not the seed** (`run_butterfly.py`). Jaccard distance of the built structure
against the seed-7 baseline:

| species | same seed rerun | pilot start nudged by **1 u** | a different seed |
|---|---|---|---|
| nest v1 (logistic) | 0.00 | 0.45 | 0.59 |
| wasp comb | 0.00 | 0.33 | 0.76 |
| traps v2 (racer) | 0.00 | 0.45 | 0.73 |
| wearers v2 | 0.00 | 0.93 | 0.86 |

Same seed is bit-identical; a 1 u nudge to where the pilot starts already rebuilds a third to most of the structure. Two
players on the same seed get different nests, webs and monsters. Replayability is driven by the player.

**The logistic claim, measured** (`run_supply.py`, 8 seeds). cos(structure centroid offset, supply direction): nest v1
**0.94** (null -0.26; offset 4-16 u). Even the template shell v0 leans toward its supply (0.66), but only by 1-7 u. The
wasp comb barely does (0.31): its stalk axis dominates. Ladley & Bullock's point holds: making workers carry real mass
makes the structure face where the mass comes from. Here that is where the player flies.

### Where Direction D stands (final scorecard, ~08:45 UTC)

Final versions, 3 seeds x wander/evader/hunter (+ racer/varied for traps), 3 min each (`run_all.py`,
`summarise.py`, `results/*.json`). Audit is 0.000 in every run: nothing in this direction removes mass except a
pilot's ram and a sprung trap.

| species | wander hits/min | evader | racer (circuit) | hunter hits/min | telegraph s | hunter kills/min | built (wander) | player trail in it | Jaccard seeds | moves/s (wander) | index queries/s | audit |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| nest_v1_logistic | 0.00 | 0.00 | - | 1.45 | 0.50 | 4.2 | 238 | 0.50 | 0.53 | 100 | 85 | 0.000 |
| nest_v2_wasp | 0.00 | 0.00 | - | 0.89 | 0.50 | 3.7 | 240 | 0.49 | 0.69 | 86 | 89 | 0.000 |
| fortress_final | 0.00 | 0.00 | - | 0.22 | 0.45 | 1.4 | 242 | 0.51 | 0.50 | 94 | 87 | 0.000 |
| traps_v2_fair | 0.00 | 0.00 | 6.44 | 0.00 | - | 9.8 | 0 | 0.00 | 0.57 | 25 | 0 | 0.000 |
| wearers_v2_contact | 2.45 | 0.00 | - | 2.00 | 1.42 | 13.3 | 322 | 0.74 | 0.92 | 2529 | 12 | 0.000 |
| wearers_v3_moult | 2.56 | 0.00 | - | 1.89 | 1.23 | 13.3 | 223 | 0.86 | 0.87 | 2085 | 13 | 0.000 |

Read across: the BUILDERS are territorial. They threaten only a pilot who comes to them (a raid, 0.2-1.5 hits/min,
0.45-0.5 s telegraph), and they pay for it: 1.4-4.2 kills/min plus the brood store. They are also cheap: <= 100 moving
prisms/s and ~87 spatial-index queries/s per 48-worker colony. The TRAPS threaten only a predictable pilot. The WEARERS
are the one roaming threat: the only species that hits a wanderer, with the longest telegraph (1.2-1.4 s, the rear),
evader 0 and the biggest payoff, at 20x the motion cost. Every species' structure is half or more made of the
player's own trail.

**Best species: the fortress** (with scar tissue and a defender caste). It is the emergent behaviour a player sees and
understands in seconds - cut the wall, the colony swarms the wound, the wall knits shut thicker, out of your own trail.
It is the cheapest per prism of stolen mass, it is a short step from shipped `WallAssembler`, and it gave the
richest counterplay results: steal vs ram, defend vs mend, thicken-where-cut. **Port design: `builders/PORT.md`**
(shared `BuilderColony` + `BuilderRegistry`, one `Fauna.IsStealableForMe` predicate that excludes shielded and
super-shielded mass, steal-on-pickup via `Prism.Steal(colonyName, domain, superSteal: false)` and never on a shield,
carried prisms on the mover contract, a deposit that is final at once with the settle as a GPU flight stamp,
`TryReserve` claim-before-place, per-colony lattice fields as NativeArrays + a Burst diffuse). **Collider budget: +N
heart crystals per colony (N = 24-48), 0 prisms**: a builder lays nothing new, it re-homes mass that already had
colliders. Wearers are the showpiece to port last. A worn body is moving mass (~2,100-2,500 prism writes/s in sim,
1,700-2,800 index re-buckets/s for a 300-prism body), so it needs a body cap and a per-phase notify policy, profiled
first. Trap builders belong in the gate-race modes, where a course forces a racing line for them to learn.

**Viewer:** `builders/viewer.html` (self-contained, three.js from jsDelivr). Ten scenes, each opened framed on its
structure:
- the fortress cut three times along one line (scar + caste);
- a steal raid on the fortress;
- the wasp comb growing, and the wasp comb raided;
- the trap web vs a racer, and vs a varied line;
- wearers stalking a wanderer, and fought by a hunter;
- the mixed cell.

Stolen prisms visibly travel and turn ruby, webs are orange.

**Why the direction stops here.** The last round's changes stopped moving the numbers:
- escalating recruitment: no change;
- moult cost cut: 8-28%;
- oriented bodies: within noise;
- the wasp's envelope rule: a structural fix, not a new behaviour.

The open questions left are in-editor ones the Python arena cannot answer: Mono/Burst frame cost, the look of a 300-prism
wearer, and whether the fortress' alarm swarm reads at game speed. What it would take to go further:
- a compiled-structure builder (Werfel, Petersen & Nagpal 2014's TERMES: a designer's target shape compiled into
  local rules) if designers want authored silhouettes - it trades emergence for control, so it is a design call;
- a predator that eats builders' carried prisms (the food web reaching into construction);
- an editor pass on the port.

Literature used: Grasse 1959 (stigmergy); Theraulaz & Bonabeau 1995, Science 269 and J. Theor. Biol. 177 (lattice
swarms, wasp nests); Bonabeau, Theraulaz, Deneubourg et al. 1998, Phil. Trans. R. Soc. B 353 (pillars, walls,
royal chambers); Ladley & Bullock 2005, J. Theor. Biol. 234 (logistic constraints); Bonabeau, Theraulaz & Deneubourg
1996 (response thresholds); Khuong et al. 2016, PNAS (time-decaying building pheromone); Werfel, Petersen & Nagpal
2014, Science 343 (TERMES).
