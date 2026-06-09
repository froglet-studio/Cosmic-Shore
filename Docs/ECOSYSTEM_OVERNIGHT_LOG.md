# Ecosystem — Overnight Work Log

Running autonomously while you're away (~8h). Bias: **safe, non-regressive** work
only — I can't validate in-editor, so I avoid changes that could leave the test
scenes broken, and I commit each coherent chunk. This log is newest-first so you
can skim what changed and why on return.

**Branch:** `claude/quirky-meitner-bcos8u`. Everything is pushed.

## ⬅️ Validate on return (the things only you can check in-editor)

1. **Tadpoles now eat (menu + skim race).** Last code session before the loop:
   foragers Consume (suction) any **unshielded** prism of **any** domain except
   fauna bodies — so the dominant trail gets grazed. Watch: do tadpoles visibly
   suck in trail prisms? Does Skim Race FPS recover at late laps now?
   - If the **race track gets eaten** → the track isn't shielded; tell me and I'll
     gate foragers differently (the current safeguard is "skip shielded prisms").
   - If the **menu flora gets stripped** → reduce `Blob Tadpole` `PopulationSize`
     (currently 25); flora regrow, so it should breathe, not go barren.
2. **Tadpoles have a health prism now** (Boid.Initialize initializes the body).
3. **No predators in either scene** (I removed sharks earlier — they ate
   everything at co-spawn). Spawn-immunity is now built (dormant) so a balanced
   predator can be re-added safely; I did NOT re-add one (didn't want to muddy
   your foraging test). Say the word and I'll add a low-count shark to the menu.
4. **Run the new edit-mode tests** (CellPhaseRules + ecology enums) — they're
   pure-logic regression guards and should pass.

**State of the two test scenes (unchanged by overnight work unless noted):**
- **Menu (Blob):** flora + tadpole forager + brittlestar. No shark (removed
  earlier — it ate everything at co-spawn).
- **Skim Race:** tadpole forager + brittlestar, no flora, no shark.

---

## Session 1 — spawn-immunity foundation (predator can return safely)

**Why:** the "only sharks" bug was predators eating co-spawned herbivores
instantly (all fauna spawn at the cell centre). To ever re-add a balanced
predator (the menu's vibrant-ecosystem goal), prey need a post-spawn grace
window. Built the mechanism now; it's **dormant** (no predators are wired into
either test scene), so current behavior is unchanged.

