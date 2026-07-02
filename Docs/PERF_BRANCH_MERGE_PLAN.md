# Performance Branch Merge Plan — Path to the Most Performant Mergeable State (July 2026)

**What this is.** An audit of every unmerged branch carrying performance work (20 branches
across 273 remote refs), each verified against current `bleeding-edge` (`c833c580`) with
ancestry checks, `git merge-tree` dry-runs, and content greps — plus the sequenced merge
plan that lands the most performant state of the game. Companion docs:
`Docs/PERFORMANCE_REFACTOR_REVIEW.md` (July 2026 verified review, this branch) and
`Docs/PERFORMANCE.md` (living ledger, arrives with the `beautiful-feynman` merge below).

**Method notes.** The repo clone is shallow — several "+5,000 commits ahead" branches are
truncated-history artifacts, not real divergence; every verdict below was re-established
with `merge-base --is-ancestor` / `git cherry` / content comparison after deepening.
Two branches that *look* attractive would actively regress the game if merged
(`beautiful-dirac` re-adds an AudioListener removed by the June 29 spatialization fix;
`loving-fermi` contains the reverted mass-conservation-violating trail cap) — merged-state
verdicts below are authoritative over branch names.

---

## The one-paragraph answer

The most performant mergeable state is: **current `bleeding-edge` + `zen-volta`
(instanced prism rendering — the single largest win, code-complete, zero file conflicts
with bleeding-edge, gated only on its own in-editor verification protocol and a shipping-
default decision) + this branch (`performance-refactoring-review-rciwzl`, rebased after
zen-volta with one `Spindle.cs` reconciliation) + four small clean merges (super-shield
mesh cut, benchmark schema v2, the performance ledger, the fauna net-sync design doc) +
a curated extraction sweep re-porting the still-valuable ideas from nine older branches
(headlined by `PrismActivationQueue` and the flora pooling core), with fourteen
superseded/dangerous branch refs deleted.** Everything below is the evidence and order.

---

## 1. Verdict table — all audited branches

