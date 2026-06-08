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
- **One live spawner:** every scene runs `RandomLifeSpawner` (the shared
  `Cell.prefab` is `Random`). `IntensityWiseLifeSpawner` is **dead** — delete it or
  reconcile; don't maintain two fauna models.
- **Fauna:** timer-driven, fixed-size populations in the cell's **controlling
  color**; **prey-linked starvation** — they consume opposing-domain prisms to
  live, and despawn if they can't feed for `starvationSeconds`; production pauses
  below `FaunaFoodFloor`. Prism-count→phase→**aggression** drives *behavior* (seek
  crystal / opposing centroid / densest), not spawn rate.
- **Flora:** plant `Phase < Settled`, grow `Phase < Frozen`, plus a **regrowth
  pulse** between Frozen and Rabid.
- **Density:** Blob (menu) thresholds scaled ~6× (Frozen 4200 / Rabid 5400).
- **Two cheats deliberately in place — your job is to retire them:**
  1. the flora **regrowth pulse** (a hard-coded oscillator), and
  2. the **fixed-period fauna spawner** (a hard-coded population source).

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
   just the regrowth pulse inverted — a cheat. The down-force on a dominant
   accumulation is the **food web**, not decay. The work that *replaces* this
   step is step 2 below; separately, **retire the regrowth pulse outright**
   (`Cell.FloraGrowingEnabled` → just `phase < Frozen`; drop
   `Cell.InFloraRegrowthPulse` + `SpawnProfileSO.FloraRegrowthPulse*`), accepting
   that a cell with no active force on it stays full until fauna/vessels eat it.
2. **Predator / herbivore split — START HERE.** *The centerpiece and the real
   first lever.* Fauna sub-type on
   `FaunaConfigurationSO` + "diet = what counts as prey" reuses the existing
   `Fauna.ResolveGoal`/consume/starvation hooks. Herbivores eat flora prisms,
   predators eat herbivore fauna → genuine Lotka–Volterra oscillation.
3. **Fauna reproduction** — *retires the fixed-period-spawner cheat.* Well-fed
   fauna breed; the spawner becomes a one-time seeder. Population becomes a true
   function of the food web.
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
Design and implement the **predator / herbivore split** (the real Phase 2 step 1
— prism decay was rejected; see §0). Goal: a fauna **sub-type** on
`FaunaConfigurationSO` (Herbivore / Predator) plus a **diet = "what counts as
prey"** parameter that reuses the existing `Fauna.ResolveGoal` / consume /
`NotifyFed` / starvation hooks — herbivores eat **flora prisms**, predators eat
**herbivore fauna**. Two-tier starvation → genuine Lotka–Volterra oscillation
(flora → herbivores → predators → …). This is the active down-force that finally
lets a dominant accumulation come down *through the food web* instead of through a
culler. Investigate the consume/starvation seam (`LightFauna.UpdateBehavior`,
`Fauna.IsStarving`/`NotifyFed`, `Cell.OpposingBlockCount`, `HealthPrism.Consume`),
**confirm the design fork with the prompter** (how diet is expressed; whether
predators target fauna bodies — which today are deliberately *not* cell-registered
HealthPrisms), implement it config-driven, and hand back precise in-editor
validation steps. Tune so the two-tier food web reaches a breathing equilibrium
rather than a single-crash extinction. **Separately** (smaller, independent pass):
retire the flora regrowth pulse outright — `Cell.FloraGrowingEnabled` collapses to
`phase < Frozen`, drop `Cell.InFloraRegrowthPulse` and
`SpawnProfileSO.FloraRegrowthPulse*` — accepting frozen accumulations as a valid
state per §0. Do **not** add prism decay to compensate.
