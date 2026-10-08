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
## 2026-10-02 14:35 UTC - round 1 harvested; round 2 launched

**All six directions reached a stopping point, and each one said why** (sections below).

| dir | answer in one line | port pick |
|---|---|---|
| A substrate | one agents + fields + quorum + bodies sim expresses 6 species as parameter sets; 1.2 ms / 10k agents, 11 ms / 100k (k = 8) | port to Burst per `substrate/DESIGN_BURST.md` as the platform layer |
| B bestiary | 8 species scored on telegraph / counterplay / variety / payoff | thieves, pack hunters, locusts (lurker next) |
| C emotion | a motion+size probe, LOFO 0.75, sealed 0.78 / 0.91; per-window timeline | needs a 5-rater blind pass (`emotion/results/emotion_viewer.html` + `ratings.py`) to become evidence |
| D builders | stigmergic builders made of stolen trail; mass audit 0.000 everywhere | the fortress (`builders/PORT.md`); wearers last |
| E hierarchy | escalator-boxcar cohorts: exact mass, no pop-in, macro-then-expand matches micro; 8 h cycles, no extinction | regions in `Cell`, stomach replaces the starvation clock, replicate macro state |
| F threat flora | snap trap R 0.97, physarum R 0.94-0.98, spores R 1.00 | snap trap, then physarum (share a grove) |

**What no direction has measured yet: a whole cell.** Round 2 asks it as one question:

| dir | branch | session |
|---|---|---|
| G living cell (all species together under E's LOD; plus multi-domain swarm dynamics) | cece/eco-living-cell | session_01C7xRo1y3JTtgh2dcm852hQ |
| game round 8 (swarm members hittable at range, predatable, counted in LiveVolume; multi-domain swarms) | cece/swarm-fauna-game | session_01J36XaFE3aw7WBwNzY7zubF |

**Needs a human, not a session:** the emotion probe's numbers are agreement with literature archetypes,
not human ratings. A 5-rater blind pass is the one step that turns them into evidence.

## 2026-10-02 ~16:00 UTC - the lead's verdict on the emotion viewer

The lead flew many of the emotion runs in the rebuilt 3D viewer. In their words:

> "I think in general you are on the right track. I didn't find any that truly evoked a strong emotion,
> probably due to the limitations of the test, but i think the test did what it needed to do which was allow
> me to confirm that all of this seems to be sensible thinking that could evoke the intended emotions once
> coupled with the player's fear of loosing hard earned crystals when a fauna's danger prisms collide with
> them and permanently reduce their vessel's elements. I can imagine being surrounded by them and having them
> all dive in at once will be scary once the stakes are felt."

What this settles and what it changes:
- **Motion design is validated as sensible, not as sufficient.** The probe and the archetypes describe a
  creature that CAN be frightening. The fear itself comes from **stakes**, which no recording carries. This
  matches Direction C's own honest limit: danger prisms that burn petals change "menacing" more than any
  motion could.
- **The stake already exists in the game.** `Docs/ELEMENTAL_ECONOMY.md`: an opposing-domain danger prism
  BURNS the pilot's petals permanently. It is the economy's only sink, and fauna spawn in the cell's
  controlling colour, so a creature's danger rods burn the pilots who do not hold that cell. So threat fauna
  do not need a new punishment. They need to deliver the existing one through readable motion.
- **The image the lead named is the design target for the scariest tier:** surrounded, then everything dives
  in at once. That is the converge-wave shape: the emotion archetype `TerSwarm`, the bestiary pack's ring and
  strike, and the locust's dense phase. Its telegraph is the ring closing; its counterplay is breaking the gap
  before it closes.
- **Every threat scorecard should now price STAKES:**
  - petals burned per encounter, with the scripted pilots;
  - how many of those burns were telegraphed;
  - what a skilled pilot loses versus a careless one.

  A threat that burns nothing is decoration, and one that burns without a telegraph is unfair. This turns
  "counterplay" into the currency the player actually feels.
- **The flyable sim (Direction H) should carry the stake:** a petal/element HUD, danger-prism burns on
  contact, crystals to recollect. A player can only judge fear when something they earned is at risk. If H
  lands without this, it is the first follow-up.

## 2026-10-02 ~22:30 UTC - round 2 harvested (G living cell, H flight)

Both round-2 directions merged into `cece/gifted-curie-x2cpd0`; full write-ups are the "Flyable ecology" and
"Living cell" sections below.

| dir | answer in one line | what it changes |
|---|---|---|
| H flight | every species flies in the browser on a JS port that passes a statistical fidelity gate against the Python (all ten species at dt 0.1 s; like-for-like at 30 Hz), whole cell ~1,400 agents at ~7 ms/step, mass ledger residual exactly 0 | the lab gains a flyable page (`flight/index.html`); judging moves from recordings to piloting. No stakes yet: hits flash and toast but cost nothing |
| G living cell | the round-1 picks survive one conserved cell together, but only after niche partition + hunger-gated predators; the naive sum was a siege (1.4% quiet, 35 hits/min). Recommended cell: 0 pop-ins at LOD 520/760/680, audit <= 1.1e-5 vol, every planted failure fires its control | thieves are the one structural failure left (the opening transient); dials stopped moving the player numbers after R6 |
| G part 2 (swarm domains) | today's rule (c) "new members wear the controlling colour" hands a nucleus-less cell to the first leader (share 0.41 -> 0.97); rule (a) "the colour of the mass that funded the egg" keeps three colours alive with a mild shelter (comeback index 0.80) | recommendation for `cece/swarm-fauna-game` (`SWARM_FAUNA.md` §16): adopt (a), merge DomainSlots under ~10%, cap churn below ~20%/min |

**Next:** stakes in the flyable sim (the lead's gate (e), section above): a petal HUD, opposing-domain danger prisms
that burn petals permanently, scored as petals burned per encounter, telegraphed share, skilled vs careless pilot.

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

**Viewer:** `builders/viewer.html` (self-contained, three.js from jsDelivr). Nine scenes, the structure ones opened framed on their
structures:
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
## Hierarchical ecology

*Direction E. Code: `Tools/Ecology/hierarchy/`. Branch `cece/eco-hierarchy`.*

A cell runs fauna at two levels. Far from every pilot they are **macro cohorts**: integer counts per region,
advanced once a second. Near a pilot a region **expands** into individual agents, sampled from that state, and
**collapses** back into it when the pilot has gone and nobody can see the result. **Flora is never LOD'd.** It
is one voxel field both levels graze, so the plants a pilot flies past are the ones the far herds ate.

### The answer, in one paragraph
The macro level is a **cohort model**, the Escalator Boxcar Train from physiologically structured population
ecology, and not a grid of stomach bins. Per region and species it keeps up to 10 cohorts. Each cohort holds:
- a count per element (int64);
- **Σstomach** and **Σstomach²**, which are additive moments, so a merge is an addition and mass is exact;
- the cohort's real **min and max stomach**.

Every count moves by a binomial, hypergeometric or multinomial draw: births from the part of the cohort above
`e_birth`, starvation from the part below 0, predation by a Holling II rate, migration by per-neighbour hops.
The rates are **fitted from the agent level, not chosen**: predation, migration, the grazing occupancy field
and the sprint cost. Three gates prove the result, and each one **fails on planted bugs**:
- conservation to the unit;
- no popping where a pilot can see;
- "macro for T, then expand" matches "micro for T".

On the enriched parameters the whole 1200-u cell (~50k individuals) **cycles for 8 hours with no extinction**.

### Architecture (what the code does)
- **Closed mass ledger.** Mass lives in:
  - flora F;
  - the skeleton K (prism mass left behind when a creature starves);
  - soil nutrient N;
  - the bodies and stomachs of macro cohorts, plus an inbox of migrants bound for a hot region;
  - the bodies and stomachs of agents.

  The flows:

  | Flow | From | To |
  |---|---|---|
  | metabolism | stomach | N |
  | flora growth | N | F (logistic × nutrient limit) |
  | grazing | F / K | stomach |
  | predation | the prey's body and stomach | the predator's stomach |
  | birth | the parent's stomach | the offspring's body and stomach |
  | starvation | body | K |

  There is **no timer and no lifespan anywhere**; the only sinks are starvation and predation, the locked law.
  The ledger is checked to the unit every step (relative drift ~1e-14 over 8 h).
- **Regions.** 912 cubes of 200 u, each holding 64 flora voxels of 50 u.
- **Macro rates fitted from micro** (`calibrate.py`):
  - **Predation:** Holling II, `a = 5.0e-5`, handling `h = 357 s`. Fitted on steady state with the first 100 s
    burned in; 149k predator-seconds, 548 kills.
  - **Migration:** hop rates per face-neighbour, grazer 0.0016–0.010/s and predator 0.003–0.014/s depending on
    hunger (sated, foraging, hungry).
  - **Occupancy.** Grazing at region scale is not uniform over the voxels. Agents bunch on food, so the macro
    level relaxes a per-voxel occupancy field toward `food^0.5`. The fit gave θ = 0.5, with 4.3% grazing error
    against 18.6% for uniform occupancy.
  - **Sprint cost:** `κ × P(encounter)`, with κ measured from agent sprint time: grazers 0.36, hunters 0.019.
- **LOD** (`sim.py`):
  - A region turns hot within `expand_radius` (280) of a pilot, or within 400 ahead inside the forward cone.
  - It collapses only past `collapse_radius` (450, which gives hysteresis), and only when **neither the agent's
    simulated position nor its drawn position** is visible to any pilot.
  - An agent budget (4.5k) scales the radii by the cube root of budget/agents.
- **Expansion is sampled, not invented.**
  - A cohort expands into exactly N agents with stomachs drawn from its Normal, clipped to its [lo, hi] and then
    shifted so their sum is exact.
  - Predators are placed uniformly. Grazers are rejection-sampled outside the predators' flee radius, because
    the macro state is a steady state and a fresh uniform scatter is not: it massacred 114 grazers in 30 s
    where the steady state kills 11.
  - Agents **emerge** from the region's impostor representatives rather than appearing.
  - Migrants arriving from a cold region spawn at the source region's representative.
- **Impostors.** 6 drawn representatives per (region, species), sized `r_body·(n/k)^(1/3)` so the cloud carries
  the region's volume. Expansion lerps agents out of them, and collapse is gated off-screen.

### Gates (each fails on planted bugs)
| gate | clean | planted bugs (all fail) |
|---|---|---|
| **conservation** (`test_conservation`) | ledger drift 5.8e-14 rel, LOD mass mismatch 2e-10, count mismatch 0, 182k expands / 185k absorbs / 4.9k arrivals | `absorb_drop_stomach` (drift 3.6e-3), `birth_free_body` (1.7e-3), `expand_lose_pool` (1.6e-2): each run 300 s and exercised |
| **continuity** (`test_continuity`, 2 pilots, final code) | **0** pop-in / 0 pop-out / 0 teleport over 572k pilot-seen agent-ticks (97k expands, 98k absorbs); 1.38M in an earlier wider-LOD run | `absorb_ignores_visibility` 74 pop-outs, `expand_no_emerge` 9.1k pop-ins + 6.3k teleports |
| **consistency** (`test_consistency`, 4 seeds, T = 300 s, 600-u world, **calibrated regime** flora cap 120) | herb count 0.2%, mean stomach 0.5%, phase TV 0.040; pred count 2.8%; kills 8.8%; flora 0.4%. **Switch arm** (pilot arrives half way): 0.3% / 0.5% / 1.5% | `macro_graze_x1.5` (herb count 63%), `expand_mean_field` (stomach SD 78%), `macro_attack_x2` (kills 49%) |

The consistency gate runs three arms per seed: all-micro for T; all-macro for T, then expanded; and a pilot
sweeping through. It compares, per species: headcount, mean and SD of stomach, the hunger-phase histogram
(sated / foraging / hungry), regional flora, and kills over the window. Tolerances are 10% count, 10% mean,
25% SD, 0.08 phase total variation, 5% flora and 30% kills.

**Consistency in the enriched regime (flora cap 240) is NOT established: an honest gap.**
(`--regime enriched`, `results/gate_consistency_enriched.json`.)

| Measure | Result |
|---|---|
| switch arm (pilot arrives half way) | passes everywhere: herbivore count 1.0%, mean stomach 1.5% |
| herbivore count, all predator measures, kills, flora | pass, with predators at 2.2% and kills at 1.3% |
| herbivore mean stomach | **fails at 16%** |
| herbivore phase total variation | **fails at 0.145** |

**Why.** With food abundant, the whole herd fills toward `e_birth` together and breeds in **one
synchronised burst** inside the window. In micro, births run 1 → 11 → 58 → 224 → 721 → 1758 → 3101 at
t = 120 … 300. The macro level starts earlier and rises slower: 21 → … → 2240. At t = 300 the snapshot
therefore lands mid-burst, where a 15 s timing error is a 16% stomach error.

**What was measured:**
- The macro mean lags micro by ~1% fill rate (−0.03 → −0.19 by 120 s).
- Macro's total stomach spread contracts less: 1.53 → 1.39, against micro's 1.10, which is exactly the
  satiation contraction ×0.77.
- Region-to-region spread is *not* the cause: macro is tighter there (sd 0.16–0.24 against micro's 0.21–0.28).

**Tried and failed to recover the sharp burst:**

| Change | Result |
|---|---|
| occupancy θ 0–1 | θ = 1 matches the mean, but births leak 90 at 120 s |
| stomach diffusion 0–0.0009 | still 14 early births |
| merge tolerance 0.06 → 0.02, cohorts 10 → 16 | no change |
| bounded cohorts | no change |

**Read it as:** a near-deterministic threshold crossing is the worst case for any moment-closed population
model. The Normal-within-cohort closure leaks an upper tail that the real, contracted distribution does not
have. The cure, not yet tried, is to track a third moment (skew) or to represent the top cohort's upper edge
explicitly. In the game the exposure is small: the burst is a regional event far from pilots, and the
long-run statistics (cycle amplitude, period, persistence) are what a pilot sees.

### Living dynamics
- **8 h at the first calibration (flora cap 120): a stable equilibrium, recorded as a negative.**
  - After a transient the cell settles at ~34k grazers / 12k predators: cv 0.25 / 0.24, regional sync 0.73,
    no detectable period.
  - Alive, but it does not breathe.
- **Enrichment turns it into cycles** (`sweep_d`, then 8 h at flora cap 240; `results/longrun_cycles8h_*.png`, reproduced on the final bounded-cohort code as `longrun_cycles8h_final_*` with grazers 18.8k–163k, predators 3.7k–34k, cv 0.68 / 0.42 and ledger drift 5.3e-15).
  This is the paradox of enrichment, in the cell.
  - Grazers 19k–164k, predators 3.8k–34k, cv 0.69 / 0.43.
  - **No extinction**, either cell-wide or in any region (0 local extinctions).
  - A ~2.3 h cycle with predators lagging grazers.
  - Flora and soil nutrient oscillate in counter-phase: the grazed-down cell regrows, which is emergent
    succession and seasons with no clock in the model.
  - About 2,600 region hops/s of migration; ledger drift 6.7e-15 over 8 h.
- **Cost of that liveliness: the cycle is cell-wide** (regional sync 0.95), not a patchwork. In the game that
  means a whole cell goes boom-and-bust together. It is a design choice: smaller flora_r, or weaker migration,
  desynchronises regions at the cost of amplitude. That trade-off was not swept.
- **Sweeps** (macro-only, 1 h, `results/sweep_*.json`):
  - Predators crash at metabolism ≥ 0.06, and are viable at ≤ 0.025 with h_half 90.
  - Cap 240 with predator metabolism 0.03 nearly kills predators (min 43). The window is narrow; keep 0.02.

### Cost (`cost/measure.py`, `results/cost.json`)
50k+ individuals in a 1200-u cell; pilots wander at 120–210 u/s.

| pilots | expanded agents mean / p95 | compiled micro per 60 fps frame (10 Hz) | macro per frame (1 Hz) |
|---|---|---|---|
| 1 | 1.9k / 2.2k | 0.15 ms | 0.036 ms |
| 2 | 3.3k / 4.0k | 0.31 ms | 0.036 ms |
| 4 | 5.4k / 6.4k | 0.58 ms | 0.036 ms |

- **Micro** is a compiled C kernel standing in for Burst: ~390–520 ns per agent per tick, off the main thread.
- **Macro** is 2.1 ms per 1 Hz step (117 ns per cohort slot over 912 × 10 × 2 slots plus 58k voxels). That is
  noise next to the micro.
- The Python reference is 3.6–8.6 ms per micro tick and ~31 ms per macro step.
- **The budget is the lever:** without it, 4 pilots expanded up to 18k agents (`cost_no_budget.json`).

### Negatives (what failed and by how much)
1. **Stomach-bin (Eulerian) macro level** (`macro_bins.py`). Upwind advection of a stomach histogram smears it
   by numerical diffusion. The hunger-phase TV against micro was 0.37 with 8 bins and still 0.27 with 32. The
   cohort rewrite fixed this outright, because a cohort's moments are transported exactly.
2. **Herbivores could never breed.** The satiation cap sat below `e_birth`. Phases are now defined relative to
   `e_birth`.
3. **Grazing credit leak** of ~0.5 volume per step. Gain was credited after predation had removed the grazers;
   it is now credited inside the grazing step.
4. **Uniform occupancy:** 18.6% grazing error. A fixed exponent was regime-dependent, so occupancy became a
   relaxing field.
5. **Poisson sprint cost** overestimated how long hunters chase by ~50×. κ is now measured.
6. **Predation fitted from probes, and from the start-up transient,** was biased. Steady-state burn-in fixed it.
7. **Fresh uniform expansion is a massacre:** 114 vs 11 kills in 30 s. Grazers are now rejection-sampled.
8. **Absorb that checks only the sim position** popped one agent out. Emerging agents are drawn elsewhere than
   they are simulated; absorb now checks both.
9. **Normal tails without bounds** (a partial fix; see the enriched-regime gap above). In the enriched regime macro births started ~90–150 s early: the Normal tail
   extends past the cohort's real maximum stomach, so a cohort that cannot yet breed "breeds". The fix is
   **bounded cohorts**: two floats per cohort, moved by the same drift as the mean. Births and starvation are
   then drawn only from the part of the Normal inside [lo, hi]. `macro_unbounded_cohorts` reproduces the old
   behaviour.
10. **Negative controls that did not bite.**
    - `birth_free_body` **passed** when its run lasted only 120 s, because no agent had bred yet: a vacuous
      control. Every bug run now uses the clean run's 300 s horizon and must be exercised.
    - `expand_flat_energy` was harmless, and became `expand_mean_field`.
    - `macro_no_sprint_cost` turned harmless once the sprint cost was measured as tiny. It became
      `macro_attack_x2`, plus a kills (flow) metric, because stocks over 300 s barely move under a predation bug
      while flows do.
11. **The first LOD radii** expanded 6.3k–18k agents with 1–4 pilots.

### Recommended game architecture
This plugs into what the Cell already owns (`Docs/ECOSYSTEM.md` §0–§15, `Tools/ecosim/`):
1. **Regions live inside `Cell`.**
   - Partition the membrane volume into ~200-u regions; this is the macro grid.
   - Prism mass stays real everywhere: flora grows as prisms in every region, hot or not, so the shared flora
     field is the prism field itself, and `PrismSpatialIndex` is the voxel lookup.
   - Only **fauna** are LOD'd.
2. **The volume spine reads both levels.**
   - `Cell.LiveVolume` (§13) is already per-domain prism volume, and it stays exact because flora and skeleton
     are real prisms.
   - Fauna body volume adds the macro ledger: count × body per cohort, which is exact and needs no agents.
   - The phase ladder therefore sees the same number whether or not anyone is near.
3. **One stomach replaces the starvation clock.**
   - `Fauna.starvationSeconds` measures time since the last feed. It is a timer standing in for an energy
     budget.
   - Give each creature a stomach that metabolism drains and feeding fills, and starve it at zero.
   - This is the same prey-linked starvation (§6 option C), but it is **conserved**: what is burned goes to
     nutrient, and the body goes to a skeleton.
   - It is also what lets a cohort summarise many creatures exactly.
4. **Spawn profiles become initial conditions and caps, not producers.**
   - `SpawnProfileSO.FaunaPopulationScale` / `MaxLivePopulation` / `Cell.ResolveFaunaCap` seed the macro counts
     and remain a hard cap on births. That is production gating, which is allowed.
   - The seeder survives only as extinction recovery.
   - Births come from the stomach tail, at the birth threshold the species already authors.
   - `CurrentFaunaSpawnPeriod` becomes the macro tick's breeding cadence, which is a biome property as §23.9
     already says.
5. **Jobs.**
   - **Macro:** a 1 Hz Burst job over regions × cohorts. Pure integer counts and moments, deterministic per seed
     and cheap enough to run on a server.
   - **Micro:** a 10 Hz Burst job over expanded agents, interpolated per frame.
   - **Impostors:** GPU-instanced, 6 per region and species.
6. **Networking.**
   - Replicate the **macro state** (counts per region, cohort, element; a few KB per second).
   - Each peer expands locally around its own pilots from the shared state.
   - Only the hot regions near *someone's* pilot need agent-level authority. This sidesteps today's per-peer
     fauna divergence (`CellNetworkSync`) for everything off-screen.
7. **The dials that shape the experience**, all fitted or swept here:

   | Dial | Effect |
   |---|---|
   | flora cap / growth rate | equilibrium vs cycles (paradox of enrichment) |
   | predator metabolism | predator persistence; narrow window |
   | migration hop rate | cell-wide vs patchy cycles |
   | `agent_budget` | LOD radii and cost |

### Open
- **Enriched-regime consistency** (above): close the synchronised-burst timing with a skew moment per cohort,
  then promote `--regime enriched` to a gate.
- The cycle is cell-synchronous. Desynchronising patches (cell-scale metapopulation) was not swept.
- Consistency is proven at T = 300 s on a 600-u world. Longer horizons rely on the 8 h macro run plus
  continuity, not on a direct micro-vs-macro 8 h comparison, which is too slow in Python.
- The C kernels are a proxy for Burst; the real numbers need a profiler in the editor.

## Threat flora (Direction F, branch `cece/eco-flora`)

**Question.** Can plants be replayable threats you fly through, not scenery and food? Five species, each a
continuous automaton or agent-field model with a body of prisms, scored against scripted pilots, searched until
the scorecard stopped moving, and costed for Burst / compute.

**Where it lives.** `Tools/Ecology/flora/`:
- `harness.py`: the flora arena on top of `common/` (whose API is unchanged; the only edit is a UTF-8 charset in
  `common/viewer.py`). It adds pilots, a plant body, plant metrics, the R / R_hard scores and the mass audit.
- Five species: `snaptrap.py`, `spores.py`, `physarum.py`, `coral.py`, `walker.py`.
- `search.py`: a (1+1) local search. Every candidate is kept in `results/search_<species>.jsonl`.
- Experiments: `elements.py`, `reroute.py` and `cost.py`.
- Viewers:
  - `record.py` builds playback in the shared viewer (`out/flora.html`).
  - `sandbox.py` builds `flora/sandbox.html` (committed, 30 KB, three.js from a CDN), a FLYABLE sandbox. It holds JS ports of the five rules with the
    tuned parameters. Controls: mouse/WASD, Shift to boost, Space for a light touch; your trail is food for the
    plants.

### The harness: what a stationary threat has to be scored against (and what it cost to learn)

Every one of these was measured; most are negatives on the harness, not on a plant.

1. **A flora threat is a PLACE.** Round 0 put the plants in the arena's 250–850 u garden shell. A wandering pilot
   met NO plant: its mean free path between clumps was about 8,000 u (0 hits/min, 1.8% lane coverage). Plants and
   pilots now share a 450 u GROVE.
2. **Pilots must want something.** Round 3 added real crystals that a pilot diverts to inside 300 u, as players
   do. A species may also publish LURES, which look the same as crystals unless a reader is inside the lure's
   tell range.
3. **Trails are the plants' continuous food.** Every pilot lays a 6-volume prism every 24 u. In game a vessel IS
   a mass source, and the trail volume is booked as created mass, so the audit stays exact. This is the one food
   source that tracks the player, so every species that eats it adapts to your routes through MASS, not through a
   special rule.
4. **The counter-pilot (the READER) took three fixes before `access` measured the plant rather than the pilot.**
   The reader reads only what a plant telegraphs.
   - It SUMMED penalties over every visible tooth. A trap cluster vetoed every direction, so it collected 10
     crystals to the blind wanderer's 44.
   - It picked from 26 fixed directions about 40° apart, so it could never home on a 12 u crystal and orbited
     them. It now includes the exact goal direction.
   - It had no give-up rule. It now drops a goal it can't reach within 15 s and a bait it can't reach within 10 s.
5. **The telegraph must COVER the strike volume.** The snap trap's glow sphere (r = 0.7 L) was smaller than its
   lip (L·tan 53°). Readers were snapped 9 times in 2 minutes by mouths that looked smaller than they were. This is
   a design rule for the port, not a harness quirk.
6. **Hits cost time, as they do in game.** A burn applies the fleet's danger slow: strength 0.5 × 3, duration
   1 s × 3, modelled as a linear recovery floored at 0.1. The arena may slow a vessel; a vessel may not
   (ELEMENTAL_ECONOMY §9).
7. **Payoff is an EXCHANGE RATE, not a harvest rate.** A band of [1, 6] crystals/min punished every colony that
   fed well: harvest rate is set by how many plants the food grows, which is a cell population budget. Payoff is
   now crystals per burn taken, in [0.5, 3]. Every species first paid about 20 free crystals/min because the heart
   sat outside its own threat. Moving the crystal INTO the threat is the fix that worked every time:
   - the trap's jaws;
   - the pod's shell;
   - the thicket's guard (thorns half-extend while it trembles).
8. **Variety is measured on TWINS.** Each twin runs the same world with the pilot nudged 5 u at the start, and
   the score is how differently the PLANTS respond. Physarum with no vessel wake scored exactly 0: a plant that
   ignores you is the same encounter every time. Making plain plant prisms rammable (a pilot flying through plain
   prisms breaks them, as in game) is what gives every species twin variety.
9. **The scorecard saturates, so it got a stretch bar.** The snap trap reached R 0.97 in two search steps.
   R_hard tightens every axis: threat 1.5–4/min, reader/wanderer ≤ 0.15, worst-decile telegraph ≥ 1 s, exchange
   rate 0.75–2, twin variety ≥ 0.5, access ≥ 0.9, coverage 0.15–0.3, route-bias growth ≥ 1.75×. It also adds a
   COLLIDER BUDGET of ≤ 2,500 live plant prisms per grove. Later searches maximise R + R_hard.
10. **Noise.** Three seeds × 2 minutes gives about ±0.04 on R. A search that keeps proposing changes inside that
    band has plateaued. The snap trap's last nine candidates sat at 0.91–0.98.

### Results

**Best of each search** (3 seeds x 2 min x 4 pilots, plus twins; obj = R + R_hard). `access` is reader goals/min ÷
wanderer goals/min, so a lure species can exceed 1: the reader takes crystals the wanderer is bitten going for.

| species | start R | best R | best R_hard | evals | hits/min (wander) | reader/wander | telegraph p10 s | crystals/burn | twin variety | access | lane cov | route-bias x | prisms end |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| snaptrap | 0.91 | 0.99 | 0.91 | 32 | 5.83 | 0.09 | 0.67 | 1.08 | 0.95 | 0.76 | 0.26 | 1.83 | 1718 |
| spores | 0.92 | 0.97 | 0.91 | 21 | 1.67 | 0.00 | 2.93 | 0.96 | 0.56 | 0.77 | 0.37 | 1.31 | 1440 |
| physarum | 0.71 | 0.98 | 0.91 | 26 | 1.33 | 0.38 | 3.60 | 2.14 | 0.48 | 0.71 | 0.35 | 2.19 | 1746 |
| coral | 0.33 | 0.64 | 0.48 | 17 | 3.83 | 0.09 | 1.50 | 0.22 | 0.04 | 0.47 | 0.10 | 27.93 | 5081 |
| walker | 0.54 | 0.96 | 0.86 | 34 | 9.17 | 0.00 | 1.88 | 2.86 | 0.80 | 4.00 | 0.34 | 1.67 | 1988 |

Two numbers in that table are artefacts, and say so here so nobody ports them:
- Coral's route-bias of 27.9x is a ratio over a near-zero baseline.
- The snap trap's telegraph p10 of 0.67 s is one noisy draw. Rescoring the same parameters for the element anchor
  gave 0.86 s.

**Stopping.** Every search ran to a plateau. The snap trap's last nine candidates sat inside 0.91–0.98, which is
the ±0.04 noise band. Physarum and the walker each ran a stage 2 on obj = R + R_hard and kept nothing new after the
stage-1 best. Coral stopped climbing at R 0.64 after 17 evals; the model, not the search, is the ceiling (below).

**The four elements** (`elements.py`; the anchor is the searched best, rescored):

| species | element | R | R_hard | hits/min | telegraph p10 s | reader/wander | crystals/burn | prisms end |
|---|---|---|---|---|---|---|---|---|
| snaptrap | anchor | 0.97 | 0.92 | 4.17 | 0.86 | 0.16 | 1.14 | 1733 |
| snaptrap | time | 0.92 | 0.87 | 3.00 | 0.71 | 0.22 | 1.09 | 1768 |
| snaptrap | mass | 0.94 | 0.87 | 4.00 | 0.45 | 0.08 | 1.16 | 1440 |
| snaptrap | space | 0.94 | 0.86 | 6.00 | 1.05 | 0.00 | 1.09 | 1672 |
| snaptrap | charge | 0.97 | 0.93 | 4.00 | 0.84 | 0.17 | 1.16 | 1743 |
| spores | anchor | 0.97 | 0.91 | 1.67 | 2.93 | 0.00 | 0.96 | 1440 |
| spores | time | 0.96 | 0.90 | 1.67 | 5.00 | 0.00 | 0.97 | 1949 |
| spores | mass | 0.86 | 0.73 | 0.50 | 5.00 | 0.00 | 1.03 | 1220 |
| spores | space | 1.00 | 0.93 | 3.83 | 4.18 | 0.00 | 1.05 | 1716 |
| spores | charge | 0.92 | 0.84 | 1.83 | 3.20 | 0.00 | 1.24 | 1422 |
| physarum | anchor | 0.98 | 0.91 | 1.33 | 3.60 | 0.38 | 2.14 | 1746 |
| physarum | time | 0.95 | 0.89 | 2.17 | 0.92 | 0.15 | 2.50 | 1808 |
| physarum | mass | 0.72 | 0.46 | 0.33 | 0.90 | 0.00 | 5.00 | 1058 |
| physarum | space | 1.00 | 0.92 | 1.83 | 0.90 | 0.00 | 3.00 | 1699 |
| physarum | charge | 0.98 | 0.95 | 2.17 | 3.88 | 0.00 | 1.88 | 1749 |
| coral | anchor | 0.64 | 0.48 | 3.83 | 1.50 | 0.09 | 0.22 | 5081 |
| coral | time | 0.66 | 0.49 | 3.83 | 1.40 | 0.04 | 0.22 | 5070 |
| coral | mass | 0.31 | 0.26 | 1.17 | 0.00 | 0.57 | 0.30 | 3080 |
| coral | space | 0.59 | 0.44 | 4.17 | 1.60 | 0.04 | 0.21 | 5338 |
| coral | charge | 0.61 | 0.42 | 7.67 | 0.49 | 0.04 | 0.22 | 5074 |
| walker | anchor | 0.96 | 0.86 | 9.17 | 1.88 | 0.00 | 2.86 | 1988 |
| walker | time | 0.99 | 0.89 | 6.83 | 2.00 | 0.07 | 2.86 | 2430 |
| walker | mass | 0.99 | 0.90 | 6.83 | 2.40 | 0.00 | 3.06 | 1394 |
| walker | space | 0.75 | 0.47 | 9.50 | 2.00 | 0.04 | 8.00 | 1248 |
| walker | charge | 0.99 | 0.89 | 6.83 | 2.00 | 0.00 | 2.64 | 1972 |

What the element pass found:
- **Space is the safe element on four of five species.** Longer reach at the same volume raised R on spores
  (1.00) and Physarum (1.00) and held the trap. The walker is the exception (R 0.75): longer thorns pay 8 crystals
  per burn, which breaks the exchange-rate band. A walker's Space variant needs its heart guard scaled WITH its
  thorns, not alone.
- **Mass is the risky element**, because "slower and heavier" is slower to TELEGRAPH too:
  - The snap trap's telegraph p10 fell from 0.86 to 0.45 s (its jaws are wider but close no slower).
  - Physarum's threat fell to 0.33 hits/min (its slower waves are dodged).
  - Coral's telegraph fell to 0 s, and its R collapsed from 0.64 to 0.31.

  The fleet rule "Mass = more volume" has to be checked against each species' telegraph, not assumed.
- **Time** stayed readable everywhere (the worst was the trap, at 0.71 s), which was the measured risk.
- **Charge** (armour: the first hit sheds a shield) was neutral-to-positive on four species.
  - On coral, Charge DOUBLED hits (7.67/min) and halved the telegraph. Armoured coral can't be rammed open, so
    the reader's escape lane closes.
  - Shielded mass being inedible (the locked law) is exactly what makes a Charge threat flora sticky.

**Re-route after a cut** (`reroute.py`). At t = 60 s a 120 u ball is cleared through the densest threat. The
table reports the time until the threat inside that ball regrows to 50% / 80% of its pre-cut value, and how much
of it has returned by +90 s.

| species | pre-cut threat in ball (3 seeds) | t50 s (median) | t80 s (median) | end fraction at +90 s |
|---|---|---|---|---|
| snaptrap | [71, 110, 127] | 41.1 | 999.0 | 0.61 |
| spores | [78, 32, 165] | 999.0 | 999.0 | 0.00 |
| physarum | [270, 206, 232] | 26.1 | 999.0 | 0.80 |
| coral | [202, 53, 142] | 999.0 | 999.0 | 0.00 |
| walker | [8, 6, 9] | 0.1 | 0.1 | 0.17 |


- **Physarum re-routes; that is its whole identity.** It is back to 50% in 26 s and to 80% by +90 s.
- The snap trap regrows to 50% in 41 s. It never reached 80% in the window, because a trap only re-buds after
  its reserve fills.
- **Spores and coral never re-route** (0% at +90 s), and these are NEGATIVES.
  - Spores germinate only where they land, and the wind carries them out of a fresh cut as fast as into it.
  - Coral's reaction is gated to coral still CONNECTED to a living heart, so a cut severs the far side for good.

  Both are fine as threats and wrong as an adaptive network.
- The walker's numbers are not a re-route at all. It holds about 8 threat units in any ball and simply walks
  back in (t50 = 0.1 s).

**Cost** (`cost.py`; the bandwidth model is stated in the JSON, and numpy ms/step was measured on this machine):

| model | size | GPU desktop / iGPU / mobile ms | Burst ms per frame |
|---|---|---|---|
| Physarum (shipped tuning) | 16k agents, 56³ | 0.14 / 0.59 / 0.79 | 0.39 @60 Hz |
| Physarum | 32k agents, 64³ | 0.20 / 0.95 / 1.27 | 0.23 @20 Hz |
| Physarum (the brief's ask) | 100k agents, 128³ | 0.98 / 5.6 / 7.7 | 0.61 @10 Hz |
| Physarum | 100k agents, 96³ | 0.54 / 3.0 / 4.1 | - |
| Gray-Scott coral | 48³ | 0.20 / 0.98 / 1.31 | 0.27 @60 Hz, 0.04 @10 Hz |
| Gray-Scott coral | 128³ | 3.0 / 17.6 / 24.0 | - |
| Snap trap | 60 traps x 27 prisms | - | 0.002 |
| Spores | 140 pods + 2,000 spores | - | 0.024 |
| Walker | 24 x 34 prisms | - | 0.001 |

The three agent species are free. The two field models are a budget question, and it is answered by the TICK RATE,
not the grid:
- A slime mould moves at u/s, so 10–20 Hz is invisible.
- Physarum at 100k / 128³ on 10 Hz Burst costs 0.61 ms per frame. On mobile GPU the same field costs 7.7 ms per
  frame, which is not shippable.
- 56³ is enough for a 450 u grove (8 u voxels against a 6–24 u prism), and is what every number above was scored at.

**Collider budget.** Every searched best holds 1,440–1,988 live plant prisms per grove, inside R_hard's 2,500. Coral
is the exception at 5,081: a reaction-diffusion surface is AREA, and area grows faster than any cap the reaction
respects. That cost is one more reason it is not in the top two.

### What failed, and by how much

- **Coral (Gray-Scott) is the negative of the night**, at R 0.64 and R_hard 0.48. It took four fixes to get it to
  grow and sting at all:
  - zero-flux boundaries, after mass leaked through the mask;
  - exponential transfers, after explicit Euler went NaN;
  - `reach` dilation;
  - a 600-step warm-up.

  It then failed on three counts:
  - It pays 0.22 crystals per burn: a heart is a point inside a surface that stings everywhere.
  - It has no twin variety (0.04): a Turing pattern forgets its initial conditions by design.
  - It breaks the collider budget.

  The "front" sting mode (only the growing edge burns) is kept as a second negative: only the actively reacting
  tips stung, which was momentary and sparse, and a blind pilot met none (0 hits/min).
- **Physarum's tube-voxel heart guard** (danger prisms around the heart) was replaced by the sclerotium BEAT: the
  heart pulses danger on a readable 60% duty cycle. The guard paid out free hearts on the off-frames of any wave.
- **Spores' counterplay is perfect (reader/wander = 0.00) and its threat is the lowest (1.67/min).** A pod that
  telegraphs 2.9 s ahead is easy to read. Turning the alarm up is what Space does, and that is the right lever.
- **The walker is the highest raw threat (9.17 hits/min, above R_hard's 4/min ceiling).** Every attempt to lower
  it in search cost twin variety first. As shipped it is a lure trap, and it should be authored as RARE.

### Top 2 to port

**1. Snap trap: a Venus flytrap clump that turns to face your routes.**
- Each trap is 27 prisms: a 3-prism stalk, two 8-prism lobes, and 8 danger teeth on the lips.
- The heart crystal sits INSIDE the jaws, so the only payout is to thread an open mouth.
- It is a five-state machine: OPEN → PRIMING (lobes glow and gape wider) → ARMED → CLOSING → SHUT, which absorbs
  any mass caught and resets.
- The trigger is a vessel inside `sense` (146 u) whose path crosses the mouth cone.
- Rules that carry over directly:
  - The glow sphere's radius must be at least the strike volume (`max(0.7 L, L·tan(gape + dgape))`). A mouth that
    looks smaller than it bites is the bug the reader found.
  - A trap cannot fire with fewer than half its lobes intact, so breaking one is real counterplay.
  - A trap re-buds lost prisms only from its own reserve (trail it ate), so mass is conserved.
  - Heliotropism is an EMA of where vessels passed (`turn_deg` 7°/s, `bud_bias` 0.3). The clump slowly faces your
    habits, which is the measured 1.83x route bias, with no special rule.
- Port as a `Flora` subclass. States are per-plant ints in a Burst job, and every prism transition is a
  clock-stamped bloom or close (one stamp per state change, zero per-frame CPU on the prisms). Lip teeth are
  ordinary danger prisms, so the burn rides the existing danger channel.
- Cost: 0.002 ms; 1,718 prisms for 9 clumps.
- Charge needs nothing new (R 0.97). Space makes it more readable, not less.

**2. Physarum network: a slime mould that cables the grove between food and your trails.**
- The model is Jones's 3D agent model: 16k agents on a 56³ trail field. Tuned values: sensor angle 30°, rotation
  40°, step 44 u/s; the agents deposit on food and on vessel trails.
- Voxels above an `on` threshold (with hysteresis down to `off`) become tube prisms. Tubes DIGEST the mass under
  them into the network's reserve, so the cables are made of what they ate.
- Danger is a Greenberg-Hastings excitable wave running down the tubes at 65 u/s with 4 s refractory. A pilot
  sees a pulse travelling toward a junction 3.6 s before it arrives (telegraph p10). That is the best telegraph
  of any species that still re-routes.
- Each heart is a sclerotium that climbs the trail gradient at 7.8 u/s and BEATS danger on a 60% duty cycle, so
  its crystal is taken between beats.
- Cut a cable and the agents re-find the gap: 50% back in 26 s, 80% by +90 s. That is the one species here whose
  threat is an adaptive NETWORK rather than a placement.
- Port as two compute dispatches, sense+move and deposit+blur+decay, on a 56³ RFloat texture: 0.14 ms desktop,
  0.79 ms mobile. Alternatively run Burst at 20 Hz (0.23 ms at 32k / 64³).
- Tube materialisation is a sparse readback every 0.5 s (a voxel crossing a threshold → lay or resorb a prism),
  never per frame.
- The trail field's "decay" is a FIELD diffusing, not mass. Every tube prism is laid from the reserve and returns
  to it, and the audit closed to rounding on every run.
- Two cautions:
  - Mass breaks it: slower waves are dodged, and R falls to 0.72. Author Mass as thicker tubes with the SAME wave
    speed.
  - The reader beats it 0.38 of the time, so it should share a grove with a placement threat (the snap trap) rather
    than stand alone.

**Runner-up: spores.** Its counterplay is perfect and it scores R 1.00 at Space. It loses the second slot because
it never re-routes, and because 2,000 drifting spores is a lot of always-moving mass for a cell's spatial index.


## Flyable ecology (Direction H, branch `cece/eco-flight`)

**Question.** The round-1 viewers were too austere to judge: "with no sense of depth it is hard to perceive this as
a 3d scene. without control it is hard to interact. without a sense of orientation, i don't know how the vessel is
moving, facing etc." Can the species be FLOWN, at game-like speed, by the code that was scored, so the judgement is
about the species rather than the viewer?

**Where it lives.** `Tools/Ecology/flight/`:
- `index.html` is the page: self-contained, 218 KB, with three.js r128 from jsDelivr as the only external host.
- `export_params.py` lifts every species' constants and defaults out of the Python by AST into `params.json`. It also
  writes a source-hash manifest; `--check` fails if the JS's parameters are stale or a Python source changed.
- `src/*.js` is the JS port: bestiary herds, the new grazer base, the fortress builder colony and the snap-trap
  clump, all on typed arrays and a hashed spatial grid. `build.py` bundles it into `sim.js`, which Node `require`s
  and the page inlines, so the page flies exactly the code the gate measured.
- `grazer.py` is a new Python species: a cute, schooling, curious grazer of environment mass. It flees a rushing
  pilot, never hits, and breeds from its gut. It is the base every other species lives among.
- The fidelity gate is `fidelity_py.py`, `fidelity_js.js` (`run_fidelity_all.sh` runs it in parallel) and
  `fidelity.py`, which compares the two and runs the negative controls.
- `browser_test.py` drives headless Chromium. Results go to `results/` and screenshots to `shots/`.
- `emotion/ratings.py` now reads the page's export too, and summarises the 1-5 threat, readability and fun scales
  per species.

### What you fly

- **The vessel.** A lit manta-dart mesh with nav lights and an engine plume.
  - Throttle runs 0-160 u/s; Shift boosts to 300 u/s on a draining meter. Turn rate is 110 deg/s (the Dolphin's
    authored turn), roll 160 deg/s; rates are eased, and the hull banks into turns.
  - Controls: W/S throttle, A/D yaw, arrows or mouse for pitch/yaw, Q/E roll. There is a mouse-steer mode, two
    touch sticks and the Gamepad API.
  - Near the membrane the vessel is turned back rather than clamped.
- **The wake.** It is conserved mass: a 10-volume prism every 15 u, aligned to the flight path at lay time.
  Thieves steal it, fortress workers carry it off, grazers and fauna eat it.
- **Depth and orientation.**
  - Lighting and space: lit instanced prisms, exponential fog, hemisphere plus key plus rim light, a starfield,
    drifting motes, and a fresnel membrane with lat/long rings and coloured poles.
  - Creatures: low-poly bodies oriented to their velocity and coloured by posture. Telegraphs are drawn as
    additive halos: a pack's red closure, a lurker's widening gape, a bull's head-down, a trap's open glow.
  - Cameras: a chase camera with lag, bank and look-ahead, plus a free orbit camera (C).
  - HUD: speed and heading/pitch/roll readout, an attitude indicator, a minimap disc, edge arrows plus brackets
    for anything telegraphing at you, and a hit/steal flash with a toast. Optional WebAudio stingers sit behind a
    click.
- **The occlusion corridor.** The game's own platform law (PRISM_ANIMATION.md section 4.7) is applied to the page:
  prisms between the chase camera and the ship, or brushing the lens, shrink out of the view. Photons only; every
  rule still sees them.
- **Modes.** One species at a time (to judge it), or the whole cell: about 1,400 agents of ten species on about 4k
  live prisms.
- **Debug overlay (`?debug=1`).** It shows the conserved-mass ledger, live + held by species + destroyed + cut =
  start + your wake. Its residual is exactly 0 in every run measured, browser and Node.
- **Judging.** Rate an encounter with R: one of six emotions, plus threat, readability and fun on 1-5.
  - Ratings persist in localStorage, inside try/catch.
  - Export copies JSON to the clipboard, falling back to a selected textarea. `ratings.py` reads it; the browser
    test round-trips one.

### The fidelity gate

The JS cannot match the Python bit for bit (different RNGs), so the gate is statistical.
- **Setup.** Each species runs in ITS OWN Python harness world (bestiary / builders / flora), mirrored in JS, against
  the same scripted pilots (wander / evader / hunter; flora: wander / reader / cutter). Seeds: 6 Python, 8 JS.
  Runs are 1.5 min.
- **Metrics.** Hits/min per pilot, hunter payoff, counterplay, three telegraph measures, and the six feel axes.
- **Agreement rule.** A metric agrees when |js - py| <= max(abs floor, rel band x max, 2.5 x combined bootstrap SE).
  A species passes only when every metric agrees.

**At the Python's step (dt 0.1 s), all ten species pass.** Means are JS / Python:

| species | hits/min wander | hits/min evader (reader) | hits/min hunter (cutter) | hunter payoff/min | shared telegraph s |
|---|---|---|---|---|---|
| pack | 4.7 / 5.7 | 0 / 0 | 2.5 / 2.6 | 4.7 / 4.7 | 1.5 / 1.2 |
| thief | 24.5 / 29.4 | 3.4 / 5.3 | 60.2 / 56.1 | 7.2 / 5.9 | 0.23 / 0.20 |
| locust | 40.2 / 28.9 | 0 / 0 | 5.8 / 8.9 | 20.2 / 30.0 | 0.10 / 0.03 |
| lurker | 1.3 / 0.4 | 0.5 / 0 | 4.5 / 4.7 | 5.0 / 5.2 | 0.9 / 0.8 |
| stampede | 7.9 / 10.4 | 0 / 0 | 18.1 / 13.3 | 3.6 / 3.6 | 0.1 / 0.1 |
| leviathan | 0.4 / 0.6 | 0 / 0 | 14.2 / 12.3 | 39.2 / 38.0 | 0.9 / 0.9 |
| mobber | 3.7 / 3.1 | 0 / 0 | 28.2 / 25.2 | 27.4 / 26.7 | 0.2 / 0.15 |
| grazer | 0 / 0 | 0 / 0 | 0 / 0 | 32.6 / 29.3 | - |
| fortress | 0.08 / 0.11 | 0 / 0 | 1.0 / 1.3 | 5.6 / 7.7 | 0.6 / 0.65 |
| snaptrap | 5.0 / 7.4 | 0.3 / 0.2 | 19.7 / 23.3 | 23.7 / 24.6 | 1.8 / 1.9 |

The locust row passes on its noise. Its swarm tips from shy to storm at a seed-dependent moment, so a 1.5-minute
run is either mostly storm or mostly not; the band is the bootstrap SE, not a loose constant. Full per-metric table:
`flight/results/fidelity.md`.

**The gate has teeth.** Three deliberately broken parameters, run through Node, must fail it, and all three do:
- `thief.WARM=1e9` (wants any trail, not fresh trail) disagrees on wander hits, coherence and burstiness.
- `lurker.GAPE=0.05` (no telegraph) disagrees on 7 metrics, including all three telegraph measures.
- `pack.SPRINT=60` disagrees on hits and all three telegraph measures.

**Two metrics were re-cut after they produced false disagreements; both re-cuts are measurement fixes, stated
here:**
1. **Counterplay (evader hits / wander hits)** is reported only when BOTH sides' wanderer is hit at least once per
   minute. Below that it is a ratio of two near-zero counts: the lurker read 0.40 vs undefined, the leviathan
   undefined vs 0.00. The hit rates themselves are still compared.
2. **First-strike telegraph lead is BIMODAL for some species.** A hunter ramming a stampede is hit with lead 0; a
   bull's charge comes 0.3-2 s after head-down. About half of the stampede's first strikes are in each mode, so the
   median flipped between 0 and 0.6 s on a few strikes. It is now two numbers: the share of first strikes that were
   telegraphed (lead >= 0.25 s), and the median lead of those that were.

**The page steps at 1/30 s, and that is a separate question.** JS at 1/30 s against Python at 0.1 s disagrees on six
species (lurker, stampede, leviathan, mobber, grazer, fortress). To separate port defects from the RULES' own
step-size sensitivity, `fidelity_py.py --dt30` runs the Python at the page's step:
- **Like for like at 30 Hz,** five of the six agree. **The lurker is the instructive one.** Its bite is a contact
  test sampled once per step: a 320 u/s lunge moves 32 u per 0.1 s step but the contact radius is about 19 u, so
  the 0.1 s Python TUNNELS through most bites. Its scored lurker under-counts its own bites by about 2x (hunter 4.7
  at 0.1 s vs 10.6 at 1/30 s). This is the game's own rule, *a fixed-timestep trigger is a SAMPLE, not a test*,
  found in the research harness. Any species whose strike is a fast lunge should be scored at the step it will run
  at, or sweep its contact.
- **Jerk** is likewise a function of step size. At 1/30 s a `steerTo` turn is three small rotations, not one, so
  jerk_rel moves 2-3x between steps on the same rules.
- **The leviathan's approach axis** still differed at 30 Hz. It is set entirely by whether a seed's random start puts
  the evader within 300 u of the body: the evader then flees to the membrane and never meets it again, at 1,100-1,900
  u. Re-run with the same 24 seeds per side, every leviathan metric agrees; 8/24 Python seeds vs 7/24 JS seeds have
  such an encounter. It was seed luck at 6-8 seeds, not a port difference.

The page's fidelity line ("agrees") uses the like-for-like 30 Hz verdict where it exists and the cross-step verdict
for the four species with no step sensitivity. The fidelity summary is baked into the page at build time.

### Performance

| | measured |
|---|---|
| whole cell | ~1,400 agents, ~3.6-4k live prisms |
| sim cost | 6.7-7.2 ms per 30 Hz step in the browser; 8.6 ms in Node, profiled |
| render CPU | 4.3-5 ms per frame, measured in headless Chromium |

- **Budget.** At 60 fps the sim runs every other frame, so CPU averages about 8 ms per frame. That leaves headroom
  in a 16.7 ms frame, but it is not measured on a GPU here. The only GL available is SwiftShader (software), so wall
  frame time headlessly is about 95 ms (11 fps), which says nothing about a real GPU. Render cost is one instanced
  draw per species plus one for prisms, plus halos/motes as points.
- **One defect found and fixed.** The spatial grid's large-radius queries de-duplicated buckets with
  `Array.from(...).sort()`, allocating every call. The grazer's 120 u food search hit that path for every grazer,
  costing 13 ms of an 18 ms step. Generation-stamped bucket marks made it O(1) and allocation-free: whole cell 18 ->
  8.6 ms per step. The grazer flock is still the largest cost (6 ms of 8.6) because a cluster of 1,100 schooling
  grazers has about 115 neighbours each.

### Verified in headless Chromium (`browser_test.py`)

- No page or console errors in any of the 11 modes.
- The vessel moves and turns under scripted keys (W, A, ArrowUp): 270 u travelled and a rotation change.
- **Thieves tail the wake.** 18 steals from the player's fresh trail in 12 s. Thieves follow the trail, not the hull,
  so "the median thief comes closer to the ship" was the wrong test; the ship flies away from them.
- **The pack rings.** Its own closure quorum reaches 0.89, past the 0.55 strike threshold. A raw bearing-spread test
  was wrong too: the pack fans out in a cone AHEAD of you (it cuts off where you are going), not round your current
  position.
- No horizontal overflow at 390 px.
- `ratings.py` reads the page's export.
- **Screenshots** at 1440x900 and 390x844: whole cell in chase and orbit, the rating panel, phone start and phone
  flight, thief, lurker, leviathan, fortress, snaptrap.
- **Critique applied from the screenshots:**
  - The phone HUD's minimap covered the speed readout, so the minimap moved under the top bar.
  - The engine plume read as a white ball on the hull; it is now smaller and dimmer.
  - Fresh wake prisms loomed across the bottom of the chase view; fixed by the corridor plus the near-lens fade.

### Known gaps

- **No human has rated anything yet.** The page is the instrument; the evidence starts when five or more pilots
  export ratings for `ratings.py`.
- ~~**Cross-species interactions are unvalidated.** In whole-cell mode thieves and fortress workers compete for the same
  wake, grazers and locusts graze the same mass, and so on. No Python scores those interactions, so the whole-cell
  mode is faithful species by species, not as an ecosystem.~~ **Covered (2026-10-08):** a Python twin of the whole
  cell now scores them; see "Whole-cell gate" below.
- **Whole-cell populations are scaled for vibrancy, not scored.** Grazers start at 900 (cap 1,600) against the
  scored 120 (cap 240); locusts are capped at 360. Single-species modes use the scored defaults. **Since 2026-10-08**
  the page has a "scored populations" picker (`?pop=scored`) that builds the cell at the scored defaults; the lively
  cell stays the default.
- **The fortress lattice is 12 sites each way in JS, 40 in Python.** Its Q template is below 0.05 past 52 u, so no
  rule reads or writes past 96 u; the fortress passes the gate at both steps.
- **The flight world slows the player only on a burn or a snap.** The bestiary's slow-on-every-hit belongs to the
  flora harness; in flight a hit flashes, toasts and logs.
- **Frame time is not measured on real graphics hardware** (see Performance).
- ~~**The page uses the game's NUMBERS (turn rate, boost ratio, chase offset), not its flight model.** It has no drift,
  no element scaling and no abilities beyond ramming.~~ **Covered (2026-10-08):** the page now flies the game's own
  scalar VesselTransformer with the Manta's authored numbers, the game's drift, Time/Mass element scaling, and the
  Manta's Soar and Yastri. The old model is `?flight=arcade`. What still differs is listed under "Game flight model"
  below.

### Game flight model (2026-10-08)

**Source.** Branch `bleeding-edge`, read with `git show`. All paths below are under `Assets/_Scripts/Controller/`
unless they start with `Assets/`. The Manta runs the base `Vessel/VesselTransformer.cs` (script guid on
`Assets/_Prefabs/Spacevessels/Manta.prefab:1895`) on its SCALAR path: `vectorFlightModel` and
`holdSpeedWhileDrifting` default off (`VesselTransformer.cs:65`, `:100`), and the prefab leaves them unset.

**The Manta's numbers** (`Manta.prefab:1898-1925`):

| field | value |
|---|---|
| `DefaultThrottleScaler` | 180 |
| `DefaultMinimumSpeed` | 0 |
| `PitchScaler` / `YawScaler` / `RollScaler` | 50 / 30 / 100 deg/s |
| `RotationThrottleScaler` | 0.2 deg/s per u/s |
| `BoostSpeedMultiplier` | ElementalFloat on Time: 1 at rest, 1.3 at level 10, floor 0.7 |
| `ThrottleScalerMultiplier` | disabled |
| `VesselStatus.boostMultiplier` | 4 (`Manta.prefab:1879`; default 4 at `Vessel/VesselStatus.cs:78`) |
| wake `trailVolume` | ElementalFloat on Mass: 1 to 2.5 (`Manta.prefab:1950-1955`) |

**Formulas, one frame.**
- **Stick.** `XSum = Ease(L.x + R.x)`, `YSum = -Ease(L.y + R.y)`, throttle `XDiff = (R.x - L.x + 2) / 4`
  (`IO/GamepadInputStrategy.cs:238-241`). `Ease(x) = sign(x) (1 - cos(x pi/4))` (`IO/BaseInputStrategy.cs:34-39`),
  so ONE full stick is only 0.29 of the turn rate; both thumbs together give 1.
- **Rotation.** `accumulated = AngleAxis(stick * (speed * RotationThrottleScaler + Scaler) * dt, hull axis) *
  accumulated` for roll (YDiff), yaw (XSum) and pitch (YSum) (`VesselTransformer.cs:894-916`). The hull then follows:
  `rotation = Slerp(rotation, accumulated, 1.5 dt)` (`:548`, `:574`, `LERP_AMOUNT` at `:44`). That is a 0.67 s lag.
  - So the Manta's turn rate GROWS with speed. Yaw is 30 + 0.2 v deg/s: 57 at 135 u/s, 66 at 180, and about 200 in
    a full Soar. Pitch is 20 deg/s higher.
- **Speed.**
  - `target = XDiff * 180 * CurrentBoostAmount() + 0` (`:971-973`).
  - `CurrentBoostAmount = BoostMultiplier * BoostSpeedMultiplier(Time)` while boosting, else 1 (`:918-930`).
  - `speed = Lerp(speed, target, 1.5 dt)` (`:1041`). A zero target also brakes at a constant 180 / 2 s
    (`Vessel/MinimumThrottleBrake.cs:9-38`).
  - Boost multiplies the THROTTLE target, so a Soar at zero throttle does nothing.
- **ElementalFloat.** `LerpUnclamped(Min, Max, level / 10)`, then floored if `UseFloor` (`Vessel/ElementalFloat.cs:137-138`).
  The level is petals / 10 (`Vessel/ResourceSystem.cs:152`, `PetalNormalized`), so the page's flower petals ARE the
  game's element levels.
- **Drift.** `Vessel/R_VesselActions/Data Containers/DriftActionSO.cs:11-29` calls `BeginDrift(Mult, driftDamping)`.
  - Each frame the trigger sum eases toward 0/1 at 12 /s for non-gamepad input (`VesselTransformer.cs:369`, `:396-410`).
  - `ApplyAnalogDrift` scales the Pitch/Yaw/Roll scalers by `lerp(1, Mult, trig)` and sets
    `Grip = lerp(1, driftDamping, trig)` (`:852-890`).
  - In `MoveShipScalar`, `driftedCourse = Grip > 0.001 ? Slerp(Course, forward, Grip dt) : Course`, then
    `Course = Slerp(forward, driftedCourse, trig)`. Outside a drift, `Course = forward` (`:1272-1293`).
  - Position moves along `Course` at the throttle-driven speed. The throttle stays live in a drift.
  - On release the trigger eases back to 0. Only then do the scalers restore and `IsDrifting` clear (`:404-410`, `:749-772`).
- **The Manta binds no drift.** `Manta.prefab:2131-2152` binds four actions:
  - OnlyRightStick: YawsteryAction-Right.
  - OnlyLeftStick: YawsteryAction-Left.
  - BothSticks: BoostAction.
  - The gamepad (and keyboard) override binds MantaAnalogTurnBoostAction to all three.

  The drift on the page is therefore one of the game's two shipped single-tier drift assets:
  - **Dolphin** (Mult 1.5, damping 0) is the default: the course freezes.
  - **Squirrel** (`?drift=squirrel`): Mult 1.8, damping 0.25.
- **Soar and the trigger Yastri** (`Vessel/R_VesselActions/Executors/MantaAnalogTurnBoostExecutor.cs:98-125`). Yaw is
  `(rt - lt) * 60 deg/s` (60 from `Manta.prefab:1330`). `BoostMultiplier = 1 + (4 - 1) * min(lt, rt)`, and boosting
  is on while `min(lt, rt) > 0.01`. On keyboard the two Shifts are the two triggers.
- **Touch Yastri** (`Executors/YawsteryActionExecutor.cs:115`, `:142`, `:214-220`). Lift one thumb after flying on
  two (`IO/TouchInputStrategy.cs:262-284`). Lifting the right thumb turns LEFT.
  - Yaw is `80 * (1 + Speed/100)^0.25 * intensity` deg/s.
  - Intensity ramps in by smoothstep over 0.35 s and out linearly over 0.25 s.
  - Turning to the other side ramps out first.
- **Element scaling the Manta actually has** (`Assets/Resources/ElementalAbilityMaps/Manta.asset:17-64`):
  - **Time** scales Soar's top speed.
  - **Mass** scales the trail's prism volume (Yastri's turn rate is deliberately unscaled).
  - **Charge** scales the Sting bomb bay.
  - **Space** scales Kabloom's blast.
  - Nothing scales turn rate or cruise speed.

**What the page does.** `?flight=game` is the default; `G` toggles to arcade. Controls:

| input | action |
|---|---|
| W/S | throttle (XDiff) |
| A/D, arrows, mouse | the eased stick, treated as both thumbs moving together |
| Q/E | roll |
| Shift, pad A or RB, touch BOOST | Soar (both triggers) |
| Z / X, pad LT / RT | the Manta's two triggers |
| touch: lift one thumb | the ramped Yastri |
| Space, pad LB, touch DRIFT | drift |

- The HUD line reads `game flight · DRIFT 60° · YASTRI ◀ · SOAR ×4.60 · wake ×1.75`. During a drift a flight-path
  marker shows where the vessel is actually going.
- Element levels come from the stakes flower; with stakes off they are at rest (level 0). Mass petals set
  `arena.trail_vol = 10 x lerp(1, 2.5, mass/10)`. The conservation ledger still closes to a residual of 0.
- The step is allocation-free: scratch vectors, and reused input and autopilot objects.

**Verified** (`browser_test.py` section 6). Both models are stepped deterministically on a paused page:

| check | measured |
|---|---|
| plain turn: course vs nose | 0.000 deg off |
| Dolphin drift: heading over 1 s | turned 55.5 deg |
| Dolphin drift: velocity direction over the same 1 s | moved 0.00 deg |
| Dolphin drift: slip | 59.5 deg |
| slip 0.4 s after release | 0 |
| X trigger | 60.0 deg/s to the right |
| touch lift | intensity 1 after 0.35 s; 51.74 deg in 0.5 s against 51.74 expected; left |
| Soar | x4.60 at 5 Time petals, x5.20 at 10, x4.00 at 0; 180 to 814 u/s in 2.5 s |
| wake | 10 at Mass 0, 25 at Mass 10 |
| audit residual | 0 |

`?flight=arcade` shows none of these behaviours. The thief and pack reaction checks, the stakes test and the
showcase test all still pass. Screenshots: `shots/desktop_drift.png`, `shots/desktop_soar.png`.

**What still differs from the game.**
- **Scale.** At the Manta's Soar (720-940 u/s) the cell (R = 1,200) is crossed in about 3 s. The membrane
  turn-back is a page rule, not the game's, and is scaled with speed so that it still holds. It also bends a
  drifting course inward.
  - The creatures' speeds and the fidelity gate were tuned against a 120-160 u/s pilot. A Soaring Manta outruns
    everything, which is the game's number, not a tuning choice.
- **Control layout.** The page uses one stick for pitch and yaw, mapped as `Ease(2v)`. The game's dual-stick scheme
  (`XSum` from both sticks' x, throttle from their spread) is not reproduced on gamepad or touch.
- **Keyboard.** The game's keyboard reads mouse DELTA for yaw and pitch and A/D for roll (`IO/KeyboardMouseInputStrategy.cs:192-195`).
  The page keeps its own layout.
- **Not implemented.**
  - Sting/Kabloom bombs: Charge and Space.
  - Mass-5 shielded turn trails.
  - Yastri's outer-lane trail flare.
  - The AI's Soar-when-straight drive (the page autopilot never Soars).
  - Gyro.
  - The touch nose-response option (`touchNoseResponse`, 0 on the Manta anyway).
  - The vector flight model (no shipped Manta uses it).
- **Drift on the Manta is borrowed.** The page binds the Dolphin's or Squirrel's drift to a hull that has none in
  the game. On a pad, drift is a binary LB eased at 12 /s rather than an analog trigger sum, because the Manta's
  triggers are already Yastri and Soar.
- **Step size.** The page steps at a fixed 30 Hz; the game runs per rendered frame. The slerp and lerp use `k * dt`
  the same way, so 30 Hz is slightly softer than 60 Hz (the per-second retention of the 1.5 /s lag differs by about 2%).
- **Wake orientation.** Wake prisms are laid along the travel direction (Course). In the game, trail prisms orient
  by facing (`blockRotation`), so in a drift the page's wake prisms point along the slide and the game's point
  along the nose.

### Whole-cell gate (2026-10-08, `cell_py.py` / `cell_js.js` / `cell_fidelity.py`)

**What it checks.** The species gate above scores each species alone in its own harness. This one scores what only
exists when they share a cell. `cell_py.py` builds the page's 'cell' composition in ONE Python arena: BArena, 4,200
scattered prisms, the pilot's trail booked as its wake (domain 1, trail flag), the fortress at its page anchor, and
snap-trap clumps in the 0.3-0.75 R band. Every rule is each species' own Python. `cell_js.js` runs the page's own
`FlightWorld` (new `opts`: `pilot`, `drop`, `pop`) with a scripted pilot in place of the player. Both book every eat,
destroy, steal and haul to the species whose step made it AND to the creator of the prism: `env`, `wake`, or the
species that laid it (a kill's skeleton, a fortress brick). Pilots wander and hunter, 6 seeds x 3 min, dt 0.1.

**Result: it agrees.** Tolerance per row is max(abs floor, rel x max, 2.5 x combined SE), as in `fidelity.py`.

| check | agree |
|---|---|
| levels: hits by species, mass flows by species x creator, populations, crystals | **93 / 93** |
| of which cross-species flows (one species taking another's mass) | 5 / 5 |
| interaction effects: full cell minus the cell without grazers / locusts / fortress | **7 / 7** |
| negative control `locust.CHEW=20` (JS only) | fails 4 rows (locust intake, population, wake eaten, thief hits) |
| negative control `grazer.CHEW=1` (JS only) | fails 1 row (wander thief hits) - weak: the gate is coarse for grazers |

The table per row is `flight/results/cell_fidelity.md`.

**What the cell does that no single species shows** (both languages agree):
- **Grazers halve the locusts' food.** Without grazers, locusts eat 6,000-9,000 more env volume per minute.
- **The fortress is the cell's biggest mover.** It hauls 25,000-50,000 vol/min, most of it the pilot's wake. Under a
  hunter it also hauls the skeletons other species leave (the 5 cross-species rows: grazer, leviathan, locust, lurker
  and pack mass, 120-3,800 vol/min each).
- Grazers, locusts and the leviathan also eat skeletons in both languages, but under 150 vol/min, below the gate's floor.
- **The fortress shields the leviathan from a hunter.** This is an artefact of publishing: each species overwrites
  the arena's target list in turn and the fortress publishes after the leviathan, so a hunter pilot chases the
  fortress core. Without the fortress it hunts the leviathan (+37 leviathan crystals/min). It is identical in both
  languages and concerns only scripted pilots; the player chooses for themselves.

**Tilts inside tolerance** (worth knowing, not failures):
- JS fortress takes about 40% more wake than Python, by both haul and steal. Thieves then get less warm wake: JS thief
  hits on a wanderer are 18/min against 32/min.
- JS snap traps eat about 3x more env (470-560 vol/min against 140-160) and less wake. The seed spread is large
  (Python 0-420 per run), so the rows pass.
- Neither was chased further. The fortress lattice difference (12 vs 40 sites) is the first suspect.

**One bug found in the reference, not the page.** BArena never records a pilot's previous position (only
FloraArena does). The snap trap sweeps ram/burn contacts along prev -> pos, so its first run swept the whole flight
path and logged 20x the hits. `cell_py.py` now records prev every step, as the page's arena does.

**Speed.** The Python grazer's all-pairs schooling at 1,600 grazers cost 0.9 s a step. `grazer.flock()` now visits
only the columns inside each spatially sorted row block's box + 30 u. It matches the all-pairs maths to 1e-9
(`python grazer.py --check-flock`), and the step dropped to 0.14 s. JS runs the same cell at about 10 ms a step.

**Not covered.** The player's own flight model is not in either reference: scripted pilots fly the arena's turn
model. dt 1/30 was not run for the whole cell. The siege (JS-original) is not in the cell gate.

## Living cell (Direction G, branch `cece/eco-living-cell`, 2026-10-02)

**Question.** Do the round-1 picks survive being put in ONE cell together - thieves, pack hunters, locusts,
lurkers, the fortress, the snap-trap clump, physarum and A's grazer school in a 1200-u arena, with real trophic
links, E's cohort LOD, and the locked rules (mass conserved and audited every step, no imposed death, nothing pops,
shielded mass never food)? And does a pilot flying through it get an *experience*, not just an ecosystem?

Code: `Tools/Ecology/living_cell/` (`world.py` mass store + numba grids, `fauna.py` the five mobile guilds,
`structures.py` flora / traps / fortress / physarum, `cell.py` the trophic wiring + macro level, `metrics.py`,
`run.py`, `calibrate.py`, `rounds.py` = every configuration tried, `domains.py` Part 2, `viewer.py`).
Results: `living_cell/results/*.json` (every round kept, including the dead ends; the `*.log` console output
that `run_all.sh` writes beside them is not committed - every number in it is in the JSON). Re-run everything:
`sh living_cell/run_all.sh` (~3.5 h on 4 cores). Viewer: `living_cell/viewer.html` (self-contained, three.js 0.128
from jsDelivr; one 5-min explorer flight through the recommended cell, species-coloured, far populations drawn as
cohort clouds, timeline of encounters / hits / C's emotion / threat underneath).

### The food web as built

```
 pilot trail (the source) --> TRAIL prisms --+--> locusts (FLORA|TRAIL)      --> packs (Holling II)
 flora <-- soil nutrient N <-- every metabolism, every exudate, every starved stomach                |
   |                                         +--> thieves steal warm trail --> HOARD --> fortress builds walls from it
   +--> grazers (FLORA|SKEL) ---------------------------------------------------> packs, lurkers, snap-traps
 physarum digests FLORA|TRAIL|SKEL it covers;  every death = crystal + SKELeton prisms (conserved)
 pilot ram / ability = the only sink;  danger tubes / trap jaws / wall bites burn pilots
```

Ledger: prisms + N + everything held by species (bodies, stomachs, cohort ΣS, reserves, stores). Worst audit over
every run of every round **≤ 1.1e-5 vol** on ~6e5 vol total. Shielded prisms eaten: **0** in every run.
Continuity: **0 pop-ins / 0 pop-outs** in every run of the recommended LOD (closest cohort expansion 308 u from a
pilot; scene load at t < 1 s excluded).

### Iteration log (every round kept in `results/iterate.json` / `baseline.json` / `long.json` / `final*.json`)

Means over seeds; player numbers are per 5-min flight (explorer + wanderer pilots, 6+ flights per run).
"enc" = ACTIVE encounters (a striking state within 200 u) from R4 on; R1-R3 counted presence.

| round (seeds x min) | change | extinct | Shannon mean/min | enc/min | variety | quiet | hits/min | emotions | verdict |
|---|---|---|---|---|---|---|---|---|---|
| baseline (4x30) | the picks at their round-1 numbers | thief 3/4 | 1.84 / 1.79 | 0.41* | 2.0 | **0.01** | **34.8** | 4.1 | a siege, not a cell |
| R1 (3x20) | niche partition (grazer FLORA\|SKEL, locust FLORA\|TRAIL), hunger-gated packs, trap exudation | 0-2 | 1.66 / 1.59 | 0.37* | 1.8 | 0.31 | 5.4 | 2.5 | quiet appears; every population sat at its cap (CV ~0) |
| R2 (3x15) | caps as backstops only, flora growth 0.004-0.010 | 0 | 1.95 / 1.87 | 0.47* | 2.3 | 0.17 | 12.6 | 3.8 | populations move; lurkers still capped |
| R3 (3x15) | leashes (thief territory, pack range) | 0 | 1.92 / 1.81 | 0.37* | 1.8 | 0.31 | 3.7 | 2.2 | presence-based encounters can't see anything |
| R4 (3x15) | encounters = ACTIVE state; lurker metabolism | 0 | 1.88 / 1.82 | 1.67 | 2.7 | 0.56 | 2.4 | 3.0 | the metric now measures meetings |
| R5 (3x15) | schools/swarms expand as clumps; packs take nearer of prey/pilot; dense locusts | 0 | 1.88 / 1.80 | 2.03 | 3.7 | 0.54 | 3.0 | 2.5 | best 15-min cell; then the 60-min run |
| LONG (2x60) | R5 for an hour | thief 2/2 | 1.81 / 1.72 | 1.76 | 3.1 | 0.72 | 1.8 | 2.4 | thieves die ~20-25 min, packs reach cap, physarum starves |
| consistency (3x6) | same cell all-micro vs all-macro | - | - | - | - | - | - | - | macro locusts 7x micro, lurkers 0.16x, thieves 2x: **the far cell was a different ecosystem** |
| calibrate | fit 8 macro dials to the micro trajectories | - | - | - | - | - | - | - | mean log error 0.65 -> 0.14 (end gaps: grazer 1%, thief 3%, lurker 0%, pack 16%, locust 54%) |
| R6 (3x30) | calibrated cell (+ packs off thieves, lurker metab 0.2, physarum upkeep) | 1-4 | 1.81 / 1.70 | 1.98 | 3.3 | 0.59 | 3.1 | 2.6 | thieves still dying with NO pack predation |
| R7 (3x30) | two thief fixes | 1-3 | 1.79-1.82 | 1.66-1.98 | 2.9-3.3 | 0.53-0.59 | 2.9-3.4 | 2.6-2.8 | each fix moved the extinction onto locusts |
| final R7 (4x45) | R7 base for 45 min | thief 3/4 | 1.81 / 1.70 | 1.73 | 2.9 | 0.64 | 2.7 | 2.5 | thieves STARVE (54 of 58 losses), eat 181 vol vs grazers' 165k |
| R8 (3x30) | **macro diets = micro diets** (the consistency gap the calibration had papered over) | **0** | 1.87 / 1.68 | 1.88 | 3.2 | 0.53 | 3.0 | 2.9 | breathes: herbivore reversals 3-5 per run (was 1-2) |
| **final (4x45)** | R8 for 45 min | thief 3/4 | **1.87 / 1.68** | **1.76** | **3.1** | **0.60** | 11.2 mean / **2.0 median** | 2.7 | recommended; thieves the one open failure |

\* presence-based, not comparable with R4+.

**Final cell, per pilot (median of 24 flights each, 4 seeds x 45 min):** explorer 1.9 enc/min, variety 3, quiet 61%,
2.5 hits/min, 3 distinct emotions, 8 species sighted; wanderer 1.6 / 3 / 64% / 1.9 / 3 / 7.5. Threat-variety entropy
1.44 bits. Emotion entropy 0.83 bits (cute / eerie / terrifying dominate; menacing and majestic appear rarely).
Threat range 0.40. Replayability: Jaccard distance between seeds' encounter sets 0.73, species-mix TV 0.36,
emotion TV 0.16 (same seed twice: exactly 0). Cost **14.8 ms/tick mean, 34.7 ms p95** (numba, one core).
The 11.2 hits/min mean is one flight family: in seed 1 the explorer met a **locust storm** (543 locust hits in one
5-min flight, flights 7-10 at 70-150 hits/min) - an emergent event nobody scripted, kept in the numbers rather than
trimmed. Encounter mix over the final: snap-traps 181, lurkers 137, fortress 50, packs 45, locusts 5, physarum 3,
thieves 2.

### Recommended composition and dials (`rounds.FINAL`)

Everything in `rounds.py` is a delta chain (`HIGHCAP -> BASE3 -> BASE5 -> CONS -> BASE6 -> BASE7 -> R8`), so the
recommendation is readable as the list of changes that each round earned:

- **Seeds:** grazers (A's school), 500 locusts, packs, 45 thieves at 3 nests, lurkers, one fortress, a snap-trap
  clump, one physarum; flora logistic on soil N, growth **0.008**, 12% Charge plants (shielded, never food).
- **Niches are what stopped the siege:** grazer `FLORA|SKEL`, locust `FLORA|TRAIL`, thief `FLORA|SKEL` + stolen
  trail + larder, packs eat **grazers and locusts only**, lurkers eat anything that wanders in (pilots included).
- **Caps are backstops, not the dynamics** (grazer 2600, locust 1200, pack 120, thief 150, lurker 60). Packs and
  lurkers still reach theirs late in long runs - see negatives.
- **Hunger-gated predators:** packs hunt only below a stomach threshold and stalk a pilot only when it is nearer than
  any prey; lurker metabolism **0.2** makes lurkers food-limited instead of cap-limited (CV 0.15, was 0.0).
- **Macro dials are FITTED, not guessed** (`calibrate.py`): grazer F_half 7.8, locust F_half 500, thief F_half 1200,
  lurker a_attack 0.038, hops 0.012/0.012/0.008 - **and each guild's macro diet is its micro diet** (R8). Without
  that last line the calibration fits rates that compensate for eating the wrong food.
- **LOD:** expand 520 u around pilots + a 760-u cone ahead, absorb beyond 680, migrants spawn only > 330 u from any
  pilot. Sweep (`results/lod.json`, 2 seeds x 15 min):

| LOD radii (expand/ahead/absorb) | pop-ins | closest expansion to a pilot | cost ms (p95) | enc/min | quiet |
|---|---|---|---|---|---|
| 360 / 520 / 470 | **39** | 139 u | 13.2 (35.0) | 2.40 | 0.52 |
| **520 / 760 / 680** | **0** | 320 u | 15.1 (33.6) | 1.75 | 0.51 |
| 700 / 1000 / 900 | 0 | 527 u | 16.7 (31.5) | 1.95 | 0.44 |

  360 is cheaper and *feels* busier precisely because creatures materialise in view - the continuity law, broken.
  700 buys nothing 520 doesn't. 520 is the knee.

### Negative controls (`results/controls.json`; the recommended cell with one planted failure, 2 seeds x 12 min)

| metric | planted failure | control value | clean cell | fired |
|---|---|---|---|---|
| persistence | pack metabolism x6 | 2 extinct | 0 | yes |
| diversity (Shannon min) | grazers only | 0.49 | 1.81 | yes |
| oscillation without freeze | no fauna | freeze 0.50, flora saturated 0.72 | 0.00 | yes |
| mass audit | a birth that doesn't pay its body | 28,113 vol leaked | 3e-6 | yes |
| shielded never food | `eat()` bypasses the shield check | 1 shield eaten | 0 | yes |
| continuity | LOD radii 120/150/160 | 1,585 pop-ins, 1,864 pop-outs | 0 | yes |
| threat variety | packs only | 0.75 | 3.5 | yes |
| quiet time | 600 immortal packs | 0.00 | 0.43 | yes |
| emotional range | grazers only | 1.75 | 2.25 | yes |
| replayability | same seed twice | distance 0.0 / 0.0 / 0.0 | - | yes |

The clean cell fires none of them.

### Why iteration stopped

The last three rounds stopped moving the player numbers (enc 1.7-2.0, variety 2.9-3.3, quiet 0.53-0.64, median hits
2-3, emotions 2.5-2.9 across R6, R7, R8 and both finals - inside seed-to-seed spread), and the ecosystem numbers had
converged except one species. The remaining failure (thieves, below) is structural rather than a dial: two rounds of
thief dials only moved the extinction onto locusts, and the one fix that worked (R8) was a *model* fix whose effect
lasts 30 min, not 45. Spending more 45-min runs on thief dials is the wrong use of compute until the opening
transient is fixed (first bullet of "what would still move the numbers").

### Negatives worth keeping

- **The baseline was a siege.** Every round-1 pick at its own tuned numbers, in one cell: 1.4% quiet, 35 hits/min,
  thieves extinct 3/4. Each direction was tuned alone against a pilot; together they summed. Niche partition +
  hunger gating, not lower counts, fixed it.
- **Presence is not an encounter.** R1-R3 measured "threat within 200 u" and reported variety ~1.8 for a cell full of
  predators; counting only creatures in a striking state (R4) doubled variety and made quiet measurable.
- **The far cell was a different ecosystem** until it was calibrated: all-macro locusts 7x the micro count, lurkers
  0.16x. Any LOD scheme needs this consistency test; ours only looked fine because pilots expand the part you see.
- **`pack.a_attack` was inert during calibration** - a cfg ordering bug let the legacy `pack_attack` overwrite it.
  Fixed after the fit; the fitted value equals the default, so the fit stands, but the pack gap (16%) is unfitted.
- **Calibration hid a diet bug.** The macro level fed grazers and locusts one shared `FLORA|TRAIL|SKEL` list and
  thieves flora only; the fit compensated (grazer F_half 7.8 = a super-grazer) and the thieves starved behind it.
  A fit can only be trusted after the model it fits has been made the same model.
- **Thieves are the fragile link: 3 of 4 seeds extinct by ~33-39 min in the final run.** Starvation, not
  predation (52 of ~57 losses starved in every seed; packs no longer eat them). It happens in the **opening flora
  crash**: flora is seeded at its cap (28.8k vol) and the herbivore boom strips it to ~5k by minute 3; thieves, the
  slowest eaters, never refill. Seed 3 shows the counterfactual: thieves that got through the crash recovered from 12
  to 27.
- **Locust storms happen rarely and only to explorers** (one episode, spanning 4 consecutive flights, in 4 seeds x 45 min) - a real emergent event, but a cell
  that wants locust weather on purpose needs it more often.
- **Packs and lurkers still end at their caps** (120 / 60) in 45-min runs - food-limited for the first half hour,
  cap-limited after. The caps are doing work they shouldn't.
- **Soil N grows ~linearly** (60k -> 130k vol in 38 min): pilots' trail is a steady mass pump and nothing sinks N
  except flora growth, which is capped by the plant cap. Conserved, legal, but not an equilibrium.
- **Thieves barely register as encounters** (2 of 423): their theft happens to the pilot's wake behind them.
  They show up as steals (2.2/min) - a player sees them as their trail vanishing, not as a creature.
- **A wasted variant:** `r7_thief_metab` duplicated `r7_base` (the dial was already in `HIGHCAP`); kept, flagged.
- **One cohort per region, normal closure** - variance-only; a bimodal region (half fed, half starving) starves too
  slowly in macro. Not fixed.

### What would still move the numbers

1. **Seed flora at its grazed equilibrium, not at its cap** (or let herbivores bloom in from zero) - removes the
   opening crash that kills thieves and hides every other species' real dynamics for the first 5 minutes.
2. **A sink for soil N that is not a timer** - e.g. flora caps rising with N (more plants, not faster plants), so the
   trail pump becomes standing biomass the food web can graze.
3. **Prey-switching packs** (Holling III) instead of hunger gates - should take packs off their cap without making
   them sparse near pilots.
4. **Thieves that read as creatures:** their encounter is behind the pilot; a "chase back" moment (thief flees with a
   glowing prism you can recapture - already modelled, 0-5 recaptures per run) is the encounter waiting to be made
   legible.
5. Re-calibrate after R8 (the fit was done on the shared-diet model).

### Part 2 - multi-domain swarms (`domains.py`, `results/domains*.json`)

**Setup.** A 700-u nucleus-less cell, 3 domain territories with 40/20/20 plants (one leader), three pilots trailing
their own domain, a swarm (cap 150 / 600 / 1500, egg cost e_birth 8 / 12 / 16), 3 seeds x 20 min. Rules for a new
member's domain: **(a)** the domain of the mass that funded its egg (stomach-weighted pick; seeds in the controlling
colour; diet = opposing mass per member), **(a1)** the single largest donor, **(b)** parent's domain, **bmix**
(b with mixed-colour seeds), **(c)** the controlling colour at birth (today), **(d)** flips to the colour of the last
thing eaten. `*_blind` = same colour rule, colour-blind diet (= a nucleus cell's exterior). `none` = no swarm.
Metrics: Simpson diversity of member domains, fixation, leader's share of territory (mean/end; none = 0.41/0.38),
**comeback index** = trailing domains' share change / leader's share change under the swarm vs `none` (1 = neutral,
< 1 the swarm shelters the leader, > 1 it grazes the leader down), legibility (fraction of body-minutes in a 100-u
cluster that is not a < 10% sliver and whose colour mix turns over < 20%/min), audit (≤ 5e-9 everywhere).

| rule (cap 600, e_birth 12) | Simpson | fixed | leader share mean / end | comeback idx | legible | slivers | turnover/min |
|---|---|---|---|---|---|---|---|
| none (no swarm) | - | - | 0.41 / 0.38 | - | - | - | - |
| **(a) food** | 0.64 | 0/3 | 0.51 / 0.43 | **0.80** | 0.82 | 0.18 | 0.2% |
| (a1) largest donor | 0.64 | 0/3 | 0.53 / 0.44 | 0.74 | 0.88 | 0.12 | 0.3% |
| (b) parent | 0.00 | 3/3 at 60 s | 0.97 / 0.97 | 0.14 | 1.00 | 0 | 0 |
| bmix (parent, mixed seeds) | 0.65 | 0/3 | 0.48 / 0.44 | 0.80 | 0.88 | 0.12 | 0.0% |
| **(c) controller (today)** | 0.00 | 3/3 at 60 s | **0.97 / 0.97** | **0.14** | 1.00 | 0 | 0 |
| (d) last eaten | 0.66 | 0/3 | 0.48 / 0.42 | 0.79 | **0.03** | 0.16 | **73%** |
| blind (one colour, any diet) | 0.00 | 3/3 | 0.41 / 0.40 | 1.08 | 1.00 | 0 | 0 |
| a_blind (a's colour, any diet) | 0.42 | 0/3 | 0.41 / 0.41 | 1.06 | 0.66 | 0.35 | 0.0% |

Across sizes: (a) at cap 150 ci 0.95, cap 1500 ci 1.02 (but turnover 23%/min at e_birth 8 -> legible 0.12; 13% at
e_birth 16 -> legible 0.90). (c) at cap 150 is milder (leader 0.45, ci 0.41) because a small swarm can't hold the
leader's colour everywhere; at 600 and 1500 it is monochrome in the leader's colour and grazes only the trailers.

**Drift / founder effects.** With colour-blind diets nothing selects on colour, so any fixation would be pure drift.
None fixed in 20 min at any size: births are 75-455 per 20 min at caps 150/600 (< 1 generation), 4.5k-9.4k at
1500 (~5-10 generations); neutral fixation needs ~1.4·N generations, i.e. hours to days of sim time. At low turnover
the founder colour simply persists (a_blind Simpson 0.40-0.55 = the seed mix), at high turnover it mixes toward
0.66 (the max for three domains). **Drift is not a risk at swarm sizes and match lengths; founder colour is.**

**Recommendation: (a), with two UI conditions. The finding does not argue against (a); it argues hard against
keeping (c).**

- (c) is the strongest anti-comeback mechanic measured anywhere in this program: the swarm wears the leader's colour,
  therefore eats only the trailers' mass, and the leader's share goes 0.41 -> 0.97 (ci 0.14). In a nucleus-less cell
  today's rule hands the cell to whoever leads at the first wave.
- (a) cuts that to a mild shelter (leader share +0.10, ci 0.80) at every size, keeps three colours alive (Simpson
  0.64, never fixes), and in **nucleus cells** - where the exterior diet is any-domain - its effect on territory is
  nil (a_blind ci 1.06): there it is purely a colour/readability decision.
- **Costs of (a), measured:** (1) slivers - 15-18% of body-minutes sit in < 10% colour fragments; **DomainSlots must
  merge or hide slots under ~10%** of a swarm or the HUD will show noise. (2) Legibility collapses when turnover passes
  ~20%/min (high churn: big swarms with cheap eggs) - cap e_birth so a member lives > ~5 min, or show the swarm's
  colour as a smoothed mix rather than per-member. (3) The residual leader shelter (ci 0.80) in nucleus-less cells.
- (a1) is strictly worse (more shelter, ci 0.74). (d) is as balanced as (a) but **illegible** (73% of colours change
  every minute, 12k flips per run) - rejected. (b) with controller seeds is (c); bmix (parent, mixed seeds) matches (a)
  and is a cheap fallback if per-egg bookkeeping is a problem.

Note for the game session implementing (a) (`cece/swarm-fauna-game`, `Docs/SWARM_FAUNA.md` §16): the stomach-weighted
pick (a) beat the largest-donor pick (a1) on balance; and the legibility numbers above are per 100-u cluster, so a
DomainSlots row per 10% of the swarm is about the finest granularity a player can follow.

## Stakes in the flight sim (2026-10-05, overnight)

Gate (e) "it has stakes", made playable. `flight/src/60_stakes.js` mirrors `Docs/ELEMENTAL_ECONOMY.md` section 4 and
`VesselElementalDebuffByDangerPrismEffect.asset` (rules in `/mnt/project-files/overnight/burn-rules.md`): 4 elements x
10 petals, a hostile danger contact BURNS petals from all four for good (1 s cooldown), an own-cell contact is a 4 s
sting, every kill drops a crystal of its species' element worth +1 petal. The page has a petal flower, a rule picker
(tuned / shipped / off) and an owner toggle (O). `flight/stakes_eval.js`: every species in its fidelity world, 3
pilots x 6 seeds x 3 min, start 20 petals, both rules (`results/stakes_eval.json`).

| pooled over species (petals / min) | careless (wander) | skilled (evader, reader) | aggressive (hunter, cutter) | runs stripped | burns telegraphed |
|---|---|---|---|---|---|
| shipped (5 petals x 4) | 3.89 | 0.91 | 10.67 | 42% | 93% |
| tuned (1 petal x 4) | 3.25 | **0.22** | 6.04 | 21% | 92% |

- **Skill matters a lot**: under the tuned rule a skilled pilot loses 15x less than a careless one. Evaders take zero
  burns from pack, locust, stampede, leviathan, mobber; the lurker (ambush) is the one threat that still taxes them.
- **The shipped rule strips a careless pilot in seconds**: snap traps 5 s, pack 16 s, lurker 20 s, stampede 37 s
  (median time to 0 petals). Even a skilled pilot is stripped by the lurker in 5/6 runs (79 s). Tuned stretches the
  same to 57-139 s and never strips an evader. Careless burn/min looks similar under both rules only because a
  20-petal pilot runs dry; the strip time is the real difference. **Recommendation: tuned (0.1 per element).**
- **Fair**: 92-93% of burned petals were telegraphed (intent > 0.5 for >= 0.25 s before contact). Weak spots: snap
  trap vs the `reader` pilot 0.67, stampede vs hunter (tuned) 0.63.
- **Crystals pay back aggressive play**: hunters regain 2-27 petals/min from kills (leviathan hunter, shipped: burns
  32, regains 27). Pilots here never steer for crystals, so this is a floor.
- **Negatives**: thief, grazer, fortress (non-hunter) and mobber (tuned) burn nothing. The thief is by design (it
  steals wake, not petals); the others are decoration as threats unless the pilot attacks.

### Big creatures in the cell
The morphing tadpole swarm (`flight/creatures/swarm_creature.js`, phase-1 sort rule, frac 8) and the trained 3D NCA
lizard (`flight/creatures/nca_creature.js`, lizard3d_swim) now live in the flight cell (mode "creatures" and the whole
cell). The swarm grazes crystals and its body plan follows its diet (eat mass crystals, become a whale); it stalks a
nearby pilot with a building glow (>= 1.4 s), lunges, and its bite goes through the stakes (telegraphed in the
headless check). Ramming it knocks units loose (molting back in 1-2 s) and sheds a crystal. The lizard circles the
cell; flying through it cuts voxels that the NCA regrows, shedding time crystals.
- Swarm: own-plan loss 0.20 / 1.03 / 0.73 / 4.03 (whale, jellyfish, pufferfish, dragonfly; bar < 8), all 12 morphs
  under 8 in 0.7-1.3 s, **including dragonfly -> jellyfish, the switch phase 1 never solved**: it works because a
  molting unit may also change body region. 0.69 ms/frame at n = 1024.
- Lizard: sparse active set + a WebAssembly SIMD kernel took a step from 33.5 ms to ~2.6 ms (max diff 7e-6 vs the
  dense step; JS fallback ~19 ms). A 20% bite regrows to 90% in 21 steps (0.7 s at 30 steps/s).

## Siege (branch `cece/lab-siege`, 2026-10-05) - "surrounded, then they all dive in at once"

The scariest tier the lead named, built as a new flyable species: `flight/src/70_siege.js` (JS-original, no Python
twin: listed in `JS_ORIGINAL` so `fidelity_js.js` skips it; the ten ported species re-ran bit-identical, gate
unchanged). Mode `?mode=siege` in the page (not yet in the whole `cell`). Scored by `flight/siege_eval.js` (modelled
on `stakes_eval.js`, tuned rule 1 petal x 4 per contact, cost measured on a never-empty petal pool so stripping does
not hide later bites; `results/siege_eval*.json`). Screenshots: `flight/shots/siege_{close,hold_orbit,dive}.png`
(`siege_shot.py`).

**The shape.** 150 members, one shared phase machine. ROAM: a dim cloud stalks you ~430 u out, working round
*ahead* of your line (the pack's fan-ahead). GATHER (3 s): within 640 u it streams round you onto a 300 u shell
centred on a point that trails you (100 u/s); members flow *along the shell surface* to slots assigned near-side
first, so the shell fills from the cloud's side and the far cap stays open. That open cap is the **iris**: the
filled share of the sphere is scripted (0.6 at the end of GATHER, 0.8 at the end of CLOSE, 1.0 at the dive), so the
way out is a visible ring (drawn in the page) that shrinks to nothing. Unreleased members hover outside the rim as a
corona, not a wall. CLOSE (2.6 s): shell 300 -> 130 u, centre nearly stops, glow 0.55 -> 0.8 (the telegraph starts
here). HOLD (1.3 s): pulsing 0.8 -> 1. DIVE: every member at once at your predicted position. **The wall is a web**:
brushing within 35 u of a wall member once the glow is up starts the dive early from all sides. Then SCATTER and a
10 s cooldown (6 s after an escape): a 11-15 s rhythm, 4-5.6 encounters a minute. Committed members (CLOSE..SCATTER)
cannot be rammed, and simultaneous arrivals count as one bite per 0.25 s; both were bugs seen in the first page run
(a dive dropped 58 crystals = +58 petals, and 56 "hits" a dive).

**Numbers (6 seeds x 3 min, bestiary world, `results/siege_eval.json`):**

| pilot | petals / encounter | petals / min | clean escapes | dived | telegraphed | first-strike lead (median) |
|---|---|---|---|---|---|---|
| wander (careless) | 6.5 | 26.0 | 13% | 85% | 1.00 | 1.67 s |
| evader (skilled) | 0.40 | 2.2 | 89% | 6% | 1.00 | 1.38 s |
| hunter (aggressive) | 5.1 | 22.7 | 21% | 76% | 1.00 | 0.57 s |
| breaker (gap reader, new) | 0.32 | 1.8 | 91% | 4% | 1.00 | 1.30 s |

Skilled / careless = **0.09** (breaker 0.07). Every burn telegraphed (target > 0.8). Median lead of all burning bites
1.6-2.2 s. Careless and aggressive pilots are stripped of 20 petals in every run; skilled ones never. Conservation
residual 0. JS probe feel (wander): size 3.4, speed_rel 1.42, approach -1.2, coherence 0.95, burstiness 1.88,
jerk_rel 0.12 - very coherent, bursty, but `approach` is near zero because the shell stays at radius until the dive.
For scale (same tuned rule, 2 min x 3 seeds): pack wander 16/min, evader 0; locust wander 33/min, evader 0.

**Ablations (6 x 3 min) - what each rule buys:**
- `nobreach` (touching the wall does nothing): careless 1.3 petals/enc, 99% walk straight out through the wall. The
  web rule is what makes the shell a shell; without it the siege is decoration.
- `noiris` (every slot released at once, no scripted gap): breaker 2.2 petals/enc (7x worse), evader 3.3 - the
  counterplay disappears. The iris is what makes it fair.
- `nosync` (dive speeds spread 0.5-1.5x): careless 7.0 vs 6.5 - no change in petals, because the stakes' 1 s
  cooldown already merges a synchronized dive into ~1-2 burns. Synchrony is felt, not priced.

**Negatives and what failed on the way:**
- First version: evader WORSE than careless (3.3 vs 1.7 petals/enc). Fleeing the nearest member keeps you at the
  centre of a sphere, the one place the dive converges on. Fixed by publishing only the *wall* (seated members) as
  the threat, so "flee the wall" points at the gap - a design choice, so the evader number partly measures that.
- The roaming cloud published as a threat drove the evader to the membrane, where the shell clipped (fill 0.43)
  and it was dived every time. A roaming cloud now publishes nothing.
- With the cloud stalking behind you, the iris opened straight ahead and the careless pilot escaped 64% by flying
  straight; stalking ahead (LEAD) fixed that. The way out is now usually behind or beside you, so it costs a turn.
- Lunges alone (bite on contact while crossing the wall) missed fast pilots: members could not accelerate in time.
- The hunter (rams) still pays 5.1 petals/enc: ramming is not a counter to a siege.
- The breaker is an ideal gap reader; a human with 2 rad/s turn may sit between evader and wander. Unflown by a human.

**Open:** add to the whole `cell` (density with 9 other species untested); give the shell its own sound (silence
then a roar at the dive); a human rating of `terrifying`; whether a 4 s telegraph stays scary once learned.

## Fair burns (2026-10-05)
The weak telegraph cells above (snap trap vs `reader` 0.67, stampede vs hunter 0.63, pack wander 0.83, lurker wander
0.83) were traced one unread burn at a time: for each, what the striker's own intent, distance and closing speed were
in the second before (`/home/claude/scratch/tele/diag.js`-style trace; `stakes_eval.js` now takes `--species` and `--out`).
- **Half of it was the metric.** `TeleTrack` judged every strike by the species' agent NEAREST the player. With many
  agents near, a strike by agent B was scored on agent A's intent: a snap trap priming 1 s in plain view, scored on
  the idle trap 3 u closer; lurker and fortress strikes likewise. Fixed: `Arena.hit(p, kind, amt, who, standing)` now
  names the striker (instrumentation only, no behaviour change), and a strike is telegraphed when THAT agent's own
  intent was > 0.5 for >= 0.25 s (reset below 0.2). A snap trap's rim teeth are a standing danger prism, always drawn
  hot: they count as telegraphed once they have existed >= 0.25 s. The nearest-agent rule remains only as a fallback
  for strikes with no named agent (the page's tadpole swarm, a leviathan gulp). Metric fix alone: lurker wander
  0.83 -> 1.00, fortress hunter 0.86 -> 1.00, snap trap wander 0.87 -> 0.97; pooled 0.93/0.92 -> 0.95/0.94.
- **Real cause 1, stampede / pack: a strike in the same frame as its telegraph.** Against a ramming hunter, a calm
  animal bolted (speed > 60, heading at you -> intent 1) and gored in that same step; intent was 0 the whole second
  before. Pack hunters bit a hunter who flew into them at intent 0, and against the wanderer a wolf's closure (the
  ring) collapsed below 0.2 as the pack converged, so the telegraph switched off 0.1 s before the bite. Fix (Python
  `bestiary/species/pack.py`, `stampede.py` and the JS port, `WINDUP` exported through params.json): **a strike lands
  only after the striker's own intent has shown for 0.4 s** (`wind` accumulates while intent > 0.5, resets below 0.2,
  the tracker's hysteresis). 0.3 s was tried first and left strikes at a measured lead of 0.23 s: the tracker sees an
  intent one frame after the species sets it and the hit is stamped at the start of its step, so 2 frames of lag.
- **Real cause 2, snap trap vs reader: a spawn on the teeth.** The reader's one unread burn (20 petals shipped) was at
  t = 0.1 s: seed 4 drops the pilot onto a trap's rim. Fix (stakes layer, not the species): **1 s spawn grace**
  (`STAKES_RULES.*.grace`, `Stakes({spawn})`; the page passes the respawn time). Not in the shipped asset; a proposal.
- Gates: fidelity (pack, stampede re-run on both sides, dt 0.1 and 1/30): page verdict PASS for all 10, negative
  controls still fail. The dt-0.1 Python vs dt-1/30 JS cross-step row still fails for stampede (it did before).
  Bestiary scorecards: pack telegraph_first 0.9 -> 0.9, unwarned 0.16 -> 0.12, hits/min wander 6.0 -> 5.6;
  stampede telegraph_first **0.7 -> 0.9** (was exactly on the bar), unwarned 0.33 -> 0.23, hits/min unchanged;
  counterplay, variety, payoff unchanged, all four verdicts pass for both.

| stakes_eval, 3 min x 6 seeds | read before | read after | burn/min before -> after |
|---|---|---|---|
| snap trap / reader, shipped | 0.67 | 1.00 | 3.33 -> 2.22 |
| snap trap / reader, tuned | 0.67 | 1.00 | 0.67 -> 0.44 |
| stampede / hunter, tuned | 0.63 | 1.00 | 9.44 -> 9.05 |
| pack / wander, shipped | 0.83 | 1.00 | 6.67 -> 6.67 |
| pack / hunter, tuned | 0.71 | 1.00 | 4.67 -> 3.56 |
| lurker / wander, shipped | 0.83 | 1.00 | 6.67 -> 6.67 |
| pooled, shipped / tuned | 0.93 / 0.92 | **1.00 / 1.00** | skilled 0.91 -> 0.80, 0.22 -> 0.20 |

- Every species x pilot cell is now >= 0.8 (all 1.00). The wind-up mostly delays strikes rather than preventing them:
  careless burn rates and strip times are unchanged, so the threat survives; a pilot who reads gets 0.4 s to act.
- **Negatives kept**: lurker under the shipped rule still taxes the skilled evader almost as hard as the careless
  wanderer (5.56 vs 6.67 petals/min; tuned 1.55 vs 5.78), so "skill clearly matters" fails there under shipped. Its
  burns are all telegraphed (the gape is a real 0.7 s+ wind-up); the evader just cannot outrun an ambush it flies
  into. 100% read is also the ceiling of this metric, not proof of readability: it says the striker's own intent was
  up for 0.25 s, not that a human could see that agent among the others.

### Lurker counterplay (2026-10-05)
The lurker taxed the skilled evader almost like the careless wanderer (shipped 5.56 vs 6.67 petals/min, evader
stripped 5/6; tuned 1.55 vs 5.78). Traced bite by bite (`stakes_eval` world, seeds 100-105): the evader DID read every
gape (it fled the striker in 96% of the gape's frames; all 8 bites telegraphed, gape 1.1-1.2 s), the gape started
170 u **dead ahead** (cos 0.93; the lurker creeps onto the point you will pass in 3 s, by design), and by the snap the
pilot was still 60-90 u away, now broadside. Frame trace: while "fleeing", the evader kept flying straight in at
140 u/s - its angle to its own goal went 166 -> 123 deg in a second.
- **Cause: the pilot, not the creature's tell.** The shared pilot turn (`Arena.step` / `00_core.js`) is a chord lerp
  `v + (w - v) * turn*dt/ang`; its real rate is ~`turn * sin(ang)/ang`, so 2 rad/s at small angles, ~1.3 at 90 deg
  and **~0.2 rad/s near 180 deg**. The evader aimed straight AWAY, i.e. straight behind for a threat ahead: a U-turn
  that would take ~10 s. Against threats that come from the side or behind (pack, stampede, siege) the lerp is fine;
  the ambush on your line is the one case it cannot answer. A human would bank sideways - the lurker card's own
  counter reads "swerve off your line".
- **Fix (evader policy, Python `common/arena.py` `_pilot_goal` and JS `pilotGoal`, identical):** a threat AHEAD
  (away . v < 0) is fled along the component of `away` perpendicular to the velocity (dead-on: `cross(v, up)`), so the
  pilot turns at its real rate; once the threat is abeam it flees straight away as before. The turn function itself
  is untouched (changing it would move every policy and every scorecard).
- **Creature-side fixes tried first and REJECTED (negatives kept):** with the old evader, giving the lurker an earlier,
  held gape and a commit range (snap only once the gape is full AND the pilot is inside `STRIKE`) made the evader
  WORSE: SENSE 170/HOLD 230/STRIKE 120 -> 0.61 bites/min (was 0.39), SENSE 240/HOLD 300/STRIKE 120 -> 1.56, 260/320/110
  -> 1.11. An earlier tell only starts a doomed reversal earlier, and a held mouth keeps the pilot "fleeing" straight
  into it. With the swerve the same variants all gave the evader 0-0.11 bites/min, so the lurker needs no change: it
  stays exactly as scored (Python and JS lurker untouched).
- **Numbers (`stakes_eval.js`, 6 seeds x 3 min, `results/stakes_eval.json`):**

| lurker | wander (careless) | evader before | evader after | hunter | evader / wander |
|---|---|---|---|---|---|
| shipped (petals/min, stripped) | 6.67, 6/6 | 5.56, 5/6 | **1.11, 1/6** | 11.39 | 0.83 -> **0.17** |
| tuned | 5.78, 4/6 | 1.55, 0/6 | **0.22, 0/6** | 8.89 | 0.27 -> **0.04** |

  Wander and hunter rows are bit-identical (the change touches only the evader). Every burn still telegraphed (1.00).
  Elsewhere: mobber evader shipped 0.22 -> 0; nothing else moved. Pooled skilled: shipped 0.80 -> 0.30, tuned 0.20 ->
  0.06 (pooled careless rose 3.89 -> 4.14 only because siege now joins the pool).
- Gates: fidelity re-run on both sides for all species (dt 0.1 and 1/30): verdicts identical to before (port row: all
  10 PASS; both-at-1/30 row: lurker PASS, leviathan passes at 24 seeds; cross-step row fails as before). Bestiary
  scorecards re-run for all 8: lurker unchanged (telegraph 0.95 s, counterplay 0.0, variety 2.0, payoff 6.22/min, all
  four verdicts pass; in that world the evader was already never bitten), thief evader hits 10.67 -> 10.44, the rest
  identical. `browser_test.py`: no fails.
- Still open: the lerp turn makes every scripted pilot slow to reverse (a wanderer re-targeting behind itself, a hunter
  overshooting); a constant-rate slerp would be truer to a ship but re-baselines every scorecard. The evader also flees
  only the NEAREST published threat, and a spent (harmless, grey) lurker is published as one: once (seed 104) the
  evader fled a spent lurker into a gaping one.

**Siege re-measured after the evader's sideways-flee fix (Lurker counterplay):** wander 6.5 / evader **0.0** / hunter
5.1 / breaker 0.32 petals per encounter; the evader escapes 99% of shells (was 89%). The siege now costs a pilot who
turns off-line nothing at all, so it is purely a test of noticing. If playtest says it is too easy for a skilled
human, the lever is the iris (narrower, or it drifts toward the pilot's escape side), not the dive.

## Living cell, round 2 (Direction G follow-up, branch `cece/eco-living-cell-r2`, 2026-10-08)

**Question.** Round 1 left four open items: (1) thieves die out in 3 of 4 seeds, (2) packs and lurkers end at their
population caps, (3) soil nutrient N climbs linearly, (4) the macro (far-from-pilot) rates were fitted before R8's
diet fix. Plus the lead's rule (2026-10-06): **every life form must be able to feed AND reproduce**.
Code: same folder; every configuration is in `rounds.py` (R9-R16, `CAL2`, `FINAL2`) with its result written
above it; results `results/r2_*.json`; rerun with the round-2 block of `run_all.sh`.

### Headline (recommended cell `rounds.FINAL2`, 4 seeds x 45 min, `results/r2_final.json`)

| | round 1 final | **round 2 final** |
|---|---|---|
| extinctions (species-seeds) | thief 3/4 | **0** |
| thief births / starved | ~11 / 52 per seed | **163 / 115 per seed** (they breed) |
| samples at cap: pack / lurker / thief | pack + lurker pinned after ~30 min | **0% / 7% (late, at a 300 backstop) / 91% (colony size, see below)** |
| packs at minute 44 | 120 (cap) | **43-74, food-limited** |
| soil N slope (last half) vs pilots' trail input | ~100% (60k -> 138k) | **2%** (flat at ~70k; plants 150 -> 546) |
| Shannon mean / min | 1.87 / 1.68 | 1.87 / 1.53 |
| audit / shielded eaten / pop-ins | 1.1e-5 / 0 / 0 | 9e-6 / 0 / 0 |
| cost ms/tick mean (p95) | 14.8 (34.7) | 12.7 (28.6) |
| encounters/min, variety, emotions | 1.76, 3.1, 2.7 | 1.10, 2.7, 2.7 |
| **quiet fraction** | **0.60** | **0.38** (explorer median 0.32, wanderer 0.41) |
| hits/min median, steals/min | 2.0, 2.2 | 3.1, 12.3 |

All 10 negative controls fire on `FINAL2`; the clean cell fires none (`results/r2_controls.json`). The freeze
control was re-planted (a fauna-less cell seeded near its cap): with grazed-level seeding a fauna-less cell is
still growing at minute 12, so the old planted failure no longer was one.

**The ecosystem goals are met; the player numbers got worse.** The cell is busier: more thieves tail you, more
lurkers wait at the flowers, quiet fell from 60% to 38%. That is the trade to decide on (see "what would move").

### What each item turned out to be

1. **Thieves: a behaviour bug, not the opening crash.** R9 seeded flora at its grazed level (13% of cap, grazers
   400, locusts 250): the opening crash vanished (flora 6k -> 2-3k -> 6k instead of 29k -> 4k) but thieves still
   fell 45 -> ~5 by minute 30. An instrumented run showed why: a thief near a pilot tailed it until it starved, and
   a thief only ate its larder while under 40% of e_birth, so a fed thief never reached e_birth and **never bred**
   (28 births in 3 x 30 min). `thief.feed_fix`: a starving thief goes home; at the nest the larder feeds it up to
   e_max at its intake rate. R11: 400-630 births per 3 runs, no extinction. (R10's alternative, making thieves the
   carrion eaters, starved the grazers and the lurkers with them and did not help the thieves.)
2. **Pack caps were hiding a collapse.** Uncapped (R9 `nocap`), packs overshot to 230 and ate grazers and locusts
   out. Diagnosis: 1326 of 1331 pack kills were *near pilots* (micro) - a hungry pack killed about one prey a second
   with no handling time and turned every 3 kills into a pup. Lowering a pack's share of a kill (`pack.eff`, the
   rest left as a carcass) then exposed a second lifecycle bug: packs hunted only below 0.6 e_max = 54, under
   e_birth = 70, so they **never bred** (R11: 0-1 births). Hunting prey up to 0.85 e_max (pilots are still stalked
   only below 0.6) made them breed - and switched on the *far* packs, which had been sitting at a mean stomach of
   ~55, just above the gate, and almost never hunted: R12 collapsed every seed by minute 3. Round 1's "pack.a_attack
   is inert in the fit" was this. The working pack (`PACK2`, designed in all-micro runs, `micro_design.py`,
   `results/r2_micro_design.txt`): 40-s handling time on a kill (the macro `h_handle`), Holling III switching
   (commit to a chase only with >= 5 prey within 300 u), keep 35% of a kill, far packs search their 6 neighbour
   regions (a 300-u sense spans them; one region alone gave 65 far kills in 10 min at 100x the attack rate).
   **Metabolism is then the pack dial** (R14): 0.06 climbs to 77-133 by minute 30, **0.10 levels at 36-64**, 0.14
   declines. Packs no longer eat thieves: with them, thieves die in 2 of 3 seeds (R13).
3. **Soil N: plant recruitment.** Plants seed new plants at a rate proportional to N above 60k (`flora_recruit`
   1.0; parent weighted by standing crop, paid from N). N goes flat (R13: -9% of input; final +2%); recruit 3.0
   draws it down. **The catch, and it is conservation, not a bug:** pilots' trail is the only source and pilot rams
   the only sink, so the mass has to accumulate somewhere. It now accumulates as living biomass: plants 150 -> 546,
   flora 6k -> 20-38k, and every level above grows with it - locusts swing to 1200-1600, lurkers climb to 230-300
   by minute 44 (their backstop, 7% of samples). A longer session will need a real sink (pilots harvesting flora,
   or a cap on plants) - a game decision, not an ecology dial.
4. **Refit** (`calibrate.py CAL2`, micro truth `r2_consistency.json` = 3 seeds x 15 min all-micro): mean log error
   0.95 -> 0.18. End gaps: lurker 4%, locust 46%, pack 71% (far packs 11 vs 38 near), grazer 94% (far 369 vs 191).
   Thieves are no longer fitted: the fit has no pilots, so micro thieves lose their real food and starve, and the
   fit taught the macro thief to starve too (F_half 1200 in round 1, 9,600 in the first round-2 fit).

### Thieves are set by their colony size (stated, not hidden)

With a pilot to rob, thieves are never food-limited: in R15 with no predator they filled the 150 cap 44% of the
time (steals 37/min, quiet 0.28); with lurkers or packs eating them, seed 3 (nests off the pilots' routes) loses
its thieves by minute 16. `FINAL2` gives each of the 3 nests a colony of 25 (like the fortress's 64 workers) and a
magpie that raids only while its nest's hoard is short (`thief.hoard_target` 10; R16: steals 21 -> 14/min, quiet
0.33 -> 0.46 at 30 min). They breed and starve at the nest limit; that number is a design choice.

### Negatives worth keeping

- R10 (carrion partition, Holling III on lurkers): lurkers extinct in every seed, thieves unhelped.
- R12: one threshold change collapsed the whole cell in 3 minutes - the far level had been carried by a gate.
- The first refit could not keep far packs alive at any attack rate (they never met prey in one region).
- `r11_eff03_nocap` logged 1 pop-in (the only one in round 2).
- The physarum is barely eating: 19-523 vol digested in 45 min, reserve down to 4-8 vol by minute 45 (it keeps
  its 330-370 tubes). Grazers clear the flora under it first. By the feed-and-reproduce rule it is the next fix.
- R10's Holling III lurker variants used the first macro switch formula (own region only); later runs use the
  sense-volume-scaled one.

### What would still move the numbers

1. **Quiet time** is the regression: fewer tailing thieves (colony 10-15 per nest) or lurkers that cannot climb
   with the herbivore base (lurker metabolism, or ambush seats limited to flowers) should buy back most of it.
2. **A sink for long sessions** (see item 3): pilots harvesting flora would close the ledger without a timer.
3. **Physarum feeding** (above).
4. The far grazers run ~2x the near ones at minute 15 (fit end gap 94%): a grazer-specific far-level fix.
