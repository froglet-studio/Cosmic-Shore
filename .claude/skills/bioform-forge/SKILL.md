---
name: bioform-forge
description: Grow the game's bestiary with compute - new animated bodies a swarm can dynamically switch into (swarm body plans in the exact SwarmPlanJson schema, found by MAP-Elites quality-diversity search over a body genome), and NCA creatures trained from them (3D neural cellular automata that grow from one cell, swim with no animation and heal). Covers the forge job, choosing elites, baking plans and NCA targets, the torch training job, and how a body reaches a swarm config with its owner's yes. Use on "/bioform-forge", "more swarm forms / bodies / bioforms", "diversity of creatures", "new forms the swarm can switch to", "forge a creature", "train an NCA creature", "grow a new animal", "a swarm with a Time / Charge / Space majority has nothing to become", or any work on Tools/Compute/evaluators/bioform_forms.py or Tools/Compute/bioforms/.
---

# /bioform-forge: bodies for the swarms, grown by search

A swarm (`Docs/SWARM_FAUNA.md`) is tadpoles swimming as one animal, and it can morph between BODY PLANS: the four
elemental ones (`Tools/Build/swarm_plans.py`, from the research designers) and Tandava's scripted forms
(`Tools/Build/tandava_plans.py`; `SwarmFaunaConfigSO.ScriptedPlans`, loaded by `SwarmPlanLibrary.LoadScripted`).
Every one was designed by hand, and 13 of the 16 travel bodies are Mass-majority (`compute.py scout`, bioforms lens).
The forge searches for many DIFFERENT good bodies at once. Read `/fauna` and `/ecology` before wiring anything into a
cell; the swarm cores are Garrett's area (swarm fauna, NCA research), Tandava's forms are the Tandava authors'.

## 1. The forge job (`bioform-forge`, MAP-Elites, python only)

- **Genome** (22 numbers, `bioform_forms.py` docstring): spine length, radius, taper, arch; a swim (lateral,
  vertical, helical wave or a pulse) with amplitude and cycles; up to six limb pairs (length, reach, angle, paddle
  sweep and phase); up to ten radial arms off the head ring; a dorsal crest; which element the head, body and limbs
  are made of.
- **Grid** (120 cells): elongation x share of the body in limbs and arms x majority element. Each cell keeps its best.
- **Quality** (lower is better): a member count a cell can afford, the share of the drawn body that survives the
  spacing thinning (MIN_GAP in every frame - the sort core's collision radius), one animal (no stray member), keepable
  motion (no member moves more than the swimmers can follow between frames), lively (neither statue nor blur), loops.
- **Valid by construction** against `tandava_plans.validate`'s single-plan rules (the identity clamp via
  `swarm_plans.identity`, MIN_GAP, the census majority, both regions).

## 2. Choosing and baking elites

```
python3 Tools/Compute/compute.py promote bioform-forge --out /tmp/forge --top 12
```

bakes the 12 best cells as `SwarmPlan_forge_<cell>.json` (the schema the game loads) and `<kind>.prisms.json` (an NCA
training target). Look before choosing: the research viewers (`Tools/NCA/build_swarm_designer.py`, the play pages)
render a plan; a short `/labmaker` page that loops a plan's eight frames is a fine way to show Garrett a shortlist.
Pick for the game, not the loss: legible silhouettes, an animal a player would name, and the thin elements first.

## 3. Wiring a body into a swarm (the owner's yes first)

A forged plan is data. To let a swarm become it, a session with the swarm owner's go-ahead:
commits the chosen `SwarmPlan_forge_*.json` next to the plans it joins (`Assets/_SO_Assets/Swarm Fauna/...`) through the
generator that owns that folder (`author_swarm_fauna.py` or `author_tandava_assets.py`: add the source there, never
drop a file in by hand), adds it to the config's `ScriptedPlans`, and runs the swarm gates
(`bash Tools/Build/swarm_core_harness/run.sh`, the collider budget in `/ecology`). Morph cost matters: a form a swarm
switches INTO should not need more members than the swarm has (Tandava's "forms only ever grow" rule).

## 4. Growing an NCA creature from a forged body (`nca-train-forged`, torch, hours)

1. Commit the chosen `<kind>.prisms.json` to `Tools/Compute/bioforms/` (a branch that merges to `bleeding-edge`).
2. Add a unit per body to the job: `{"id": "<kind>", "args": {"name": "<kind>"}}`; set `status: ready`.
3. Machines with torch train it with `Tools/NCA/nca3d.py train --prisms ...` on the research branch, warm-started from
   the lizard, 6000 steps, checkpointing every 50 - any machine resumes a unit another machine started (relay).
4. A finished unit's `model.pt` goes through `export_nca3d_creature.py` (research branch) and a SPECIES row in
   `Tools/Build/author_nca_creatures.py` (`Docs/NCA_CREATURES.md`) - Garrett's area: ask first.

nca3d has no Time colour: a Time prism trains in Jade (`bioform_forms.DOM_OF`); say so when showing one.

## 5. Making the forge better

- New descriptors (symmetry, segment count, how far the body travels in a loop) widen the bestiary: add them to
  `measure()`, the job's `algorithm.descriptors`, and bump `VERSION`.
- A measure from the real swarm core (does the sort core actually HOLD this body? `swarm_core_harness`) is the
  strongest quality term the forge lacks; it needs `dotnet8` - a `/compute-job` build.
- Feed and pose twins (Tandava's `_feed`, coils) are a second genome on the same members: forge them as a pair so the
  commit between them stays lossless (`tandava_plans._keep`).
