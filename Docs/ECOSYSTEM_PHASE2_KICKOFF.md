# Phase 2 Kickoff — Cosmic Shore Emergent Ecosystem

**Purpose:** paste the block below into a fresh Claude Code thread to begin Phase 2
of the cell-ecosystem work. It's a self-contained brief — the new thread should
read the referenced docs first, then go deep. Phase 1 (a working prey-linked
fauna + flora ecology) is on `claude/keen-newton-OwUKo` and proposed to
`bleeding-edge` via PR; base Phase 2 on the merged result (or `keen-newton` if not
yet merged) and cut a fresh feature branch.

---

## ⬇️ Paste into the new thread

You are continuing the **Cosmic Shore cell-ecosystem** work. **Phase 1** — a
working, prey-linked fauna + flora ecology — has shipped (branch
`claude/keen-newton-OwUKo`, PR to `bleeding-edge`). Your mission is **Phase 2**:
turn it into the *ultimate dynamic, emergent* ecosystem — a small set of
fundamentals whose interactions produce rich, self-balancing, surprising behavior
— while **retiring the first-approximation scaffolding** Phase 1 left behind.

### Read first (in order)
1. **`Docs/ECOSYSTEM.md`** — the system map and your bible: the prism-count→phase
   spine, the three feedback loops, the §6 prey-linked-starvation decision, §7
   predator/herbivore extensibility, §9 key files, **§10 roadmap (Phase 2)**.
2. **`CLAUDE.md`** — architecture + the **"Favor Emergent Systems Over Bespoke
   Solutions"** and **"Don't cheat emergence without asking"** sections. Internalize
   the **fundamentals** (Domain, Mass/prisms, Cells, Elementals, Flora & Fauna,
   Vessels) and the order-of-preference: use an existing fundamental → tune it →
   extend it → propose a new one (with sign-off) → bespoke only as last resort.
3. **`Docs/DENSITY_PARTITIONING_AUDIT.md`** + **`DENSITY_PARTITIONING_HANDOFF.md`**
   — the density foundation the ecology sits on.

### Current state (what Phase 1 leaves you)
- **Two spawners, both live:** most scenes (Menu, Skim Race, …) run
  `RandomLifeSpawner` (`Cell.prefab` default `Random`), but the **WildlifeBlitz +
  Maelstrom** scenes select `IntensityWiseLifeSpawner` (`cellTypeChoiceOptions:
  1`). `IntensityWiseLifeSpawner` is **NOT dead — do not delete it.** (Earlier
  notes wrongly called it dead.) The two have diverged (Random has the prey-linked
  `FaunaFoodFloor` gate + population bursts; IntensityWise spawns 1/tick, phase-
  gated); reconcile rather than remove if it matters.
- **Fauna:** timer-driven, fixed-size populations in the cell's **controlling
  color**; **prey-linked starvation** — they consume opposing-domain prisms to
  live, and despawn if they can't feed for `starvationSeconds`; production pauses
  below `FaunaFoodFloor`. Prism-count→phase→**aggression** drives *behavior* (seek
  crystal / opposing centroid / densest), not spawn rate.
- **Flora:** plant + grow at a **steady rate until Frenzy** (`FloraPlantingEnabled =
  FloraGrowingEnabled = phase < Frenzy`). The regrowth pulse **and** the staggered
  phase-gated self-limit are both retired (that collapsed the phase ladder 6→3:
  Calm/Restless/Frenzy); a full cell now stays full until an active force eats it
  (valid state per §0).
- **Diet split landed (code):** `FaunaDiet` (Herbivore default | Predator) on the
  `Fauna` base; `LightFauna` consume branches by diet (herbivore→prisms,
  predator→herbivore fauna via `Predated`); both bounded by the shared starvation
  clock = two-tier Lotka–Volterra. **Predators aren't active until authored** —
  see ECOSYSTEM.md §7.1 (predator prefab + config + wire into a `SpawnProfileSO`).
- **Density:** Blob (menu) thresholds `RestlessEnter 600 / FrenzyEnter 1000`
  (perf-bounded — prism count is the dominant frame cost; see ECOSYSTEM.md §12).
- **All scaffolding cheats retired:** regrowth pulse, flora self-limit, and the
  fixed-period spawner as population source (now reproduction-driven; the timer
  only seeds back up to the floor — ECOSYSTEM.md §6.1). Prism decay was rejected
  (§0), not added.

