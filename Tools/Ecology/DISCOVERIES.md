# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.

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