| Branch | Last work | Verdict | Why (evidence) |
|---|---|---|---|
| `claude/zen-volta-t9su0i` | **2026-07-02** | **MERGE (gated)** | Instanced prism rendering (Entities Graphics companion entities, Checkpoints A–C), instanced octahedron shields + shared-mesh cache, VFX apply ~97ms→bounded(256), ~23ms volume walk→O(growing), O(N) collider-LOD sweep→O(transitions), spindle SRP-batching, diagnostics+stress tooling. Zero changed-file overlap with bleeding-edge since its Jun-29 merge-base. Gates: its own doc's 10-step in-editor verification; decide the shipping default (code defaults OFF, committed `Resources/PrismRenderConfig.asset` ships **ON**); Android GLES3 vs Vulkan min-spec decision; fix stale `PrismRenderConfigSO` docstring. PR #573 stacks it into `confident-clarke`. |
| `claude/confident-clarke-f99p6r` | 2026-06-30 | **MERGE (as zen-volta's base)** | The prism-perf integration branch (kind-turing + Checkpoints A–C). zen-volta continues it; land via PR #573 chain. |
| `claude/performance-refactoring-review-rciwzl` | 2026-07-02 | **MERGE (after zen-volta)** | This branch. 25 verified fixes + review docs. Conflicts with zen-volta: `Spindle.cs` (see §3 recipe) and `BoidSimulationController.cs` (our deletion wins — it also satisfies zen-volta's rename motive). `Cell.cs`/`Prism.cs` auto-merge. |
| `claude/optimistic-planck-3f1ztv` | 2026-06-08 | **MERGE (cherry-pick `81746e28b`)** | Super-shield mesh 24→8 faces (3× tri cut) + 173-line topology/containment tests. Cherry-pick dry-run: zero conflicts. Complementary to zen-volta (touches only `Stellated*` files; zen-volta's shield work touches only regular octahedron files). Compounds if super-shields later ride the shared-mesh path. |
| `claude/beautiful-feynman-gnchcw` | 2026-06-19 | **MERGE** | `Docs/PERFORMANCE.md` (281-line ledger) + stale-audit-doc corrections. `merge-tree` clean vs bleeding-edge. One doc-hunk conflict vs this branch: resolve by taking feynman's rewritten Progress-Update section + this branch's more precise Rec-7 wording (collider-LOD ≠ full collider replacement), cross-link both docs, refresh the ledger's §5 for the July batches. |
| `claude/tender-pasteur-mxw3t4` | 2026-06-11 | **MERGE** | Benchmark schema v2 (CPU thread breakdown + physics time in captures). Bleeding-edge is still schema v1; the benchmark directory is untouched upstream since divergence — zero conflicts. |
| `claude/optimistic-maxwell-uet05g` | 2026-06-14 | **EXTRACT `eb146ee63`, delete** | Sole unique content: 415-line `Docs/ECOSYSTEM_NETWORK_SYNC.md` (fauna network-sync plan for the lava lamp — the still-open ECOSYSTEM.md item 4). Cherry-picks near-clean; refresh its §1 "current state" (predates the June ecology rework) before acting on it. |
| `claude/audit-flora-pooling-f5vmD` | 2026-06-12 | **REBASE-PORT (after zen-volta + this branch)** | Unlanded value: `SpindlePoolManager`, `HealthPrismPoolManager` (+prefab), pooled `LifeForm`/`Flora` paths, batched `SpindleAnimDriver` (48/64 caps, off-screen skip), `PrismActivationQueue`, strict prism pooling. None on bleeding-edge (grep-verified). Its Spindle rewrite supersedes this branch's Spindle fix but conflicts with zen-volta's shared-material batching — port the pooling core and re-shape the anim driver to zen-volta's model (§3). ~10-file conflict surface, no docs. Ecology protocol applies (pooled spindles must still wither — continuity law). |
| `claude/review-optimization-branches-WDr9T` | 2026-05-05 | **EXTRACT `b7f2f8814`, delete** | The prior curated "no-brainer port" was never merged. Of its 7 fixes: 2 landed independently (ClearPrisms MPB, MaterialStateManager snapshot), 2 are covered by this branch (TurnMonitorController LINQ, CurrentScore deletion), **3 still open**: `GenericPoolManager` sync-prewarm cap (scene-load hitch), `GunTransformer` per-Update `GetComponentsInChildren`, `AIPilot` cached `WaitForSeconds` (5 sites). Hand-apply — paths moved. |
| `claude/add-prism-activation-queue-CEoJM` | 2026-03-06 | **EXTRACT (top single idea), delete** | Thundering-herd fix: bleeding-edge `Prism.cs:175/235` still starts one coroutine + one `WaitForSeconds(0.6)` **per prism** — the branch profiled 49,856 coroutines resuming in one frame (1.9s stall, 10.1MB GC) on mass spawn. Re-port carefully (destroyed-guard, spatial-index registration ordering, `_lodCulled` interplay); prefer folding into the `PrismTimerManager` centralized-timer pattern. ~1 day incl. benchmark proof. |
| `claude/add-spawnable-caching-wg2zr` | 2026-03-04 | **EXTRACT `0b4b69ec8`, delete** | The caching system landed; the **cache-key bug fix didn't** — `SpawnableBatman/Cord/Helix` hashes omit `intensityLevel`/`domain`, so intensity changes can serve stale trail data today. Fold the three fields into the base key, strip subclass hashes. |
| `claude/optimize-mobile-performance-7uCEG` | 2026-03-09 | **EXTRACT residuals, delete** | Beyond the WDr9T subset, still open: `Projectile` material clones → MPB, `ParametricJetEffect` per-call `new Gradient()`, `Prism` per-init `LayerMask.NameToLayer(string)`. |
| `claude/optimize-menu-performance-5EODy` | 2026-03-08 | **EXTRACT ideas, delete** | 4 of 5 still open on bleeding-edge: DailyChallengeModal 1Hz throttle, QuestTrackView RectTransform caching + idle gate, InfiniteScroll `ForceUpdateCanvases`→targeted rebuild, HangarScreen card reuse. Files rewritten since — re-implement, don't port. (First two were independently re-found by this branch's review, Tier-3.) |
| `claude/benchmark-mobile-performance-SYydw` | 2026-03-09 | **EXTRACT 2 items, delete** | Still open: `ShapeDrawingManager` LineRenderer material leak (`lr.material = new Material(shader)`, no cleanup — trivial); mobile HyperSea skybox SubShader (re-implement against the reworked shader, gate via `GraphicsSettingsApplier` quality tiers). Rest superseded/moot. |
| `claude/optimize-scene-load-times-5sopf` | 2026-03-05 | **EXTRACT 1 item, delete** | `PrismTimerManager` `_disposing` early-out + swap-remove (mass-teardown O(1)) still missing. Scene-load piece superseded by `SceneLoader`/`SceneTransitionManager`; `[ScenePerf]` markers superseded by the benchmark suite; ActivationQueue via CEoJM instead. |
| `claude/optimize-pool-manager-6tOOr` | 2026-03-07 | **EXTRACT via WDr9T, delete** | Sync-prewarm cap — included in `b7f2f8814`. The stale-refs half landed independently (`ReleaseAllActive`). |
| `claude/optimize-shield-effect-CgpSK` | 2026-04-15 | **EXTRACT concept, delete** | The shockwave-ring visual lost to the shipped octahedron shield language — do not port. **Still live**: per-prism shield **SFX stacking** (`PrismStateManager.cs:129/150` plays one SFX per prism per AOE wave) — extract the per-wave-origin audio coalescing concept only. |
| `claude/add-mobile-performance-manager-IUiaA` | 2026-03-07 | **DELETE (superseded)** | `MobilePerformanceManager` merged in March and was later retired; `GraphicsSettingsApplier` is now the documented single writer of engine graphics state; its benchmark suite lost to `Utility/PerformanceBenchmark/`. Reviving it would violate the single-writer settings design. |
| `claude/ecs-migration-guide-Db42i` | 2026-06-12 | **DELETE (rejected by successor)** | zen-volta's `Docs/PRISM_ECS_MIGRATION.md` §2 reviews it by name: stale by two generations (PrismAOERegistry→PrismSpatialIndex, VContainer→Reflex), Phase-0 double-books every prism (perf regression), defers rendering (the actual cost). Its salvageable ideas are already folded into the successor doc. |
| `claude/kind-turing-3lh823` | 2026-06-18 | **DELETE after zen-volta merges** | `git log kind-turing --not zen-volta` = empty — fully contained. |
| `claude/density-partitioning-sync-6OXTO` | 2026-05-09 | **DELETE (superseded + rejected)** | Every durable idea shipped in stronger form (add-time domain snapshot via the audit branch, kernel smoothing via Phase 1/2, Blue anyGrid); its `DensityPartitionSystem` singleton is rejected *by name* in `DENSITY_PARTITIONING_AUDIT.md` §4.4; its `Cell.cs` integration points were rewritten by Phase 3. |
| `claude/audit-density-partitioning-2EvgR` | 2026-06-03 | **DELETE (fully merged)** | Branch head is an ancestor of bleeding-edge; its HANDOFF doc lives on (updated) at `Docs/DENSITY_PARTITIONING_HANDOFF.md`. |
| `claude/beautiful-bohr-wspnmf` | 2026-06-12 | **DELETE (fully merged)** | Ancestor of bleeding-edge (merged via `cb506b88e`). |
| `claude/sweet-pascal-N3rny` | 2026-05-28 | **DELETE (fully merged)** | Ancestor of bleeding-edge. |
| `claude/loving-fermi-DVWsY` | 2026-06-02 | **DELETE (fully merged; do NOT cherry-pick from it)** | Ancestor of bleeding-edge — and it contains the trail ring-buffer cap (`64d8f0c8`) that was deliberately reverted (mass conservation). |
| `claude/beautiful-dirac-K5720` | 2026-05-26 | **DELETE (superseded; merging would REGRESS)** | Its 3 commits landed via cherry-picks (`0c4e977c6`, `257927aa2`) with later refinements; its `CameraManager.prefab` still carries the AudioListener that the June-29 spatialization fix (`686935ccd`) removed. |

---

## 2. The sequenced path forward

Measure every wave with the PerformanceBenchmark suite (baseline → change → Compare —
the ledger's methodology). One wave per PR; don't batch unrelated waves.

### Wave 0 — clean merges, no editor gate (can land today)
1. **Cherry-pick `81746e28b`** (planck: super-shield 8 faces + tests) onto bleeding-edge.
2. **Merge `beautiful-feynman`** (PERFORMANCE.md ledger) — clean.
3. **Merge `tender-pasteur`** (benchmark schema v2) — clean; do this *before* Wave-1
   verification so captures carry the thread/physics breakdown.
4. **Cherry-pick `eb146ee63`** (maxwell: fauna net-sync design doc), refresh its §1.

### Wave 1 — zen-volta (the decisive win) — needs the editor
5. Run zen-volta's own verification protocol (`Docs/PRISM_ECS_MIGRATION.md` §5 steps
   1–10 + §7 shader-reimport loop): stress-scene color parity, HexRace visual A/B
   (bloom/wither/explode/implode/shield/danger/theme), Frame Debugger
   draws-decoupled-from-count, `ents≈prisms` probe, collider-LOD near-count parity.
6. **Decide the shipping default**: code defaults OFF, the committed
   `Resources/PrismRenderConfig.asset` ships ON. Either ship OFF and flip per-platform
   after device passes, or record the Android Vulkan min-spec decision (Entities
   Graphics needs Vulkan/Metal/DX — GLES3 devices fall back to the legacy path).
   Fix the contradictory `PrismRenderConfigSO` class docstring.
7. Land PR #573 (`zen-volta` → `confident-clarke`), then `confident-clarke` →
   `bleeding-edge`. Zero file overlap with bleeding-edge's toy/audio commits since the
   merge-base — expected conflict-free.
8. Delete `kind-turing` (contained).

### Wave 2 — this branch (rciwzl)
9. Rebase/merge `performance-refactoring-review-rciwzl` onto post-zen-volta
   bleeding-edge. Two hand-resolves:
   - **`Spindle.cs` union** (both wins): keep zen-volta's 8 shared phase-variant
     materials for `_Phase` (SRP batching — no MPB at rest); keep this branch's
     `SetDeathAnimation` MPB helper for the transient wither/condense window only;
     delete both clone paths; at rest call `SetPropertyBlock(null)` so the renderer
     re-enters the batch (do NOT leave a rest MPB write).
   - **`BoidSimulationController.cs`**: accept this branch's deletion (also satisfies
     zen-volta's `Entity` name-clash rename motive).
   `Cell.cs` / `Prism.cs` auto-merge (verified disjoint hunks).

### Wave 3 — extraction sweep (small re-ports; one PR, benchmark-proven)
10. WDr9T `b7f2f8814` remainder: `GenericPoolManager` sync-prewarm cap,
    `GunTransformer` component caching, `AIPilot` cached waits.
11. **`PrismActivationQueue`** (from CEoJM, or the flora-pooling branch's copy) —
    re-shaped onto `PrismTimerManager`; validate with the stress scene. The
    highest-value single extraction (measured 1.9s stall on mass spawn).
12. Spawnable cache-key fix (latent stale-cache bug — arguably a correctness fix).
13. Small fixes: `ShapeDrawingManager` LR material leak, `Projectile` MPB,
    `ParametricJetEffect` gradient reuse, `Prism` cached layer id,
    `PrismTimerManager` `_disposing` + swap-remove.
14. Menu UI sweep: DailyChallengeModal 1Hz, QuestTrackView caching + idle gate,
    InfiniteScroll targeted rebuild, HangarScreen card reuse.
15. Shield **SFX** coalescing per wave origin (concept from CgpSK; visual stays
    octahedron).

### Wave 4 — flora pooling port (ecology protocol; after Waves 1–2)
16. Port `SpindlePoolManager` + `HealthPrismPoolManager` + pooled `LifeForm`/`Flora`
    paths from `audit-flora-pooling`; re-shape its `SpindleAnimDriver` to the
    reconciled Spindle model (shared materials at rest, MPB only while animating,
    caps + off-screen skip). State collider impact (none — pooling, not population);
    verify wither/condense continuity in-editor.

### Wave 5 — mobile GPU (re-implementation)
17. Mobile HyperSea skybox SubShader (re-implement against the current shader), wired
    through `GraphicsSettingsApplier` quality tiers — never a hardcoded mobile manager.

### Branch hygiene (after each wave lands)
Delete: `audit-density-partitioning-2EvgR`, `beautiful-bohr-wspnmf`, `sweet-pascal-N3rny`,
`loving-fermi-DVWsY` (merged ancestors); `beautiful-dirac-K5720`,
`density-partitioning-sync-6OXTO`, `ecs-migration-guide-Db42i`,
`add-mobile-performance-manager-IUiaA` (superseded/dangerous); `kind-turing-3lh823`
(after Wave 1); the seven March branches + `optimize-shield-effect` + `optimistic-maxwell`
(after their extractions land); `review-optimization-branches-WDr9T` (after Wave 3).

---

## 3. Backlog items surfaced by the audits (not branch-bound)

- Fauna swarm-reach instrumentation (`DENSITY_PARTITIONING_AUDIT.md` §7.4-2) was never
  built — the "can the swarm reach shell mass?" question is still unmeasured.
- `DENSITY_PARTITIONING_AUDIT.md` never received its §8 ecology/HUD addendum
  (HANDOFF §3.3) — mitigated by `ECOSYSTEM.md` §§5–12.
- The ecology overnight-log "Validate on return" in-editor checklists remain open.
- Scene-view gizmo overlays for live anti-domain density answers (the one good idea
  bound to the rejected `DensityPartitionSystem`) — could be rebuilt on the per-cell
  grids if wanted.
