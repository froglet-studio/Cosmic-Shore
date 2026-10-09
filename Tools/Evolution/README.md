# Darwin Lab — rounds

The browser studio for the heritable genome (`Docs/EVOLUTION.md` §7). Newest round first. Live page:
https://claude.ai/artifact/5axngABeaGGT2ev4ZR2ZaL (the repo copy is the source; republish to the same URL).

```
Tools/Evolution/
  sim.js               the EXACT JavaScript port of the C# core + arena (the page flies this)
  gate_parity.cjs      holds sim.js to results/golden.json bit for bit, with three planted defects that must bite
  lab_template.html    the page; DarwinLab.html is baked from it - never hand-edit the bake
  build_lab.py         bakes the page (+ --check: drift against the real assets, staleness of the bake)
  DarwinLab.html       standalone (file://; what verify_lab.cjs checks)
  DarwinLab.artifact.html  the body the Artifact tool publishes (same content, web fonts added)
  results/             shipped.json (every number's provenance), results.json (the evidence), golden.json (parity)
```

The round loop: `bash Tools/Build/evolution_harness/run.sh` → `node Tools/Evolution/gate_parity.cjs` →
`python3 Tools/Evolution/build_lab.py` → `node .claude/skills/labmaker/verify_lab.cjs Tools/Evolution/DarwinLab.html`
→ publish → write the round here.

## Round 1 — 2026-10-09 (`cece/friendly-goodall-woddjw`)

**Built.** The lab from nothing, on the `/labmaker` contract: a live arena at 10–1000× with four trait traces, the
genome scatter and the population strip; 33 sliders in five groups, each naming the asset or class field it was copied
from (`shipped.json`), with shipped-vs-yours underlining and a header count; eight regime presets (the evidence rows
and the two controls); a headless Scorecard (your settings + mutation off + selection off, 3 seeds × 1 h, ~1 s); the
baked Evidence with a sparkline per regime; a Parity tab that re-runs the golden cases in the browser; the decision log
on the artifact's `db` (browser fallback outside the viewer); an About with the provenance table and the not-modelled
list.

**Measured.** See `Docs/EVOLUTION.md` §6.2 for the table. Headline: tempo **−0.32** under famine, **+0.75** on rich
food; selection-off control **−0.08 ±0.23**; the neutral cohesion locus drifts without direction.

**Checked.**
- `bash Tools/Build/evolution_harness/run.sh` — 46 core + 10 arena gates OK, 6/6 negative controls bit; evidence
  (9 regimes × 5 seeds × 4 h) in 8 s; golden written.
- `node Tools/Evolution/gate_parity.cjs` — 4/4 cases exact (20 + 20 + 10 + 10 census rows of 21 numbers each), RNG
  first draws, 8 mutation samples, phenotype and packed genome exact; 3/3 planted defects caught.
- `python3 Tools/Evolution/build_lab.py --check` — OK (0 drift, page fresh).
- `node .claude/skills/labmaker/verify_lab.cjs Tools/Evolution/DarwinLab.html` — PASS at 1600 × 900 and on an
  emulated iPhone 13; screenshots read: the HUD and the trait values no longer overlap their panel titles (fixed in
  the second bake); the phone layout stacks.
- The published page's `db` wiring: one probe document written to `decisions`, listed back, deleted.

**Found.**
- The parity gate's third negative control (the cost of pace removed) did NOT bite on the cap-bound Blob case: nobody
  starves there inside twenty minutes, so upkeep is inert. Moved to the famine case. Lesson: plant a defect where the
  mechanism it breaks actually runs (`LEARNINGS.md` L-STU-15).
- A seed floor near the carrying capacity erases adaptation (the `sparse` regime: 6,807 founders injected against
  2,744 births, no shift). A biome that turns evolution on needs its floor well below what it carries.
- Selection lowered the carrying capacity in famine (275 selected against 300 inert).
- The first bake was 2.9 MB because it carried every census row and every final genome; the page reads neither, so the
  bake keeps one row per five minutes and no genomes (596 KB).

**Decision needed (designer).** Which biome switches `Evolution.Enabled` on first, and whether a size locus is wanted
at all (`Docs/EVOLUTION.md` §8).

**Not modelled** (also on the page): space, plants as individuals, vessels, predator genomes, any mechanism for
cohesion, frame cost, and the shipped cap of 6.