### ⚠️ Locked invariant — mass is conserved (no passive decay). Read ECOSYSTEM.md §0.
A prism (flora health-prism or vessel-spawned) is removed **only by active
forces**: a vessel using an ability, or fauna eating it. There is **no decay,
aging, lifespan, timed culler, or growth/decay oscillator** — population
homeostasis is the **food web**, not artificial removal. A large prism
accumulation is a *valid* state; it persists until something active eats it, and
if no fauna can reach edible prey the correction shows up as fauna *starving*, not
prisms vanishing. **Prism decay was considered and rejected** — do not re-propose
it. If a cell "freezes," strengthen the food web (fauna diet/reach/spawning) or
vessel abilities; never add a culler. (CLAUDE.md → "Mass" + "Don't cheat
emergence.")

### Phase 2 build order (ECOSYSTEM.md §10 — each step ships alone, composes with the fundamentals, and retires a cheat)
1. **~~Prism mortality / decay~~ — REJECTED (see §0 above).** A timed culler is
   just the regrowth pulse inverted — a cheat. The down-force is the food web, not
   decay. **Done instead:** the regrowth pulse *and* the flora phase-gated
   self-limit are both retired (`FloraGrowingEnabled = FloraPlantingEnabled =
   phase < Frenzy`; steady growth until frenzy, phase ladder collapsed 6→3).
2. **Predator / herbivore split — code DONE, authoring/tuning remains.** The diet
   machinery is in (`FaunaDiet`, `Fauna.diet`/`Predated`, `LightFauna` consume
   branch); both diets share the starvation bound → Lotka–Volterra. **Next:**
   author a predator prefab + `FaunaConfigurationSO`, wire alongside the herbivore
   config in a `SpawnProfileSO`, then tune to a breathing equilibrium (§7.1). This
   is the active down-force that lets accumulations come down through the food web.
3. **Fauna reproduction — ✅ LANDED (ECOSYSTEM.md §6.1).** Well-fed fauna breed;
   the spawner is a seeder topping species up to their seed floor. Population is a
   true function of the food web.
4. **Elemental integration** — flora/fauna express effects through **Elementals**
   so vessels *feel* the ecology.
5. **Domain territory dynamics** · 6. **flora succession** · 7. **cross-cell migration.**

### Working rules (non-negotiable)
- **Favor emergence; don't cheat.** If the direct path would hard-code an outcome
  that *should* emerge from fundamentals interacting, **stop and ask the prompter
  first** (CLAUDE.md). Name the fundamentals involved; prefer the solution that
  leaves them more expressive.
- **You cannot run Unity.** Every change is validated by the human in-editor — that
  is the gate. Make changes self-reviewable: state the expected in-editor behavior
  and the exact knobs to tune. Never claim something works that you haven't seen
  work; report honestly.
- **Config separation / SOAP.** Tunables live in ScriptableObjects, not hard-coded.
  Cross-system comms via SOAP events/variables, not singletons or static events.
- **Surgical.** Match surrounding style; don't refactor working systems without
  need. Three similar lines beat a premature abstraction.
- **Involve the prompter.** Pause at genuine design forks (use the question tool).
  Take one coherent system per pass; commit + push each with conventional-commit
  messages. Develop on a **feature branch — never `bleeding-edge`**. Open a PR only
  when asked.

### Your first task
The regrowth-pulse retirement and the predator/herbivore **diet machinery are
already implemented** (see "Current state" + ECOSYSTEM.md §7). Two tracks remain,
pick up wherever the prompter points:

- **Activate + tune the food web (no code, then tuning).** Author a predator
  fauna (duplicate a `LightFauna` prefab, set `Diet = Predator`), make a
  `FaunaConfigurationSO`, wire it **alongside** the herbivore config in a
  `SpawnProfileSO` (§7.1). Then validate in Menu_Main and tune `PopulationSize`,
  `BaseFaunaSpawnTime`, `starvationSeconds`, `consumeRadius` so flora → herbivores
  → predators **oscillates** (breathes) instead of one-shot extinction. Likely
  code follow-ups surfaced by tuning: explicit predator "seek nearest herbivore"
  steering (today predators reuse the shared density goal), and gating predator
  *spawn* on herbivore count rather than the prism `FaunaFoodFloor` proxy.
- **Step 3 — fauna reproduction** (the remaining cheat): well-fed fauna breed; the
  fixed-period spawner becomes a one-time seeder so population is a true function
  of the food web. **Confirm the design fork with the prompter** before building.

Working rule still holds: **mass is conserved — never add prism decay/culling to
"fix" an accumulation** (§0). If a cell stays frozen, strengthen the food web.
