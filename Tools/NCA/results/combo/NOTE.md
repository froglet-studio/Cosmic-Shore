# combo — hgrid2 made LOSSLESS (starvation → molting) and cheaper

**Headline (scorecard, `scorecard.json`, published model = G8):**
`accurate 0.94 worst seed | lossless True (0.0/1k) | 12.2 ms/step @134 | organic True | mixed`

| seed | passed / feasible (loss-8 bar, 3 samples) | own whale / jelly / puffer / dragon |
|---|---|---|
| 7 | **16 / 16** | 2.26 / 2.33 / 3.23 / 3.46 |
| 23 | 15 / 16 | 1.44 / 1.82 / 2.41 / 4.24 |
| 41 | 15 / 16 | 1.33 / 2.37 / 2.10 / 2.90 |
| 1000 (held out, not used for any choice) | 13 / 15 | 1.52 / 1.40 / 2.21 / 3.47 |

The G16 twin (`g16/`, same rules, full-size grid) scores 16/16, 16/16, 14/16 at seeds 7 / 23 / 41 — the
same 46/48 in total. **Every remaining miss sits at the bar's edge** (7.6–10.5, passing 1 of 3 samples),
and they are always one of two switches: pufferfish → dragonfly and dragonfly → jellyfish.

So against the brief's target ("16/16 at 7, 23 and 41, lossless, organic, local-or-mixed, no dearer than
hgrid2"): LOSSLESS, ORGANIC, MIXED and CHEAPER are met outright; ACCURATE is 16/16 at seed 7 and 15/16 at
23 and 41 (G16: 16, 16, 14). Not a clean sweep, and I would not claim one.

Code: `combo_model.py` (the model; subclasses `hgrid2_model.Boid2`, nothing shared edited except one
appended `combo:` branch in `swarm_eval.load_model`), `combo_eval.py` (candidate runner), `combo_publish.py`
(this folder), `combo_terms.py` (per-term breakdown), `combo_cost.py` (cost curve). No learning anywhere:
`params.json` is the whole model.

## The question the brief asked first: does hgrid2 stay 16/16 when surplus must be re-purposed instead of withered?

**No — not by itself.** Replacing the starvation with a straight molt (same selection, same staggered
per-tadpole clock, same quota; the tadpole re-forms its crystal into the element its OWN domain is most short
of, domain never changes) gives **12 / 12 / 11 of 16** at seeds 7 / 23 / 41, own plans all fine
(1.25–3.9). Zero deaths (hgrid2: 120 self-inflicted deaths over the scorecard's grow+switch run, 0.47/1k).
Every failure is a BIG→SMALL switch (whale → jellyfish/dragonfly, pufferfish → dragonfly, dragonfly →
jellyfish). Diagnosed (`runs/combo/diag_sw.py`, per 60 steps after the cull), three separate reasons, each
fixed by one lawful mechanism:

1. **Laying overshoots, and nothing can shed it.** hgrid2 relies on starvation to clean up after its laying
   homeostat; without it a whale → jellyfish ends at n = 127 against a plan of 88 (loss 25). Fix
   **`lay_cap = 1.0`**: no egg while the body holds the plan's headcount (eggs included). Not creating mass
   is allowed; aging it out is not. → 13/16.
2. **An overfull DOMAIN has nowhere to molt to.** The cull leaves the right element count but, e.g., 34 + 51
   tadpoles in two domains whose dragonfly slots want 26 + 44 (the third slot, 6 units, belongs to a domain
   the swarm does not have). Every class of an overfull domain is "full", so its surplus elements never
   molt and composition freezes at [6, 12, 21, 49] against a wanted [2, 3, 18, 53]. Fix **`ratio = 2`**
   (`ratio_target2`): headcount is not a goal, element RATIOS are — a domain's spare capacity, and a whole
   domain the plan has no slot for, takes the element mix the BODY AS A WHOLE is still missing against the
   plan's ratios. (`ratio = 1`, scaling each slot's own mix up, got the elements half right — it copies the
   wrong slot's mix into the spare room: 14/16.) Composition then converges in ~60 steps.
3. **An orphan domain has no fine field.** After dragonfly (3 domains) → jellyfish (2 slots) the third
   domain's 15 tadpoles want nothing, so they drifted with the outline only (loss 9.5). Fix
   **`orphan_proxy`**: an orphan steers by the fine field of its ELEMENT's best slot (it keeps its domain and
   pays the colour; its shape and element are right) → 6.4. **`transfer2`** extends that to sort's REGION
   TRANSFER: extras of an overfull class steer to their element's unfilled sites in another region (whale →
   dragonfly 10.5 → 9.7).

