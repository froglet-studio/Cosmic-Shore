# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.

## 2026-10-02 03:42 UTC — round 1 launched (six directions, one session each)

| dir | branch | session |
|---|---|---|
| A substrate | cece/eco-substrate | session_01BgXDdDz85ZaWyoYM74U3F1 |
| B bestiary (threat fauna) | cece/eco-bestiary | session_013uHbpsRDpKXxnzXoo8uEAq |
| C emotion probe | cece/eco-emotion | session_01WBZ9zBeaWGX3UZ589UP98K |
| D builders and thieves | cece/eco-builders | session_016gLUaNKjEYTHQZkDmXH3t7 |
| E hierarchical ecology | cece/eco-hierarchy | session_019eCUxuj386nfbZFngZgRFq |
| F threat flora (optional) | cece/eco-flora | session_01PAowH8iPzv2LZEMPjGVyqz |
| game round 7 (3 big GPU swarms) | cece/swarm-fauna-game | session_01C1G9JxfU65kaHsa5TXg1tt |
## Substrate (Direction A, `substrate/`, branch `cece/eco-substrate`)

**Question:** can one agents + fields + quorum + bodies substrate express many species, and what does it cost at
10k–100k agents? **Answer: yes on both counts.** Six species are pure parameter sets of one sim: grazer
school, locust phase-changer, pack, leviathan, lurker and stampede. The Burst-shaped fused kernel costs
**1.2 ms per tick for 10k agents** and **11 ms for 100k** at k = 8 on 4 cores. All locked laws hold, audited
every step. Everything is rerunnable: `sh substrate/run_all.sh`, then `run_final.sh`.

### What the substrate is (no behaviour branches)

- **Agents.** Struct-of-arrays: pos, vel, intent, five drives (hunger, fear, curiosity, aggression,
  attachment), phase, stock, grow, role/slot.
- **A species is two `Regime`s.** Every weight is `lerp(solitary, gregarious, phase)`, so a phase change IS
  a behaviour change.
- **Context steering.** Each drive paints interest or danger over 18–26 directions. Danger soft-masks
  interest, and a soft-argmax picks the intent.
