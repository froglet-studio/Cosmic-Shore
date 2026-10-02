# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.

## Emotion probe (Direction C, `cece/eco-emotion`, 2026-10-02 night)

**Question:** can "cute / playful / eerie / majestic / menacing / terrifying" be measured from motion and size
alone, and then used as a search target? **Answer: yes, with stated limits.**
- **Measuring works:** a 32-feature, literature-grounded extractor plus an interpretable two-judge probe
  recognises an emotion from a *mechanism it has never seen* 75% of the time (chance is 1/7).
- **Searching works:** one 20-parameter creature family under game laws reaches every emotion, by
  parameters alone.

The headline game finding: **today's fauna mostly evoke nothing**. They read as neutral background, because
none of them attends to the pilot. Code and how to run: `emotion/README.md`. Sources: `emotion/LITERATURE.md`.

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

### Today's game species (`species.py`, every constant quoted from a shipped asset)
Probe v3, 4 seeds × 3 viewers.

| species | hovering pilot | cruising pilot | evading pilot |
|---|---|---|---|
| worm colony, as encountered | neutral | neutral | neutral |
| worm attack cycle, isolated (hunt window forced on) | **eerie** | neutral | neutral |
| shark, radius 30 (assumed) | menacing (judges agree 0.25) | majestic | neutral |
| shark, radius 60 | majestic | majestic | majestic |
| tadpole flock | **cute** 0.79 | cute | neutral |
| swarm whale / dragonfly | eerie / neutral | neutral | neutral |

- **The worm can only threaten a parked pilot.** In 5 of 6 runs it never left cruise. It grazes toward food,
  hunts only 12 s in every 26 s, and only engages a pilot within 220 u. Its 26 u/s pursuit and 70 u/s lunge
  can never reach a 90 u/s ship. Its isolated attack cycle reads *eerie* rather than menacing, because the
  follow-the-leader segments move in lockstep (synchrony) and the head faces you.
- **The shark hunts prey fauna and never targets pilots**, so it can never read as a threat to one. Only its
  size makes it majestic.
- **The fauna are background, not antagonists.** No game species *attends* to the pilot: tracking is low,
  pursuit near zero, gaze not on you. That is exactly what the program's direction B has to change.
- Judge agreement on game species is lower (0–1.0 per run) than on held-out archetypes (0.84). Real species
  sit outside the reference set, so treat low-agreement readings as uncertain.

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

Hovering pilot; one letter per window (n neutral, c cute, p playful, e eerie, m majestic, t terrifying):

| species | windows | peak threat | mean threat |
|---|---|---|---|
| bestiary leviathan, seed 301 | `mmmmmmmmmeeeeette` | 0.48 | 0.23 |
| bestiary stampede, seed 301 | `nnnnnnnnntttttmnn` | **0.74** | 0.26 |
| bestiary lurker, seed 301 | `nnnneppcppppppttt` | 0.48 | 0.27 |
| bestiary locust, seed 301 | `nnnnnnnnnnnncccct` | 0.79 | 0.06 |
| game worm attack cycle | eerie in 15 of 17 windows | **0.095** | 0.07 |

- **The leviathan plays exactly its stated arc** ("majestic awe → dread up close"): majestic → eerie →
  terrifying as it closes.
- **The stampede's panic is a terror spike** inside an otherwise neutral encounter.
- **The lurker's snap is visible** as terrifying windows at the end.
- **The worm's telegraph and lunge never read as a threat in any window.** That is the clearest game
  finding of the night: the shipped kaiju attack reads *eerie* (lockstep body, facing you), and the lunge is
  too slow (70 u/s) and too brief to register as looming.
- **Recommendation:** report `peak_threat` alongside the mean for any ambush or burst species.

### Negatives (kept)
1. **The old scorecard's feel stats separate the six emotions at 0.32 LOFO.** Nearly all the signal is in
   the new relational features.
2. **v1 called a radius-1.5 dot "majestic"**, and the search found it. With six forced choices, a small thing
   that ignores you has nowhere to go but majestic. v1 read **every one of 5 neutral background families as
   majestic (12/12)**.
   - Fix: a NEUTRAL class. Under v3 the dot reads neutral 6/6, and majestic is carried by size first:
     awe needs vastness (Keltner & Haidt 2003).
   - It changed the game verdict from "majestic" to "neutral". *An affect probe needs a "nothing" class, or
     indifference reads as awe.*
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
9. **A headless viewer test caught a load-order bug** (the panel read `RUNS` before it existed) and a
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