With all four: **candidate C (G16) 16/16, 16/16, 14/16; C8 (G8) 16/15/15, held-out 13/15.**

## Ablations (`ablations.json`; seed 7 unless stated; all lossless except the baseline)

| variant | passed / 16 (7 / 23 / 41) | note |
|---|---|---|
| hgrid2 (starvation) | 16 (published) | 120 deaths, 0.47 / 1k tadpole-steps |
| molt only | 12 / 12 / 11 | big→small switches stay overfull |
| + lay_cap | 13 | headcount right, composition stuck |
| + lay_cap + filler transfer (outline only) | 11 | worse: fillers abandon their element |
| + lay_cap, molt 2x faster | 13 | molt speed is not the limit |
| A = + ratio 1 + orphan_proxy | 14 / 14 / 14 | |
| B = A + transfer2 | 14 | |
| **C = + ratio 2 + orphan_proxy + transfer2** | **16 / 16 / 14** | G16 |
| D = C, molt 2x faster | 13/15 @41 | no |
| E = C + hgrid2's neighbour swaps | 14 @41 | inert, as sort found |
| C12 (G = 12, cell 8) | 15 @7 | |
| **C8 (G = 8, cell 12) — published** | **16 / 15 / 15**, 13/15 @1000 | cheapest, same total |

Also tried and dropped: scaling the fine wanted density in an overfull domain (`wscale_max`, inert), and
scaling the fine target body up by (n/plan)^(1/3) when overfull (`grow_scale`, inert). Both are left in the
config, off.

## Per-term breakdown of the own plans (`terms.json`, G8; `g16/terms.json`; hgrid2 `terms_hgrid2_baseline.json`)

| plan | pos only | + element | + domain | full | n |
|---|---|---|---|---|---|
| whale | 0.71 | 2.24 | 2.86 | 3.34 | 193 |
| jellyfish | 0.65 | 0.65 | 0.85 | 1.22 | 88 |
| pufferfish | 0.59 | 1.81 | 2.18 | 3.05 | 180 |
| dragonfly | 1.44 | 2.03 | 2.41 | 3.27 | 77 |

G16: 2.15 / 1.03 / 3.27 / 2.56; hgrid2 (starving): 1.64 / 0.86 / 3.17 / 3.18. The own plans are where
starvation never fired much anyway, so molting costs them ~nothing; the coarser grid costs the whale ~1.

## Cost (`cost_curve.json`, one quiet process, 1 thread, 64 steps of a grown swarm, ms/step)

| model | whale | jelly | puffer | dragon | mean |
|---|---|---|---|---|---|
| hgrid2 (starve, no cache) | 22.9 | 21.0 | 26.8 | 20.5 | **22.8** |
| combo G16, no cache | 25.5 | 24.8 | 27.7 | 19.7 | 24.4 |
| combo G16 | 13.6 | 16.1 | 13.4 | 11.5 | **13.6** |
| combo G12 | 17.7 | 12.2 | 14.2 | 11.8 | 14.0 |
| combo G8 | 14.0 | 12.8 | 11.8 | 9.1 | **11.9** |