- **Fields.** 40³ per cell: food (a read of live prism volume), alarm, threat (the pilot's wake, plus a
  predator's deposit), and a per-species trail. Separable 3-tap blur plus decay.
- **Quorum.** One signal for every species:
  `(w_dens·density + w_prox·pilot proximity + w_alarm·alarm) · hunger^q`, with hysteresis and contagion
  from neighbours' phase.
  - Locusts and packs read density.
  - The lurker reads proximity.
  - The stampede reads alarm.
- **Bodies.** A group quorum (on mean hunger/fear, with hysteresis) assembles members into slots of a body
  plan. The body is itself an agent one level up: it has its own heading, steered by the school's summed
  drives.

### Results

| claim | measurement | file |
|---|---|---|
| ONE locust param set reads cute sparse+fed and terrifying dense+hungry | Direction C's frozen probe, held-out seeds 41/1000/5: **P(cute) 0.861** sparse+fed and **P(terrifying) 0.951** dense+hungry. Scorecard: sparse 0 hits, heading-to-pilot +0.70 yet near-fraction 0.001 (curious, keeps its distance); dense 46–47 hits/min, speed 1.18× the pilot, coherence 0.72 | `results/emotion.json`, `results/quorum.json` |
| the flip emerges unscripted | 200 locusts with modest food: a gregarious BAND (20–31% of the population) forms at **15.8 / 18.2 s** (two seeds), dissolves when fed, and re-forms as hunger rises | `results/quorum.json` |
| a loose swarm condenses into a creature and dissolves | leviathan: **4–5 condense→dissolve cycles per 6 min** on 3 seeds, no script; shape error while assembled **6–7% of body length** (members hold slots on a flying, turning manta) | `results/assembly.json` |
| species span threat levels | hits/min vs wanderer / evader: locust 41.3 / 0, pack 9.7 / 1.0 (the naive demo pack scores 0), lurker 4.0 / 0, grazer, leviathan and stampede 0 / 0. Telegraphs: locust 1.4 s, pack 4.9 s, lurker 5.2 s | `results/scorecard.json` |
| mass conserved, no imposed death, continuity | 13 audits, 4 min each (6 species, hunter / wanderer / scarce, both backends): mass drift **≤ 4e-16** relative; every death is 'pilot' or starvation-at-hunger-1; 0 pop-ins, 0 pop-outs, 0 teleports, 0 accel violations | `results/laws.json` |

### Scaling (bench.json, idle 4-core container)

Per-agent-step cost, whole step including Python-side world bookkeeping:

| backend | 1k k=1 | 10k k=1 | 10k k=8 | 100k k=1 | 100k k=8 |
|---|---|---|---|---|---|
| numpy (reference) | 14.9 µs | 12.7 µs | 2.1 µs | 20.3 µs | 2.75 µs |
| numba (hot stages) | 8.7 | 5.2 | 1.8 | 4.9 | 1.3 |
| fused (Burst-shaped) | 4.8 | 1.4 | 0.85 | 1.06 | 0.44 |
| **fused kernel only** (= `AgentStepJob`) | — | **0.38** | **0.12** | **0.42** | **0.11** |

- **Where the numpy time goes at 100k, k = 1:** context map 960 ms, neighbour reads 655, world 208, term
  assembly 126.
- **Fractional update cuts the two biggest stages by k.** It does not cut the hash build, drives,
  integration or the fields; the fields cost a flat ~2 ms.
- **What a port inherits:**
  - **Main thread:** ≈ 0.02–0.05 ms per tick (scheduling plus one `GraphicsBuffer` upload).
  - **Worker budget at a 10 Hz sim:**
    - 10k agents ≈ 1.2–4 ms per tick;
    - 100k agents ≈ 11–42 ms per tick, i.e. one core's worth. PC yes; on mobile cap a cell near 20–30k.
  - Design: `substrate/DESIGN_BURST.md`.

### Findings worth keeping (several are negatives)

1. **Cell MOMENTS are good enough, and exact neighbours are not worth 4–5×.**
   - The 27-cell moment read (count, centroid, mean velocity) is O(1) per agent.
   - Exact pairwise neighbours changed behaviour within noise: dense-hungry hits 46 → 43.5, sparse
     identical.
   - Cost: 29 vs 147 ms at 100k; 2.0 vs 8.4 ms at 10k (`results/nbr.json`).
2. **…but moments make separation and cohesion COLLINEAR, and the swarm collapsed.**
   - Both terms point along the neighbour-centroid axis, so they flip-flop.
   - With them, calm grazers turned 72°/s and their median nearest-neighbour distance was **2.1 u**: they
     stacked.
   - **Fix: one signed spring** toward or away from the centroid, crossing zero at a per-regime target
     crowding. Result: 20 u spacing and turn 72 → 46°/s.
   - The `crowd` target is per regime (solitary 0.5, gregarious 2.0). That is what lets crowding at food
     patches, rather than hunger alone, trigger gregarization.
3. **Fractional update is NOT free for pursuers.**
   - Re-steering 1/k smooths motion: grazer turn 88 → 38°/s at k = 16.
   - But it costs a pursuer its target. Locust frenzy hits/min: 48 at k = 1, 42.5 at k = 8, 24 at k = 16.
     Before the spring fix it was 47.5 → 18.5 at k = 8 (−61%, `results/frac_v1_presspring.log`).
   - The swarm program's "frac 8 is cheaper AND smoother" holds for agents chasing STATIC wells, not for
     ones chasing a turning pilot.
4. **Attention LOD keeps both.**
   - Re-steer every step within 150 u of a pilot or when fear/aggression > 0.6; the rotating 1/k slice
     covers everyone else.
   - Locust frenzy at k = 16: **47.5 hits/min (vs 48 at k = 1)**.
   - At 100k with one pilot the steered fraction is 0.1252 vs 0.125: the engaged minority costs nothing
     measurable.
5. **Smoothing the intent (exponential low-pass) is a weak lever.** Blend 0.5 is roughly free (grazer turn
   68 → 55°/s, locust hits 48 → 47.5); 0.7+ costs pursuers a third of their hits. Momentum interest along
   the current heading barely helps (`results/blend.json`).
6. **Cute needs a GAIT.**
   - A 30-sample search over the locust SOLITARY regime only took P(cute) from 0.14 (read: majestic 0.45)
     to 0.88.
   - Ablation on held-out seeds: dropping the 27 u/s, 2.15 Hz hop costs 0.86 → 0.45.
   - The rest of the gain is speed enough to stay engaged with the pilot, a close comfort ring (69 u),
     strong curiosity and a round published aspect (1.06).
   - The gregarious end was untouched and still reads terrifying.
   - So the substrate now has `gait_hz` / `gait_amp` / `aspect` per regime: a gait moves the body, never
     the heading.
7. **Fear-triggered assembly is a piñata.**
   - A school that condenses when frightened (the body forms ~6 s after the charge arrives) is rammed
     apart by a hunting pilot: 139 of 160 members in 8 s.
   - The shipped leviathan assembles on satiety only. Fear-assembly needs an armoured or dangerous body
     (Direction B).
8. **Same-tick pool-slot reuse reads as a teleport.** A dead agent's slot was reborn in the same tick. The
   index must not change identity inside a tick, or GPU interpolation draws a streak (`freedTick` guard).
9. **Population churn is heavy.** Locusts: ~950 births and 300–760 starvations in 4 min, every death
   dropping a crystal. That is lawful (no imposed death), but in the game it means crystal spam. A
   reproduction cost / threshold tune belongs to Direction E.
10. **Pursuers orbit their target** — the Dubins problem again (CLAUDE.md `AI_ORBIT_BREAK`).
    - Body members closing on a slot at an unbounded gain flew at 700 u/s around a body they never
      reached.
    - Fix: closing speed ≤ ½ · turn · distance, and steer by the member's desired VELOCITY: slot velocity
      plus closing speed.
    - A finite-difference slot velocity flung members at the clip when the centroid re-seated, so the
      slot velocity is rigid-body `v + ω×r`.

### Recommendation

Port the substrate to Burst as specified in `DESIGN_BURST.md`:
- one `AgentStepJob` = `kernels_nb.fused_step`;
- cell-moment neighbours with the spacing spring;
- 40³ fields;
- species as ScriptableObjects holding two Regimes + quorum weights + an optional body plan;
- **k = 4–8 with the attention LOD always on**;
- render from one instanced buffer.

It is the platform layer every other direction's species can run on.

11. **A pack's read is set by whether it STRIKES, not by how it stalks** (`results/emotion_pack.json`).
    - The shipped pack reads playful 0.88.
    - A 48-sample parameter search reaches **terrifying 0.60**: ~10 large (13 u), elongated (aspect 3.2)
      members, slow stalk. But menacing never exceeds **0.10** in the search.
    - **Menacing appears only when the quorum never fires.** 4–5 big members holding the ring and never
      striking read **menacing 0.37–0.42** as the top emotion.
    - Turn the strike back on and the run-average read splits playful 0.44 / terrifying 0.43: the strike
      phase is what reads as play.
    - Two levers tested and NOT ported: feeding the pilot's velocity forward into the ring term (+0.04
      menacing) and publishing a gaze toward the pilot (shifts the read to eerie, 0.27).
    - Design consequence: a pack is a menace→terror ARC, so a per-phase emotion read (not a run average)
      is the right measurement. Handed to Direction C/B.

Open:
- **The stampede almost never tramples** (0.3 hits/min in one run). The alarm quorum flips the herd (phase
  0.8–0.9), but a herd fleeing AWAY rarely runs through the pilot.
- **Multiple bodies per species** need a clustering step; today it is one body per species.

