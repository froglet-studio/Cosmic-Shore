# Living Ecology — discoveries

Program: `Tools/Ecology/PROGRAM.md`. Shared harness: `Tools/Ecology/common/` (arena + scripted pilots +
conserved mass, threat/feel scorecard, playback viewer). Template: `examples/demo_pack.py`.

## 2026-10-02 03:50 UTC — program opened
- Harness smoke: a naive 24-agent chase pack at 100 u/s never catches a 120 u/s wandering pilot (0 hits/min),
  and a hunting pilot kills 15.5/min. That's the baseline any real pack-hunter has to beat: a pack that only
  chases is no threat to anything faster than it. A real threat needs coordination (cut-offs, encirclement,
  ambush), not raw speed.


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