**Changes (`Fauna.cs`, `LightFauna.cs`):**
- `Fauna.predationImmunitySeconds` (default 6s) + `_spawnTime` stamped in a new
  `Awake` (runs during `Instantiate`, before any predator's first tick) +
  `IsPredationImmune`.
- `Fauna.Predated` now returns `bool` and refuses while `IsPredationImmune`
  (or already eaten). Predators feed (`NotifyFed`) only on a real kill.
- `LightFauna` predator branch updated to `if (… && prey.Predated(...)) NotifyFed()`.

**Not done (deliberately deferred):** actually re-adding a shark to the menu —
that's a behavior change I want to make only after the immunity is proven, so I'm
leaving the scenes predator-free for now.

---

## Session 2 — self-review of Boid.cs (found + fixed a real bug)

Reviewed the most-churned file holistically. **Bug found:** the any-domain
forager change made tadpoles eat *other fauna's body prisms* — brittlestar/shark
bodies are `HealthPrism`s but not `Boid`s, so they reached the prism-eat branch
and a tadpole would Consume them (herbivores eating fauna, which should be the
predator's job). **Fix:** the forager edible check now also excludes any prism
under a `Fauna` (`GetComponentInParent<Fauna>()`), so foragers eat only
trail/flora mass — never shielded structure (track) and never fauna bodies.

**Noted (not changed — consistent with LightFauna, minor):** initializing the
tadpole body prism (so it has a real health prism, per your note) also registers
it in the cell density grid, so fauna bodies count toward `LiveBlockCount`/phase.
LightFauna already does this for brittlestar/shark bodies. A future cleanup could
make `Prism.RegisterWithCell` skip fauna-owned bodies, but that touches LightFauna
too, so I left it.

---

## Session 3 — corrected the "IntensityWiseLifeSpawner is dead" docs

Checked spawner selection across all scenes. `IntensityWiseLifeSpawner` is **NOT
dead** — `MinigameWildlifeBlitz`, `MinigameWildlifeBlitzMultuplayerCoOp`, and
`MinigameTournamentMultuplayer` select it (`cellTypeChoiceOptions: 1`). The docs
(ECOSYSTEM.md §1 root-causes, §8, §9, §10; the kickoff brief) claimed it was dead
and recommended deletion — that would have broken those scenes. Corrected all five
places to say it's live (used by WildlifeBlitz + Tournament), do not delete, and
note it has diverged from `RandomLifeSpawner` (no `FaunaFoodFloor` gate, 1/tick).
Doc-only; no code change.

---

## Session 4 — reviewed LightFauna predator/herbivore paths (clean)

Read LightFauna's consume logic end-to-end. Correct: herbivores eat opposing
**flora** (HealthPrism+LifeForm branch) AND opposing **trails** (plain-Prism
branch); the predator branch matches the Fauna base (eats any herbivore species),
respects post-spawn immunity, and feeds only on a real kill. No bug, no change.
Observation: brittlestars are still opposing-domain only (don't graze the dominant
trail) — only the tadpole does that now; acceptable since the tadpole is the
primary cleaner.

---

## Session 5 — edit-mode tests for CellPhaseRules (phase spine)

Added `Assets/_Scripts/Tests/EditMode/CellPhaseRulesTests.cs` (20 tests, guarded
`#if UNITY_EDITOR`, NUnit). Covers the pure phase-resolution function that drives
the whole ecology: climbing (keys off Enter), descending (keys off Exit, strict
`<`), **hysteresis band stability** (no chatter across an oscillating count),
multi-step spike/crash transitions resolving in one call, boundary cases, and the
Default threshold-table invariants (Enter>Exit per phase, monotonic Enters,
`IsAllZero` legacy-substitution guard). All expectations hand-traced against
`CellPhaseRules.Compute`. Safe (test-only).

---

## Session 6 — ecology enum drift guards

Added `EcologyEnumIntegrityTests.cs` (UNITY_EDITOR-guarded): locks the integer
values of `FaunaDiet` (Herbivore 0 / Predator 1 — now serialized on the fauna
prefabs as `diet:`), `CellPhase` (None 0 … Rabid 6), and `CellAggressionLevel`
(0/1/2), plus monotonic-ordering and uniqueness checks. Prevents a silent
reorder from corrupting prefab/asset fields. Test-only.

---

## Session 7 — ECOSYSTEM.md accuracy pass + loop wound down

The forager behavior changed several times (opposing-domain → any-domain →
any-domain-unshielded-non-fauna via Consume/suction), and sharks were removed, so
the bible had stale statements. Fixed §7 (herbivore description = the tadpole now
Consumes any unshielded non-fauna prism; brittlestar = opposing), §7 intro (only
the two herbivores are wired in; shark built-but-unwired), and §7.2-A (menu has no
shark; spawn-immunity is built, not a to-do).

**Loop wound down here.** I've completed the safe, high-value backlog (spawn-
immunity foundation, a real bug fix, the IntensityWise doc correction, code
review, two test files, validation checklist, doc accuracy). What remains is
either (a) behavior changes that need your in-editor validation (re-add a balanced
predator, per-scene forager diet, voracity/count tuning) — which I won't do blind
while you're away — or (b) low-value churn. Per the plan, I stopped rather than
manufacture risky changes to fill hours. The heartbeat monitor timed out on its
own; not re-armed.

**To resume the food web on your return:** the spawn-immunity makes re-adding a
predator safe — add the `Blob Shark` config back to the Blob profile (low
`PopulationSize`). Tell me your read on the foraging (does the tadpole graze
visibly? does Skim Race FPS recover?) and I'll tune from there.

---

## Backlog (planned for later sessions, safest first)
1. Doc consolidation — `ECOSYSTEM.md` has drifted across many edits; make it match
   the current code (cell-config spawn is live; scene-placed `*Population` via
   `fauna2` is dead; forager = any-domain-unshielded Consume; etc.).
2. Reconcile `IntensityWiseLifeSpawner` — likely dead now that HexRace uses Random;
   document clearly (no blind deletion).
3. Edit-mode tests for testable logic (`FaunaDiet`, predation immunity timing,
   `GameDataSO` team counts) where they don't need Unity runtime.
4. Self-review pass over all fauna changes for latent bugs.
5. (Behavior, higher-risk — may leave for you) re-add a low-count shark to the
   *menu* with immunity, to restore the predator/prey layer; per-scene forager
   diet so the menu isn't stripped by any-domain foragers.