**Why this direction stops here.** The last three experiments did not move the recommendation:
- exact neighbours vs moments: same behaviour at 4–5× the cost;
- intent low-pass: a weak lever;
- pack parameters: expressivity, not architecture.

The architecture (cell moments + spacing spring + attention LOD + one fused agent kernel + 40³ fields) has
been stable since the spring fix.

What remains is either in-engine or another direction's question:
- only Unity can measure Burst against numba;
- multi-body clustering and armoured bodies belong to Direction B;
- reproduction cost and churn belong to Direction E;
- per-phase emotion reads belong to Direction C.
## Bestiary (Direction B, `cece/eco-bestiary`) — 2026-10-02

Eight threat species in `bestiary/species/`, each a struct-of-arrays population steered by a weighted sum of
local terms whose weights are continuous drives. No behaviour trees, no scripted sequences. Each one has
ablations (negative controls) that remove its key rule. Scored by the shared scorecard against wanderer,
evader, hunter and skimmer, plus two extra pilots (below), 3 seeds x 90 s, pilot trails on.
Files: `bestiary/scorecards.json` (with ablations), `bestiary/emotion.json`, `bestiary/bestiary.html` (2.3 MB,
`build_viewer.py`), `bestiary/README.md`. Mass conservation: max ledger drift 0.0 in every run.

### The table

| species | emotion (built for / outside probe read: hover · cruise · evade, threat 0-1) | counterplay | telegraph first strike (s) / unwarned | hits/min wander · skim | cp evader · aware | payoff kills/min | variety | agents · numpy us/agent-step |
|---|---|---|---|---|---|---|---|---|
| **pack** (encircle) | dread / terrifying · eerie · menacing, threat 0.50-0.63 | break the gap before the ring closes; change heading | **0.9** / 16% | 6.0 · 6.5 | 0.0 · 0.0 | 4.7 | 1.10 | 5.5 · 56 |
| **leech** (hitchhiker) | cute / cute · cute · cute (0.79-0.87), threat 0.05 | hard turn shakes them off; skirt the puddle | **2.0** / 14% | 49 · 24 (drains) | 0.0 · 0.11 | 2.4 | 1.76 | 63 · 17 |
| **locust** (phase change) | cute -> terrifying / sparse: cute 0.87 all viewers; after 55 s: terrifying · terrifying · cute, threat 0.03 -> 0.62 | leave before it tips; outrun the march; thin it | **0.9** / 9% | 39 · 116 | 0.0 · 0.07 | 21.3 | 1.00 | 200 · 64 |
| **stampede** (herd) | majestic / cute · cute · majestic, threat 0.16-0.25 | approach from outside alarm range; sidestep the bull | **0.7** / 33% | 10.9 · 5.3 | 0.0 · 0.10 | 3.8 | 1.06 | 71 · 44 |
| **lurker** (ambush) | eerie / cute · cute · cute, threat 0.29-0.44 | read the gape and swerve; strike first or while spent | **0.85** / 9% | 0.9 · 4.9 | 0.0 · 1.25 (noise, see below) | 6.2 | 2.0 | 15 · 104 |
| **leviathan** (assembled) | majestic / cute · majestic · majestic, threat 0.15-0.34 | keep clear; never linger at the tail; pick off loose units | **1.0** / 0% | 0.45 · 3.1 | 0.0 · 0.0 | 37.1 | 2.0 | 133 · 6 |
| **thief** (steals trail) | mischief / cute · cute · cute, threat 0.08-0.14 | turn back on laden thieves (recapture); weave | **1.5** / 22% | 39 (steals) · 89 | 0.27 · 0.76 | 5.6 | 1.46 | 17 · 32 |
| **mobber** (mobbing flock) | playful / cute · cute · cute, threat 0.00-0.08 | keep speed > 110 u/s; don't loiter at the roost | **1.15** / 0% | 6.2 · 70 (pecks) | 0.0 · 0.0 | 25.1 | 2.0 | 45 · 16 |

All eight pass all four gates (telegraph >= 0.7 s on the first strike, counterplay < 1, variety > 0,
payoff > 0). The feel profiles differ: z-scored over the six shared feel axes, the closest pair (locust vs
thief) is 1.14 apart, mean pair 3.46. On Direction C's threat axis the eight span 0.0 (mobber, sparse
locust) to 0.63 (pack), and the locust alone crosses most of it (0.03 -> 0.62) with no scripted switch.

### What the ablations say (each removes one rule; same 6 pilots x 3 seeds)

| ablation | result vs the full species |
|---|---|
| pack `chase` (chase the current position, no spread, no quorum, same stamina) | wanderer hits **0.22 vs 6.0 per min (27x fewer)**, first-strike telegraph 0.0, unwarned 86%. Geometry, not speed, is how a 95 u/s pack catches a 120 u/s pilot. |
| pack `noquorum` (spread + prediction, each hunter strikes alone) | same hit rate (6.0) but telegraph 0.4 s, unwarned 45%, aware counterplay 0.19 vs 0.0. The quorum adds no damage. It is what makes the attack READABLE and dodgeable. |
| leech `nogrip` (turning never shakes) | drains 164 vs 49 per min on the wanderer (3.4x): the shake-off is the whole counterplay. `nopounce`: wanderer hits 0. The hop is how a 12 u/s puddle reaches anything. |
| locust `solitary` (phase pinned 0) | **zero hits** from every pilot. `gregarious` (pinned 1): 53 vs 39 per min, telegraph 3.85 s. All the threat lives in the phase; the sparse swarm is genuinely harmless. |
| stampede `nobulls` | **zero hits**: the bull charge is the entire direct threat. `selfish` (flee straight away, no contagion, no herd-centre flee): the SAME hit rate (10.9) and a better telegraph (1.25 s). **Negative:** the herd-centre flee and alarm contagion added nothing measurable. They change how a stampede looks and where it tramples (env mass trampled 1266 vs less), not how often it hurts you. |
| lurker `nogape` | telegraph 0.2 s, unwarned 100%, and wanderer/skimmer hits 0 (the instant snap fires from too far and misses). The gape costs nothing; it gives the lurker its reach. `nocreep` (never moves while unwatched): wanderer hits 0. Creep is how an ambush predator finds a pilot that does not come to it. |
| leviathan `noassemble` | zero hits, payoff 93/min (a shoal is free crystals). `nogulp`: wanderer hits 0, skimmer 2.4. Assembly makes the threat; the gulp is what reaches a passing pilot. |
| thief `cold` (any trail, never tails a ship) | steals **10.9 vs 39 per min**, aware counterplay 0.55. Wanting only the warm wake (< 1.5 s) made thieves both more effective and more legible: they fly where you can see them. `bold` (never veers off): steals barely change (40), but payoff doubles (12/min). Timidity buys survival, not theft. |
| mobber `speedblind` | wanderer pecks 39 vs 6.2. The speed gate is what turns a swarm into a nuisance you outfly. `nodive`: zero hits and zero telegraph. |

