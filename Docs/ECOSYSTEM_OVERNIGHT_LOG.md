# Ecosystem — Overnight Work Log

Running autonomously while you're away (~8h). Bias: **safe, non-regressive** work
only — I can't validate in-editor, so I avoid changes that could leave the test
scenes broken, and I commit each coherent chunk. This log is newest-first so you
can skim what changed and why on return.

**Branch:** `claude/quirky-meitner-bcos8u`. Everything is pushed.

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
