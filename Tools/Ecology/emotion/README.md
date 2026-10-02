# Direction C — the emotion probe

Can "cute / playful / eerie / majestic / menacing / terrifying" be measured from motion and size, and then
searched toward? Results and negatives: `../DISCOVERIES.md` § Emotion probe. Literature: `LITERATURE.md`.

| file | what |
|---|---|
| `../common/affect.py` | the affect feature extractor (~32 features, each tied to a paper). Shared: `common/scorecard.py`'s `Probe.feel()` now emits them as `a_*` plus `emo_*` probabilities |
| `archetypes.py` | the reference set: 4 generator FAMILIES per emotion (24 mechanisms), parameters drawn per variant, all under the same physical laws |
| `sealed.py` | a 5th family per emotion, written before the last feature round and scored ONCE (`evaluate.py --sealed`) |
| `dataset.py` | archetypes x variants x viewers -> `results/reference_set.json` (+ `reference_evade.json`, `sealed_set.json`) |
| `model.py` | logistic (L2) / prototype / sub-prototype models; LOFO, within, transfer, permutation importance |
| `iterate.py` | grows the feature set one literature group at a time -> `results/iterations.json` |
| `evaluate.py` | round 1: fits the 6-class ensemble probe (now kept as `results/probe_v1.json`) |
| `v2.py`, `v3.py` | round 2 (5 families, sealed-2) and round 3 (+ NEUTRAL class, `neutral.py`) |
| `final_probe.py` | promotes v3 to **`results/probe.json`** (7 classes, model + held-out permutation importance per emotion + metrics). Re-run it after `evaluate.py`, which would overwrite probe.json with v1 |
| `probe.py` | `EmotionProbe.load().score(features)` / `.explain(f, emotion)` / `.advise(f, target)`; affect coordinates |
| `critter.py` | ONE parametric creature family (21 parameters) under game laws |
| `search.py` | CMA-ES (and a same-budget random baseline) on the critter for each target emotion -> `results/search.json` |
| `species.py` | today's game species modelled from shipped asset numbers (worm, shark, tadpoles, swarm bodies) |
| `score_species.py` | probe readings of the game species -> `results/species_scores.json` |
| `siblings.py` | probe readings of sibling branches' species on their own `common/` -> `results/sib_*.json` |
| `viewer.py` | `results/emotion_viewer.html`: every archetype, sealed family, searched creature and game species, with the probe's bars and a HUMAN RATING panel (export JSON) |
| `ratings.py` | compare exported human ratings with the authored labels and the probe |

Reproduce: `python dataset.py && python dataset.py --variants 6 --viewers evade --seed 5000 --out results/reference_evade.json && python iterate.py && python evaluate.py && python dataset.py --sealed --seed 9000 --out results/sealed_set.json && python dataset.py --sealed2 --seed 9500 --out results/sealed2_set.json && python v2.py && python v3.py && python final_probe.py && python search.py && python score_species.py && python viewer.py` (about an hour on 4 cores; needs numpy + scipy).

Use it on a new species: put `PROBE_SPECIES = {"name": factory(rng, pilot)}` in a module under
`Tools/Ecology/<dir>/` and run `score_species.py`, or just read `feel["emo_*"]` from the shared scorecard.