### Findings

1. **Encirclement works, and its value is fairness rather than damage.** With the same stamina, the naive chase
   lands 27x fewer hits than the encircling pack. Prediction plus angular spread puts hunters ahead of your
   line. The quorum ("strike when the ring closes") adds no hits over solo strikes. It moves the telegraph
   from 0.4 to 0.9 s and lets an aware pilot escape every time (0.19 -> 0.0). Both halves are kept.
2. **One continuous drive gives the cute-to-terrifying range.** The locust is green, shy and harmless sparse,
   and the probe reads it cute at 0.87 from all three viewers. It is the same code after 55 s of breeding on
   food: the cloud tips at ~28 s (mean over runs), 82% gregarious at peak, and the probe reads terrifying (0.58 hover,
   0.40 cruise, threat 0.62). Ablating the drive removes the entire threat.
3. **A swarm breaks the shared telegraph metric.** The shared median credits every follow-up bite with the
   0.1 s since the previous one, so locust, stampede, thief and mobber all score 0.1-0.2 s. Scoring only the
   FIRST strike of each engagement (no hit in the previous 3 s) gives 0.7-1.5 s. `telegraph_first_s` is
   reported alongside the shared one, and the gate uses it.
4. **The shared evader is clairvoyant and fast,** so most species score counterplay exactly 0. Two extra
   pilots separate "fled well" from "saw everything": `evader120` (same speed as the wanderer) and `aware`
   (same speed, flees only threats within 350 u). Under `aware`, fleeing still fully counters pack, leviathan
   and mobber, mostly counters leech/locust/stampede (0.07-0.11), only partly counters thieves (0.76), and
   is no help against the lurker (1.25 over 3 seeds; 6 seeds give w 0.45/min, too rare to separate from
   noise). That is by design: a thief's counter is turning on it, and a lurker's is reading the gape. Fleeing
   is the wrong verb for both.
5. **A threat a non-engaging pilot never meets is no threat, and most first cuts were that.** Grazers, leech
   puddles, lurkers and the leviathan all scored 0 against the wanderer at first. What fixed each was a LOCAL
   reason to be where pilots go, never a director: herds follow trail scent upstream, leeches creep to
   fresh trail, lurkers creep toward your path only while you are not looking, the leviathan follows your
   lanes, thieves tail ships. Sparse encounters still leave variety saturated at 2.0 (disjoint hit
   histograms) for lurker, leviathan and mobber.
6. **The outside emotion probe is mostly a size detector on these species.** Small agents read cute almost
   regardless of behaviour (the mobber 0.77-0.99, thieves 0.61-0.75, the lurker 0.43-0.50 against an
   eerie target). It does separate the locust's two phases, the pack (threat 0.5-0.63) and the two giants
   (majestic from cruise and evade). Its threat coordinate orders the eight sensibly; its top label misses
   eerie and playful. I used it as feedback and did not tune to it. One round that did (pack: fewer, bigger,
   elongated hunters that stare at the predicted pilot) moved the pack from playful/majestic to the threat
   cluster without losing score. A per-hunter speed jitter tried in the same round cost the telegraph (0.9 -> 0.4
   s) and was reverted.
7. **Negatives kept:** herd-centre flee and contagion added no measurable threat (above). The
   `MAX_PER_HULL = 6` leech cap was needed: uncapped, a hunter pilot took 490 drains/min. Two state-machine
   bugs of one shape (`x <= 0 and x + dt > 0` is true at every frame x == 0) silently disabled the bull charge
   and the mobber's pull-up until found; transitions now latch on `was`. The lurker's feel is degenerate on the
   shared probe: its median speed is 0, so `burstiness` reads 3.5e7. That is its signature, not a bug, but
   the shared `feel` needs a floor for still species.

### Cost and the substrate (Direction A, `cece/eco-substrate`)

numpy timings are per step, single thread. They are Python-bound and only rank species. Every species is
O(neighbours) per agent with n <= 360 here; the all-pairs blocks become the substrate's spatial hash.

