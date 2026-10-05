# Discoveries — every swarm process family, so nothing is lost

The day's research ran about forty sessions across thirty-four branches (`cece/swarm-*`) plus the
main branch's history. Sessions get archived and their context condensed, so this file is the
durable record. **Part A–C below are full reports**, mined read-only from every branch on
2026-10-01 ~16:00 UTC, with evidence (numbers, commits, files) for each item. This page is the
index and the cross-family synthesis.

**Why it is organised by process family.** The lead's direction: *speciate* the process. Find
several distinct families of lifeform-discovery process and keep them distinct. A superset that
unifies their capabilities is the LAST step, and only if possible. Orthogonal processes that stay
separate are a fine outcome. Several lifeforms may ship.

## End of day, 2026-10-01 (23:50 UTC) — where the swarm stands

**The answer to "performant, lossless, emergent" is: a designed composition controller + a local shape rule +
a fractional update.** Four front runners clear every scorecard axis, three are held, and one has already been
made 2.25x lighter AND much smoother without giving anything up.

| front runner | what it is | accuracy (loss-8 bar, 3 samples) | lossless | organic | smoothness | ms/step (Python, 1 thr) | held |
|---|---|---|---|---|---|---|---|
| **sortfeel, frac = 8** (`lite_sortfeel:results/lite_sortfeel/params.json?frac=8,vec_look=1`) | emergent cell sorting, flat-bottom wells + wander, 1/8 of the swarm re-steers per step | holds sortfeel (12/12/13/13 of 13 at 7/23/41/101) | yes | yes | **0.77** | **1.28** | PASSES the hold |
| combo (G8) | grid morphogen made lossless (molting, ratio targets, orphan steering) | 16/15/15/15 of 16 | yes | yes | 0.51 | 8.4 (7.5 at frac 2, holds) | `results/hold/combo.json` |
| posinfo2 | LEARNED rule + designed molting controller | 13/12/13/13 of 13 | yes | yes | 0.50 | 7.0 | `results/hold/posinfo2.json` |
| evofate | the evolved rule (the lead's "beautifully organic") + fate wells | 12/11/12 of 13 | yes | yes (edge at seed 23) | — | 5.0 | not held |

**What the day established:**
1. **Composition is designed, shape is local.** Every model that passes tier 1 pairs a designed controller (who is
   laid, who molts into what) with a local shape rule. Learned composition never got there; learned SHAPE did
   (posinfo2 - the tightest own-plan losses of any model, 1.6-2.7).
2. **Lossless is a molt, not a death.** Replacing starvation with molting (a surplus tadpole re-forms its crystal into
   the element its own team is short of) kept hgrid2's accuracy (combo) and is now in the C# grid core.
3. **A fractional update is cheaper AND smoother** (round 5). Coasting on a slightly stale intent is a low-pass: it
   eases a change in instead of jolting. sortfeel at 1/8 re-steering: smoothness 0.44 -> 0.77 (above evo's 0.58),
   2.25x faster, accuracy held. What it buys depends on where the time goes: half of sortfeel's step is all-pairs
   neighbours (cut 8x); half of combo's is a coarse grid every tadpole pays (frac 2 holds; frac 4 teleports one
   tadpole on one event); posinfo2 is a LEARNED rule trained at a 1/2 firing rate - a different schedule needs
   retraining, it cannot be bolted on (k = 4 leaves the organic band).
4. **The hold must be calibrated by a noise control.** lite_posinfo2 ran the unchanged rule with its random stream
   shifted by one draw per step, and it FAILED the first hold: per-seed "no lost test", own losses +15% and
   smoothness -0.05 were inside the noise of one realisation. `hold.py` now gates on robust tests, summed passes
   with an allowance for marginal ones, seed-averaged own losses and smoothness -0.08; the noise control passes
   and real regressions (a lost organic band, a collapsed student) still fail (`results/hold/verdicts_v2.json`).
5. **Distilling a learned rule fails in closed loop** (R^2 0.98 per step, 0/52 grown); DAgger converges (error
   30-98 -> 7-13 in two rounds) but needs the hold as its training signal.

**Open problems, in order:**
1. **dragonfly -> jellyfish** is the one switch every lossless model misses or barely makes (loss 7.4-10.4). Its
   floor is the orphan third team: a perfect jellyfish with a fraction f of its units on a third team costs ~25 f,
   so a lawful corrector must keep the orphan team under ~1/3 of the body or give it a place of its own (a design
   call: an appendage, or a bud-off as a new colony).
2. **The jolt.** combo, sortfeel and posinfo2 spike to 4-7x their own pace right after a cull or strike (evo eases
   in at ~1.7). The fractional update fixed it for sortfeel; a ramped corrector gain is the general fix.
3. **Hold smoothness at one seed** - it needs 2-3 seeds before it can gate a small change.
4. **The game** (cece/swarm-fauna-game): grid (combo rules), sort and evofate species are ported to C#; the
   recommended next port is sortfeel's flat wells + wander + the frac 8 schedule into SwarmSortCore. Round 5 there
   needs an editor pass (compile, JSON import, visuals, Mono cost; QA-SWARM-ROUND5).
   **Round 6 (01:38 UTC) shipped that port.** SwarmSortCore now runs sortfeel with 1-in-8 updates. The shipped assets
   carry SortUpdateFraction 8 and SortNoise 0.1; the session's own summary text said 4 and 0.03, so confirm the
   values in the inspector. Results in CoreCLR:
   - 52/52 in game mode, 0 deaths. Research-mode C# reproduces Python: the held config scores 49/52.
   - Organic: planar excess 0.015. Round 5 measured 0.280, which was outside the band.
   - Smoothness 0.917 over 3 seeds; round 5 scored 0.751.
   - 0.055 ms per swarm-step at B16, against 0.149 in round 5: 2.8x cheaper, 0.43 us per tadpole.
   - Colliders unchanged at 5,008.
   Mono cost is unmeasured. Before trusting these numbers, it needs the QA-SWARM-ROUND6 editor pass: compile and
   import, confirm the 5 new config values, a side-by-side against the old settings, a burst-kill morph, a
   fly-through flinch check, and Mono frame cost.

## The yardstick, now

16 transitions: the 4 own plans, plus every one of the 12 majority switches with a fair cull
(`swarm_eval.cull_to`). Each test is run 3 times and passes on a majority. A test also fails
whenever its divergence to the wanted plan is over **8** (`swarm_nca.MAX_TEST_LOSS`, default
`LossCfg`), and tier 2 runs only if all four own plans pass. Under this bar:

| model | own-plan losses (whale / jelly / puffer / dragonfly) | 16-test |
|---|---|---|
| field (designed) | **3.1 / 1.6 / 1.4 / 4.6** | 9/13 feasible (switched bodies land at 9–12 on four transitions) |
| hgrid oracle / G2+grid | 8–17 | 1/15, 1/16 |
| evo, evo compact, G2, H3, every gradient run | 15–30 | 0/16 |

**The diagnosis that sets the next round** (`runs/diag_terms.py`, `runs/diag_comp.py`, reproduced
independently in Part A D27–D28). The learned and evolved bodies have the right OUTLINE: position
alone costs 2.3–4.5. The loss comes from SORTING. Element placement adds 4–10 and domain regions add
1–6. Trimming to the plan's headcount and mix barely helps (13–24 → 12–18). Own-plan loss ranks
exactly with how GLOBAL each family's sorting mechanism is: global slot assignment (field) <
per-class grid deficits (hgrid) < per-group anchors (compact) ≈ none (G2, evo).

## Update 23:10 UTC — round 5 interim: a fractional update makes the front runners cheaper AND smoother

All three `lite_*` sessions recorded their holds first (`results/hold/{combo,sortfeel,posinfo2}.json`) and then
measured candidates against them. Interim, mid-session; the final harvest is at ~23:45.

| candidate | ms/step (hold, 1 thread) | smoothness | hold verdict (gate as of 23:10) |
|---|---|---|---|
| **lite_sortfeel `frac=4, vec_look=1`** | **1.52 vs 2.88 (1.9x)** | 0.436 vs 0.44 | one MARGINAL test lost: mass -> time at seed 7 (a 2-of-3 test at loss 7.9 in the baseline) |
| **lite_combo `frac_k=4`** | 6.9 vs 8.4 | **0.60 vs 0.51** (worst lurch 6.3 -> 4.5) | teleport 1.73 vs 1.5 on one event (charge -> time); the coast-decay repair is built |
| lite_posinfo2 distilled student (k4, H 8, f4) | 4.7 vs 7.0 | 0.52 vs 0.50 | loses real switches at seeds 7 and 101 - the small network is not there yet |

Findings so far:
1. **Coasting is a low-pass, and that is the smoothness the lead asked for.** combo with 1/4 of its tadpoles
   re-steering per step rose to 0.60 (every event's lurch fell; the mass heal's 6.26 -> 2.54): a tadpole coasting
   on a slightly stale intent eases into a change instead of jolting. The fractional update is not only cheaper.
2. **Where the time goes differs by family, so the same trick buys different amounts.** sortfeel is half an
   all-pairs neighbour block (18,349 pair evaluations per step): updating 1/4 of the swarm cuts pairs 4x and the
   step 2x; past k = 8 the O(N) work dominates and the curve is flat (1.29 ms at k = 8 and 16). combo is half a
   coarse-grid step that every tadpole pays every step and that does not depend on N (Python per-op overhead on a
   G = 8 grid); its fractional fine layer saves only 6% at B = 1 and 19% at B = 16 until the grid is amortised
   too.
3. **Cost per swarm is flat in B** for both (no cross-swarm batching yet): each cell's swarm is its own core in
   the game, and the fractional update composes with that.
4. **The hold's teleport gate was stricter than the smoothness calibration**: it allowed max(1.0, +10%) of the
   world's top speed where swarm_smooth's documented comfort range is 1.5x (a fast Time swimmer plus a collision
   push). Fixed in hold.py to max(TELEPORT_OK = 1.5, +10%); the verdicts above are recomputed under it.

## Update 22:20 UTC — HOLD the front runners, score the smoothness of change, then go light

The lead (22:00): *"Once a front runner starts scoring well enough we will want to hold its scores
including a score for smoothness of changes. Then optimize for the lightest weight or most computationally
performant and scalable method. One thing we did before is only update a fraction of a larger swarm each
frame."* Three new pieces, all on this branch:

- **`swarm_smooth.py` — smoothness of CHANGE.** `swarm_feel` scores a body that has already grown; this
  scores how it gets from one form to the next: the 4 standard switches and a vessel strike on every plan,
  recorded step by step. Per event: `backtrack` (uphill loss beyond the settled body's own wiggle), `lurch`
  (the worst step's p95 speed over the change's own median pace - a jolt inside the change), `teleport`
  (largest single step over the world's top speed), `jerk_rel`, `molt_burst` / `birth_burst` (share of the
  body changing element / hatching inside 8 steps - a pop), deaths. `smoothness` = 1/(1 + mean rough).
  Calibrated over three passes (the first cuts mistook a heal's swimming wiggle for backtracking and a
  speed-capped Time tadpole for a teleport; both corrected and documented in the file).
- **`hold.py` — the hold.** `--record` freezes ACCURATE (seeds 7/23/41/101, 3 samples, loss-8 bar, per
  test), LOSSLESS, ORGANIC and SMOOTH; `--baseline` fails a candidate on any regression and reports cost,
  which is what the next stage optimises.
- **Calibration** (`results/hold/calibration/`, seed 7):

  | model | smoothness | worst lurch | worst molt / birth burst | backtrack | note |
  |---|---|---|---|---|---|
  | evo (the lead's "beautifully organic") | **0.58** | 2.3 | 0.00 / 0.43 | 0.12 (its heals do not heal) | eases in |
  | combo | 0.51 | 6.3 | 0.18 / 0.29 | 0 | jolts after a cull / strike |
  | field ("its mistakes feel like bugs") | 0.51 | 4.4 | **0.31** / 0.30 | 0.06 | its settled loss wiggles 50% - the "bugs" |
  | sortfeel | 0.44 | 6.7 | 0.12 / 0.29 | 0 | the same jolt |
  | posinfo2 (measured 22:20) | 0.50 | 4.2 | 0.32 / 0.50 | 0 | eases in (mean lurch 2.7); one birth flash |

  **Both front runners jolt.** Right after a cull or a strike their pace spikes to 4-7x the change's own
  median; evo eases in at ~1.7. No front runner backtracks or teleports. A ramped response (an acceleration
  limit, or the corrector's gain easing in after a composition change) is the cheapest smoothness gain.
- **A scoring bug fixed (`swarm_nca.LOSS_PERMS`).** The loss tried slot -> domain maps into ids {0,1} only
  for a two-region plan, so a PERFECT jellyfish whose teams were ids 0 and 2 scored **14.5** (id 2 and 1:
  10.5). It now ranges over every domain id; it can only lower a loss, and models that read `sn.PERMS` for
  their own wells are unchanged. **It is not why dragonfly -> jellyfish fails**: sortfeel rescored with it
  at seeds 7 and 101 came back identical to the digit. That switch's floor is the orphan third team: an
  oracle-perfect jellyfish with a fraction f of its units on a third team costs ~25 f (f = 1/3 -> 8.24), so a
  lawful corrector must keep the orphan team under about a third of the body (dilute it by laying the two
  kept teams) or give it a place of its own. Puffer -> dragonfly (two teams into three regions) has an
  oracle floor of ~2: that one IS reachable.
- **Round 5 started**: `lite_combo` (session_012QQrhFC4CTk2NV7Khe65SJ, cece/swarm-x-lite-combo) and
  `lite_sortfeel` (session_01BG7RdsNpZ4qogRx2hnhnuL, cece/swarm-x-lite-sortfeel), brief `briefs/lite.md`:
  record the hold, profile, then make it light - fractional update (1/k of the swarm re-steers per step,
  per-update rates scaled by k, k = 1..16), amortised bookkeeping, vectorisation, a spatial hash - with
  ms per swarm-step for 1..64 batched swarms and every published config passing the hold.
- **The game moved too**: cece/swarm-fauna-game ported combo's lossless rules into SwarmGridCore (22:14).
- **A third front runner landed at 22:15: posinfo2**, a LEARNED rule (G2 + body-frame inputs) inside a designed
  molting controller with a majority guard: **51 of 52 feasible over seeds 7 / 23 / 41 / 1000**, own plans 1.6-2.7 (the
  tightest of any model), 0 self-inflicted deaths, organic, 5.2 ms/step. Smoothness 0.50 (it eases in like evo, mean
  lurch 2.7, but one switch births half its body inside 8 steps). It now leads the gallery, and a third lite session
  (`lite_posinfo2`, session_016hrNL5qiGWAu9SKaJTAkkm, cece/swarm-x-lite-posinfo2) holds it and adds the learned-rule
  levers: distil the network, prune inputs, evaluate it only for the fraction being updated.

## Update 21:40 UTC — all four axes at once: three models now clear the scorecard

The lead's goal was "a combination of strategies that are performant, lossless, and emergent". As of
this round three models pass every scorecard axis (accurate at the loss-8 bar on three seeds,
lossless, in the organic band, local-or-mixed), each from a different family:

| model | family | seed 7 / 23 / 41 (passed / feasible) | own plans, seed 7 | lossless | ms/step @n (Python, 1 thread) | organic | my seed-101 check |
|---|---|---|---|---|---|---|---|
| **combo** (G8, published) | grid morphogen + lossless corrector | **16/16, 15/16, 15/16**; held-out 1000: 13/15 | 2.3 / 2.3 / 3.2 / 3.5 | **0 deaths** | 12.2 @134 | **in** (planar excess 0.09) | 15/16 (own 1.6/2.3/1.7/2.8) |
| combo G16 twin | same, full grid | 16/16, 16/16, 14/16 | 1.4 / 2.1 / 3.5 / 3.1 | 0 | 13.8 | in | — |
| **evofate** (C2) | the EVOLVED rule + sort's fate | **12/13, 11/13, 12/13** | 3.0 / 3.7 / 1.6 / 6.3 | **0** | 5.6 @126 | **in** (planar 0.12; 0.19 at seed 23 - its edge) | 12/13 (own 3.0/4.1/1.7/5.6) |
| **sortfeel** (hand-picked) | emergent sorting, sheets removed | **12/13, 11/13, 13/13** | 1.5 / 2.7 / 1.2 / 4.2 | **0** | 6.3 @126 | **in** (planar 0.05, was 0.27) | **13/13** (own 1.6/2.7/1.0/4.7) |
| hgrid2 (for reference) | grid morphogen | 16/16 at 7 and 23 (my rescore) | 1.2 / 2.0 / 2.9 / 3.0 | no (120) | 20.6 | in | — |

**Seed 101 (held out, rescored by the lead, 3 samples, loss-8 bar, `runs/rescore/*_s101.json`)** confirms all three: every model passes tier 1 and tier 2 at a seed none of them was tuned on. sortfeel goes clean (13/13). The one switch that keeps failing is the same in every family: **dragonfly → jellyfish** (time→space), which combo and evofate pass 1 of 3 samples and sortfeel 2 of 3. That switch is now the single open accuracy problem shared by every lossless model, and fixing it there is worth more than any further tuning elsewhere.

What each one teaches:
- **combo** (`results/combo/NOTE.md`): replacing hgrid2's starvation with a straight molt alone drops
  it to 12/12/11 - every failure a BIG→SMALL switch - and three lawful mechanisms get it back:
  `lay_cap` (no egg while the body holds the plan's headcount: "not creating mass is allowed; aging
  it out is not"), `ratio = 2` (an overfull DOMAIN's spare room takes the element mix the whole body
  still lacks: headcount is not a goal, element ratios are), and `orphan_proxy` / `transfer2` (a
  member with no class in the plan steers by its element's best slot). Molting speed is NOT the
  limit (2x faster: worse); hgrid2's neighbour swaps are inert (as sort found). G = 8 costs the
  whale ~1 of loss and nothing else. The remaining misses sit at the bar's edge (7.6-10.5) and are
  always puffer→dragonfly or dragonfly→jellyfish.
- **evofate** (`results/evofate/NOTE.md`): the round-1 evolved rule - G2's weights and the evo genome
  UNCHANGED, learned death suppressed - plus one sticky fate per tadpole (one of sort's wells) and a
  small designed pull with a DEAD ZONE: inside its well a tadpole is pure G2. The dead zone and
  syncing the pull to the cell's own firing are "the whole story for the feel". Own plans went
  16-24 → 1.6-6.3 while jitter (2.57 vs evo's 2.53) stayed evo's. This is the model the lead asked
  to give "another scene test once you get to the next level of scoring"; it is there.
- **sortfeel**: the sheets were WELL COMPRESSION (not adhesion); flat-bottomed wells + a wander term
  took planar excess 0.27 → 0.05 at no accuracy cost.
- **posinfo2** (interim, learned rule + designed quota/molting homeostat): 11/13 at seed 7, lossless;
  scorecard pending.

**In the game** (`cece/swarm-fauna-game`, Docs/SWARM_FAUNA.md §9): sort is the third species
(`SwarmSortCore`), 32/32 own plans over 8 seeds in game mode, zero self-inflicted deaths in 64 runs,
**0.05-0.16 ms/step** (the grid core is 0.5-1.1, field 0.1-0.9). Findings that matter for the
research: sort's ABSOLUTE surplus is why continuous molting does not defeat morphing here (the field
model's proportional surplus did); the code rides the swimming body for free because a well is a
fixed set of plan units; and the game body needed 0.1 voxels/step of noise to stop riding its wells
like slots. Round 5 of the game work (launched 21:38) ports combo's lossless corrector into the grid
species (removing its post-morph debris) and builds `SwarmEvoFateCore`, the evolved rule's scene
test.

## Update 21:00 UTC — the first lossless LEARNED rule, and evo's body under the grid

- **posinfo2 (interim): a learned rule that is LOSSLESS** - posinfo's network (G2 + body-frame
  positional inputs) with its learned death channel masked, a designed scaled (element, domain) quota +
  molting homeostat, and BPTT fine-tuning on yardstick-fair culls. Seed 7: own 3.3 / 4.1 / 3.9 / 3.7,
  **11/13 feasible**, 0 self-inflicted deaths. Learned shape, designed composition, no killing: three
  of the four axes from the family the lead finds most organic. Scorecard at 7 / 23 / 41 pending.
- **evo body + grid steering (hgrid2 direction b, `results/hgrid2/evo_grid`)**: the round-1 EVOLVED rule
  moves, hatches and looks exactly as shipped; the grid only adds composition, plan lock and a steer.
  Own 2.2 / 3.2 / 1.9 / 4.9, **13/16** (only the three into-dragonfly switches miss, 8.3-9.8), held-out
  12/15 on two seeds, and in the organic band "evo-like". This is the candidate for the lead's "second
  scene test" of the evolved rule - except that it still corrects composition with hgrid2's starvation,
  which the game forbids. `combo`'s lossless corrector applies to it unchanged.
- **hgrid2 findings worth carrying**: MIGRANTS fixed sorting (a tadpole where its class is barely
  wanted heads for the nearest site where its class is MISSING; the whale 6.1 → 1.2) - no local
  gradient can carry a unit across a packed body; a PLAN LOCK (60 steps) stops a culled body re-breeding
  its old majority; a yardstick property: a 2-slot plan only scores domains {0, 1} (`dmap_low`).
  Held-out now 15/15 at seeds 1000, 2000 and 3000.
- **sort** republished as `well_look` (seed 7 own 1.27 / 2.18 / 0.58 / 4.60, 12/13) with held-out
  seeds 101-104 all 13/13.
- **zoo** (MAP-Elites over field + nine on/off behaviours - breathe, wave, jitter, orbit, burst, curious,
  hunt, rush, bristle): 142 map elites, **seven named creatures**, each 13/13 feasible at 3 samples on
  the old bar (field's own-plan losses, ~1-3, so they should hold at the loss-8 bar; not yet
  rescored). Playable per-creature pages in `results/zoo/elites/`. A personality family, not one
  creature: the lever for "multiple lifeforms may ship".
- **evo16** hardened the evolved genome (128/128 held-out, 16/16) but only on the OLD bar: own-plan
  losses 13-26, so it fails tier 1 at the loss-8 bar. Its value now is evo_grid's body.

## Update 19:30 UTC — the families converge: designed composition + local shape

**Three LOCAL families now pass tier 1 under the loss-8 bar, and one passes everything.** All of them
pair a DESIGNED composition controller (who lays or molts into what) with a LOCAL shape rule.
Composition is the easy part to design and the hard part to learn; shape is where the organic
look lives. That pairing is the strongest cross-family finding of the day.

| model | locality | own-plan losses (whale / jelly / puffer / dragonfly) | 16-test, seed 7 | held-out | lossless? |
|---|---|---|---|---|---|
| **hgrid2** (grid morphogen 2) | local fields | **1.2 / 2.0 / 2.9 / 3.0** | **16 / 16** | seeds 1000, 2000, 3000: 15/15 feasible each (its session); my seed-23 rescore: **16/16, every test 3/3 samples** (own 1.2 / 1.6 / 2.0 / 3.4, worst switch 7.6) | **no**: starves misplaced surplus on a timer |
| **sort** (emergent cell sorting) | local + census + body centre | 1.4 / 2.4 / 1.05 / 5.0 | 12 / 13 feasible | 13 / 13 / 13 / 12 over 4 seeds (its session); my seed-23 rescore: **12/13 feasible** (own 1.2 / 2.4 / 0.7 / 3.3; the one miss is the same dragonfly -> jellyfish domain-conservation case as seed 7); its session re-measured seeds 101-104: 13/13 feasible on every one | **yes**, by design (molting + region transfer) |
| **posinfo** (learned rule + positional input + designed homeostat) | local + body frame | 3.2 / 6.9 / 4.4 / 6.7 | 4 / 16 (switches untrained) | seed 23 (my rescore): tier 1 3/4 - dragonfly passes 1 of 3 samples, jellyfish 2 of 3; fails held-out tier 1 | homeostat molts; not audited |
| field (designed, global assignment) | global | 3.1 / 1.6 / 1.4 / 4.6 | 9 / 13 | — | yes |
| distill (local student of field) | local | ~25-45 | 7/16 (old bar) | — | — |
| meta (designed metamorph + learned shape) | local | 11.8-14.8 | 7/8 old yardstick, 5 seeds | — | yes |
| colony (shared GRU colony state) | mixed | — | 5.0/8 mean (control 4.25) | — | — |

**The new scorecard** (`scorecard.py`) scores any model on the lead's three words at once: ACCURATE
(swarm_eval at the bar, per seed), LOSSLESS (self-inflicted deaths per 1k tadpole-steps; the
yardstick's cull and molting do not count), PERFORMANT (ms/step, one grown swarm, one thread) and
EMERGENT (the organic feel band + declared locality). Baseline: **field** is lossless, 3.4 ms/step
at 134 tadpoles, and OUT of the organic band (jerk_rel 0.15 "too clean", 9.6% of members stuck),
matching the lead's verdict.

Scorecard (`scorecard.py --skip-eval`, seed 7, 1 thread; ms/step measured while two rescores shared
the 4 cores, so compare models against each other only):

| model | lossless (self-inflicted deaths, per 1k tadpole-steps) | ms/step @ grown n | organic band | jerk_rel | coherence | jitter | stuck | planar excess | locality |
|---|---|---|---|---|---|---|---|---|---|
| field | **yes** (0) | 3.4 @ 134 (1 core free) | **no** (jerk_rel too low, 9.6% stuck) | 0.15 | 0.77 | 0.66 | 0.096 | 0.03 | global |
| hgrid2 | **no** (120; 0.47/1k) — hunger withers misplaced surplus | 20.6 @ 135 | **yes** | 0.58 | 0.63 | 0.93 | 0.0 | 0.08 | local |
| sort | **yes** (0) | 3.8 @ 126 | **no** — planar excess 0.27 (whale 0.70 flat vs plan 0.43, pufferfish 0.90 vs 0.69) | 0.99 | 0.58 | 0.80 | 0.0 | **0.27** | mixed (census + body centre) |

**Each of the three fails exactly one axis, and a different one.** Field is lossless and accurate
but machine-like. hgrid2 is accurate and organic but kills to correct its composition. Sort is
accurate, lossless and cheap, but its tissues pack into flat SHEETS (the failure the lead named in
evolved compact: "clusters make planar surfaces with its crystals"). Our first guess was differential
adhesion; **the sortfeel session measured it and the guess was wrong** (`results/sortfeel/diag.json`):
the sheets are WELL COMPRESSION - tadpoles pack tighter than the plan inside each Gaussian well
(whale nearest-neighbour 2.1 vs the plan's 2.8, pufferfish interior 99% flat), and removing adhesion
makes them flatter, not rounder. Its fix in progress: flat-bottomed wells plus a wander term. The
round-4 combination stays: hgrid2's fine morphogen for the organic texture, sort's molting for
lossless composition, sort's fate for accuracy.

**The in-game grid species** (`cece/swarm-fauna-game`, `SwarmGridCore`, Docs/SWARM_FAUNA.md §8):
hgrid2 ported to C#, side by side with field in the Swarm cell. Research mode reproduces Python
hgrid2 (own 7.9 / 4.7 / 4.3 / 7.7 vs 6.7 / 4.6 / 5.0 / 7.4 over 8 seeds; feel metrics equal to the
second decimal). Game mode (one domain, hunger never kills) lands at 5.1 / 1.6 / 3.4 / 3.9, 32/32.
Findings that flow back to the research:
- **One domain makes the grid easier**: the domain sort was the hard part of hgrid2.
- **A body hovering on its goal must not chase its own jitter**: re-aiming every step spun the field creature ~1,000°/min while feeding and smeared the grid body (whale 8.1 → 5.6 once heading was held).
- **Without molting, a morph leaves debris**: with hunger disabled (no imposed death), the old majority's surplus clings to the new body. This is the gap the round-4 `combo` session targets.
- **The grid's cost is per step, not per member**: 0.6-1.2 ms/step on CoreCLR against field's 0.1-0.9; shrink G or run the coarse grid every other step.
- **Feel in numbers**: field in the game 9.4% stuck, coherence 0.78 (lock-step); grid 0% stuck, 0.57 — the field's mistakes are frozen members, the grid's are members still searching.
- An **overtune pass** (the lead authorised "the next order of magnitude across the board") put 24 swarms in the cell: 5,008 always-on heart colliders at the caps against the Lattice cell's 1,080. It is unprofiled; QA-SWARM-GRID asks for a Profiler capture.

**Sort's mechanism ablations** (results/sort/NOTE.md): fate commitment is essential (0.7/13
without); molting + cross-laying carry the switches; adhesion helps (12.7 → 11.3 without);
Potts swaps are inert; one well per type (a single French flag) fails (4/13). CMA-ES found
Steinberg's rule unprompted: unlike cells repel harder than like cells.

**Round 4** (launched 19:14): `combo` (cece/swarm-x-combo) builds hgrid2 with its hunger death
replaced by sort's molting/transfer, plus sort's fate and adhesion where needed, plus a cost curve
(G = 16 / 12 / 8, coarse grid every other step), aiming at all four scorecard axes at seeds
7 / 23 / 41. `posinfo2` (cece/swarm-x-posinfo2) takes the learned rule (the lead's favourite feel)
to the full scorecard, with the composition homeostat on during training.

## Update 18:20 UTC — first local tier-1 pass, and two negatives

- **hgrid2 (Grid morphogen 2: coarse class-deficit grid + a FINE per-class morphogen + continuous target + feed-forward) passes tier 1 under the loss-8 bar**: own-plan losses 6.1 / 4.8 / 5.5 / 7.2 at seed 7 (3/3 samples each), 6 of 15 feasible transitions. Held-out: seed 23 own 4/4 (standard switches 2/4); seed 41 own 2/4 (whale and dragonfly at 1/3 samples - at the bar's edge). The first approach in which every tadpole reads only fields at its own position and the body is accurate. It is the family the lead judged "accurate and organic"; its feel metrics are in results/hgrid2/feel.json.
- **Specialist ensemble** (`ensemble_swarm.py`: run the single-plan specialist the majority names): own 10.8 / 7.0 / 11.1 / 10.7 - the best LEARNED own-plan numbers, but every switch fails (a specialist cannot regrow from a foreign body).
- **Task-vector hybrids of the specialists are a clear negative** (results/hybrid/NOTE.md): every blend is worse than its parts (25-82).

## The lead's feel review (2026-10-01, watching the gallery) — a selection signal, not a score

- **Evolved rule (evo):** "a beautifully organic feel. It always feels like a swarm, but it is interesting to watch it try to be more." A great candidate for another scene test once it reaches the next level of scoring.
- **Evolved compact:** "very jerky most of the time, and its clusters like to make planar surfaces with its crystals" — not desirable traits, but notable.
- **Designed field + flocking:** "the transitions from one to another are fun, but it has lost too much of the organic imperfection. Its mistakes just feel like bugs, not emergence."
- **Grid morphogen (designed, hgrid oracle):** "an excellent direction. It feels both accurate and organic. Continue to explore how this can be used."

So the target is ACCURATE (under the loss-8 bar) *and* ORGANIC. Exact global assignment gets accuracy and loses life; no sorting keeps life and loses accuracy; the local grid morphogen is the only family so far judged to have both. Jerk and planarity are now named failure modes worth measuring.

## Catalogue of process families

| # | family | how it produces a lifeform | sorting | composition | plan decision | status | unique strength |
|---|---|---|---|---|---|---|---|
| 1 | **Gradient local rule** (G2 lineage, E/F/G/H, specialists) | backprop through time on a Sinkhorn loss, one MLP per particle | none (emergent outline only) | learned laying/egg gates (fragile) | implicit | outline yes, sorting no; fills the 280 cap | a single learned rule, perception-only, no blueprint |
| 2 | **Designed field** (field) | authored slots, RBF fields, boids, reactions | global assignment | molting | census + 12-step dwell | the only tier-1 pass; in the game now | game-ready, C# port 0.1–0.6 ms/step, heals 0.92–1.07 |
| 3 | **Quality diversity over field** (zoo) | MAP-Elites over about 50 field genes | inherited | inherited | inherited | code only, no elites published | personalities: a family of creatures, not one |
| 4 | **Morphogen grid over boids** (hgrid) | two-level CA: a coarse grid of class deficits steering boids | per-class density deficits | starvation + class-directed eggs | designed grid | just over the bar | spatial control field; switches shed loot crystals; flow channel |
| 5 | **Evolved behaviour genomes** (evo, evo compact) | CMA-ES over on/off behaviour genes + parameters around a body | none | lay homeostat, egg choice | evolved lock (unused) | 16/16 old bar, 0/16 now | structural search on the true objective; a 31-parameter no-network rule |
| 6 | **Distillation** (distill) | DAgger from the field teacher into a local learned student | to be learned | learned heads | learned → designed census | code only | measures the global-vs-local information gap |
| 7 | **Centralised learned control** (colony) | a shared GRU colony state votes the plan and biases breeding | — | breeding bias | colony vote | untested (container restarts) | a "mind" for the swarm |
| 8 | **Learned metamorphosis** (meta) | learned per-unit identity change (molting) + selective laying | — | metamorphosis | implicit | clear negative (morphs to the wrong element as often as the right one) | — |
| 9 | **Robustness curriculum** (play) | learned rule hardened with predator sensing + strikes | — | — | — | chaotic, no learned parting | the strike/heal metrics and a drift control |
| 10 | **Hierarchical reaction shell** (creature) | a designed vessel-reaction shell over evo's body | inherited | inherited | inherited | interaction works; body fails tier 1 | readable body during encounters; ram kills roughly halved |
| 11 | **Embedded economy** (swarm-fauna-game, Unity) | field + funded laying, starvation, swimming in a real cell | global | molting only after a morph | census + dwell | 33/33 headless harness; editor untested | the only family that lives in the game |
| 12 | **Emergent cell sorting** (sort, launched 15:53) | differential adhesion / chemotaxis / positional cues, searched by CMA-ES | LOCAL by construction | — | — | running | aims at the measured gap without a blueprint |
| 13 | **Positional information for the learned rule** (posinfo, launched 15:53) | G2 plus a body-frame / morphogen input, trained by backprop | learned with position input | — | — | running | tests whether the learned family lacked information or capacity |

Earlier, pre-swarm families (Part A §3): grid 2D NCA, animated NCA, 3D NCA, collision/particle NCA,
particle swim, prism particles, designed target generators. Each is recorded with the bug that
taught it something.

## The discoveries that matter most (one line each; evidence in Parts A–C)

1. **Tier 1 is a sorting problem.** Outline costs 2–4.5; element placement and domain regions carry the rest (A D27–D28).
2. **Composition control is the shared key to switching.** Four independent mechanisms fix switch drift: molting, starvation + class-directed eggs, the lay homeostat + egg choice, and learned heads. None of them sorts (B §6).
3. **Every switch solution wraps G2 unchanged as the body** and adds a non-gradient controller around it (A §2).
4. **Learning the plan DECISION fails repeatedly.** Its errors confirm themselves through composition feedback, so a designed census with hysteresis wins (hgrid vote 3/8, distill's swap, collapsed laying gates) (B §6).
5. **Continuous molting defeats morphing in the game.** 159–182 kills, 0 switches; molting only in the settle window after a commit restores it (C game).
6. **The yardstick had to be fixed three times**: the runner-up-only cull (found independently by field, hgrid and evo, and exploited by CMA), vacuous switch passes, and "closest of four" against "looks like it" (the loss-8 bar) (A D6–D7, D23, D25; B §6).
7. **A size-blind loss makes the seed an optimum.** "Never grow" is a shortcut with or without a laying gate, and the gate's gradient is one-sided, so "never lay" is absorbing (A D1–D2).
8. **State runaway caused the NaN hangs and mass extinctions.** Python's `max(nan, eps)` returns nan, so the OT loop never ended; the overflow loss fixed it (A D4–D5).
9. **The pufferfish is the default attractor**, and Charge creeps into Space seedings, so the jellyfish→pufferfish switch is nearly free (A D9).
10. **Specialists peak at step 1000**, then degrade on the four-plan test while their own plan keeps improving. They become attractors that capture other seedings (A D20).
11. **Every gradient swarm trained on a single animation frame** (`anim=0`), so none ever learned to swim (A D22).
12. **A GPU gives no speedup here**: the step is Python-bound, 2.3–3.4 s/step on a 3080, the same as CPU (A D19).
13. **Greedy slot assignment equals Hungarian** (104.3 vs 103.6), so a port needs only a sort. Field's C# runs 0.1–0.6 ms/swarm-step; the game build measures 0.09–0.85 ms at 10 Hz with interpolation (B field, C game).
14. **Player-facing behaviours measured to work**: a startle wave cuts tadpoles touched by a fly-through from 0.31 to 0.08 (jellyfish); Time tadpoles mob a loitering ship (24.3 vs 2.4 within 2 ship radii); pufferfish inflation; heal 10–30 steps after a third of the body is struck; homeostasis under knife-edge predation, so converting a swarm takes selective, fast killing — a skill target (B field).
15. **The game economy changes the creature.** Charge flora are inedible because their leaves are shielded, so the pufferfish pays double for its eggs. Post-morph bodies are thin (40–90 tadpoles) until fed. Food element sets only the cost of an egg, not its element (C game).
16. **Collider budget at full size**: 616 always-on heart colliders for three swarms, against Lattice's 1,080 (C game).

## Open threads (deliberately not mistaken for results)

- zoo (MAP-Elites personalities over field) and evo16 (evo hardening) report at the OLD "closest of four" bar only: zoo found the old yardstick never binds (259/259 candidates, 48/48 uniform-random genomes pass); evo16 measured the round-1 evo genome at 128/128 over 8 held-out seeds. Both must be rescored at the loss-8 bar before they count.
- Seven sessions were archived with follow-up runs still in flight (distill's final DAgger round, meta l3, colony V2, evo stage 5, play p3, hgrid round 1, H1). Their published results are merged; the in-flight runs are lost.
- The task-vector hybrid stage (`swarm_hybrid.py`): built, never run. All four specialists are done except Mass.
- Food element steering the laid element in the game: proposed, not implemented.
- The H3 rescore used the default loss (75–128); its own scale-invariant loss gives 17–31. It fails the bar either way.

---


# Part A — gradient lineage and earlier NCA phases


Scope: the full `Tools/NCA` history of `cece/gifted-curie-x2cpd0` (97 commits, 0bf14382 2026-09-29 →
0fd0fdd3 2026-10-01 15:51), the README and briefs, the gitignored scratch in `Tools/NCA/runs/`
(logs of try2/try4/try6/try7, e4/e7/e8 outputs, `rescore8_*.out`, and two uncommitted diagnosis
scripts), and the branches `cece/swarm-exp-{e1,e1b,e2,e2b,e3,e3b,e5,e5b,e6,e6b,f1,f2,f3,f5,f7,g2,g5,h1,h2,h3}`
and `cece/swarm-solo-{mass,space,charge,time}` (NOTE.md / STATUS.md / summary.json / log.jsonl / commit
messages). Nothing in git was modified. Two diagnostics (`runs/diag_comp.py`, `runs/diag_terms.py`,
written by the coordinating session at 15:39/15:51 but whose output was never saved) were RE-RUN
read-only for this report; their output is quoted in D27/D28 and exists nowhere else.

Conventions: "sink" / divergence = debiased Sinkhorn divergence of the hatched swarm against a plan
(lower = closer). Seeding k = a seed at plan k's element/domain mix. Log notation `ma>ti` = mass
seeding currently scored against the time plan (a switched sample). `n` = hatched tadpoles,
capacity 280. "Old scorer" = `min()`-based tests before 226d0ef0; "strict" = alive + n≥32 + strictly
closest (239891f2); "bar 8" = also divergence ≤ 8 (1b41fe12); "16-test" = `swarm_eval` with fair cull
and 3 samples (08650d86/2959ba2d).

---

### 0. One-paragraph verdict

Backprop-through-time on one local tadpole rule (G2 lineage, ~357 KB MLP, 192 hidden) reliably learns
**which body to grow from a seed's element ratio** (own-plan 4/4 by divergence AND by geometry-only
divergence from E8 onward) and grows **recognisable static body silhouettes** (positions-only
divergence 2.5–4.5), but it never learned (a) to stop growing at the plan's size (every co-trained
swarm fills the 280-slot cap, except late G5), (b) to place the right ELEMENT and DOMAIN in the right
place within the body (that is where 60–80% of its residual divergence lives, D28), (c) to switch
plans after losing its majority (best: 1–2 of 4 standard switches, 0–2 of 12 on the 16-test), or
(d) to swim (every swarm run trained with `anim=0` and frame-locked). Under the absolute bar of 8
introduced at the end of the day, **no gradient-trained rule passes a single test** (best own-plan
divergences 11.6–17.6). Its lasting contributions are the G2 body that other families wrapped
(evo, hgrid hybrid, field hybrid, play, specialists), a long list of failure mechanisms with their
fixes (state runaway, NaN hang, size-blind shortcut, vacuous switches), and the yardstick itself.

---

### 1. DISCOVERIES — swarm co-training (the four-plan gradient lineage)

#### D1. A size-blind loss makes the seed itself an optimum: "never grow" is a shortcut, with OR without a laying gate
- **Mechanism.** The swarm loss normalises the swarm to a probability measure (`a = w / w.sum()` in
  `swarm_loss`), so a Sinkhorn divergence carries no size term; a 16-point clump at roughly the right
  spread matches any plan "well enough" (divergences ~16–31 at n=16–20, only slightly worse than full
  280-tadpole bodies at ~14–40). Shrinking also avoids the cost of misplacing hundreds of units.
- **Evidence.** F2 (`learned_lay`) ended steps 500→2000 at n=16–21 per seeding (log: 20/18/19/16 at
  500, 16/16/16/17 at 1000, 21/6/18/15 at 1990); F5 (laying gate + scale-inv) sat at n=7–26 from step
  1000 to **5000** (508 log lines; LR dropped to 1.5e-4 at 3750, no change); F7 (all four fixes)
  n=35/28/24/28 at 990. Crucially the shortcut ALSO appeared **without** a laying gate, through
  refusing to hatch eggs (hatching is learned): E3b n=280/7/16/16 at step 500 and 30/18/21/19 at 750
  before regrowing to 280; F3 18/16/16/16 at 750; E7 (`runs/e7.out`) n=16/16/16/16 at step 240.
  Branch commit messages (publisher, old scorer) called F2 "5/8", F5 "6/8 at step 5000", F7 "6/8";
  strict scoring gives 0/8, 0/8, 1/8 (computed from each branch's summary.json).
- **Fix that worked.** A one-sided, plan-agnostic body floor `w_body·relu(1−n/min_body)²`
  (min_body=76 = the smallest plan, w_body=20; 239891f2) plus `MIN_TEST_BODY=32` in the scorer. G2 =
  F2 + floor grew all four seedings to 280 again (5/8 strict).
- **Why it matters.** Any distribution-matching loss on a variable-population automaton needs a
  separate mass/size term; "headcount is not a goal" is right for plan choice but the loss must still
  forbid not-growing. Collapse to the seed is an attractor whenever the loss is size-blind, so it
  will reappear in any family that trains on this divergence.

#### D2. The laying gate's gradient path is one-sided, so "never lay" is absorbing
- **Mechanism.** `learned_lay` gates laying with `q = sigmoid(4·s[30]+3)` (q≈0.95 at s=0); the coin
  has no gradient, so each LAID child carries a zero-valued straight-through weight
  `(q−q.detach())/q.detach()` (score function). The gradient only exists on children that exist:
  once a parent stops laying there are no children to carry any signal back to its decision, and
  nothing in the loss rewards a child that was never laid.
- **Evidence.** F5 never recovered in 4000 steps at n≈16–20; F2 briefly re-grew to 280/268/280/275 at
  step 1250 and fell back to 30/46/22/22 by 1500 (oscillation, not recovery). Code: `SwarmRule.lay`
  (swarm_nca.py ~415–440).
- **Why it matters.** A score-function estimator over a discrete reproduction decision needs an
  exploration floor (min q, entropy bonus) or a term that prices the ABSENCE of offspring; otherwise
  the policy can fall into a no-data region. The body floor (D1) is what supplied that pressure.

#### D3. Designed, element-blind, crowding-only budding fills every swarm to the 280-slot cap
- **Mechanism.** The designed reproduction rule lays whenever a hatched tadpole has fewer than
  `k_bud=7` neighbours within `r_lay=4` (with p_bud=0.2). Any finite body has an under-crowded
  surface, so laying continues until there are no free slots. Collision fixes spacing (r0=2.4), so a
  full swarm is physically 1.5–3.7× the plan's headcount (plans 192/88/179/76) and cannot take a small
  plan's geometry.
- **Evidence.** Every gradient run's census reads n=280 for all four seedings at evaluation (e1b,
  e3b, e5b, e6b, e8, f1, f3, g2, h1, h2, h3, all four specialists); the only exceptions are the
  never-grow runs (D1), early try runs (capacity 360: try2 n≈350), and G5 at step 6000
  (180/224/270/172 — D16). Field's NOTE: "every learned switched swarm sits at the 280 cap".
- **Why it matters.** Size control cannot be learned through an actuator the rule does not own.
  Every family that solved sizing (field, hgrid, evo's homeostat) did it with a DESIGNED or evolved
  laying controller, never with this backprop rule.

#### D4. State runaway was the root cause of the NaN hangs and of mass extinction; the overflow loss is the fix
- **Mechanism.** The state update is `s += ds` with nothing bounding it. A rule whose update grows
  with |s| grows exponentially until overflow. The death channel (31) is among the channels that
  run away; a tadpole dies when s[31] > 1, so a runaway rule kills its whole swarm. The ±1000 clamp
  added as a guard only hides it — a clamped channel passes no gradient, so the pool cannot be
  repaired.
- **Evidence.** In E7's step-250 pool nearly every sample sat at the ±1000 clamp on many channels
  (only fresh seeds stayed below 310). The shared warm start (try6 step 250,
  `results/swarm_coevo/warm_start.pt`) ALREADY had smax 35–40 at step 0 (log lines of every F/G/H
  run: smax 35.9–41.5 at step 0; per-seeding `over` 4.5–21.9), i.e. every E-series run started from
  a runaway rule. E2b (laying gate, no overflow loss) went 280 → 96/38/73/12 (step 500) → 16 (750) →
  0/0/0/0 (990): fully extinct. E7 log shows mass deaths `d60`, `d22`. Adding
  `w_over·mean(sum relu(|s|−5))` (97a4ac3b) from the warm start: smax 40.4 → 6.8 by step 60 in E8
  (log: 40.4, 44.3, 25.7, 10.7, 22.9, 11.7, 6.8…), and every F/G/H/solo run then kept smax ≈ 4–10 for
  thousands of steps with zero non-finite events. Resumed from an already-pinned pool the penalty
  swamped the loss and swarms collapsed (README).
- **Why it matters.** Conclusion recorded in README: "every run before the F series trained on
  runaway pools". The pre-F results (E1b–E6b) are therefore not comparable. The same lesson had
  been learned once already in the particle phase (P5) — free-running hidden state drifts where no
  loss looks; the overflow loss belongs in the default config of any unbounded-state NCA.

#### D5. A NaN in the OT cost turned into a silent infinite loop (Python `max(nan, x)` returns nan)
- **Mechanism.** `sinkhorn_ot` used eps-scaling: `e = max(float(Cd.max()), eps); while True: …; if
  e <= eps: break; e = max(e*0.5, eps)`. With a NaN cost, `e` is NaN; `NaN <= eps` is False forever,
  and Python's `max(nan, eps)` returns its FIRST argument (nan) because `eps > nan` is False. With
  +inf, halving stays inf. The process burned 390% CPU forever with no exception.
- **Evidence.** Diagnosed independently by five overnight babysitter sessions with `py-spy dump
  --locals` (`e: NaN` in `sinkhorn_ot ← swarm_loss/_divergence ← train`): E1 hung at step 500 (85 min
  silent, resumed and re-hung); E2 at steps 200 and 220 (twice, before the first checkpoint, so never
  resumable); E3 after 200 and 450; E5 at 320/370, then after the step-500 checkpoint EVERY one of
  10 watchdog relaunches hung between steps 510–530, with the step-500 loss reproducing exactly
  (163.854) — deterministic; E6 at 170 and 360 (scale_inv path, `_divergence:627`). Precursors in
  the logs: headcount collapse just before the hang (E3: all plans n=16; E6: 82/65/86/143; E2:
  n=17/20). Fix 53248260: sanitize costs (`nan_to_num`), a 200-iteration guard, clamp states at
  ±1000, drop+reseed non-finite samples, skip non-finite gradients, log `nf`; plus
  `NCA_NAN_DUMP` to save the first finite→non-finite step (1394925a) and `smax` logging (c89761f3).
  The guarded reruns E1b–E6b all reached step 1000 with nonfinite=0.
- **Why it matters.** An adaptive loop with a float termination test needs an iteration cap and a
  finiteness check; silent hangs cost ~10 overnight run-hours here. Also: snapshot intervals must be
  short enough that a deterministic failure is resumable (snap_every 250 → 100).
- **Small observation.** E3's babysitter wrote "CPU training is not bit-deterministic, so a relaunch
  may get past the bad step", while E5 showed resumes from the same checkpoint ARE deterministic
  (loss exactly 163.854 every time) — both were observed; determinism held for the resume path.

#### D6. The old scorer passed extinct swarms: `min()` over a tie of sentinels
- **Mechanism.** An extinct swarm scores the sentinel 100 against every plan; `min()` over the tie
  returned the first key, "mass".
- **Evidence.** E2b's publisher reported "2/8 tests pass" at step 1000 with every swarm extinct
  (branch commit ea7765f7; summary.json: n=0 everywhere, every cross = 100.0). Fixed in 226d0ef0
  (`tests_passed`: alive, below sentinel, STRICTLY closest).
- **Why it matters.** The first of four successive yardstick corrections in one day (D6, D7, D24,
  D25), each of which changed the ranking of runs.

#### D7. Vacuous switch passes: a switch test passes even when no switch happened
- **Mechanism (found for this report).** `lose_majority(..., to=X)` returns None if X is already the
  majority (`to not in others`), so `done=False`; but `tests_passed` scores the switch row without
  checking `done`. A seeding that grew the WRONG majority — specifically the switch target's element
  — passes its switch test without any cull.
- **Evidence.** F1 step 2000 (`cece/swarm-exp-f1` summary.json): the Space seeding grew a
  Charge-majority body (el [127,50,63,40], own-plan row: charge 26.95 < space 40.17); the
  "space→charge" switch has `done: false`, and strict scoring counts it as passed. README calls F1's
  two switches "genuine"; at step 2000 one of them is vacuous. The same "(could not switch: too few
  of the new element)" line appears in E8 at steps 2000 and 3000 and in solo_charge at 3000.
- **Why it matters.** A switch test must require `done`; otherwise drift into the target's element
  scores as a successful metamorphosis. swarm_eval's fair `cull_to` handles feasibility separately
  (n/a), so this specific hole matters for the 8-test history.

#### D8. Shape is decided only after the overflow fix: before it, every seeding was "a pufferfish" by geometry
- **Mechanism.** With element and domain costs zeroed (geometry-only table, added 8ade03af), the
  pre-overflow rules grew a generic blob that is geometrically closest to the pufferfish plan; their
  own-plan "passes" came from the w_elem=60 composition readout, not shape.
- **Evidence.** Geometry-only rows (mass/space/charge/time seeding → closest plan): E1b all four rows
  closest to charge (12.7/13.2/13.4/13.5); E3b mass→charge 10.4, time→charge 15.4; E5b mass→charge
  12.6, charge→mass 12.2, time→charge 15.6; E6b: charge closest for 3 of 4. E8 (first with w_over):
  4.75/5.67/6.69/8.79 diagonal vs 9–30 off — the first time shape alone picked every plan. Later G2
  3.65/6.38/5.10/6.42, H2 2.8/6.2/3.4/4.9 (best geometry of any co-trained rule).
- **Why it matters.** "Own plan 4/4" in the E series was a composition readout. The geometry-only
  table is the correct check, and it showed the overflow loss was what let shape learning happen.

#### D9. The pufferfish is the default attractor; Charge creeps into Space seedings
- **Mechanism.** The pufferfish (a near-spherical shell) is the plan closest to a generic dense blob,
  and Charge — the jellyfish's runner-up element (25 vs 54 in the plan) — out-breeds Space under
  element-blind budding, so jellyfish seedings drift toward Charge and the switch whose target is
  Charge (jellyfish→pufferfish) is the "easy" one.
- **Evidence.** Grown jellyfish compositions vs plan [25,2,54,7]: G2 [101,2,147,30]; E8@1000
  [74,9,189,8], E8@3000 **[176,4,95,5] (Charge majority — the space seeding became a pufferfish,
  geometry 8.92 charge vs 8.15 space)**; F1@2000 [127,50,63,40] (Charge majority). E8@2000 geometry:
  the space seeding is closer to charge (7.54) than to space (10.09). The only standard switch that
  G2, G5, H1, H2 pass is space→charge; F1, E1b, E3b, H3, solo_charge also pass it.
- **Why it matters.** One switch "passing" consistently across runs is evidence of a bias, not of
  learned switching. Element share is a population-dynamics quantity, and with element-blind laying
  the mix drifts toward whichever element breeds most where the body has free surface.

#### D10. Headcount sensing was required to break bimodal growth, but a headcount loss made growth oscillate
- **Mechanism.** A purely local rule cannot know body size: from a seed it either exploded to
  capacity or never hatched. Giving each tadpole the swarm's headcount/100 and element mix (like a
  game Cell tracking its fauna; 1335f905, zero-weight warm start) let it modulate hatching. With a
  headcount loss term (`w_count=4`, try4) the population then oscillated instead of settling.
- **Evidence.** try4 (`runs/swarm_try4.log`): n per seeding at steps 50/100/150/250/300/500 =
  21/116/180/79/209/53 (mass), 23/143/151/45/84/64 (space)… vs targets 192/88/179/76. try6 (warm
  from try4, w_count→0, scored against the current majority, e8b16467) went back to the cap
  (n 280/224/252/102 by 250).
- **Why it matters.** Global population signals are a sanctioned "game Cell" input and are needed;
  but a count target trained through BPTT on a 28-step truncated window produces limit cycles. The
  later choice "headcount is not a goal" removed the oscillation and re-created D3/D1.

#### D11. Early swarm tuning facts (try series, all on 2026-10-01 01:00–04:30)
- **Mutation compounding:** element mutation 2% → 0.5% (ea62cf1b) because mutants compounded into the
  wrong element mix over long rollouts (laid eggs of a mutant breed true).
- **Origin-anchored loss exploded:** long-horizon pool samples drifted away as whole swarms and the
  loss reached sinks > 1000 → centred, translation-invariant loss with a Huber position cost
  (quadratic to 4 scales then linear), an 80-voxel membrane, and pool hygiene that replaces blown-up
  samples (25faee74) and samples far off their headcount (bd1092f1). Side effect: the viewer camera
  had to track the centroid because grown swarms drift (e175c100).
- **Killing was cheaper than not hatching:** at death penalty 10 the rule hatched to the cap and then
  killed the excess (try2: d=52/39/47/54 deaths per seeding at step 270 with n≈350/360) → w_survive
  60 (d4ecbac5): deaths ~0 in every later run.
- **Capacity 360 → 280** (0e2a383d).
- **Soft count cheat:** an alpha-weighted count let the rule keep every tadpole barely visible and
  pass for a swarm a quarter its real size → a hatched tadpole counts exactly 1 in the forward pass
  with a steep-sigmoid straight-through gradient (in 8c2d6485, `decode`).
- **Why it matters.** Each of these is a reward-hacking route of a differentiable population; the
  list is a checklist for any future swarm loss.

#### D12. Five reasons the first rule never switched (try6: 4/4 own, 0/4 switch), from a design review
1. The label was read at the END of the rollout, so a swarm that out-lays the new majority and
   reverts is scored correct — the signal REWARDS reverting.
2. Laying was designed, element-blind and gradient-free: the rule could not change its own
   composition.
3. w_elem=60 made the score mostly a composition readout (plan geometry differs by only 5–22).
4. The hatch/death gradient was weak.
5. Switched samples did not live long enough in the pool to restructure.
Fixes, all behind flags (a 3-step run reproduces the old code exactly): sticky labels
(`sticky_plan`, cooldown 240, margin 0.15, p_ratio 0.5), learned laying gate, composition-relative
contrastive loss (`rel_elem`, w_mix 20, w_con 10, margin 4). Of the three, only sticky labels gave a
clean improvement; the laying gate needed the body floor (D1); the contrastive loss destabilised
(D13). The deeper problem (D3/D14) remained.

#### D13. The composition-relative contrastive loss destabilised training and did not help switching
- **Evidence.** E3b (sticky + contrastive): loss 91→296 at step 250, populations collapsing to 7–16
  at steps 500/750 and regrowing to 280 by 990; con hinge up to 14.2. The charge seeding grew a
  TIME majority (el [70,7,49,154]). F3 (+overflow): 3/8 strict, collapse to 16–18 at 750, time
  seeding grew a Space majority ([15,13,134,118]). F7 (all four fixes): never grew (n 19–35).
  E4 (all three fixes, no scale_inv): loss rose from ~100 to 450 by step 260 (stopped); the hinge
  (0–11) and mix error (0.00–0.42) stayed small while the divergence itself grew to 50–97, every
  sample at the 280 cap — the divergence term, not the new terms, was diverging (D14).
- **Why it matters.** Reweighting the target marginal to the swarm's own mix makes the objective
  move with the swarm; combined with BPTT through a population that can collapse, it made the
  landscape oscillatory. It was dropped from the main line after F7.

#### D14. Grown swarms cannot take a small plan's geometry because size, not shape, dominates → `scale_inv`
- **Mechanism.** At collision spacing a 280-tadpole swarm is physically larger than any plan; a
  switch to a smaller plan would need deaths, which are penalised. With element/domain zeroed the
  four targets are still 12–27 apart, yet grown swarms sat 25–37 from their own plan, so the gap was
  size. `scale_inv` centres the swarm and rescales it to the plan's RMS radius (uniform scale costs
  0, a 1.3× stretch still costs 0.35–0.70; cross-plan separation unchanged; c4390d94).
- **Evidence / outcome.** Scale-inv runs (E5b 4/8, E6b 3/8, E8 5/8, F5 0/8, F7 1/8, G5 5/8, H1 5/8,
  H3 6/8 strict) never beat the default-loss best (G2/H2 5/8) on tests passed, and scale_inv was
  itself a NaN trigger (E6's hang was on `_rescaled`: a near-degenerate swarm with RMS clamped only to
  1e-3 scaled up by plan_rms/r overflows the cost; E5/E6 notes). It also made the divergences
  incomparable across runs (README: "compare by tests passed").
- **A yardstick consequence found here.** The loss-8 rescore of H3 (`runs/rescore8.sh`) evaluated a
  scale-inv-trained rule WITHOUT `--scale-inv`: its own-plan divergences read 74.9/111.1/128.4/107.7
  (`results/swarm_coevo_h3/eval16.json`) against 17.7/30.5/31.2/26.4 in its own scale-inv 16-test
  (46b90de8). It fails the bar either way, but the stored numbers describe a mis-configured scoring.
- **Why it matters.** Making the loss blind to size hides the overgrowth instead of fixing it, and
  removes the last signal against the D1 shortcut ("with scale_inv, deliberately none").

#### D15. Sticky labels alone (F1) gave the first genuine switches — and the whale that kept its old body
- **Evidence.** F1 @1000 (`fc71274e` summary): 5/8 strict; switches pufferfish→dragonfly and
  jellyfish→pufferfish genuine (done=True); own plan 3/4 because the MASS seeding grew a
  TIME-majority body (el [26,69,12,173]; whale 45.1 vs dragonfly 40.3). Its whale→jellyfish switch
  ended with Mass back in the majority ([50,153,40,37]) and scored 16.3 against the whale — after
  the cull Mass re-out-bred Space and regrew a whale. F1 @2000 still 5/8, but one switch vacuous (D7).
- **Why it matters.** Element-blind laying means the post-cull majority is decided by who breeds
  fastest, not by who was culled — the root of every drifting switch (G2's whale→jelly ends with a
  Charge majority; E8's whale→jelly ends Time-majority [103,25,33,119]).

#### D16. G5 (laying gate + body floor + scale-inv, 6000 steps) is the only co-trained rule that partially sized its bodies
- **Evidence.** `cece/swarm-exp-g5` (step 6000; on main too): census n = 180 / 224 / 270 / 172 for
  plans of 192 / 88 / 179 / 76 — the whale is within 6% of its headcount (180 vs 192, el
  [26,111,23,20] vs plan [19,126,25,22]). In training, headcounts per pool sample ranged 45–280
  after step ~1250 (log). Own plan 13.3/15.2/11.6/17.6 (best scale-inv diagonal); geometry
  4.1/6.6/4.1/7.6. Still 5/8 strict (switch: only space→charge), 0 under bar 8, never 16-tested.
  LR schedule 5e-4 → 1.5e-4 (3750) → 4.5e-5 (5250); loss fell to 57 at step 4000 (best of any F/G).
- **Why it matters.** Given long training, the learned gate + floor CAN regulate size somewhat — but
  only toward "below the cap", not to the plan's count for small plans (jellyfish 224 vs 88).

#### D17. G2: the canonical gradient rule, and what each later yardstick did to it
- **Config.** sticky labels + learned laying gate + body floor (76, w 20) + overflow loss, per_kind 2,
  pool 24, seed_every 6, roll 48–96, bptt 28, CPU, step 1000 (~2.5–5 s/step).
- **Scores across yardsticks.** Old/strict 8-test: 5/8 (own 4/4, margins 13.9/17.5/15.3/20.7 vs
  42–84 off-diagonal; one switch, space→charge). Evo's held-out 16 seeds: mean 4.25/8 (the switch
  pass is a ~50% coin; did not survive 3-sample resampling). Field's fair-cull 8-test: 4/8. 16-test
  fair cull: 6/16 (own 4/4, switches jelly→whale and puffer→whale). Loss-8 bar: **0/16**.
- **As a substrate.** + field's composition controller (majority hysteresis + molting): 6/8,
  own-plan 10.9/15.2/13.1/17.8; + hgrid grid composition: 7/8 (12/16 fair; 1/16 at bar 8);
  + evo's 95-float genome (lay homeostat + egg choice, G2 weights untouched): 8/8 then 16/16 fair
  (0/16 at bar 8); play fine-tunes and all four specialists start from it. G2's probe heal
  0.317/−1.246/0.273/0.025 (play baseline): a struck jellyfish regrows WORSE than right after the
  cut (it refills to the cap, ignoring shape).
- **Why it matters.** G2 is the best learned BODY GENERATOR; every family that succeeded on switching
  added a non-gradient composition controller around it. Switching is a control problem the BPTT
  rule could not solve, but its motion/look/hatch layer was portable.

#### D18. Learned egg choice (`learned_egg`) did not fix switching; it made mixes overshoot or undershoot
- **Mechanism.** Four hidden channels (26–29) become the parent's softmax preference over its egg's
  element; a share `p_cross` of eggs take the picked element; a zero-valued straight-through score of
  the chosen element rides on the child (same estimator as D2). Domain always breeds true.
- **Evidence.** H1 (e8 + egg, p_cross 0.1): 5/8 strict. H2 (g2 + egg): 5/8 at step 2000 with the
  lowest training loss of the lineage (51.75 at 1990) and best geometry diagonal (2.8/6.2/3.4/4.9),
  own plan 12.5/16.0/15.0/18.6 — never rescored on the 16-test or bar 8. H3 (RTX 3080, p_cross
  0.25, scale-inv, 3000 steps): 6/8 strict (whale→jelly and jelly→puffer), 5/16 on its own scale-inv
  16-test (own 4/4, switches 1/12), 0/16 at bar 8. H3's mixes: jellyfish **[16,9,251,4] = 90% Space**
  vs plan 61%; pufferfish [113,60,34,73] = 40% Charge vs plan 73% — the choice amplified one
  majority and lost another. Every swarm still at 280.
- **Why it matters.** An egg-choice actuator gives the rule composition control in principle, but a
  gradient through a zero-valued score weight on a 28-step window was not enough to learn a
  homeostat; evo found the same actuator useful only with an explicit deficit-driven form
  (evolved table D, "majority amplifier").

#### D19. A GPU did not speed this up
- **Evidence.** H3 on an RTX 3080 logged 2.3–3.4 s/step (`sec` in log.jsonl), the same as CPU runs at
  the same batch (G5 2.0–3.3 s/step on 4 CPU threads). The step is Python-bound (per-sample laying
  loop in `_lay`, frames×slot-permutations loops in `_divergence`, eps-scaling loop).
- **Why it matters.** Scaling this family needs vectorised laying and OT, not hardware.

#### D20. Specialists (single-plan fine-tunes from G2): best at step 1000, then the 4-plan score degrades while their own plan keeps improving — they become attractors
- **Evidence** (`cece/swarm-solo-*` STATUS.md and step evals; publisher score = summed wanted
  divergence + penalties): solo_charge 4341.8 (4/8) @1000 → 6366.5 (2/8) @3000; solo_time 3372.3
  (5/8) @1000 → 6392.1 (2/8) @2000 → 5420.3 (3/8) @3000; solo_space 5384.6 (3/8) @1000 → 6375.0
  (2/8) @2000 → 5387.1 (3/8) @3000; solo_mass reached only step 1990 (rule never on main). Meanwhile
  the OWN plan improved: charge loss on its seeding 15 → ~10.7; space own divergence 9.12 at step
  3000 (geometry 2.72) and training sink as low as 6.4–7.5 (under the bar 8 — the only gradient
  numbers under 8, in training snapshots); mass training sink 6.1–14.7.
- **Attractor takeover:** solo_space grows jellyfish from MASS (34.1 vs 58.7 whale; el
  [38,49,132,61] Space majority from a Mass seed) and CHARGE seeds (28.1 vs 50.6); solo_mass grows a
  Mass-majority whale from a TIME seed (el [34,123,47,76]; geometry 2.9 to whale) — the specialist's
  element out-breeds everything; solo_charge's mass seeding becomes Charge-majority [123,17,79,61].
  The geometry-only diagonal collapses to 1/4 for every specialist (the whole row picks the
  specialist's plan).
- **Size note.** solo_mass's laying gate produced the only visible per-sample size modulation in a
  specialist (training n 146–280); the others ran at 280.
- **Why it matters.** Fine-tuning on one plan rewrites the shared element→plan map, not just the
  plan; task vectors from such specialists are not "orthogonal additions". The task-vector hybrid
  stage (`swarm_hybrid.py`, 6ae8b6b2: sums, means, leave-one-out, Dirichlet mixes, per-layer
  crossover) was built but **never run** — no results exist, and one of the four genomes never
  finished. Specialists' strike probes (own plan): charge heal 0.436, space 0.756, time 0.677.

#### D21. The heal metric is ill-conditioned when a strike barely changes the score
- **Mechanism.** `heal = (cut − recovered)/(cut − before)`; when a strike leaves the score nearly
  unchanged (or improves it) the denominator → 0 or negative.
- **Evidence.** solo_time probe: mass heal **−12.946** (before 52.17, cut 52.91); solo_charge space
  and time heal `null` (cut < before: the strike improved the score); play p1 step 500 heal_mean
  19.9; evo space −1.17. Specialists' off-plan rows are meaningless for the same reason.
- **Why it matters.** Report heal only where the strike hurt by more than a threshold, or report
  recovered-vs-before directly.

#### D22. Every swarm locked onto a single animation frame — the swarms never learned to swim, by construction
- **Mechanism.** All swarm runs trained with `anim=0` (configs of try6/try7/e8 and every gpu_run
  override set leave it at 0); the static loss takes the best of the 8 target frames, so each
  seeding settles on one frame and holds it — the same collapse the 2D lizard swim showed under a
  phase-free loss (E2), here never challenged. Time's per-element speed is therefore never measured
  (`speed: null` throughout).
- **Evidence (computed from 20 branch logs).** Most-frequent matched frame per seeding is identical
  across all independent runs: mass → frame 1, space → 6, charge → 0, time → 1 (or 5); e.g. G5 mass
  frame 1 in 380 of 415 logged steps, charge frame 0 in 456/460; F5 charge 0 in 446/452. All runs
  are warm-started from try6, so the lock is inherited.
- **Why it matters.** The "swimming whale / pulsing jellyfish / dragonfly runners" of the targets
  were never part of the gradient objective in the swarm phase; a learned swim would need the
  clock-from-birth stage that worked for the lizard (E2) and the `anim_loss` that exists but was
  unused.

#### D23. The eighth test of the 8-test yardstick was unfair, and `lose_majority` only ever switched to runner-ups
- **Mechanism.** `lose_majority(..., to=X, mode="excess")` removes only enough of the OLD majority
  for X to pass it; it never checks the other elements. A dragonfly [2,3,18,53] culled until Mass
  leads Time leaves [2,3,19,2] — Space is 73% of survivors, so the honest body is the jellyfish. A
  whale [19,126,25,22] holds 25 Space vs 19 Charge, so "Charge takes over" handed the majority to
  Space. Only runner-up switches ever happened.
- **Evidence.** Found independently by field and evo (field NOTE; evo NOTE "test 8 contradicts the
  dragonfly's own mix"; evo's CMA-ES moved D[Time] to carry Mass 10% above Space 7% to pass it —
  yardstick fitting). E8's earlier dragonfly→whale pass disappeared once its mix was controlled by
  the field hybrid (direct evidence it came from mix drift). Fix: `cull_to` (2959ba2d) removes just
  enough of every element that does not trail the target; infeasible tests become n/a.
- **Why it matters.** The learned rules' early "first switch" (E8 dragonfly→whale, 41.5 vs 52.9–63.8)
  was partly an artefact of mix drift + an unfair cull.

#### D24. Testing all 16 transitions with 3 samples each, and the fail-fast tiers
- 4 own plans → 4 standard switches (if ≥3 own pass) → 8 others (if tiers 1+2 ≥6). Stateless
  learned rules branch every switch off one grown swarm, so 16 tests × 3 samples run in ~100 s on
  2 threads (G2 111 s, H3 96 s); stateful models (field, hgrid) regrow each seeding (hgrid 830–844 s).
  Single rollouts were noisy enough that G2's standard-switch pass was a coin flip.
- Gradient results on it: G2 6/16 (own 4/4, switches jelly→whale, puffer→whale), H3 5/16 (own 4/4,
  1/12), then 0/16 for both at bar 8.

#### D25. The absolute bar: "closest of four" is not "looks like the plan"
- **Mechanism.** A swarm 35 from every plan passed by being the least far. 1b41fe12 adds
  `MAX_TEST_LOSS = 8` (default LossCfg) to both scorers and gates tier 2 on 4/4 own plans.
- **Evidence (`runs/rescore8_*.out`, eval16.json on main).** Own-plan divergences at rescore: G2
  15.2/17.7/15.6/23.5 → 0/16; evo (G2 body + genome) 19.5/19.8/14.9/24.8 → 0/16 (was 16/16); evo
  compact 22.7/29.8/21.3/30.1 → 0/16 (was 16/16); hgrid hybrid (G2 + grid) 7.9/14.9/8.7/12.6 → 1/16;
  hgrid oracle 1/15; field 3.1/1.6/1.4/4.6 → 9/13 feasible. Best gradient own-plan diagonals ever
  recorded: G5 11.6–17.6 (scale-inv), H2 12.5–18.6, G2 13.9–20.7 — none under 8.
- **Why it matters.** It overturned the day's headline ("two approaches solve the full problem"):
  both learned-body approaches (G2-based) fail the bar, and the evo 16/16 was built on G2's
  imprecise body. Only the designed field model's bodies are within 8.

#### D26. Branch publishers over-reported throughout the day
- Each branch's commit message carries the publisher's count with the scorer of its base commit:
  E2b "2/8" (all extinct, D6), F2 "5/8" at 1000 and 2000, F5 "5/8"/"5/8"/"6/8" at 1000/2000/5000, F7
  "6/8", H3 "6/8". Strict recomputation from each branch's summary.json: E1b 5, E2b 0, E3b 5, E5b 4,
  E6b 3, E8 5, F1 5 (one vacuous), F2 0, F3 3, F5 0, F7 1, G2 5, G5 5, H1 5, H2 5, H3 6, try6 4; at
  bar 8 every one is 0.
- **Why it matters.** When comparing families later, never quote a branch commit message's count;
  recompute from summary.json with the current `tests_passed`.

#### D27. (new measurement) Overgrowth is NOT what keeps G2 above the bar
- Re-ran `runs/diag_comp.py` (uncommitted; output never saved before): grow each plan for 240 steps
  (seed 7), then delete tadpoles down to the plan's EXACT count and element mix, randomly or keeping
  the compact core.
  | rule, plan | n / mix grown | loss | → plan count+mix, random | → core |
  |---|---|---|---|---|
  | G2 mass | 280 [44,172,32,32] | 13.93 | 13.12 | 12.98 |
  | G2 space | 280 [122,9,143,6] | 19.13 | 13.20 | 14.72 |
  | G2 charge | 280 [211,20,2,47] | 17.22 | 15.32 | 14.34 |
  | G2 time | 280 [10,14,57,199] | 17.02 | 17.82 | 28.99 |
  | evo mass | 280 [12,237,20,11] | 20.91 | 18.20 | 22.03 |
  | evo space | 280 [42,14,206,18] | 21.27 | 15.31 | 17.84 |
  | evo charge | 280 [220,13,12,35] | 15.86 | 12.73 | 11.66 |
  | evo time | 280 [13,24,25,218] | 24.13 | 17.41 | 28.32 |
- Even an oracle cull to the right count and mix leaves G2 at 13–18: the excess bodies cost only
  0.8–6 points. (Note the G2 space mix here has 122 Charge vs 143 Space — D9 again.)

#### D28. (new measurement) G2's residual is mostly WHERE each element and domain sits, not the silhouette
- Re-ran `runs/diag_terms.py`: the same grown bodies scored with cost terms removed.
  | rule, plan | full | pos+elem+dom | pos+elem | pos only |
  |---|---|---|---|---|
  | field space/charge/time | 1.45/1.38/5.45 | 1.42/1.03/5.35 | 0.00/0.00/1.47 | 0.00/0.01/1.18 |
  | G2 mass | 13.93 | 13.17 | 7.00 | 2.55 |
  | G2 space | 19.13 | 17.00 | 13.58 | 3.51 |
  | G2 charge | 17.22 | 16.29 | 10.84 | 4.14 |
  | G2 time | 17.02 | 15.86 | 6.31 | 4.51 |
  | evo mass/space/charge/time | 20.9/21.3/15.9/24.1 | 20.0/18.5/15.1/22.3 | 14.7/13.0/10.5/13.6 | 2.3/3.5/4.2/4.5 |
- Reading: positions alone are 2.5–4.5 (a recognisable silhouette); adding the element label adds
  4–10 (elements in the wrong regions); domains add 1–6 (mass: 6.2); the per-unit attributes (prism
  size, tier, facing, spindle) add only 0.8–2.1. So the gradient rule learned the outline and the
  look but not the internal map of which element/domain goes where — the thing field gets for free
  from slot assignment (pos+elem 0.00).
- **Why it matters.** It locates the remaining gap precisely: a regional element/domain painting
  problem, which a slot-assignment or template layer solves and the local BPTT rule did not.

#### D29. Process facts worth carrying
- Overnight runs were babysat by separate sessions forbidden to edit code; they diagnosed with
  `py-spy dump --locals`, wrote NOTE.md, and the coordinating session implemented fixes — the NaN
  hang was diagnosed five times independently before the fix landed (cost ~10 run-hours).
- `gpu_run.py swarm` always checks device-vs-CPU on one step (identical survivors/positions/states,
  gradient within 1%) before training: every log shows `CHECK OK` with 0.00e+00 diffs on CPU.
- `--tag/--set` parallel experiments, publisher every 1000 steps, snapshots every 100 (after the
  hangs), resume from `latest.pt`.
- Flags are always off by default and "a 3-step run reproduces the old code exactly" — made A/B
  attribution possible across E/F/G/H.
- Warm-start chaining: try4 → try6 (250) → warm_start.pt → every E/F/G/H/solo run; inherited traits
  (runaway state D4, frame lock D22) propagated through the whole lineage.

---

### 2. Process family: gradient-trained local rules (backprop through time)

**How it produces a lifeform.** A single per-particle MLP (state 32 channels; inputs: own state,
same-domain mean, other-domain mean, SPH-style gradient, crowding, plus 5 global signals —
headcount/100 and element mix) outputs a state delta and a velocity, applied stochastically (fire
rate 0.5). Designed physics surrounds it: collision, a membrane, designed budding (optionally gated
by a learned channel), element/domain inheritance, and element identity enforced inside the decoder
(`prism_h`: a Space prism is a rod, Mass near-cubic, only Charge has tiers). A pool of partially
grown swarms (plus fresh seeds) is rolled out 48–96 steps, the last 28 are back-propagated, and the
swarm is scored with a debiased Sinkhorn divergence against the target plan (position, element,
domain, prism, tier, facing, spindle; min over slot→domain permutations and frames). The rule is
then a growth law: seed in, body out, with healing as a by-product of the pool + damage regime.

**Capabilities demonstrated.**
- Grow a body from a 16-tadpole seed and keep it (pool training makes it an attractor).
- Choose the body plan from the seed's element ratio (4/4 own plan by geometry from E8 on).
- Respect discrete game vocabularies exactly (straight-through one-hots; identity in the decoder).
- Learned periodic motion WITHOUT a clock, when trained with a clock-from-birth stage (grid 2D, 3D,
  free particles — earlier phases).
- Regeneration after cuts (lizard grid/particle phases), and partial healing in swarms.
- Produces a portable body generator (G2) that other families wrapped successfully.

**Limits found (each with its mechanism).**
- No size control through designed crowding-only budding (D3); size-blind loss → seed shortcut (D1);
  score-function gate absorbing at "never lay" (D2).
- No composition control: element-blind laying + 28-step credit window cannot learn a homeostat
  (D12, D15, D18); switching fails or drifts; the pufferfish/Charge bias (D9).
- Residual divergence is element/domain placement (D28), not outline — 13–24 vs the bar of 8.
- Unbounded state needs an overflow penalty or it runs away, kills swarms and hangs OT (D4, D5).
- Truncated BPTT through discrete events (laying, hatching, death) is high-variance; contrastive /
  relative losses destabilise (D13); long runs regress (E8 5/8 @1000 → 4/8 @2000/3000; specialists
  best at 1000, D20).
- Animation is a separate objective that must be switched on and bootstrapped with a clock; the swarm
  phase never did (D22).
- Python-bound cost: ~2–15 s/step, GPU gives no speedup (D19).

**What is orthogonal to other families (and therefore composable).**
- The learned rule owns MOTION, LOOK (prism/tier/facing/spindle) and HATCH/DEATH; other families
  supplied what it lacked as separate layers: field's slot assignment + majority hysteresis +
  molting (composition, sizing, switching), hgrid's coarse morphogen grid (composition), evo's
  gradient-free genome over new actuators (lay homeostat, egg choice). All three wrapped G2
  unchanged and lifted its switching (field hybrid 6/8, hgrid hybrid 7/8 → 12/16, evo 16/16 fair) —
  but none of them fixes the body precision (D25: all G2-bodied models fail bar 8), because that
  precision gap is in the regional element map (D28), which only slot assignment (field) closes.
- Gradient training can ADD a parameter to an actuator that exists; it cannot invent an actuator
  (README on learned_egg). Gradient-free search (evo) is the complementary family for structural
  changes; specialists + task arithmetic (round 1b) would be the complementary family for merging
  separately-learned skills, but specialists turned into attractors (D20) and the hybrid stage
  never ran.
- Distillation (round 2 brief, `distill_*.py`: behaviour cloning + DAgger of field into a local
  student) is the same gradient family pointed at a designed teacher instead of a target cloud;
  it inherits this family's estimators but sidesteps the size/composition credit problem by
  supervising per-tadpole actions.

---

### 3. Earlier NCA phases (each a separate process family)

#### P1. Faithful reproduction of Growing NCA (grid 2D) — the known-good floor
- Every hyper-parameter is the reference Colab's (72×72, 16 channels, Sobel perception, 8,336
  params, p=0.5 updates, alive mask, pool 1024, damage). Target re-rendered from the Noto Color Emoji
  font because GitHub was unreachable (bundled `assets/lizard.png` later, byte-identical target).
- **Exact sparse update**: a cell dead before the update is zeroed regardless, so the MLP runs only
  on alive∧firing cells — selftest holds value to 0 and gradient to 4e-7 with a negative control;
  2.7× faster on CPU (0.67 vs 1.8 s/iter).
- Results (8000 steps): regenerating train loss 10^-3.69; grows by step ~96 (10^-3.2), holds 10^-4.38
  unchanged to step 4000, regrows half-cuts in ~200 steps; rotating ONLY the Sobel kernels rotates
  the grown lizard. Growing control peaks at step 96 (10^-3.96) and fragments by 500 (nothing asked
  it to stop). Persistent holds 10^-4.95 from 200 to 4000 and regrows 3 of 4 cuts.
- **Yardstick lesson #1:** after the corner cut the persistent lizard regrows a WHOLE lizard shifted
  two cells left (10^-4.96 once shifted back) — pixel error scores a perfect regrowth as a failure.
- JS runner (~100 lines, channels-last Float32Array) matches torch to 1.4e-7 (2.8e-6 on trained
  weights) with a sign-flipped Sobel negative control; written as the reference for a compute-shader
  port.

#### P2. Animated NCA (grid 2D swim)
- Same 8,336-param cell, 8-frame travelling body wave generated from the emoji; must become a limit
  cycle with no clock.
- **Phase-free training collapses to a still**: a blurred average lizard is equally close to every
  frame, so the best-start loss gives no consistent direction to oscillate (frame 4 at every step
  200–400, best/worst margin 0.13 log10). **Fix: clock-from-birth stage** (frame ⌊t/8⌋ mod 8 counted
  from the seed) breaks the symmetry; 250 stage-1 steps already advance through the frames in order
  (margin 0.62) and beat the still floor (10^-2.93 vs 10^-2.72).
- **Yardstick**: "the best any STILL image can do against the loop" (10^-2.72) — training went under
  it in 250 steps.
- Result (1500 clock + 6500 pool, warm start from the regenerating lizard): tempo 8.75 steps/frame
  vs 8 (9% slow — the consensus clock of half-firing cells is a noisy average and nothing penalises a
  slightly long period inside the 32-step window), 8/8 frames in order indefinitely, 10^-3.23 vs best
  frame, tail cut regrows in ~50 steps and keeps 8.78 tempo. Spots softer: 12 hidden channels now
  also carry a clock.

#### P3. 3D animated NCA (helical swim)
- Identity + x/y/z Sobel (/32), 16 channels, 64→128→16 (10,384 params), emoji inflated into a pillow
  body (half-thickness ∝ √distance-to-edge), helical wave (tail traces a circle, impossible in 2D).
- **Three exact reductions** (perception only at alive∧firing via a 27×4 stencil matrix; crop to
  bbox(alpha>0.1)+2; separable alive test) + per-step checkpointing: 19 s → 4 s per iteration.
- **Embedding warm start**: copy a 2D model's identity/gx/gy weights with z = 0 → 100-step A/B
  10^-3.03 vs 10^-2.59 from scratch (below the 10^-2.84 still floor): the 2D clock transfers.
- Container restart killed the run at step 1425 → checkpoint model + Adam + step every 50 and
  rebuild the unsaved 2.8 GB pool by growing seeds for random lengths (lizards return at scattered
  phases).
- Result (3500 steps): 8.49 steps/frame, 10^-3.51 vs best frame (10^-3.61 at 2000), still floor
  10^-2.84, best/worst gap 1.16; ball cut through the tail half regrows by ~50 steps, tempo 8.47.
  JS 3D runner 5.6e-7 (z-flip control 5.2e-1).

#### P4. Collision (particle) NCA — the cell on free particles
- Continuous positions, same 16 channels, perception within radius R (SPH kernels), learned state
  delta + velocity; designed collision, alive test and BUDDING of a dormant child next to a visible
  particle short of neighbours (the particle analogue of an empty grid cell next to life). Loss via
  a differentiable Gaussian splat onto the 72×72 target.
- **First run exploded**: loss 0.011 → 1e24 in 50 steps, then extinct, overwriting the only model →
  texture-NCA overflow loss, rollback to the last healthy snapshot on non-finite/exploding/extinct
  batches, numbered snapshots. (The same lesson the swarm phase had to relearn as D4.)
- **The blob, traced by ablation**: a free-particle lizard plateaued at a pale diagonal ellipse
  (log10 −1.91) whatever the speed (vmax 0.6/0.15/0 identical → not motion); a direct splat fit
  reaches −3.5 lattice / −4.6 free (→ not the renderer); a fixed jittered lattice learns legs (−2.27
  at 750 → perception can learn it); re-jittered per sample slows (−2.04); free particles with
  **moment-corrected perception** −1.96/−2.13 at 250/500 — blob gone. **Mechanism**: the plain
  gradient `Σ g·dx·(s_j−s_i)/(1+Σg)` is wrong by up to 91% (2D) / 94% (3D) on a LINEAR field when
  neighbours sit randomly; on a fixed lattice the error is constant and learned around, on free
  particles it is noise and the answer to noise is the average — a blob. The SPH least-squares
  gradient `M^-1 Σ g dx ds` is exact for linear fields on any arrangement (4e-5 / 2e-4).
- Perception only on edges of updating particles: bit-identical, 16.3 s → 1.4 s per lattice rollout.
- Result (4000 steps, zero rollbacks): legs, curled tail, back stripe; grown 10^-2.74 by step 96,
  shape 10^-2.63..−2.86 over steps 200–3000 (3 seeds), quarter cut back to pre-cut error (10^-2.80)
  in 200 steps, ~430 particles stable; ~20× less precise than the grid (renderer floor −4.6).
- **Emergent behaviours nobody trained**: the body TRANSLATES (treadmill of budding at one edge,
  dying at the other: 0.006 px/step at step 1250; with learned velocity off it drifts FASTER, so the
  rule was fighting it; more training anchored it to 2 px / 3000 steps); some colonies **bud a second
  lizard** at step 500 (punished by the loss, but a behaviour a grid cannot have).
- **Verifier blind spot**: the first JS runner passed the grown-colony check while killing 11 of 40
  colonies in their first 3–7 steps — it stored own alpha in a `Uint8Array` before the alive max,
  truncating 0.96 to 0; a grown colony always has a visible neighbour, a seed beside its first bud
  does not. "A verifier that starts from a mature state cannot see bugs that only matter at birth" →
  the verifier now also checks a lone seed + one dormant child (1.8e-7).

#### P5. Particle swim (2D) — animation on free particles
- **No-headroom failure**: at the grid's amplitude (6 px) three runs never locked on, each running at
  ~60% of the tempo asked (11.6/12.4/19.9 steps/frame vs 8/8/12) and scoring worse than one still on
  the clock task — neighbouring frames differ by 10^-2.78 while the particle lizard's precision is
  ~10^-2.7, so there was no signal. At amp 16 the still floor is 10^-2.28 (half a decade of
  headroom, like the grid at 6).
- **pool_seeds**: the phase-free pool stage let the swim slide to a still (tempo ~30, frame spread 0.1)
  while drifting 5 px/1000 steps; mixing N fresh seeds held to the birth clock into every pool batch
  anchored the tempo.
- **LR drop ends the tempo/shape trade-off**: before the 10× drop at step 3000 the rule kept trading
  tempo against shape; after it both held (3250: 8.2 steps/frame on 3 seeds).
- Shipped step 4250 (chosen by `score_snapshot`: 4 seeds × 1000 steps, rejects extinctions, tempo
  within 20%): 8.34–8.48 steps/frame, 10^-2.57 in place, ~1 px/1000 drift; quarter cut (106
  particles) back within 100 steps at 8.58. Step 4500 (final) was WORSE (10^-2.32): the last
  snapshot is not the best. Batch scoring surfaced a rare early extinction (1 of 2 colonies under
  `manual_seed(0)` at step 3250) that hand-picked seeds had missed.

#### P6. Prism particles (3D, the game's vocabulary)
- Each particle is a Cosmic Shore prism: 3 domain logits, 4 tier logits (plain, danger, shield
  octahedron, super-shield stella), half-extents `0.9·exp(0.4·tanh c)`, 6D rotation; colours from the
  live palette; straight-through one-hots so a rule can never show a colour or shape the game cannot
  draw.
- Shapes are the game's own containment tests at the 3× circumscribing scale; the stella gauge agrees
  with `StellatedOctahedronMeshGenerator.ContainsPointLocal` on 200,000 points; Monte Carlo volumes
  8 : 36 : 109 vs exact 8 : 36 : 108 — **shielding costs 4.5× volume and super-shielding 13.5×**, so
  the rule pays for shields in the loss.
- **Render fix**: a logistic edge's tail TRIPLED a small prism's rendered volume → C1 smoothstep ramp
  one voxel wide (exact box filter of a flat face), union compositing `1 − Π(1 − k a)`.
- **Target recolouring**: nearest CIELAB appearance after a 36° hue rotation — without it every green
  lands on Gold. Tier is coupled to colour, so the target dictates where big shapes go.
- **Amplitude 5** by the same SNR argument as P5 (at 2.2 voxels neighbouring frames differ by exactly
  the still floor 10^-2.7).
- `widen_channels` warm start: lifted 2D model + 16 zero-output channels, bit-identical until the loss
  moves it (checked on one step with an identical fire mask).
- Cost ~0.35 s per backpropagated rollout step at 8×400 particles → ~40 s/training step; shipped run
  BPTT 48, batch 4, capacity 280.
- **CPU run outcome (step ~1300 of 4500, never finished)**: grows from one prism to 280, colours right,
  but "a central blob of mostly shields and stellae" — the rule reached for the biggest volume
  primitives; 10^-1.99 at step 1000 vs still-volume floor 10^-2.38, best log10 −2.25 (never under the
  floor); **18 extinction rollbacks** in the from-birth stage by step 1263 and an overflow spike
  (28,392) at step 1300 (`runs/prism_swim3d.log`). The GPU run (`gpu_run.py`, full backprop, batch 8)
  was prepared but no `results/prism_swim3d_gpu` exists.
- three.js r128 bug: setting `InstancedMesh.count = 0` first sizes `instanceColor` empty → every
  prism drew black.

#### P7. Designed targets (whale prism designer → tadpole units → four elemental plans)
- All-prism humpback (`whale_model.js`): 210 game-drawable prisms, exact SAT overlap check every
  frame, zero intersections, 88×36×44 grid (super-shield dropped).
- Tadpole units from `TadPoleFauna.prefab`: element-fixed heart crystal (world size 2.298 Charge/Space,
  1.737 Mass/Time), parametric spindle (length, bend, roll, thickness), free body prism; whale 115 /
  jellyfish 99, zero intersections; one JS generator drives designer page and Python export.
- Four elemental plans: Mass whale 192 (66% majority), Space jellyfish 88 (61%), Charge pufferfish
  179 (73%), Time dragonfly 76 (70%); element identity enforced in the generator (Charge only stateful,
  Mass ≤1.6 axis ratio, Space rods ≤¼ cross-section, Time small, runs laps → the fastest units);
  domains are REGION SLOTS scored up to permutation (min over slot→domain assignments). Zero
  intersections on all 8 frames for all four.

#### P7b. The prism whale as a grid NCA (second native creature for the flight cell; CPU, 2026-10-05)
- Target: P7's humpback (`results/whale_target/prisms.json`, 210 prisms × 8 swim frames) voxelised by
  `nca3d.py prism_frames` into the lizard's RGBA grid format: boxes |local| ≤ h, shield octahedra
  Σ|local_i|/3h_i ≤ 1, 3× supersampled. The designer whale is a SHELL of sub-voxel plates, so it is
  thickened 0.35 voxel, closed and filled, each voxel coloured by its nearest prism in the flight
  palette (Jade → space blue back/fins/flukes, Gold → charge gold belly/pleats, Ruby → mass coral
  mouth/eye; danger tier ×0.8). D = whale UP (the axis NcaCreature already maps to world up),
  H = lateral, W = length. Scale 0.5, pad 3 → grid 16×26×39 (D,H,W), 456 voxels at α>0.5; the
  swim's per-voxel frame-to-frame change matches the lizard's (0.28 vs 0.26). Figure:
  `results/whale3d_target/target.png` (8 frames, two views), `target.gif`.
- CPU fact worth keeping: on this shared 4-core box torch with 4 threads ran 10-20× SLOWER than 1
  thread (other jobs held the cores; intra-op barriers stall). Train with `--threads 1`.
  Batch 4: 3.4-6 s/it clock stage (vs ~12 s/it batch 8).
- Run: `nca3d.py train --target whale --init3d results/lizard3d_swim/model.pt` (same 16/128 layout,
  warm start), batch 4, pool 256, 800 clock + 5200 pool steps, lr drop at 4000; guard rolls back on
  non-finite / >20× median / extinct loss and resets bad pool samples. First 100 steps (aborted
  3600-step trial): loss 0.0221 → 0.0019. Export: `export_whale_creature.py` → `nca_whale.js`
  (`window.NcaWhale`). Result: see `runs/whale3d_swim/status.json`, promote to `results/` when done.

#### P8. Related work found
- Kim, Pajouheshgar, Süsstrunk, Jakob, Park, "Neural Particle Automata" (arXiv 2601.16096, SIGGRAPH
  2026) independently built the same base particle model (SPH perception with moment-corrected
  gradient, learned position update, splat loss, regeneration, 3D anisotropic Gaussians). Claimed new
  here: growth by budding, a designed collision force, a learned periodic stroke on free particles,
  and a discrete primitive vocabulary taken from a game.

---

### 4. Open / never-done items in this lineage (so they are not mistaken for results)
- H2 (best loss and geometry of the lineage) and G5 (only partial size control) were never 16-tested
  or bar-8 scored with matching `scale_inv`.
- The H3 bar-8 rescore used the wrong loss config (D14).
- The task-vector hybrid stage never ran; solo_mass stopped at step 1990 and its genome is only on
  its branch.
- No swarm run ever trained the animation (`anim=1`); Time "fastest units" speed never measured.
- The prism-swim GPU run never produced a result.
- `tests_passed` still does not check `done` on switch rows (D7).

---

# Part B — designed and evolved families


Scope: `cece/swarm-x-field`, `cece/swarm-x-hgrid`, `cece/swarm-x-evo`, `cece/swarm-x-zoo`,
`cece/swarm-x-distill` (all mined read-only on 1 Oct 2026; branch tips dd5f3b74, 2b2c68e9, a7e859af,
ddc0a5be, 0e9a8a74). Context numbers from the integration branch `cece/gifted-curie-x2cpd0`
(tip 835cadbb) are marked [int].

No `cece/swarm-x-evo16` branch exists on origin (the round-3 brief `briefs/evo16.md` has not pushed).
`zoo` and `distill` pushed CODE ONLY: no NOTE.md, no elite map, no eval16/summary/probe. Their
"discoveries" below are what the code, commit sequence and docstrings establish, and are labelled as
such.

### 0. The yardstick, as it stands now (needed to read every number below)

| Yardstick | What passes | Introduced |
|---|---|---|
| 8-test `swarm_nca.rollout` + `tests_passed` (strict: alive, >=32 tadpoles, strictly closest) | 4 own + 4 fixed switches, `lose_majority` "excess" cull | round 1 |
| 16-test `swarm_eval.evaluate` (fair `cull_to`, 3 samples, majority vote, fail-fast tiers) | 4 own + 12 switches | 2959ba2d |
| + `MAX_TEST_LOSS = 8` (absolute bar; tier 2 needs 4/4 own) | as above, and divergence to the wanted plan <= 8 | 1b41fe12 |

Commit 1b41fe12: "A swarm 35 from every plan was passing because it was the closest of four."

Current `eval16.json` under the loss-8 bar [int, cfa85405 / 0fd0fdd3]:

| Model | passed / feasible | own losses (whale / jelly / puffer / dragon) | n (headcount) |
|---|---|---|---|
| **field** | **9/13** (own 4, std 1, rest 4) | **3.07 / 1.57 / 1.40 / 4.64** | 192 / 88 / 179 / 76 (exactly the plans') |
| hgrid oracle | 1/15 (own 1: jellyfish) | 8.45 / 7.95 / 9.00 / 17.36 | 195 / 94 / 184 / 80 |
| hgrid hybrid (G2 + grid) | 1/16 (own 1: whale) | 7.89 / 14.88 / 8.67 / 12.63 | 196 / 91 / 181 / 80 |
| evo (G2 + genome) | 0/16 | 19.48 / 19.76 / 14.90 / 24.78 | 280 x4 |
| evo compact (no NN) | 0/16 | 22.73 / 29.76 / 21.30 / 30.12 | 280 / 280 / 280 / 258 |
| G2 (reference) | 0/16 | 15.21 / 17.72 / 15.56 / 23.46 | 280 x4 |

Diagnosis [int 835cadbb, `runs/diag_terms.py`]: the own-plan loss recomputed with terms switched off -
pos only: G2 2.6-4.5, evo 2.3-4.5, field 0.0-1.2; + element: G2 6.3-13.6, evo 10.5-14.7, field 0.0-1.5;
+ domain/visuals: G2 13.9-19.1, evo 15.9-24.1, field 1.4-5.5. Trimming G2/evo to the plan's exact
headcount and mix only moves them 13-24 -> 12-18 (`runs/diag_comp.py`). **The outline is fine; the gap
is SORTING** (which element / domain sits where inside the body). That reframes everything below:
field wins by global slot assignment (it sorts), hgrid gets partway by per-(element, slot) grid
densities (it half-sorts), evo/compact/G2 do not sort at all.

---

### 1. Branch `cece/swarm-x-field` - designed attractor fields + boids (no learning)

Files: `field_swarm.py` (680 lines, model + publish), `field_probes.py`, `field_hybrid.py`,
`field_strict_yardstick.py`, `field_showcase.py`, `field_port/FieldSwarmCore.cs` (365 lines),
`field_port_check.py`, `results/field/*` (NOTE, summary, rollout, probe, probes_extra, hybrid,
seed_sweep, strike_sweep, strict_yardstick, port_check, params, showcase.html). 8 commits
229eba9d..dd5f3b74, 12:52-13:32 UTC.

#### Discoveries

1. **A designed model solves the stated problem outright.** Mechanism: boids (sep 2.0, align 5.0,
   arrive-to-home with velocity feed-forward, per-element top speed 0.8/0.8/0.8/2.0) steering to a HOME
   slot in the plan's own animated point cloud (8 frames, 6 steps/frame, ping-pong). Evidence: 7/8 on the
   stock 8-test yardstick (229eba9d), **8/8 on a fair cull** (3395c06d, `strict_yardstick.json`, summed
   wanted divergence 42.1), 13/13 feasible on the 16-test yardstick (2959ba2d), and today 9/13 under the
   loss-8 bar - the ONLY model in the portfolio that passes tier 1 (own losses 3.07/1.57/1.40/4.64).
   Why it matters: it set the ceiling every other family is measured against and is now the game target
   (`briefs/fauna_game.md`: "basically cheating, but it could still produce great gameplay").

2. **Slot assignment = the sorting mechanism.** Hungarian (scipy) on the scorer's own cost
   (Huber position + 60*element + 25*domain-region) with a stickiness bonus (4.0), re-solved every 8
   steps or on membership change. Ablation (`probes_extra.json`): field-only (no assignment) still
   passes 7/8 but own divergence rises 3-8x (9.9/8.7/14.4/22.8 vs 3.1/2.8/1.3/5.5). So "the field alone
   already tells the plans apart" but only assignment makes them crisp. In hindsight (835cadbb) this is
   exactly why field is the sole tier-1 pass: assignment is global sorting by element and domain.

3. **Greedy assignment is as good as Hungarian** (summed divergence 104.3 vs 103.6;
   `ablations`). The port needs only a sort (`Array.Sort`), no O(n^3) solver. Matters for C#/Burst.

4. **Molting is the composition key; it relaxes "element fixed at birth".** A tadpole of a SURPLUS
   element changes element into a deficit one over 10 steps (molt_rate 0.03), crystal re-forms, domain
   never changes. Ablation `molt=0`: whale->jelly switch divergence 33.1 (vs 6.5), puffer->dragonfly
   28.6 (vs 7.7) - "the old mass plates stay". Rationale stated: the only mass-conserving answer to
   surplus once nothing may die on a clock (shedding as a crystal would be an imposed death).

5. **Plan choice by element RATIOS with time hysteresis (dwell 12), and a freeze while contested.**
   While the majority is contested the swarm neither lays nor molts - "that is exactly what made the
   learned rules flip back". Ablation: dwell 1 vs 12 is indistinguishable on the one-shot yardstick
   (7/8 both); it matters only under trickle predation.

6. **Knife-edge predation probe: the body is homeostatic.** `tie_churn`: grown whale culled to
   Mass = Space + 1, then a predator eats 2 random Mass-or-Space tadpoles EVERY step for 120 steps;
   4 seeds x dwell 1/12: **0-1 commits per run, never a flip-back.** While its majority holds it lays
   its own element back faster than this predator eats. Player meaning: converting a swarm requires
   *selective, fast* predation - a skill target, not an accident.

7. **YARDSTICK BUG: the `lose_majority` cull.** The dragonfly plan is [Charge 2, Mass 3, Space 18,
   Time 53]; the stock cull to Mass removes Time until one behind Mass, leaving [2,3,19,2]: **Space is
   73% of survivors**. A faithful swarm correctly becomes a jellyfish (19.9 vs 63-75). Field refused to
   claim the `mode=field, molt=0` ablation's spurious 8/8 (its dragonfly never grew its Space runners:
   56/76 with Space 3). With a strict cull ([2,3,2,2] left) 9 survivors regrow a 178-tadpole whale
   (12.2 vs >=60). Found independently by field, hgrid and evo; fixed upstream as `cull_to` (2959ba2d).
   It also exposed that **E8's earlier dragonfly->whale pass came from mix drift**: under field's
   composition controller E8 LOSES that test (`hybrid.json`).

8. **Composition control alone is a portable but insufficient fix for learned rules**
   (`field_hybrid.py`, `hybrid.json`; 341cbc49). Wrapping G2 / F1 / E8 (weights unchanged) with only
   hysteresis + molting: G2 5->6/8, F1 5->6/8, E8 5->5/8 (gains jelly->puffer, loses dragonfly->whale).
   Own-plan divergences improve (G2 13.9/17.5/15.3/20.7 -> 10.9/15.2/13.1/17.8; F1 15.3/40.2/30.1/22.8 ->
   9.7/10.2/10.9/17.8). On the fair cull: G2 4/8, F1 5/8, E8 5/8, with controller 4/4/5. Why not more:
   the learned swarms sit at the 280 cap (2-4x plan headcount) and keep their old SHAPE at the new mix
   (puffer->dragonfly stays puffer-shaped in all three). Negative result: composition is necessary, not
   sufficient.

9. **Exact headcount and mix, zero deaths.** Laying breeds true; a parent of a deficit
   (element, domain) lays beside the nearest empty slot of that kind, up to the plan's unit count -
   "a body grows from its holes". Census: whale [19,126,25,22] = plan exactly; 192/88/179/76. Zero
   deaths except the cull. Geometry-only diagonals 0.07/0.09/0.43/1.41 vs 12-26 off-diagonal.

10. **Seed robustness.** `seed_sweep.json`: rollout seeds 1-9 x {Hungarian, greedy}: 7/8 in 16/16 runs,
    the failure always the dragonfly->whale cull; summed wanted divergence 103-117.

11. **Heal / strike.** `probe.json`: heal 1.07 / 1.04 / 0.92 / 1.06 (>1 = better than before), headcount
    back to 192/88/179/76 within 120 steps. Strike sweep (`strike_sweep.json`, sphere 1.0/1.5/2.0 x RMS,
    19-67 tadpoles = 21-44% removed): back within 1.2x of pre-strike in **10-30 steps**, never switches
    plan (a strike is element-blind).

12. **Switch latency.** 20-30 steps after the cull until the new plan is closest (scored every 10);
    commit waits 12 dwell; morph spectacle runs 60 steps (vortex about the vertical axis peaking
    mid-morph, old field fading).

13. **Player-facing behaviours (designed, measured):**
    - *Path-anticipating startle wave*: tadpoles near the ship OR its path 10 steps ahead are startled;
      startle relays neighbour-to-neighbour (relay 0.8, decay 0.9) - a fish school's startle cascade.
    - *Per-element flee*: Time darts 2.0, Space jets 1.4, Charge holds 0.6, Mass shoulders 0.5; startled
      Charge plates flash DANGER.
    - *Pufferfish inflation*: homes x(1 + 0.45*threat), deflates after.
    - *Mobbing*: a ship slower than 1 voxel/step is orbited by Time units at 1.4 ship radii. Loiter probe:
      dragonfly mob **24.3** tadpoles within 2 ship radii (max 37) vs 2.4 with mobbing off; body re-forms
      after (4.9 vs 4.5 before).
    - *Predator pass* (0.6 RMS sphere, 3 vox/step through the centroid, kills nothing): share of school
      touched, reacting vs inert: whale 0.28/0.37, jelly **0.08**/0.31, puffer **0.11**/0.28, dragonfly
      **0.11**/0.36; worst score during pass 5.0/4.9/10.6/8.1 (react) vs 3.5/3.6/4.0/6.2 (inert);
      re-formed in 4/8/18/9 steps. The whale barely parts by design (heavy).
    - *Swimming* (`wander`): anchor travels a wandering path and the body yaws to face it; costs score
      only through orientation (whale 26.7, dragonfly 36.0) because the scorer is not rotation-invariant;
      composition still picks the plan, still 7/8. Off for scoring.

14. **Performance.** numpy per swarm-step 0.9 ms (76) / 1.2 (88) / 3.7 (179) / 4.3 ms (192), dominated by
    the O(n^2) neighbour pass and the Hungarian every 8 steps. **C# port** (`FieldSwarmCore.cs`: plain .NET 8,
    System.Numerics, struct-of-arrays, uniform hash grid, greedy assignment, everything incl. vessel
    reaction) scored by the UNCHANGED Python scorer: **7/8, summed 101.7 (Python 103.6)**, same tests pass
    and fail; steady state **0.10 ms (63-76 tadpoles) to 0.61 ms (179) per swarm-step**, idle or with a
    vessel (`port_check.json`: idle 0.099-0.521, vessel 0.109-0.587); growth dearer (<=1.6 ms incl. JIT)
    because laying/molting scan slots x tadpoles. A cell with 16 swarms ~2-10 ms/frame before Burst. Per-
    tadpole state 8 floats. No GPU.

15. **Weakness exposed by the loss-8 bar (from [int] eval16).** Post-switch bodies are rougher than grown
    ones: mass->space 9.25 (rate 0.33), charge->time 7.87 (rate 0.33), time->mass 12.39, mass->time 12.62,
    time->space 10.76 (0.67). Passing switches: space->charge 2.39, mass->charge 2.28, space->time 5.92,
    charge->space 5.74. Infeasible (target element held <2): space->mass, charge->mass, time->charge.
    So field's remaining frontier is the polish of a switched body within 240 steps, not the decision.

#### Process family: **DESIGNED FIELDS (a puppet with a global blueprint)**

- **How it produces a lifeform.** A human writes the creature: a per-plan slot table (the target JSON,
  baked per frame), a per-(plan, element) RBF density field as fallback, a global min-cost assignment of
  tadpoles to slots, a swarm-wide census for the plan decision, and hand-tuned reactions. Nothing is
  trained; `params.json` (~40 numbers) IS the model. Regenerated in ~4 min.
- **Capabilities.** Exact sorting, exact headcount and mix, full heal in 10-30 steps, smooth morph
  spectacle, rich, controllable player reactions, sub-millisecond C# port, deterministic and debuggable.
- **Limits.** Every body plan must be authored as a point cloud; global (swarm-wide) knowledge is used
  everywhere (assignment, census, "nearest empty slot") - not emergent; relaxes element-at-birth via
  molting; switched bodies under the 8 bar on 4-5 transitions; orientation-locked scorer forbids
  swimming during tests.
- **Orthogonal to the others.** It is the only family whose sorting is GLOBAL and explicit (assignment),
  whose composition is fixed by MOLTING (identity change) rather than birth/death, and whose behaviour is
  authored rather than searched. Keep it as the "game-ready puppet" species: zoo searches *its* parameter
  space (a sub-family), distill tries to *imitate* it locally; neither replaces it.

---

### 2. Branch `cece/swarm-x-hgrid` - a cellular automaton steering a collision automaton

Files: `hgrid_core.py` (grid geometry, splat/sample, `PlanFields` 47 channels + 12 flow), `hgrid_boid.py`
(`FieldBoid`, `OracleField`, `HSwarm`), `hgrid_hybrid.py`, `hgrid_vote.py`, `hgrid_nca.py`,
`hgrid_e2e.py`, `hgrid_chain.py`, `hgrid_eval.py`, `hgrid_seeds.py`; results `hgrid/{oracle,hybrid_g2,e2e}`
(summary, rollout, probe, chain, rule_e2e.pt), NOTE.md (recommendation never completed: "(to be
completed)"). 9 commits 6bdcce00..2b2c68e9, 12:58-15:07 UTC.

#### Discoveries

1. **A coarse grid morphogen can carry the plan decision AND the composition, with simple boids doing
   collision.** Grid: 16^3 lattice of 6-voxel cells snapped to the swarm centroid; trilinear splat/sample.
   `PlanFields` rasterises each plan/frame into 47 channels: wanted density per (element x domain-slot)
   class + per-element attribute fields (prism, Charge tier, facing, spindle) as density-weighted sums.
   Boid rule (~60 lines): move up the gradient of its class DEFICIT x3 + all-class deficit x1 + plan flow
   x4, persistence 0.6, noise; lay with p = 0.1 x relative class deficit (grid integral), egg goes toward
   its class deficit, 25% of births (and all births from a full class) take the most-wanted element of
   the parent's domain; look eased toward attribute channels. Result: **oracle 7/8, hybrid 7/8** (8-test),
   12/15 feasible and 12/16 on the fair 16-test (a03ff963), oracle own divergence 7.2/9.8/10.3/17.7.

2. **Deficit-gradient descent per (element, domain) class is a semi-local SORTING mechanism.** Under the
   loss-8 bar hgrid lands just over the line: oracle 8.45/7.95/9.00/17.36, hybrid 7.89/14.88/8.67/12.63 -
   between field (1-5) and the unsorted learned rules (15-30). Why it matters (inference from the
   835cadbb diagnosis): density deficits by class sort elements and domains without a slot table; the
   residual is grid resolution (6-voxel cells) and the dragonfly's thin Time runners (17.4).

3. **The grid fixes the learned rule's composition, not its motion.** `hgrid_hybrid.py`: G2 weights as
   shipped (motion, look, hatching, death) + grid laying/egg-element only: G2 5/8 -> **7/8**; own
   7.2/17.2/6.8/11.1; switches 3/4. Bodies come out at plan size (196/91/185/80 oracle), NOT the 280 cap.
   This is the cleanest evidence that G2's switch failure was mix drift.

4. **Starvation as a composition sink (the only family that uses death).** A tadpole whose class the
   swarm holds >15% more than the plan wants AND that sits in an overfull cell gains hunger and withers to
   a crystal. Whale culled of Mass shed ~55 crystals becoming a jellyfish; the 4-creature chain sheds 126.
   Ablation `starve=0`: still 7/8 but misfits linger (whale->jelly keeps 18 Mass + 31 Time, divergence 31.4
   vs 11.2; puffer->dragonfly 39.6 vs 19.9, a near miss against puffer 42.4). Game reading: "eat the
   whale's mass and it turns into a jellyfish and spills crystals" - loot falls out of the switch. Note
   the tension with "no imposed death": this is framed as starvation (no niche), which the ecology
   invariants allow, unlike field's molting which changes element instead.

5. **A density field cannot express circulation -> add a FLOW channel.** The formed swarm sat still
   (0.05 voxel/step) until the plan's own frame-to-frame unit velocity was rasterised as 12 FLOW
   channels; then 0.13-0.44 voxel/step, Time fastest (runners lap the wings, jelly pulses, whale units
   drift along the body). General finding: a positional target needs a velocity field to look alive.

6. **Chain demo: one swarm becomes four creatures.** Eaten four times: whale (8.4) -> jelly (11.7) ->
   puffer (10.0) -> dragonfly (18.0), next best 54+; 4/4 for oracle and hybrid (`chain.json`).

7. **Probe.** Oracle heal 0.72 / 0.99 / -0.08 / 0.82 (puffer regrows to size but spines are
   pose-sensitive); hybrid 0.94 / 0.99 / 0.30 / 1.73; e2e -0.35 / 0.75 / 0.07 / -4.22 (worse). G2 baseline
   0.33 / -0.87 / 0.67 / n.a.

8. **NEGATIVE: a learned vote NCA (plan decision per cell over designed templates) scored 3/8 twice.**
   v1 soft field, v2 hard per-cell argmax + 5% hysteresis. Per-cell accuracy vs live majority 0.88-0.90,
   yet closed loop 3/8: Space and Time seeds grew pufferfish, the whale never released Mass after a cull.
   Mechanism: the composition feedback that gives the designed grid its hysteresis makes a learned grid's
   mistakes PERMANENT (a hesitant field breeds Mass/Charge -> majority follows -> label agrees with the
   mistake; an untrained cell's argmax is "mass"; a soft mixture of plans asks for a mixed budget that
   favours the two big plans). Labelling with the sticky *intended* plan instead did not learn in 40
   episodes (accuracy 0.3-0.5): the cue (mix at the cull) is gone by the time the error shows. "G2's
   credit-assignment problem moved one level up, not solved by it."

9. **NEGATIVE: a learned field NCA (grow the whole 47-channel morphogen, DAgger vs the oracle) is too
   slow on 4 CPU cores.** First version diverged (unbounded state); bounded + leaky + value-
   parameterised was stable but after 7 episodes (~1700 field steps) density error ~1.0 relative
   (= predicting empty). Needs GPU-scale training. Paused (`runs/hgrid_nca`).

10. **End-to-end: G2 conditioned on 9 grid-sample inputs, fine-tuned with swarm_loss** (`hgrid_e2e.py`,
    zero-init input columns so step 0 = hybrid). Step-150 checkpoint 7/8 (bfceb711), own 7.81/13.85/9.41/
    9.67 - Time improves from 11.1 to 9.7, Space worse; heal degrades. No clear win before the session
    ended.

11. **Engineering traps:** (a) the field the boids follow must be DETACHED when training the grid through
    its own loss - otherwise every position keeps the whole autograd history (14 GB, OOM-killed; 0632c06d);
    (b) relu on a zero-init density readout passes no gradient (regress raw state); (c) one rollout is
    noisy at the +-10-20 summed-divergence level -> `hgrid_seeds.py` multi-seed comparison.

12. **The switch is not a wave** with the designed grid: the whole field flips in one step (global
    decision). The vote NCA was meant to make a plan sweep across the body as a spatial wave; it failed
    for the reasons in 8.

13. **Cost:** 8-test rollout ~45-55 s (vs field's ~4 min for its whole publish incl. probes); the
    16-test eval takes 830-844 s (oracle/hybrid) vs field 92 s and evo 186 s - the slowest family to
    evaluate (torch grid ops per step).

#### Process family: **MORPHOGEN GRID (a CA over a collision automaton, two-level hierarchy)**

- **How it produces a lifeform.** An upper-level coarse 3D field holds, per cell, how many of each
  (element, domain) class should be there and what they should look like (and how they should move);
  lower-level boids read only their own cell (deficit gradient, flow, attributes) and do collision.
  The field can be designed (oracle), or learned (vote NCA, field NCA), and the boid can be designed or
  the learned G2 rule (hybrid, e2e).
- **Capabilities.** Plan-sized bodies with the right mix, composition steering for any per-particle rule
  (lifts G2 5->7/8), loot-dropping switches via starvation, flow-driven liveliness, chainable switches;
  partial sorting (losses ~8-17) with NO per-particle slot identity.
- **Limits.** Learning the upper level failed both ways tried (credit assignment at the grid level;
  compute); the designed grid flips globally; 6-voxel resolution caps crispness (just above the loss-8
  bar); slowest to evaluate; poorer heal on pufferfish and with e2e.
- **Orthogonal.** The only family whose control lives in a SPATIAL FIELD rather than on particles or a
  global table; composition by STARVATION + class-directed egg element; per-class density (not slot)
  identity. It is a natural carrier for a learned *plan generator* (the field) separate from the motion,
  and the natural home of the "sorting by deficit" idea between field's global assignment and fully local
  rules.

---

### 3. Branch `cece/swarm-x-evo` - gradient-free search over behaviour genomes (CMA-ES)

Files: `evo_model.py` (`EvoRule` = G2 + genome; fitness helpers incl. batched probe and liveliness),
`evo_search.py` (CMA-ES), `evo_compact.py` (no-NN rule, track b), `evo_motion.py`, `evo_publish.py`;
results `evo/` (NOTE interim 13:35, genome.json/.npy, robust.json, evo_log.jsonl, probe, summary, rollout)
and `evo/compact/` (same + log). 4 commits 5a8e9264..a7e859af, 12:55-14:15 UTC. The final NOTE (stage 2,
compact track write-up) was never written.

#### Discoveries

1. **Behaviours, not weights, carried the gain: CMA-ES that can ADD/DELETE actuators.** Genome (95
   floats, final layout + B6) wraps G2's frozen weights: B1 lay homeostat (`sigmoid(k*deficit/0.1+b)/
   sigmoid(b)`, deficit = desired share D[locked majority] - current share; production gating only), B2
   egg choice (share sigmoid(p_egg) of eggs take softmax(beta*deficit/0.1); domain true), B3 majority lock
   (margin 0.25*sigmoid(lock)), B4 sated rest (no-op), B5 output-layer gain/bias (35+35), B6 per-element
   swirl about the plan's long axis. Each has an on/off gene (>0 = on), so CMA can delete it. Stage 1
   searched 25 behaviour dims only (sw_out = -1, B5 never searched). Result: **8/8 at seed 7, 7.81/8 mean
   over 16 held-out seeds** (13x8, 3x7) vs G2 5/8 and 4.25/8 (6116db59). On the fair 16-test: **16/16,
   every test 3/3 samples** (421b702c) - out-of-sample on the 12 new switches, though searched at seed 7.

2. **Ablation: the lay homeostat is the load-bearing behaviour** (`robust.json`, 16 held-out seeds):
   best 7.81; without egg choice 7.19; **without lay homeostat 5.44**; all off (= G2) 4.25. Lock evolved
   to a 1% margin (effectively off: sw_lock -0.44). B5 weight fiddling: unused. Answer to the user's
   "only fiddle with weights or add/subtract behaviours?": adding two behaviours did it; weights untouched.

3. **Evolution sharpened the desired-share table into a MAJORITY AMPLIFIER.** `D` evolved from the plans'
   mixes to e.g. Mass majority 0.01/0.90/0.07/0.02 (plan 0.10/0.66/0.13/0.11), Charge 0.83 (plan 0.73),
   Space 0.76 (plan 0.61), Time 0.81 (plan 0.70). Egg share 0.259, beta 3.42, gate k 3.0 b 0.34. Census:
   whale 237/280 Mass (85% vs plan 66%), jelly 205/280 Space, puffer 219/280 Charge, dragonfly 220/280
   Time. Why it matters: amplifying the majority makes switches robust (the new majority runs away from
   the old), but it DISTORTS the composition, which the loss-8 diagnosis shows is costly: evo's +element
   term (10.5-14.7) is worse than G2's (6.3-13.6), and evo's own losses (14.9-24.8) are no better than
   G2's. (Inference connecting genome.json, summary census and 835cadbb.)

4. **Evolution EXPLOITED the yardstick bug.** Stage 1 sat at 7/8 on dragonfly->whale until CMA moved
   D[Time] to carry Mass (10%) above Space (7%), so the stock `lose_majority` cull handed Mass the
   majority. "That is yardstick-fitting, not biology" (NOTE). Same weakly for whale->jelly (Charge must
   stay below Space). Independent third discovery of the cull bug; also a cautionary case: CMA-ES on a
   strict discrete objective finds and fits the yardstick's flaws.

5. **Evolution curve (stage 1, 25 dims, pop 10, sigma0 0.5, 2 fresh seeds per gen shared by the
   population, 4 workers, ~31 min):** best fit 6.63 (gen 0) -> 6.65 (4) -> 7.17 (9) -> 8.20 (14); mean
   passed 5.45 -> 6.80; held-out (CMA mean, 8 seeds) at gen 14: 8,7,8,8,8,8,8,8. Fitness = tests passed +
   0.5 x mean per-test margin ((best other - wanted)/sum, -1 if < 32 tadpoles).

6. **A compact NO-NEURAL-NET rule (31 genes) does as well as G2+genome on the old yardsticks.**
   `evo_compact.py`: each plan is a DESCRIPTOR, not a template - per animation frame one Gaussian per
   (element, domain-slot) group (mean + Cholesky, ~10 numbers x 5-6 groups x 8 frames), fitted once from
   the targets; each tadpole carries a private anchor u ~ N(0,I) drawn at hatch; home = centroid +
   scale*(mu_g + L_g u), scale ~ (n/plan size)^gamma; swirl about the plan's long axis (Time hardest),
   jitter; visuals take the plan's per-element average eased at vis_rate; laying = evo's homeostat.
   Result: 7/8 at seed 7, **7.75/8 held-out** (a7e859af), **16/16** on the fair 16-test (9c8939bc). CMA
   curve: best 6.10 (gen 0) -> 8.14 (gen 8) -> 8.2 plateau; held-out 6.36 (gen 4) -> 7.80 (gen 9) -> 8.20
   (gen 14) -> 7.94 (gen 19); ~100 s/gen. Evolved D even more extreme (Charge 0.94).

7. **But under the loss-8 bar both evo models fail every test** ([int] eval16): evo 19.5/19.8/14.9/24.8,
   compact 22.7/29.8/21.3/30.1, all at the 280 cap (compact time 258). Mechanism: the Gaussian-group
   descriptor gets the outline and coarse per-group placement but blobs, not crisp sorting; the 280-slot
   cap (2-4x plan headcount) and the majority amplifier inflate the element term. Neither model has a
   headcount target (that was a round-3 brief item: `evo16.md` "a HEADCOUNT target per plan").

8. **Weak heal.** G2+genome probe heal: mass 0.46, **space -1.17** (worse after regrowing), charge 0.94,
   time 0.60; swarms refill to 280. Compact: 0.04 / 2.24 / null (span too small) / 1.68 (the clipped
   `fast_probe` uses [-1,1]). Heal + liveliness fitness terms were added (`EVO_W_HEAL`, `EVO_W_LIVE`,
   4b49ab24) with a batched probe, but no run using them was published.

9. **Liveliness metric** (`evo_model.liveliness`): per plan, mean over elements of min(1, measured 8-step
   heart displacement / plan per-frame speed), weighted by the element's target speed - "runners that run
   laps score 1, a jittering blob ~0". `evo_motion.py` reports per-element heart speed, centroid drift and
   RMS breathing. Reusable as a "looks alive" descriptor.

10. **B6 swirl actuator** added as an on/off gene (per-element rad/step = 0.05 tanh(gene) about the plan's
    long axis, from the plan's principal eigenvector) - the motion counterpart of the composition genes.

11. **Cost.** CMA generation ~95-150 s (pop 10 x 2 seeds on 4 workers); fast_rollout batches all four
    plans in one batch; 16-test eval 186 s (evo) / 137 s (compact).

#### Process family: **EVOLVED BEHAVIOUR GENOMES (CMA-ES over actuators + their parameters)**

- **How it produces a lifeform.** Take a base rule (the backprop-trained G2 MLP, or a ~30-parameter
  designed template rule), bolt on candidate BEHAVIOURS each gated by a switch gene, and let CMA-ES
  optimise the strict discrete objective directly over the full 480-step horizon (no BPTT truncation),
  with fresh common-random-number seeds per generation and held-out seed checks.
- **Capabilities.** Optimises what backprop never sees (discrete pass/fail, long horizon); performs
  structural search (keeps/deletes behaviours: found homeostat >> egg choice >> lock, weights unneeded);
  cheap (25-31 dims converge in ~15 gens / 30 min on 4 cores); ablations come for free; generalised from
  8 tests at one seed to 16 tests and to held-out seeds (7.75-7.81/8).
- **Limits.** Exploits whatever the fitness function permits (fitted the cull bug, amplified the majority
  at the cost of composition fidelity); inherits the base rule's sorting blindness - cannot fix
  placement-within-body, which is now the bottleneck (0/16 under the loss-8 bar); always fills the 280
  cap; weak heal; dimension-limited (B5's 70 dims never searched).
- **Orthogonal.** The only family that searches over the STRUCTURE of the rule (behaviour on/off) and
  optimises the true evaluation end to end; composition by PRODUCTION GATING (lay homeostat + egg
  choice), never death, never molting; works as a wrapper around any base rule (learned or designed).
  Complements: sorting mechanisms from other families (field assignment, hgrid deficits, the planned
  `sort` affinity matrix) can become genes for it to switch on and tune.

---

### 4. Branch `cece/swarm-x-zoo` - quality-diversity (MAP-Elites) over field's parameters

Files: `zoo_model.py` (`ZooCfg(FieldCfg)`, `ZooSwarm(FieldSwarm)`, GENES table), `zoo_probe.py`
(behaviour descriptors), `zoo_search.py` (MAP-Elites), `zoo_publish.py` (map, distinct-elite selection,
full per-elite eval), `zoo_showcase.py` (per-elite playback pages, gzip int16 frames ~1-2 MB);
`results/zoo/inert_crowd.json` only. 3 commits a909e3a3..ddc0a5be, 14:16-15:08 UTC. **No NOTE, elite
map, elites.json or eval16 pushed** - the search ran in `runs/zoo` (gitignored).

#### Discoveries (from the code and commits)

1. **One designed model spans many creatures via switchable behaviour genes.** Field kept intact, plus
   nine on/off-gated behaviours: `breathe` (whole-body pulse, rhythm), `wave` (travelling peristaltic
   pulse along the long axis, added ddc0a5be), `jitter` (Ornstein-Uhlenbeck wobble), `orbit` (each
   tadpole circles its own slot - shimmer), `burst` (radial explosion at the start of a morph - drama),
   `curious` (approach a ship at 1-3x sense range; flee wins up close), `hunt` (all elements mob),
   `bristle` (any startled tadpole flashes DANGER), `rush` (laying multiplier after a sudden headcount
   drop - heal), plus per-plan `inflate4` (any plan may swell, not only the pufferfish). Total ~50 genes
   in unit-cube coordinates incl. field's boid, laying, molting, morph, sense/relay, per-element flee and
   mob, mob_speed. Presentation genes (`wander`, `turn`) evaluated at 0.

2. **Player-facing behaviour DESCRIPTORS as measurable axes** (`zoo_probe.describe`, averaged over plans):
   liveliness = idle mean speed; looseness = distance to home slot; cost = ms/step; **stance** =
   log2(crowd/inert crowd) with a parked ship (<0 flees, >0 mobs); touched share in a ship pass; heal_steps
   after a strike (to within 1.2x + 0.5, every 5 steps, cap 150); drama = peak RMS radius / grown radius
   during a switch; switch latency. Baseline `inert_crowd.json`: share within 2 ship radii of a body that
   does not react - whale 0.20, jelly 0.034, puffer 0.072, dragonfly 0.040.

3. **The viable behaviour space (all-pass constraint) is narrow along the chosen axes.** ddc0a5be
   re-binned the map axes "to measured ranges": stance [-1.5..2.0] -> [-0.25..2.6] (nothing flees
   measurably below an inert body; the space skews to mobbing), live [0.3..0.8] -> [0.38..0.65] vox/step,
   drama [1.1..1.75] -> [1.12..1.38]. Mutation widened (sigma {0.06,0.12,0.25} -> {0.08,0.16,0.3,0.45},
   per-gene rate 0.30 -> 0.35) - evidence that the first map was under-filled. (Inferred from the diff.)

4. **QD acceptance = full pass, cheap rejection.** A candidate must pass EVERY feasible test
   (passed == feasible >= 13) with 1 sample per test and fail-fast gates (4, 8), so a failure costs about a
   third of a full evaluation; within a cell the crisper body (lower mean wanted divergence) wins;
   farthest-point selection in descriptor space (scaled by 5-95% range) picks the 4-8 most distinct elites.
   **Caveat:** this acceptance rule predates the loss-8 bar (1b41fe12, 15:31, after zoo's last commit
   15:08). Under the bar field itself is 9/13, so the zoo's archive (if any) was admitted on the old bar
   and the current gate would admit nothing until switched bodies get under 8.

#### Process family: **QUALITY-DIVERSITY over a designed model (MAP-Elites, a sub-family of field)**

- **How it produces lifeforms.** Mutate/crossover elites in a ~50-gene designed behaviour space, keep
  only individuals that still pass the yardstick, bin survivors on player-facing descriptor axes
  (stance x liveliness x drama), keep the crispest per cell, then pick maximally distinct elites, name
  them and give each a playback page.
- **Capabilities.** Produces a SET of creatures (personalities: mobbers, shimmerers, theatrical morphers,
  pulsing breathers) instead of one optimum; correctness is a hard filter, so every elite is a valid
  species; descriptors double as design vocabulary and per-elite cost tracking (game budget).
- **Limits.** Inherits field's global assignment (not emergent) and its loss-8 weakness on switched
  bodies; descriptor ranges narrow under the all-pass constraint; results not yet published; the
  acceptance gate needs updating to the loss-8 bar.
- **Orthogonal.** The only family whose objective is DIVERSITY of behaviour rather than a single score;
  it explores the "personality" axes (how it reacts, moves, morphs) that no other family measures. Its
  descriptor probe (`zoo_probe`) and gated-behaviour pattern can be applied to any other family's
  parameter space (e.g. evo genomes, hgrid boid weights) to make zoos of emergent species too.

---

### 5. Branch `cece/swarm-x-distill` - distilling field into a purely local learned rule (BC/DAgger)

Files: `distill_student.py`, `distill_teacher.py`, `distill_train.py` (BC + DAgger), `distill_finetune.py`
(short-horizon BPTT + BC anchor), `distill_publish.py`. 7 commits f36be115..0e9a8a74, 14:14-15:51 UTC.
**No results pushed** (no NOTE, eval16, summary or student.pt on the branch).

#### Discoveries (from the code and the commit sequence)

1. **A concrete local-perception contract for "emergent, not puppet".** Each tadpole sees only itself,
   neighbours within World.R (kernel w = (1-d^2/R^2)^3: per-element / per-domain weighted counts, mean
   offset, mean velocity, mean plan belief, separation vector, startle), the population signals SwarmRule
   already has (headcount, element mix), and a ship within sensing range. No slot table, no global
   assignment, no "nearest empty slot". 78 features (+6 genome features); MLP 3x256 SiLU; heads:
   velocity 3 | look 11 | lay gate + egg direction 3 + egg element 4 | MOLT gate + target 4 | plan belief
   PB 4 (a hidden channel neighbours see) | startle 1. Stateless (all state in swarm channels).

2. **Designed LOCAL morphogens replace global knowledge:** (a) a centroid estimate Z per tadpole refined
   by dynamic average consensus (carries its own motion, diffuses over the neighbour graph, leaks 0.004
   toward its own position so births/deaths cannot bias it forever; 2 consensus sweeps/step, weight 0.6);
   nothing reads the true centroid. (b) an inherited clock CLK (animation phase from the parent).
   Positional information this way is the same idea as the later `posinfo` brief.

3. **Teacher as a LABELER on a shadow swarm (DAgger done right).** `distill_teacher.Labeler` runs the
   unchanged `FieldSwarm` on a shadow of whatever swarm is simulated - including the student's own -
   keeping the teacher's private per-tadpole memory (slot HOME, startle, birth age) across steps while
   positions/elements/molts are re-synced each step; labels: new velocity, look, startle, committed plan,
   lay events (parent, direction, element), molt starts (who, into what). Episodes mix growth, fair-cull
   switches to random elements, strikes and ship passes; DAgger advances by the student with a
   per-sample teacher coin beta.

4. **Plan belief: learned head -> designed census mode (84835f8f).** The sequence learned PB (f36be115)
   -> `plan_mode=census` (each tadpole reads the population census and keeps its own dwell counter: a new
   majority must hold 12 steps before it believes it - field's hysteresis computed by every tadpole) ->
   `--plan_mode` flag (20f07e55) indicates the learned plan belief was not adequate and was replaced by a
   locally-computed designed decision. (Inference from commits; no numbers published.) Same lesson as
   hgrid's vote NCA: the plan DECISION is the hardest part to learn.

5. **Genome features (0e9a8a74):** own element's plan share - live share, the full deficit vector (plan
   mix - live mix) and plan headcount - live headcount, all under the tadpole's OWN plan belief. "The plan
   constants are the genome; no new perception." I.e. the student was handed evo's/field's composition
   signals as inputs - pointing to composition (lay/molt) being hard to infer from raw census alone.

6. **Stage 3 finetune design:** start states from the student's own rollouts (grow 240, fair cull to a
   random element, 240 more) snapshotted at random times with STICKY labels; K steps of BPTT through
   velocity and look heads only (laying/molting stay sampled, non-differentiable); loss = swarm_loss +
   w_over + min_body + W_BC x BC loss on teacher-labelled rows. Calibration knobs lay_scale / molt_scale /
   lay_temp exist for post-hoc rate tuning.

7. **What is unknown:** whether a local student reaches field's SORTING (the 835cadbb bottleneck) at all.
   Field's sorting comes from global assignment, which has no local analogue in this feature set except
   the centroid estimate Z and the clock; distillation of a global assignment into local features is the
   open question, and the student's score was not published.

#### Process family: **DISTILLATION (imitation of a designed teacher into a local learned rule)**

- **How it produces a lifeform.** Behaviour-clone the teacher's per-tadpole actions (velocity, looks,
  lay/molt events, plan) from teacher-driven episodes, then DAgger (teacher labels the student's own
  states) so it recovers from its own mistakes, then fine-tune on the real swarm loss with a BC anchor.
- **Capabilities.** Turns a working puppet into a candidate emergent creature; isolates exactly which
  global ingredients a local rule cannot reproduce (each feature/actuator can be ablated); dense
  per-step supervision avoids the credit-assignment wall that sank vote-NCA and learned laying gates.
- **Limits.** Bounded by the teacher (it imitates field's behaviour, including molting); global
  information the teacher uses (assignment) may be unlearnable locally; results not yet in.
- **Orthogonal.** The only family that uses a DESIGNED model as a supervisor for a LEARNED local rule;
  it is the bridge between "designed" and "emergent", and its probes measure the information gap between
  global and local, which no other family measures.

---

### 6. Cross-family findings

1. **Three independent rediscoveries of the cull bug** (field, hgrid, evo) -> `cull_to` (2959ba2d). In
   evo it was found by watching CMA exploit it; in field by refusing a spurious 8/8; in hgrid by the
   chain demo. Lesson: a strict discrete test needs a semantics check, and a searcher will find the gap.
2. **Composition is necessary, not sufficient.** Four composition mechanisms, all of which fix switch
   drift: molting (field), starvation + class-directed eggs (hgrid), lay homeostat + egg choice (evo),
   learned lay/egg/molt heads (distill). Composition alone lifts learned G2 to 6/8 (field controller) or
   7/8 (grid controller) - the grid adds spatial class targets on top of mix control.
3. **Sorting is the frontier under the loss-8 bar.** Ranking by own-plan loss tracks the sorting
   mechanism's globality: global assignment (field 1.4-4.6) < per-class grid deficits (hgrid 7.9-17.4) <
   per-group Gaussian anchors (compact 21-30) ~ none (G2 15-23, evo 15-25).
4. **Headcount:** only field and hgrid grow bodies at plan size; G2 / evo / compact always fill the
   280 cap (2-4x), which the diagnosis says matters less than sorting (trim -> 12-18).
5. **Learning the plan DECISION fails repeatedly** (hgrid vote NCA 3/8; distill moved to a designed
   census; learned laying gates collapse without a body floor) - the decision with hysteresis is cheap to
   design and hard to learn because its errors are self-confirming through composition feedback.
6. **Heal:** designed field heals fully (0.92-1.07, 10-30 steps); grid-composition rules heal moderately
   (0.7-1.0 except puffer); evo/G2 heal poorly (space -1.17 / -0.87).
7. **Performance:** field C# 0.10-0.61 ms/swarm-step (single-threaded managed, no Burst) is the only
   measured game-port number; eval time field 92 s < compact 137 s < evo 186 s << hgrid 830-844 s.

### 7. Keeping the families distinct (species of process)

| Family | Produces lifeforms by | Sorting | Composition | Decision | Unique axis |
|---|---|---|---|---|---|
| field | authoring (slots + fields + reactions) | global assignment | molting (identity change) | census + dwell | game-ready puppet, C# port |
| zoo | QD search over field's genes | inherited (global) | inherited | inherited | behaviour diversity / personalities |
| hgrid | two-level CA: grid morphogen over boids | per-class density deficits | starvation + class-directed eggs | grid (designed) | spatial control field, loot-shedding switches, flow |
| evo | CMA-ES over behaviour on/off genes + params | none (base rule's) | production gating (lay homeostat, egg choice) | evolved lock (unused) | structural search on the true objective |
| distill | BC/DAgger from field into a local MLP | to be learned locally | learned lay/egg/molt heads | learned -> census | measures global-vs-local information gap |

Merging would erase the contrasts: e.g. adding assignment to evo would make it field; making hgrid's grid
global would make it field with a raster. Recommended reuse is via WRAPPERS and GENES (evo can switch on
any family's mechanism; zoo's descriptor probe applies to any family) rather than code merges.

---

# Part C — colony, metamorphosis, play, creature, and the in-game build


Mined 2026-10-01 (read-only). Branch heads:

| branch | head | base | own commits |
|---|---|---|---|
| `cece/swarm-x-colony` | `a6050a41` | `6cbd9690` (gifted-curie) | 2 (12:52, 13:45 UTC) |
| `cece/swarm-x-meta` | `cfb25e33` | `6cbd9690` | 4 (12:51 to 15:36) |
| `cece/swarm-x-play` | `015b1b85` | `6cbd9690` | 4 (12:54 to 15:41) |
| `cece/swarm-x-creature` | `9d9d9a11` | `4379f772` | 2 (14:54, 15:15) |
| `cece/swarm-fauna-game` | `ff55379d` | `5e7a37af` (= bleeding-edge HEAD) | 3 (14:51 to 15:09) |

**Yardstick context.** At 15:31 the parent branch added an absolute bar (`1b41fe12`, `MAX_TEST_LOSS = 8`): a test fails if
its divergence to the wanted plan is over 8, even when that plan is the closest. Tier 2 now needs 4/4 own plans. Every
G2-derived number below was reported BEFORE that bar. Under it, **all four G2-derived branches score 0** (their
own-plan divergences are 12-32). Only the designed field model passes tier 1 (`cfa85405`).

**Element index order** in every census array is `[Charge, Mass, Space, Time]`.

---

### 1. `cece/swarm-x-colony`: a shared GRU colony brain

#### Discoveries

1. **No result was produced. This is code only.**
   - The branch holds only `Tools/NCA/colony_nca.py` (576 lines). There is no `results/colony/`, no NOTE.md, no
     evals and no trained rule.
   - The second commit makes the trainer snapshot every 20 steps "so a container restart loses little". So the
     session was being killed by container restarts. meta hit the same problem (it checkpoints every 25 steps).
   - **Why it matters:** the colony-brain hypothesis is still untested. Do not cite it as a negative.

2. **Design: a population-level actuator that the per-tadpole rule lacked.**
   - One `GRUCell` (H = 32) per swarm reads a pooled, domain-neutral summary each step: count, element shares and
     counts, RMS size, the shape eigenvalues of the normalised gyration tensor, and the mean of hidden channels
     12..25. Attention pooling (two learned queries) is optional.
   - It broadcasts two things:
     - a K = 8 code, concatenated into every tadpole's input (`w1` gets K zero-initialised columns);
     - a **per-element breeding bias added inside the learned laying gate**:
       `q = sigmoid(lay_gain * s[LAY] + lay_bias + col_lay[elem])`.
   - The docstring states the key insight: *"a full swarm (280 slots) can only change its mix while it refills after
     a loss"*. So the colony must decide WHICH elements breed. That is the same lever evo's homeostat and the game's
     plan-driven laying use.
   - `selftest` asserts that a fresh colony model is the G2 rule bit-for-bit over 40 steps (difference below 1e-3,
     same alive set) in the vote/mean and free/attn modes.

3. **An inspectable decision bottleneck.**
   - In vote mode, `p = softmax(W h)` runs over four learned plan codes, and code = `p @ E`.
   - `w_vote` can supervise that vote with the sticky plan label (cross-entropy over the BPTT window). `vacc`, the
     vote accuracy, is logged.
   - `vote_trace()` records the vote and lay bias every 10 steps across the 240-step cull. It would be the first
     direct readout of "when did the swarm decide to switch".

4. **The trainer adds an `excess` cull mode** beside `ratio` and `tie` (`p_ratio 0.34`, `p_excess 0.33`), described as
   "the eval's mode". It uses separate learning rates: rule 5e-4, colony 2e-3.

#### Process family: centralised learned control

- **How it produces lifeforms:** the G2 per-tadpole MLP, trained by BPTT, is conditioned on a slow shared latent.
- **Capability:** hysteresis and plan memory live in one recurrent state rather than being inferred locally by 280
  units. It can bias breeding per element.
- **Limits:** untested. It inherits G2's 280-slot cap and its composition-dominated loss.
- **Orthogonal to the others:** it is the only family with a learned, recurrent, population-level state. The game's
  designed equivalent is `Dwell` (12 steps) plus the plan-deficit laying choice. The colony could replace or learn
  those. Its breeding bias overlaps evo's homeostat and meta's selective laying.

---

### 2. `cece/swarm-x-meta`: metamorphosis and selective laying

#### Discoveries

1. **Learned metamorphosis does not fix switching (a clear negative).**
   - Run m1: `p_meta 0.05`, bias −4, 12-step metamorph, slowed 0.75, cap of 8% concurrent. It was warm-started from G2
     and evaluated every 250 steps: tests 4, 5, 4, 4, 4 at steps 250 to 1250. The baseline G2, re-measured, is 5/8.
   - The published rule (step 500, 5/8) has the same single switch as G2 (jellyfish → pufferfish).
   - The training loss was flat at about 83-116 over 1,290 steps. It never trended down.
   - (`results/meta/NOTE.md`, `m1_evals.jsonl`, `m1_log.jsonl`)

2. **Metamorphs go toward the WRONG element as often as the right one.**
   - Metamorph counts after the cull (mass/space/charge/time switch tests) rose from 0/0/1/0 at step 250 to
     6/48/17/195 at step 1000, then fell to 6/5/1/34.
   - The time→mass test ended with a **Space** majority after 195 metamorphs.
   - It "learned to metamorph in bursts after a disturbance", but without direction.
   - The next fix was a conformity prior (`meta_conform`: plus conform × log(share of the target element), so
     metamorphs lean toward the majority). That is commit `5d1d0813`. No result for it was committed.
   - **Why it matters:** a per-unit identity change with a score-function gradient is too weak and noisy a signal
     to learn direction from. The game reached the same conclusion from the opposite side (see §5, D1): molting must
     be designed, targeted at plan deficits, and confined to a window after the swarm has committed to a plan.

3. **Under the loss-8 bar the published meta rule fails every test, yet its geometry is fine.**
   - Own-plan divergences: whale 14.35, jellyfish 16.06, pufferfish 12.45, dragonfly 31.85.
   - Geometry-only divergences (element and domain costs zeroed): 3.74, 4.63, 3.80, 6.52, all under 8.
   - Every swarm sits at the 280 cap while the plans hold 76-192. For example, the whale census is `[23, 203, 37, 17]`
     against a wanted `[19, 126, 25, 22]`.
   - So the bar failure is a **composition and headcount** failure, not a shape failure. This holds for every
     G2-derived body.

4. **Strike healing improved slightly over baseline.**
   - Probe heal values (mass/space/charge/time): 0.587 / 2.609 / 0.609 / 0.389.
   - The baseline was 0.04 / n/a / −0.53 / 0.55.
   - Heal above 1 means it overshoots the pre-strike score. The probe is noisy, with one seed per plan.

5. **Engineering details that worked:**
   - Widening the state 32 → 40 channels: PREF 32-35, DRIVE 36, and the designed channels PROG 37, TGT 38 and CARRY 39.
   - Exact column remapping on load, so `p_meta = 0` reproduces G2 bit-for-bit over 60 steps.
   - The "credit at completion" carrier channel: a zero-valued channel parks the start decision's score-function
     gradient and moves it onto `Swarm.bw` only when the metamorph completes, so the loss credits the tadpole in its
     NEW identity.
   - This straight-through-on-completion pattern is reusable for any delayed discrete event.

#### Process family: learned per-unit identity change

- **How it produces lifeforms:** G2 BPTT, plus a learned per-unit decision to change element (drive and preference
  channels), plus a learned egg-element choice (`learned_egg` on new PREF channels, `p_cross 0.1`). Domain never
  changes.
- **Capability:** a swarm can rewrite its own composition without deaths.
- **Limits:** no learned directionality; bursty metamorphs; no improvement over G2 within 1,250 steps.
- **Orthogonal to the others:** it is the only family that LEARNS when and to-what a unit transmutes. The game molts
  by a designed rule (surplus → deficit, only during settle). Creature and evo never molt; they change the mix by
  laying.
- **Game invariant tension:** molting transforms a lifeform's element without a death, which conflicts with
  `ECOSYSTEM.md §40`. The game kept it, flagged, and windowed.

---

### 3. `cece/swarm-x-play`: strike-hardened PlayRule with predator sensing

#### Discoveries

1. **Single-trajectory numbers are chaotic.**
   - `play_compare.py` docstring: *"two runs of the same rule differ in the first decimal after a few hundred steps,
     from float threading order alone, so one rollout or one strike is not evidence."*
   - The comparison tool scores 3 rollout seeds (7, 8, 9), 4 strike seeds, a drift control, and 3 predator seeds.
   - It also defines a **drift control**: grow, then run 120 undisturbed steps and score again. *"A rule whose body
     wanders on its own 'heals' to wherever it was going anyway."*
   - **Why it matters:** every single-seed heal number in the portfolio (field 0.92-1.07, evo −1.17, meta 2.6) is
     weak evidence without a drift baseline.

2. **The heal metric is unstable. Use "excess" instead.**
   - `heal = (cut − recovered) / (cut − before)` blows up or returns null when the strike barely changes the score:
     `heal_mean` 19.9 at step 500, −217 for charge at step 875, many nulls.
   - play's replacement: mean of (recovered − before) over strikes. A value of 0 means healed.
   - Baseline G2 excess was 4.06. p1 went 2.44, 2.51, 2.08, 1.96, **−0.4 (step 625, best)**, 1.2, 0.71, 2.07, 1.85,
     0.77, and 3.08 at step 1375.
   - Strike-hardening helps recovery noisily and not monotonically. Tests stayed at 4-5 of 8 (pre-bar), against 5
     for G2.

3. **The predator-reaction objective did not produce a gap.**
   - In `predator_probe` the predator has radius 6. `gap` is the mean distance from the predator centre to the
     nearest tadpole while the predator is inside the swarm.
   - Baseline G2 gaps were 3.99 to 4.34. Under p1 they stayed between 3.85 and 5.76.
   - A gap below the radius means tadpoles are still inside the predator sphere. The body is only being shoved
     (push 0.8), never parting ahead of the predator.
   - Kills and crowd numbers moved without a trend: G2 kills 22/15/19/23; p1 at step 1375 killed 11/10/8/17.
   - p2 (`c32d1d62`) raised the predator input gain ×3 ("so the zero-init columns matter sooner") and strengthened
     the reaction objective. No p2 result is committed.
   - **Why it matters:** gradient-learned avoidance through 7 zero-initialised input columns is slow. Every
     successful "parting" behaviour in the portfolio is DESIGNED: field, creature's shell, and the game core. In the
     game core a ship pass touched 0-2% of members with reaction on, against 3-5% inert.

4. **Interaction curriculum.**
   - Per picked sample per step: strike p = 0.25, cull 0.08, scatter 0.12, predator 0.35.
   - Predator speed 1-3 voxels/step, radius 4-8, and it "appears outside its sense range".
   - The reaction loss is `w_avoid · mean relu(1 − d/(1.6 r))²` over live units, with the end-of-window shape loss
     doing the "close behind it" half.
   - Damage is never applied to a seed younger than 60 steps.

#### Process family: robustness curriculum on a learned rule

- **How it produces lifeforms:** G2 fine-tuned by BPTT with damage and predator augmentation (the regenerating-lizard
  NCA recipe). It adds 7 sensed predator inputs: relative position × falloff, falloff, and velocity × falloff, within
  3 predator radii.
- **Capability:** possibly faster strike recovery, with excess down to −0.4 at its best.
- **Limits:** no learned gap behaviour; noisy; no gain in switching.
- **Orthogonal to the others:** it is the only family that changes the TRAINING DISTRIBUTION (damage curriculum)
  rather than the architecture or actuators. It is also the only one that puts vessel or predator SENSING inside the
  learned rule. Creature and the game sense vessels in a designed shell.
- **Lasting contributions:** the drift-control probe and the multi-seed protocol are methodological, and apply to
  every family.

---

### 4. `cece/swarm-x-creature`: evo body plus a vessel-reaction shell

No NOTE.md, showcase.html or rollout.json were committed. The deliverables present are `eval16.json`,
`interaction_v1.json`, and the code in `creature_model.py`, `creature_probe.py`, `creature_heal.py`,
`creature_eval.py` and `creature_showcase.py`.

#### Discoveries

1. **A reaction shell can be layered onto a learned body without re-earning the yardstick.**
   - `pos_shown = pos_learned + off`, with `off ← off·(1 − k_ret·calm) + reaction`, `k_ret 0.10` and `max_off 30`.
   - Every layer is gated on a ship, a wound, or a majority change. With no vessel near, the model is bit-for-bit
     `EvoRule`.
   - So `eval16` = **16/16** (3/3 samples on every test, 140.5 s), all at n = 280.
   - The learned rule keeps perceiving the displaced positions, which is what lets a carved body re-knit.
   - **Under the loss-8 bar it is 0/16.** Own divergences are 19.48, 19.75, 14.90 and 24.80; switch divergences run
     17-34.

2. **The shell measurably parts the school and halves ram kills.**
   - Source: `interaction_v1.json`, one seed, shell against inert. The ram pass is 3 voxels/step with a ship radius
     of 0.6 × the body RMS.

   | plan | touched, flyby (shell / inert) | ram kills (shell / inert) | ram heal (shell / inert) |
   |---|---|---|---|
   | Mass (whale) | 0.357 / 0.436 | 133 / 143 | **0.898 / −0.10** |
   | Space (jellyfish) | 0.30 / 0.507 | 92 / 173 | −0.386 / −1.159 |
   | Charge (pufferfish) | 0.125 / 0.321 | **42 / 107** | 1.188 / 5.182 |
   | Time (dragonfly) | 0.121 / 0.296 | **36 / 98** | 0.506 / 0.425 |

   - Reaction latency is 1-13 steps. The pufferfish is fastest at 1 step, the whale slowest at 13 (ponderous by
     design).
   - **Readability is 1.0 in every encounter** (shell and inert): at every scored frame the body was still closest
     to its own plan.
   - Wound memory (eggs laid into remembered holes, `wound_reach 12`, `wound_life 160`) is the likely reason the
     whale ram heals at 0.898 against −0.10.

3. **The costs of the reactions are real and measured.**
   - Mobbing distorts the dragonfly while it lasts: worst divergence during a 120-step circle was **39.76 with the
     shell vs 25.23 inert**, with re-form taking 20 steps.
   - The puffer's inflation raises its divergence from 15.1 to 17.9-18.7 during an encounter. That is intended:
     it is a readable swell.
   - A chase leaves the whale at 19.14 against 16.42 inert. This is plausibly whale shielding or escort drift.
   - Space under a chase reaches 29.7 against 27.65 inert.
   - **Why it matters:** the player-facing behaviours trade shape-loss for liveliness. A loss-8 bar applied *during*
     an interaction would penalise exactly the behaviours the brief asked for.

4. **Element temperaments as data** (`DEFAULTS`):
   - Flee per element (C/M/S/T): 0.5 / 0.3 / 1.2 / 2.0.
   - Space jets 2 of every 6 steps (gain 2.2), the bell contracts 0.35, and the whole body jets 0.6 per step.
   - Puffer inflation is 0.55 at full threat, with danger shown above startle 0.35.
   - The whale tucks its minority inward (0.35) and pushes its Mass hull toward the ship (0.25).
   - Time darts (0.8 zig-zag), mobs a ship slower than 1.0 voxel/step, and orbits at 1.4 ship radii.
   - **Escort** (last commit): a ship cruising past at 1.0-2.4 voxels/step, within 3 body radii and not heading in
     (heading_in < 0.3), is followed. Strength by majority (C/M/S/T): 0 / 0.55 / 0 / 0.35. "The whale follows like a
     curious whale."
   - **Switch tell:** when the live majority changes, the body shivers for 36 steps (jitter 0.7, swirl 0.25) before
     the learned rule re-forms it.
   - **Crystals left by the dead drift to the nearby ship** in the showcase script.

5. **The ram heal is single-seed.** That is why the branch added `creature_heal.py`, which pools `swarm_probe` across
   seeds 11-14. No pooled numbers were committed.

#### Process family: hierarchical composition (learned body plus designed reaction shell)

- **How it produces lifeforms:** evo's learned body (G2 MLP plus a 95-float CMA-ES genome: lay homeostat and egg
  choice) owns growth, look and composition. A designed elastic offset layer owns the player-facing reactions.
  Production-side behaviours (wound memory, switch tell) act only through laying and display.
- **Capability:** lively, element-specific reactions that are readable 100% of the time and halve ram kills, while
  the 16-test switching (pre-bar) is inherited intact.
- **Limits:**
  - The body is a G2 body: 280-cap, overfull, with own divergences of 15-25, so it fails the loss-8 bar.
  - Mobbing costs shape.
  - The probe is single-seed.
- **Orthogonal to the others:** it separates the brain (composition and plan) from the reflex (a reaction offset
  that springs back). That makes it rule-agnostic: the shell could wrap the field model, a distilled rule, or a
  colony rule just as well. The game's sim core does the opposite and folds reactions into the steering forces.

---

### 5. `cece/swarm-fauna-game`: the in-game Unity implementation

#### What it is

The designed **field** model is ported to a pure-C# struct-of-arrays core, `SwarmFieldCore.cs` (618 lines,
System.Numerics, no `UnityEngine`). The surrounding pieces:

- `SwarmFauna.cs` is a heartless population anchor: the worm-colony shape, the fixed 10 Hz clock with
  interpolation, feeding, starvation, vessel sensing and extinction.
- `SwarmTadpoleFauna.cs` is a real `Fauna` with its own heart, killed through the sealed `Die`. It has no
  `Update` of its own.
- `SwarmFaunaConfigSO` holds every number. The four baked plans are JSON in research units.
- The Swarm cell, its 3 swarm species and 3 flora configs, `CellConfigs[12]` in Menu_Main, both prefabs and every
  asset are authored by `Tools/Build/author_swarm_fauna.py --check`.
- The research ↔ game interface is the baked plans plus `UnitScale`.

**Verification:** NOTHING has run in the Unity editor; that is tracked as `QA-SWARM-FAUNA`.

- The core harness is 33/33. **I re-ran it in this session** (dotnet 8 installed in the scratchpad, same file
  extracted from `ff55379d`): 33/33 OK, 5.6 s.
- The glue type-checks against a hand-written stub only.

#### Discoveries

**D1. Continuous molting defeats the morph mechanic: the single most important game-side change.**

- Mechanism: the research molts at all times, at `MoltRate 0.03`, so 3% of the body per step (the code comment says
  about 30 per second). Kill the majority one at a time, and the swarm molts minority members back into the
  majority. It shrinks in proportion and never switches.
- Fix: molting runs only inside a `SettleSteps` window (300 steps = 30 s) after the swarm COMMITS to a new plan.
- Evidence: the doc §7.3 and the `SwarmFieldParams.SettleSteps` comment. **I reproduced it independently** with a
  variant harness (`scratchpad/h/Exp.cs`): kill the majority one per step from a grown swarm whose stomach is empty.

  | mode | puffer | whale | jellyfish | dragonfly |
  |---|---|---|---|---|
  | shipped (settle window) | 101 kills → dragonfly, 1 switch at +10 steps, n = 78 | 102 → jellyfish, n = 90 | 30 → pufferfish, n = 58 | 36 → jellyfish, n = 40 |
  | **molting always on** | one sweep killed 159 → **still pufferfish, 0 switches, n = 20** | 182 → still whale, n = 10 | 65 → still jellyfish, n = 23 | 61 → still dragonfly, n = 15 |
  | no molting (`SettleSteps = 0`) | still 1 switch at +10, but mix `[30,1,16,31]` | mix `[19,24,25,22]` | mix `[25,2,24,7]` | mix `[2,3,18,17]` |

- With no molting, the switch happens, but the new body keeps the old mix: Space leads by 1 in the dragonfly case.
  The settle-window molts (15-35 per morph) are what convert surplus into deficit. For example, the dragonfly's mix
  goes `[2,3,18,17]` → `[11,1,24,4]`.
- **Why it matters for the research:**
  - The research yardstick culls INSTANTLY (`cull_to`), so the molting-feedback failure never appears there.
  - Field passes the yardstick with continuous molting that would make it unplayable.
  - The yardstick should cull gradually (one or a few per step) to be faithful to the game.

**D2. Homeostasis on food: a morph is a burst-damage objective.**

- Harness test 3b: 100 Time kills spread over 200 steps (1 per 2 steps = 5 per second at 10 Hz) against a fed
  dragonfly produced **0 switches** (n stayed 76). The same 36 kills delivered in a burst against an emptied
  stomach switch in 1 second.
- The doc calls it "an emergent difficulty dial set by the feeding ground".
- **Caveat I found:** test 3b uses a bottomless stomach (1e6 per element). In the game the stomach is capped at
  `StomachEggs` = 24 eggs of food. A real fed dragonfly can re-lay at most 24 members before it must graze again.
  It grazes 12 biters per step, so refill speed depends on flora density.
- So the harness overstates in-game homeostasis. The true kill-rate threshold is unmeasured, and is a playtest item.

**D3. Funded laying makes the feeding ground the strategy layer, but the element of the food does NOT choose the
egg.**

- An egg of element e costs `EggVolume[e]` of element-e food. That cost is the volume of that element's body prism:
  Charge 20.45, Mass 40.31, Space 22.18, Time 12.8. Eaten mass converts to swarm mass 1:1.
- Foreign food pays at `CrossElementCost` = 2×.
- Harness: 10 eggs' worth of own-element food laid exactly 10. 10 eggs' worth of SPACE food in a whale laid **5
  MASS-majority eggs** (mix `[2,21,3,3]`).
- Which element is laid is chosen by the plan's deficit. Food decides only affordability.
- The doc's claim that "food of a rival element is effectively a slow push toward that element" is **not evidenced
  by the harness**. The egg element stays plan-driven. The brief's "eat element X → lay X" idea was NOT implemented.

**D4. Charge flora are inedible, so the pufferfish always pays double.**

- Charge plant leaves are shielded (`ECOSYSTEM.md §35`), and shielded mass is never food (`Fauna.IsShieldedMass`).
- So the cell plants no Charge flora, and every Charge egg costs 2× food from another element.
- The puffer (131 of 179 members Charge) grows at roughly half rate by construction.
- **Why it matters:** an invariant conflict that falls straight out of the world and shapes growth rates per element.
  The research models free, element-blind food.

**D5. Post-morph bodies are sparse ghosts of the new plan.**
After the selective-kill morph the swarm holds only the survivors:

| morph | n | mix | the new plan |
|---|---|---|---|
| jellyfish → pufferfish | 58 | `[42,1,6,9]` | 179, `[131,1,16,31]` |
| dragonfly → jellyfish | 40 | | 88 |
| whale → jellyfish | 90 | | 88 (close) |
| pufferfish → dragonfly | 78 | | 76 (close) |

- Morphs into a smaller plan land at full size. Morphs into a larger plan need food to fill in.
- Median shape error is still small (0.20-1.37 voxels). The members find homes, but the silhouette is thin.
- The research grows the new body for free in 240 steps.

**D6. Kills needed to morph (harness; small creatures are the accessible morphs, big ones are bosses):**

| creature | kills | becomes |
|---|---|---|
| dragonfly | 36 Time | jellyfish |
| jellyfish | 30 Space | pufferfish |
| whale | 102 Mass | jellyfish |
| pufferfish | 101 Charge | dragonfly |

- The switch commits 10 steps (1.0 s) after the last kill, because `Dwell` is 12.
- While contested, the swarm neither lays nor molts.
- The morph vortex runs 60 steps (6 s). The settle window lasts 300 steps (30 s).

**D7. Tick rate and smoothing.**

- 10 Hz with interpolation is enough. All research constants (boids, arrive, startle decay) are per-step and carry
  over unchanged; only `TickHz` maps them to seconds.
- **Facing must be interpolated too**, or members visibly tick-rotate.
- Render is `lerp(prev, current, alpha)`, which costs one tick (100 ms) of latency.
- `MaxStepsPerFrame` = 3 drops time rather than spiralling.
- Running the sim at frame rate would make every constant frame-rate dependent.

**D8. Units and scale.**

- `UnitScale` = 2 world units per voxel. The whale is about 130 world units long, with about 5-unit slot spacing:
  "shootable pieces, still one silhouette at arena range". Smaller scales make tadpoles overlap their own hearts.
- Cruise is 0.35 voxels/step ≈ **7 world u/s**, against vessels at 60+ u/s. Whether that reads alive or sluggish is
  open.
- **Speed cross-walk** (my arithmetic): 60 u/s ÷ (2 × 10) = **3 voxels/step**. That is exactly the speed creature's
  flyby probe used, so the research's vessel speeds are game-faithful at this scale.
- The harness reaction test used 6 voxels/step (120 u/s), with a vessel radius of 9 world units (4.5 voxels).

**D9. Vessel reaction and mobbing were reproduced in the port.**

- A straight ship pass at 6 voxels/step touched:

  | creature | reacting | inert | peak threat (reacting) |
  |---|---|---|---|
  | whale | 2% | 5% | 0.72 |
  | pufferfish | 1% | 3% | 0.44 (it inflates) |
  | dragonfly | 0% | 3% | 0.42 |

- Mobbing: a loitering ship had 25.7 tadpoles within 2 ship radii with mobbing on, against 2.3 off. The research
  measured 24.3 against 2.4.

**D10. Dragonfly shape error.**

- The median home error is **4.04 voxels**, against 0.21-0.83 for the other plans.
- Cause: the wing "runners" lap at about 3 voxels/step against `VMax` 2.0, so they always trail their slot.
- With runners excluded the body holds. The harness gate uses runners-excluded median error under 3.
- Fix options for the research: a higher `VMax` for runners, or slower wing frames.
- This also explains why the dragonfly's own-plan divergence is the worst in every G2-derived run (24-32).

**D11. Performance.**

- Measured on CoreCLR, ms per step:

  | plan | doc | my re-run |
  |---|---|---|
  | pufferfish (179) | 0.95 | 0.847 |
  | whale (192) | 0.85 | 0.680 |
  | jellyfish (88) | 0.14 | 0.117 |
  | dragonfly (76) | 0.11 | 0.091 |

- Greedy slot assignment (a sort) and the boids neighbour loops dominate. Burst was not justified.
- At 10 Hz this is ≤10 ms per second of game time per swarm. Three grown swarms come to about 25 ms/s, or 0.4 ms per
  60 fps frame (amortised).
- **Mono in a player will be slower**, which is QA step 9.
- Unmeasured per-frame costs:
  - a transform write plus `NotifyBodyPrismsMoved` for every live member, every frame (up to 576);
  - one `Physics.OverlapSphereNonAlloc` per tick per swarm for vessel sensing, with radius = plan RMS × 2 × 1.6 + 220
    world units.

**D12. Collider budget.**

- **616 always-on heart colliders at cap**, against the Lattice cell's 1,080 (asserted by the author script):
  - 576 tadpole hearts (3 swarms × 192, since each swarm's cap is the largest plan);
  - 40 flora hearts (10 + 14 + 16).
- Plus up to 576 body-prism colliders.
- Realistic load is lower: dragonfly 76, jellyfish 88.
- The volume ladder is MODELLED, not measured:
  - Restless enter/exit: 447k / 332k.
  - Frenzy enter/exit: 3.19M / 2.81M.
  - It is derived from a modelled mature cell of about 7,762 prisms and about 1.28M volume. Re-measure in the editor.

**D13. One-colour law: domain slots dropped.**

- The research gives each slot a domain (A/B/C regions). The game collapses that: every member wears the cell's
  controlling colour. "A creature made of four elements" is legal; "a creature made of three teams" is
  cross-domain spawning.
- The three populations differ by starting ELEMENT MIX, not colour.
- **Why it matters:** the research's slot-to-domain minimisation (multi-domain region shape) has no in-game
  consumer. If the research wants multi-domain bodies, that is a fundamentals conversation.

**D14. Molting and death invariants.**

- A heart caught mid-molt is restored to full size before any death path releases it, so a molt can never make a
  kill pay less. The crystal's world scale is the reward.
- Molting is visibly animated:
  - the heart shrinks away over 0.5 s;
  - it re-forms as the new element;
  - the body prism re-grows through a clock-stamped `ChangeSize()`.
- `SettleSteps = 0` is the escape hatch if molting is judged to violate `§40`.

**D15. Starvation is plan-preserving and the only self-death.**

- After 90 s without a meal, the swarm sheds one member every 4 s.
- It sheds from the element in greatest surplus relative to the plan mix, so starvation tightens the body toward its
  plan rather than eroding the majority.
- A shed member withers to its crystal and leaves its prism as a skeleton.
- An extinct anchor lingers 10 s, then the seeder hatches a fresh 24-member swarm.

**D16. Danger and shields in play.**

- Charge members startled above 0.45 (exit 0.15) turn their plate DANGER: the pufferfish's spines.
- A danger prism hurts every vessel, the swarm's own domain included. Under the elemental economy an
  opposing-domain danger prism BURNS petals permanently.
- Charge members on shield slots wear octahedral shields: two hits to kill. That is the jellyfish's 25 bell
  members. Danger wins over shield.

**D17. Cell design.**

- Disjoint bands keep the three populations apart:

  | band (world radius) | starts as | grazes |
  |---|---|---|
  | inner 430-600 | whale | Mass flora (Arbor) |
  | middle 660-840 | dragonfly | Space flora (Spire) |
  | outer 900-1120 | pufferfish | Time flora (Frond) |

- The jellyfish is never seeded; it appears only by morph.
- Grounds were chosen so most residents graze the element they become when their majority is killed (dragonfly →
  Space, puffer → Time). The whale grazes its own element: fastest growth, hardest to convert.
- Each swarm config has `InitialSpawnCount / PopulationSize / MaxLivePopulation` = 1. A new swarm hatches with 24
  members. Grazing goes to the nearest flora heart in the band. A plant without a bite for 10 s is marked barren for
  45 s.

#### Process family: designed field model with an economy, embedded in a world

- **How it produces lifeforms:** no learning. Designed attractor slots per plan (8 animated frames), greedy
  assignment with stickiness 4 and a mismatch penalty of 60, boids, a morph vortex, designed vessel reactions, and
  plan choice by majority with hysteresis. The game adds funded laying, starvation, swimming toward food, a band
  clamp, and molting confined to the settle window.
- **Capability:**
  - exact plan headcount and mix when fed;
  - deterministic one-commit morphs with no flip-back;
  - real kills, crystals and food-web coupling;
  - CoreCLR cost under 1 ms per step.
- **Limits:**
  - Designed ("cheating", as the lead accepts).
  - Post-morph bodies are thin until fed.
  - Homeostasis is only modelled with a bottomless stomach.
  - Untested on screen.
- **Orthogonal to the others:** the only family subject to conservation (an egg costs food) and to world
  invariants: shield-is-not-food, one colour, the sealed death path, continuity. The only one with real units, real
  time and real cost. Its parameters (`Dwell`, `SettleSteps`, `CrossCost`, `StomachEggs`) are the levers the research
  families lack.

#### In-game findings that should change the research

1. **Tick rate.** Score and design per 10 Hz step. The research's step constants already map 1:1. Render
   interpolation of position AND facing is mandatory. Do not train rules whose behaviour depends on frame-rate-sized
   steps.
2. **Smoothing and latency.** Interpolation adds about 100 ms. Reactions with latency of 1-13 steps cost 0.1-1.3 s
   in game. The whale's 13-step latency is 1.3 s, which may read as unresponsive.
3. **Units and scale.**
   - 2 world units per voxel. Vessels at 60 u/s are 3 voxels/step, and at 120 u/s they are 6 voxels/step.
   - Vessel radius is 4.5 voxels, about 0.25-0.3 of a body RMS (body RMS 14.8-20.5 voxels).
   - Predator and strike probes should use these values.
   - Swarm cruise is 0.35 voxels/step.
4. **Funded laying.** Add an egg cost and a stomach to the research world:
   - egg cost = that element's body-prism volume (Charge 20.45, Mass 40.31, Space 22.18, Time 12.8);
   - 2× cross-element cost;
   - a 24-egg stomach cap;
   - no Charge food.

   Free laying (0.04 / 4) hides growth-rate asymmetry, the puffer's double cost, and post-morph sparseness. The game
   uses LayRate 0.02 and LayMax 2, gated on the stomach.
5. **Starvation.** Shed the surplus element every 4 s after 90 s unfed. A rule should survive famine without
   collapsing its plan.
6. **Molting window.**
   - Gate molting on "after commit, for N steps". Continuous molting yields 0 switches under per-step kills (my
     table above).
   - The yardstick's instantaneous `cull_to` should become a gradual cull: kills per step at a player-plausible rate
     (about 1 per step, i.e. 10 per second, as a burst) and a fed-swarm variant (see D2).
   - This bears on meta's learned metamorphosis too: direction AND timing must be gated.
7. **Body size.**
   - The 280-slot cap is a research artifact. The game caps at the largest plan (192) and reaches exact plan
     headcounts.
   - G2-derived bodies fail the loss-8 bar mainly on composition and headcount: meta's geometry-only divergences are
     3.7-6.5.
   - The research should cap at the plan size, or include headcount in the target.
8. **Post-morph realism.** Score switches at a FINITE food budget. In the game the new body is 40-90 members until
   fed, and may be thinner than the plan.
9. **Domain slots.** Drop the A/B/C domain-region term for game-bound rules (one-colour law), or treat it as
   cosmetic only.
10. **Collider counts.** One heart per tadpole is always on. Fewer, larger tadpoles per creature directly lower the
    budget: a dragonfly costs 76 hearts, a whale 192. A research objective that prefers smaller headcounts for the
    same silhouette helps the game.
11. **Performance.**
    - Greedy assignment plus boids at ≤192 units is under 1 ms per step on CoreCLR. A learned MLP per unit must fit
      a similar budget at 10 Hz across 3 swarms (about 1-3 ms per second per swarm on Mono, to be measured).
    - Per-frame transform writes for up to 576 members are the likely real cost.
12. **What matters to players** (from the harness and creature, still unplaytested):
    - burst-kill morphs;
    - visible parting (touched 0-2% vs 3-5%);
    - mobbing (25.7 vs 2.3);
    - puffer danger spikes (a real hazard);
    - the switch tell.

    Unmeasured: whether the morph reads at arena range, whether 7 u/s reads alive, and whether a 10 Hz pose under
    fast startle looks smooth.

---

### Cross-family synthesis (what is orthogonal and could be combined)

| family | lever | it is the only one that... |
|---|---|---|
| colony | a learned population latent plus a per-element breeding bias | ...has a learned recurrent plan memory |
| meta | learned per-unit element change | ...learns transmutation (failed: undirected) |
| play | a damage curriculum plus predator sensing in the learned rule | ...changes the training distribution (noisy, no learned gap) |
| creature | a designed elastic reaction shell over a frozen learned body | ...separates reflex from brain, rule-agnostically |
| game | the designed field model plus an economy and world invariants | ...conserves mass, runs at real units and time, and is engine-ready |

Recommended combinations (my inference):

- Creature's shell (startle, jets, inflate, mob, escort, wound memory, tell) maps cleanly onto the game core. The game
  already has startle, flee, mob and inflate; it lacks wound memory, the tell, escort and the elastic return.
- Play's multi-seed and drift-control methodology should be applied to all heal numbers.
- Any learned rule meant to replace field must be scored with:
  - gradual culls and the settle-window molting rule;
  - funded laying;
  - plan-size caps;
  - the loss-8 bar.

Scratch artifacts: `scratchpad/h/` contains the extracted core, `Program.cs`, the plans, `run.out` (my 33/33 re-run)
and `Exp.cs` (the molting-mode experiment).