The big lever is an **exact cache** of the coarse plan field: with `quant = 1` the grid centre snaps to whole
cells, so the field is a pure function of (plan, frame, centre) and a swarm that holds still between frames
re-uses it (~97% hit rate) instead of re-splatting ~80 channels every step. Proven bit-identical (positions
after 272 steps equal to the last bit, with and without the cache). The corrector itself costs ~1.6 ms over
hgrid2 in Python (its per-tadpole loops). Grid size buys another ~13% at G = 8; accuracy over three seeds is
the same total. The C# port runs these 5–15x faster.

## Probe (vessel strike, `probe.json`, G8)

| plan | before | killed | right after | recovered | n after | heal |
|---|---|---|---|---|---|---|
| whale | 1.23 | 34 of 192 | 5.01 | 1.06 | 191 | 1.05 |
| jellyfish | 1.82 | 20 of 88 | 8.04 | 1.45 | 88 | 1.06 |
| pufferfish | 3.10 | 45 of 181 | 9.66 | 3.90 | 180 | 0.88 |
| dragonfly | 4.25 | 27 of 76 | 19.81 | 4.47 | 77 | 0.99 |

Every body refills to its headcount and heals to 0.88–1.06 of its own pre-strike quality — far stronger
than evo's probe heal. (G16: 0.91 / 0.96 / 1.11 / 0.95.)

## Emergent / organic

In the organic band at both grid sizes, and **hunger-free hgrid2 keeps the lead's favourite property**: the
feel metrics are indistinguishable from hgrid2's (jerk_rel 0.578 vs 0.580, coherence 0.631 vs 0.628, jitter
0.91 vs 0.93, phase 0.84 vs 0.86, planar_excess 0.088). The molt-only model is in band too.

Locality, declared **mixed**: a tadpole reads fields at its own position (hgrid2's coarse grid + fine
morphogen) plus a CENSUS (class counts, used by the molt quota, the ratio target and the laying cap) and the
body's centre (the grid's frame). No tadpole is ever assigned a slot; orphan/transfer steering picks a
DOMAIN's field to follow, never a site.

## What a player would see

Exactly hgrid2's swarm while it grows and holds a body — the loose school that crystallises. The difference
is after a switch (members eaten): instead of the old majority's surplus withering away one by one on a
hunger clock, those tadpoles **visibly change colour/crystal** (a molt is a 17–50-step clock; the game should
animate the crystal shrinking out of the old shape and blooming into the new one) and swim into the new
body's empty places. A domain the new body has no room for does not die either: it joins the body, takes the
right element and fills the right shape, wearing its own colour — a stripe of foreign team colour inside a
correct animal. After a vessel strike the wound refills with eggs exactly where the holes are.

## Recommendation for the next round / the port

1. **Port C8 (or C at G16) as the next species, on the existing hgrid2 C# port**, replacing its disabled
   hunger with the molt. What the port MUST preserve, in order of how much each bought:
   - `ratio_target2` (spare capacity takes the body's global residual element mix) — without it the
     game's finding 17 (surplus clings as debris) is exactly what you get;
   - the **laying cap** at the plan's headcount (eggs counted);
   - the orphan / region-transfer steering (follow the element's best slot's fine field; keep the domain);
   - the molt quota rules (a class molts at most its excess, a receiving class takes at most its deficit,
     never let a non-major element tie the major through a molt);
   - the plan-field cache (pure function of plan, frame, snapped centre). C# should cache it the same way.
2. **The remaining edge (puffer→dragonfly, dragonfly→jelly at 8–10) is the dragonfly's third domain**:
   when the new body has a slot for a domain the swarm lacks (or lacks a slot for a domain it has), the
   loss pays the domain term no lawful corrector can remove (molting never changes a domain). Combine with
   sort's **fate commitment** for the dragonfly only, or let a domain-orphan BUD OFF as a new colony (the
   worm-colony split precedent) — a scorecard change, because today any departure counts as a death.
3. Measure the C# cost of the molt/ratio bookkeeping; in Python it is per-tadpole loops (~1.6 ms), in C#
   it should be negligible next to the grid.