| species | per agent per step | on A's substrate today? | needs from A |
|---|---|---|---|
| pack | bearing push over packmates + ring slot + closure | **yes**: A's `pack` is the same design (ring ahead + quorum strike), arrived at independently | nothing |
| locust | density count + Vicsek + 1-in-4 food search | **yes**: A's `locust` regimes + quorum are this rule; its fused numba step is the port | nothing |
| leviathan | spring to a body slot + one composition controller per body | **yes**: A's `BodyPlan` (fixed slots, flat-bottom well) | a **posture clock** for the gulp |
| stampede | alarm max over neighbours + flee inheritance | mostly: alarm = A's alarm FIELD (deposit + gradient) | posture clock (bull head-down -> charge -> rest) |
| mobber | orbit spring + staggered dive | mostly: A's `w_ring`/comfort ring | posture clock (pull-up -> dive) |
| lurker | O(1) + seat search on relocation | partly | posture clock (gape), a **gaze sensor** (am I in a pilot's view cone) |
| leech | O(1) free; attached = a frame transform | no | **attach** to a moving frame (host hull) with a grip drive |
| thief | claim book + carry | no | **ownership**: claim (TryReserve) -> steal (owner switch) -> carry -> drop/recapture |

Four primitives would put the whole bestiary on A: (1) a **posture clock** (ramp -> act -> rest, the
intent channel *is* the ramp; it is the telegraph engine for five species); (2) **attach** (agent rides a
frame: a host hull or a carried prism); (3) **ownership** of prisms (claim, steal, carry, recapture; the
serpent wall `Steal` already exists in game); (4) a **gaze** sensor (the pilot's forward cone). None needs
per-agent branching beyond a clock compare.

Collider budget for a game port: zero per agent if members are GPU-instanced and pilot contact is a
`PrismSpatialIndex`-style query (the swarm round-7 shape). Exceptions: the leviathan's units are danger
prisms (in game: up to 140 body prisms with colliders while assembled), and every lifeform's heart crystal
(one always-on collider per individual; for the locust's cap of 360 that is the dominant cost, so locust
hearts should drop only on death, carried as data until then).

### Top 3 to port first

1. **Thieves.** Nothing in the game steals today, and the verb already exists: `Prism.Steal` from the
   serpent walls. A cell's thieves nest on a plant. When a ship comes within ~700 u they fall in behind it
   like gulls behind a trawler, snatch prisms from the last 1.5 s of its trail, and fly them home at half speed
   to a visible hoard. Nothing is destroyed, so the conserved-mass law holds trivially. The counterplay is
   the interesting part: fleeing barely helps (aware 0.76); turning back does, because a laden thief is slow
   and knocking it down returns the prism to you. The hoard grows into the fattest target in the cell (all
   your stolen mass in one place), which gives a natural objective for a raid. The port is cheap
   (17 agents, ~0.5 ms numpy, O(1) each), and the new platform piece is a prism claim book (`TryReserve`
   already exists) plus a carry transform.
2. **Pack hunters.** A pack of 5-7 long, staring hunters for dread. They are slower than you, and they
   predict your line and fan out ahead of it on a ring. When the ring closes (the quorum, visible as the
   pack tightening) they strike together, then back off winded. The measured value is legibility: the same
   pack without the quorum hits as often but telegraphs half as long and can't be escaped as cleanly. Its
   design converged independently with Direction A's `pack` parameter set, so it runs on the substrate with
   no new primitive. Bites map to Strike-class combat hits with an elemental steal; a winded hunter is the
   payoff window.
3. **Locust.** The program's emotion thesis in one species: shy and harmless sparse, a storm dense and
   hungry, with the probe confirming the flip (cute 0.87 -> terrifying 0.58). It is also the only species
   whose threat is created by the food web: the swarm's size is the food it found, so a cell with
   untended mass breeds its own storm. Players manage it the way the brief wants: leave before it tips, or
   thin it early while it is still cute. It needs A's substrate for scale (200-360 agents), which A has
   already built as its locust parameter set. Gate it on the heart-collider decision above.

Runner-up: the **lurker** (cheap, 16 agents; reuses crystal visuals as mimicry; the creep-while-unwatched
rule is the most distinctive mechanic here). It is the fourth port, after the posture clock lands.

### Where iteration stopped

Each species stopped once a further round moved no gate. The last rounds changed only the probe reading
(pack) or improved every axis at once (thief wake 2.0 -> 1.5 s: telegraph 1.1 -> 1.5, unwarned 42 -> 22%,
aware 1.19 -> 0.76). What would still move numbers: (a) the stampede's 33% unwarned first strikes are all
hunter-pilot engagements, where the nearest agent is a calf with no posture, not the charging bull. A
herd-wide posture would fix the metric, not the creature. (b) Lurker and leviathan meet a wanderer
< 1/min, so their variety and aware-counterplay need 6+ seeds or longer runs to mean anything.
## Emotion probe (Direction C, `cece/eco-emotion`, 2026-10-02 night)

**Question:** can "cute / playful / eerie / majestic / menacing / terrifying" be measured from motion and size
alone, and then used as a search target? **Answer: yes, with stated limits.**
- **Measuring works:** a 32-feature, literature-grounded extractor plus an interpretable two-judge probe
  recognises an emotion from a *mechanism it has never seen* 75% of the time (chance is 1/7).
- **Searching works:** one 20-parameter creature family under game laws reaches every emotion, by
  parameters alone.

The headline game finding: **today's fauna are majestic giants and cute tadpoles, and none of them
attends to the pilot.** Their only threat, the worm's attack cycle, reads menacing only to a pilot who stays
put. Nothing reads terrifying in any 8 s window. Code and how to run: `emotion/README.md`. Sources: `emotion/LITERATURE.md`.

### What was built
- **`common/affect.py`:** 32 features read from agent tracks and body size, all relative to the viewing pilot.
  - Looming: rate of view filling, and Lee's tau.
  - Gaze and pursuit (Gao's chasing and wolfpack studies), orbit, encirclement, convergence.
  - Synchrony, regularity and mimicry (the uncanny valley).
  - Bounce and wobble frequency, still-then-burst, and *sneak*: moving only while the pilot isn't looking
    (stalking).
  - Laban effort: acceleration, jerk, unpredictability.
  - Size, extent, collective mass and roundness (baby schema).
  - Tracking: whether it holds its distance to you (contingency).
- **`common/scorecard.py`:** `Probe.feel()` keeps every old key and adds `a_<feature>` and
  `emo_<emotion>`. These are plain floats, so `combine()` averages them. A failure in either new piece leaves
  the old scorecard intact.
- **Reference set:** 6 emotions × 4 generator families (24 mechanisms), plus 5 neutral families. Everything is
  under the same laws: speed cap, an acceleration cap that falls with size, positions only by integration.
  Each family gets 12 random variants against two viewers (a hovering pilot at 25 u/s and a cruising one at
  90 u/s), plus an evading viewer kept for transfer tests.
- **Probe** (`results/probe.json`): the average of a multinomial logistic model and a sub-prototype model
  (k-means centres inside each emotion, i.e. its named mechanisms).
  - Each judge is interpretable on its own; the two agree 84% of the time on held-out families.
  - `score`, `explain` and `advise` (what to change to move a creature toward a target emotion).
  - Affect coordinates: valence and arousal (Russell circumplex) plus threat (Fanselow's predatory
    imminence), so a near miss counts as a near miss.

### Agreement, held out (the numbers to quote)
LOFO means a whole generator family of every emotion is held out, so the probe must recognise a mechanism it
never saw.

| test | result |
|---|---|
| LOFO, v3, 7 classes, 5 families | **0.746** (chance 0.143); 0.713 on the six emotions; neutral recall 0.95 |
| LOFO, v1, 6 classes, 4 families | 0.724; valence sign right 0.88; threat rank ρ 0.89 |
| within known families (random CV) | 0.99 (an easy number; LOFO is the honest one) |
| transfer to an unseen evading pilot | 0.98 |
| **SEALED-1** (5th family per emotion, written before the last feature round, scored once) | **0.778**; valence sign **1.00** |
| **SEALED-2 + sealed neutral** (6th family, written before any 5-family model, scored once) | **0.911** (v1 on the same set: 0.896) |
| motion only (no size, no roundness) | 0.655; size adds about 4 points, shape about 3 |

### Iterating to a plateau (`results/iterations.json`, `results/curve.json`)
Literature groups were added one at a time and LOFO measured after each (logistic / sub-prototype):

| added group | LOFO |
|---|---|
| old scorecard feel | 0.32 / 0.35 |
| + looming | 0.54 / 0.50 |
| + scale and baby schema | 0.60 / 0.64 |
| + gaze, chase, encircle | 0.66 / 0.69 |
| + coordination and uncanny | 0.70 / 0.71 |
| + effort and rhythm | 0.72 / 0.72 |
| + attention, mass, sneak (designed from LOFO failures) | 0.67 / 0.73 |

- Regularisation is flat: λ from 0.01 to 1 gives 0.69–0.66.
- More prototypes per emotion is flat: 2–5 give 0.70–0.74.
- **More variants per family stopped helping** (8 → 12: 0.722 → 0.724).
- **More families did not stop helping:** 1 / 2 / 3 / 4 training families gave 0.49 / 0.65 / 0.72 / 0.76.

The plateau is in mechanism *coverage*, not in the features or the fit. New mechanisms carry labels I wrote
myself, so the next real gain needs **human ratings**. The viewer collects them.

### Search (`search.py`, `critter.py`)
- **The creature:** 20 parameters (count, size, aspect, speed, approach and preferred distance, orbit,
  approach-retreat, bounce, synchrony, noise, still duty cycle, formation rigidity, spread, facing, keeping
  pace, sneaking).
- **Laws, enforced by construction:** speed ≤ 160 u/s; acceleration ≤ 600·√(3/size); conserved body budget
  n·size³ ≤ 120³ (one whale *or* a swarm).
- **Method:** CMA-ES, 252 evaluations per target, against a same-budget random search.
- **Fitness:** the *geometric* mean of the two judges' probabilities, so a creature that fools one judge
  scores low.
- **Validation:** 6 fresh seeds × hover, cruise, and an evading viewer that was never in the fitness.

| target | CMA-ES val. fitness / hit | random val. fitness / hit | what CMA-ES found |
|---|---|---|---|
| cute | **0.995 / 1.00** | 0.51 / 0.67 | one radius-3 round critter, orbiting at about 60 u, bouncing at 3 Hz, approaching and retreating |
| playful | **0.69 / 0.89** | 0.35 / 0.39 | 8 radius-5 agents, fast and unpredictable |
| eerie | **0.75 / 1.00** | 0.47 / 1.00 | 120 tiny members in a semi-rigid formation, still 80% of the time, all facing you, keeping pace |
| majestic (v3) | 0.90 / 1.00 | — / 1.00 | two radius-95 bodies at 16 u/s, about 450 u away, with a 0.16 Hz rise and fall |
| menacing | 0.85 / 1.00 | 0.77 / 1.00 | two long bodies (aspect 4) circling at about 440 u, facing you, freezing 74% of the time |
| terrifying | **0.86 / 1.00** | 0.60 / 1.00 | about 106 radius-25 bodies, packed tight, charging at the 160 u/s cap |

So **one parameter family reaches all six emotions plus neutral** by parameters alone. CMA-ES clearly beats
random on confidence for cute, playful, eerie and terrifying. Majestic and menacing are close to the family's
defaults, so random finds them too.

### Today's game species (`species.py`, every constant quoted from a shipped asset; bodies declared)
Probe v3, 4 seeds × 3 viewers. The worm's segments and an assembled swarm creature are each declared as
**one body** (`agent_body_id`, see negative 9).

| species | hovering pilot | cruising pilot | evading pilot |
|---|---|---|---|
| worm colony, as encountered | majestic | majestic | majestic |
| worm attack cycle, isolated (hunt window forced on) | **menacing** 0.52 (judges agree 1.0) | majestic | majestic |
| shark, radius 30 (assumed) | menacing (judges agree 0.25) | majestic | neutral |
| shark, radius 60 | majestic | majestic | majestic |
| tadpole flock | **cute** 0.79 | cute | neutral |
| swarm whale / dragonfly, as one body | majestic (judges agree 0–0.5) | majestic | majestic |
| the same swarm read as a group | eerie | neutral | neutral |

- **The game's range today is majestic giants plus cute tadpoles.** The only threat is the worm's attack
  cycle, and only against a pilot who stays put.
- **The worm can only threaten a parked pilot.** In 5 of 6 runs it never left cruise. It grazes, hunts only
  12 s in every 26 s, and only engages within 220 u. Its 26 u/s pursuit and 70 u/s lunge can never reach a
  90 u/s ship.
- **No game species ever reads terrifying in any 8 s window** (timeline below).
- **The shark hunts prey fauna and never targets pilots.** It reads big and indifferent, so majestic.
- **Nothing in today's game *pursues* the pilot**, which is exactly what direction B adds.
- Judge agreement on the swarm bodies is low (0–0.5): they are uncertain readings, outside the reference set.

### Sibling species (`siblings.py`, run on their own `common/`; v3, 4 seeds per viewer, final branch states)
The packs' play-vs-dread analysis and the facing test (negative 7) were run on the bestiary's *earlier*
pack, which read playful; the final pack below reads terrifying to a hovering pilot.

| species | intended | hovering pilot | cruising pilot |
|---|---|---|---|
| substrate locust, dense and hungry | terrifying | **terrifying 4/4** ✓ | **terrifying 4/4** ✓ |
| substrate locust, sparse and fed | cute and shy | neutral 4/4 | neutral 4/4 |
| substrate grazer | cute school | playful 4/4 | neutral 4/4 |
| substrate pack | hunter | playful 4/4 ✗ | playful 4/4 ✗ |
| substrate leviathan | assembled creature | majestic 3/4 | neutral 3/4 |
| bestiary pack (final) | dread | **terrifying 4/4** ✓ | majestic 4/4 |
| bestiary leviathan | awe, then dread up close | **terrifying 3/4** ✓ | eerie 3/4 |
| bestiary thief | mischief | **playful 4/4** ✓ | cute 2 / neutral 2 |
| bestiary mobber | playful nuisance | cute 4/4 (valence ✓) | cute 4/4 |
| bestiary leech | cute and clingy | neutral 2 / cute 1 / playful 1 | **cute 4/4** ✓ |
| bestiary locust | cute when sparse | **cute 3/4** ✓ | neutral 4/4 |
| bestiary lurker | eerie (stillness, snap) | neutral 2 / eerie 1 / terrifying 1 | neutral 4/4 |
| bestiary stampede | majestic | neutral 2 / majestic 1 / terrifying 1 | neutral 4/4 |

- **Every species with an intended threat reads as a threat to a pilot that stops to look**, except the
  substrate pack (speed plus swirl reads as play).
- **To a cruising pilot almost everything turns neutral.** A creature the pilot outruns cannot hold an
  emotion. The same lesson as the worm: an emotion needs an encounter that lasts.
- **The lurker reads as background unless it snaps during the window.** An ambusher's whole emotion is in
  one rare event, and a 40 s average dilutes it. That limit of a time-averaged probe is recorded below.
- **The substrate pack reads as play.** Fast, small, unpredictable and swirling, without facing the pilot,
  is the play-fight signature. Animals need play signals (Bekoff's play bow) precisely because play-fighting
  and fighting share the same motor patterns. `advise(f, "menacing")` says what to change: fewer close fast
  passes (lower `tau_inv`), face the pilot (raise `gaze`), slow down.

### Emotion over time (`emotion/timeline.py`, `results/timeline.json`, `results/sib_bestiary_timeline.json`)
A 40 s average hides an arc and dilutes a single snap. The timeline reads the probe on 8 s windows every
2 s. On the reference families an 8 s window costs almost nothing: 0.978 per window over 986 windows, and the
mean of windows is right 100% of the time. (This is a check that short windows don't break the probe, not a
held-out score.)

Hovering pilot, seed 301; one letter per window: c cute, p playful, e eerie, **J majestic**, **M menacing**,
T terrifying, n neutral.

| species | windows | peak threat | mean threat |
|---|---|---|---|
| bestiary leviathan | `JJJJJJJJJeeeeeTTe` | 0.48 | 0.23 |
| bestiary stampede | `nnnnnnnnnTTTTTJnn` | **0.75** | 0.26 |
| bestiary lurker | `nnnneppcppppppTTT` | 0.48 | 0.27 |
| bestiary locust | `nnnnnnnnnnnnccccT` | 0.79 | 0.06 |
| bestiary pack | `TTTTTTTTTTTJTTTTT` | 0.80 | 0.55 |
| game worm attack cycle (as one body) | `MMMMMMMMMMMMMMMMM` | 0.65 | 0.62 |
| game worm colony | `JJJJJJJJJJJJJJJJJ` | 0.32 | 0.31 |
| game shark, radius 30 | `MMJJJJJJJJJJJMJJJ` | 0.47 | 0.32 |
| game tadpole flock | `ccccccccccccccccc` | 0.01 | 0.01 |

- **The leviathan plays exactly its stated arc** ("majestic awe → dread up close"): majestic → eerie →
  terrifying as it closes.
- **The stampede's panic is a terror spike** inside an otherwise neutral encounter.
- **The lurker's snap is visible** as terrifying windows at the end. **The game worm's attack is steady
  menace, never terror.**
- **Recommendation:** report `peak_threat` alongside the mean for any ambush or burst species.

### Negatives (kept)
1. **The old scorecard's feel stats separate the six emotions at 0.32 LOFO.** Nearly all the signal is in
   the new relational features.
2. **v1 called a radius-1.5 dot "majestic"**, and the search found it. With six forced choices, a small thing
   that ignores you has nowhere to go but majestic. v1 read **every one of 5 neutral background families as
   majestic (12/12)**.
   - Fix: a NEUTRAL class. Under v3 the dot reads neutral 6/6, and majestic is carried by size first:
     awe needs vastness (Keltner & Haidt 2003).
   - Small indifferent things (the r30 shark to an evader, the tadpoles to an evader, the swarm read as a
     group) now read neutral. The genuinely vast ones (the worm colony, the r60 shark, swarm bodies
     declared as one body) stay majestic for the right reason, size. *An affect probe needs a "nothing"
     class, or indifference reads as awe.*
3. **The first terror swarm was misclassified as cute.** Its separation push piled the agents into a ball
   milling on the pilot, a magnified cute bunch. The probe was right; the generator was wrong. It was fixed to
   attack in waves.
4. **The three features designed from the LOFO failures** (tracking, mass, sneak) helped the sub-prototype
   model (0.705 → 0.729) and **hurt the logistic model** (0.717 → 0.672). The logistic judge therefore leaves
   them out.
5. **The stop-motion "flicker" (sealed-1) read as menacing** (0.04 eerie): still, facing you, then a sudden
   synchronised move. Eerie is the weakest class overall (LOFO recall 52/120, confused with cute and
   menacing).
6. **Menacing ↔ terrifying is the commonest confusion** (25 of 120 terrifying runs read as menacing). It is
   graded by imminence, not a categorical boundary, which is why the threat axis exists.
7. **Facing the pilot alone does not make a pack menacing** (`results/sib_*_facing.json`). Re-probing every
   sibling species with a heading toward the pilot, motion unchanged:
   - bestiary pack: playful/majestic → **eerie** (3/4) / neutral;
   - substrate pack: stays **playful** 4/4 (its ~200 u/s swirl dominates);
   - slow grazers and the sparse locust: → **eerie**.

   In the probe, attention plus calm motion reads as *watchers*, and attention plus a fast swirl is still
   *play*. The wolfpack effect gives the attention; menace also needs the slow, direct, persistent approach
   that `advise` asks for. A design takeaway: to move a pack from play to dread, slow it down and make it
   approach straight, not only make it look at you.
8. **`advise` followed by hand stalls; search does not** (`emotion/retune.py`, `results/retune.json`).
   - **By hand:** I took the searched playful critter and applied the hints toward menacing one at a time
     (lower bounce, lower noise, no approach-retreat, face the pilot, slower, bigger and elongated).
     P(menacing) went only 0.05 → 0.25, and the critter ended eerie/neutral. It was an *avoider*
     (approach −0.18) that ignored the pilot's motion, and no feature nudge fixes that. Feature hints do not
     compose through a body's dynamics.
   - **By search:** CMA-ES *started at* the playful critter (σ 0.12, 120 evaluations) reached menacing on
     **0.5** of validation runs. Its largest moves were the stalking recipe: freeze 77% of the time (from 0),
     much bigger, a tighter group, a larger standoff distance, facing the pilot, keeping pace.
   - **So:** use `advise` to *explain* a reading, and `retune.py` to *change* a species.
9. **The probe could not tell a jointed BODY from a lockstep GROUP** (`results/worm_tuning.json`).
   - The worm's 8 follow-the-leader segments measured as 8 agents in perfect synchrony, so they read
     *eerie*, and no shipped dial changed that. A 2×3×2 sweep of lunge speed 70 / 140 / 220, pursuit
     ×1.45 / ×3 and telegraph 1.2 / 0.5 s moved peak threat only 0.31 → 0.55.
   - Presented as **one** body (radius 95, aspect 8), the *unchanged* worm reads **menacing**: peak threat
     0.82, mean 0.61. With a 220 u/s lunge and a 0.5 s telegraph it reads terrifying in 21 of 51 windows.
   - **Fix:** `common/affect.py` takes `body_id` (published as `agent_body_id`) and merges a body's parts
     into one agent before any feature. Every species built from parts must declare its bodies, or its
     emotion is wrong.
   - The reference set itself declares none. The assembled-giant families (MajAssembly, the swarm whale)
     were trained as groups. Declaring them would change the probe; that is not done, and is recorded here.
10. **I mislabelled my own timeline legend.** The first version abbreviated both majestic and menacing as
    `m`; the letters were recomputed (J / M), and the earlier bestiary rows were confirmed majestic.
11. **A headless viewer test caught a load-order bug** (the panel read `RUNS` before it existed) and a
   camera that made encounters invisible at cell scale. Both fixed: initialisation on `load`, and a
   follow-pilot camera.

### Honest limits (what motion cannot carry)
- **Our labels are not human ratings.** No study maps motion features to these six *evoked* emotions; the
  affect-from-motion literature mostly labels the mover's own emotion (happy, sad, angry, afraid). Every
  agreement number above measures *consistency with our literature-derived archetypes*. The viewer's blind
  rating panel plus `ratings.py` is the step that turns them into evidence (about 5+ raters per run).
- **Baby schema is mostly faces and proportions:** big eyes, high forehead, head-to-body ratio. The probe
  sees only size and an authored aspect ratio. Cute *art* will do more than cute *motion*.
- **Looming drives arousal more than fear** (PMC11126809). The probe needs attention or pursuit on top of
  looming to call terror rather than excitement. Without sound, a fast approach is as easily thrilling as
  frightening.
- **Play and attack share motion.** Separating them needs signals: a sound, a colour, a posture, a
  telegraph.
- **Too-perfect motion is eerie only relative to an expectation of life.** A rigid lattice of prisms may read
  as machinery rather than as uncanny. The uncanny needs an almost-living look that only art can give.
- **The probe averages over 30–40 s.** An ambusher whose whole emotion is one rare snap (the bestiary
  lurker) is diluted into background. `timeline.py` now gives a per-window readout and a peak statistic; the windows are still 8 s long, so
  an instant shorter than that is still averaged.
- **The probe reads 30–40 s encounters with one pilot.** It does not see mood built over a match, sound
  (FMOD stingers, a roar, silence before a strike), lighting, the camera or the stakes (whether it can hurt
  you). Danger prisms that burn petals change "menacing" more than any motion could.
- The game species are models from shipped numbers, not captures from Unity; several constants are
  ASSUMED and marked in `species.py`.

**Recommended next steps:**
1. Run a 5-rater blind pass in `results/emotion_viewer.html` and run `ratings.py`.
2. Give the bestiary's packs gaze-on-pilot and slower, more direct stalking, then re-probe them.
3. Have direction B score every species with `feel["emo_*"]` from the shared scorecard, so threat design
   is measured against intent.
