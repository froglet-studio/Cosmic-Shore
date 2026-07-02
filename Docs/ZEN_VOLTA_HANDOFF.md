# Handoff → zen-volta owner (instanced prism rendering)

**From:** the `claude/performance-refactoring-review-rciwzl` workstream (codebase-wide perf
review + extraction of every unmerged optimization branch).
**To:** whoever is driving `claude/zen-volta-t9su0i` / PR #573 to merge.
**Date:** 2026-07-02. **rciwzl HEAD at handoff:** `b5cf0ca34` (17 commits over bleeding-edge, compiles clean).

**One line:** your branch is the biggest single win in the whole perf backlog and a live
profiler capture proves it; this doc gives you the full picture of the *other* perf branch
that will merge alongside yours, the **exact** conflict surface between the two (4 files,
with a copy-pasteable resolution for each), the recommended merge order, and the two
product decisions only you can make.

---

## 1. Why you should care about `rciwzl` at all

Two performance branches are landing in the same window. Yours (`zen-volta`) is the
**render/architecture** attack: instanced prisms via Entities Graphics, instanced shields,
the collider-LOD sweep, spindle SRP-batching, volume cache, VFX cap. Mine (`rciwzl`) is the
**allocation/hot-path/correctness** attack: ~30 verified fixes + an extraction sweep that
re-ported the still-live wins from nine older optimization branches.

They are **almost entirely disjoint** — I ran `git merge-tree` between the two tips. Out of
~50 files each touches, they collide in exactly **4 files** (below). Everything else merges
untouched. So this is a clean two-branch land, not a tangle.

Full branch-by-branch audit of all 20 unmerged perf branches (what to merge / extract /
delete) is in **`Docs/PERF_BRANCH_MERGE_PLAN.md`**. What rciwzl itself changed and why is in
**`Docs/PERFORMANCE_REFACTOR_REVIEW.md`**. The living ledger is **`Docs/PERFORMANCE.md`**.

---

## 2. The profiler capture that validates your branch (use this)

A live Unity Profiler capture on rciwzl (instrumented run, `DiagnosticsHUD` present, ~53 ms
main-thread frame) showed the top three self-time costs, **all of which are things zen-volta
fixes**:

| Profiler row | Self | What it actually is | zen-volta fix |
|---|---|---|---|
| `DomainVolumeIndicator.Update` | **15.64 ms (29.5%)** | Misattributed — it's `Cell.EnsureVolumeFresh` walking **every prism's `transform.lossyScale`** (parent-hierarchy walk) on the 0.25 s volume recompute that the gauge's sample triggers. | **volume cache** (`1c2022288`) — measured 22.96 ms → O(growing)/frame |
| `PrismColliderLodManager.Update` | **8.56 ms (16.1%)** | O(population) LOD sweep every tick | **Checkpoint C** population-independent sweep + `QueryUnionOfSpheres` |
| `PrismOctahedronShield.Update` | 8,740 calls | per-shield no-op `Update`s | **central shield ticker** + instanced settled shields |

**~26 ms of a 53 ms frame is your branch + the one commit I pulled forward.** If you need a
number to justify the merge, this capture is it. (I already pulled the volume cache onto
rciwzl — see §4 — so if you re-profile that branch, `DomainVolumeIndicator.Update` should
have collapsed and `PrismColliderLodManager.Update` should now be the #1 row, i.e. the
direct empirical case for landing zen-volta next.)

---

## 3. THE CONFLICT SURFACE — 4 files, exact resolutions

Verified with `git merge-tree --write-tree rciwzl@b5cf0ca34 zen-volta`. When the second of
the two branches rebases/merges onto the first, these are the only files that need hands.
**rciwzl has unique, non-conflicting changes in `Prism.cs` and `PrismScaleManager.cs`
(PrismActivationQueue wiring, `scaleAnimator.GrowthRate` re-sync, cached layer id) that live
in separate regions and MUST survive the merge — don't blanket "take theirs" on whole files.**

### 3.1 `BoidSimulationController.cs` — modify/delete → **accept rciwzl's deletion**
rciwzl deletes it (verified dead: zero code refs, zero scene/prefab GUID refs, and it did a
**synchronous GPU readback every `Update()`** — a per-frame pipeline stall). zen-volta only
renamed its `struct Entity` → `BoidEntity` to dodge the `Unity.Entities.Entity` name clash.
Deleting the file satisfies that motive even better (the clash can't recur). **Resolution:
`git rm` it / keep deleted.**

### 3.2 `Spindle.cs` — content (3 hunks) → **union, both wins**
The two branches pull the same code opposite ways and both are right about their half:
- **zen-volta** removes the per-spindle `_Phase` MaterialPropertyBlock (MPB excludes the
  renderer from the SRP Batcher → ~600 un-batched spindle draws) in favor of **8 shared
  phase-variant materials** bucketed by world-position hash.
- **rciwzl** deletes the `originalMaterial`/`temporaryMaterial` **clone** path (one `Material`
  alloc + a leak-on-mid-condense-death per spindle) and drives `_DeathAnimation` through an
  **MPB** (`SetDeathAnimation`).

**Union recipe (this is the intended end state):**
1. Keep zen-volta's `GetPhaseVariant` + `RenderedObject.sharedMaterial = phaseVariant` —
   `_Phase` never touches an MPB.
2. Keep rciwzl's `SetDeathAnimation(float)` MPB helper, but for the wither/condense window
   **only** (the MPB now carries *only* `_DeathAnimation`, so it can't fight the variant).
3. Delete both clone paths (`UseTemporaryMaterial`, `RestoreOriginalMaterial`,
   `temporaryMaterial`, `originalMaterial`).
4. At animation end and in `DisableSpindle`, call `RenderedObject.SetPropertyBlock(null)` (NOT
   `SetDeathAnimation(0f)`) so the renderer **re-enters the SRP batch** at rest — otherwise a
   rest-state MPB write permanently re-excludes exactly the spindle zen-volta just un-excluded.

This mirrors your own prism-path principle: only the actively-animating subset leaves the batch.

### 3.3 `PrismScaleManager.cs` — content (2 hunks) → **take zen-volta's**
Both add work to the batched apply loop after `block.transform.localScale = …`. zen-volta is
the **superset**: `SyncRenderTransform()` (companion-entity matrix sync, no-op on legacy) **+**
`RefreshVolumeCache()`. rciwzl has only `RefreshVolumeCache()` (I dropped the render hook when
I pulled the volume cache forward, because `SyncRenderTransform` doesn't exist on rciwzl).
**Take zen-volta's version of both hunks.** Requires `PrismScaleAnimator.OwnerPrism` — present
on both branches.

### 3.4 `Prism.cs` — content (3 hunks after the Grow fix) → mostly **take zen-volta's**, keep rciwzl's cached layer id
- **Restore-path hunk** (`MarkRestored` region): zen-volta adds
  `PrismColliderLodManager.NotifyPrismActivated(this)` after `RefreshVolumeCache()`; rciwzl
  omits it (that hook doesn't exist on rciwzl). **Take zen-volta's** — the hook is real on
  your side.
- **Default-layer hunks (×2)**: rciwzl caches `DefaultLayerId` (kills a per-init
  `LayerMask.NameToLayer` string hash); zen-volta inlines `NameToLayer` + logs an
  invalid-layer warning. **Recommend keeping rciwzl's cached property** (it's the perf win and
  its `-1` branch already handles the invalid case); optionally fold zen-volta's
  `Debug.LogWarning` into the property's `-1` branch. Either compiles.
- **`Grow()` no longer conflicts** — see §4; I restored it on rciwzl so both sides are
  identical there.
- **DO NOT lose** rciwzl's unique `Prism.cs` changes in the non-conflicting regions:
  `PrismActivationQueue.EnsureInstance().Enqueue(...)` in `Initialize`, `PrismActivationQueue
  .Instance?.Cancel(this)` in `ResetState`, `ExecuteDeferredActivation` (replaces
  `CreateBlockCoroutine`), and `scaleAnimator.GrowthRate = growthRate` re-sync. A 3-way merge
  keeps them automatically; a manual "checkout theirs" would clobber them.

**Also auto-merges (no conflict, but both touch it):** `GenericPoolManager.cs` — zen-volta and
rciwzl edit different regions (rciwzl added `maxSyncPrewarm`; verify the hunk survives).

---

## 4. What rciwzl already did FOR you (so you can drop it from your reconciliation)

- **Pulled your volume-cache commit (`1c2022288`) forward onto rciwzl** (`96ce7042b`), because
  the profiler proved it was rciwzl's #1 cost too. Adapted: dropped the `SyncRenderTransform` /
  `NotifyPrismActivated` hooks (don't exist on rciwzl), added `PrismScaleAnimator.OwnerPrism`.
  The `Cell.EnsureVolumeFresh` read (`Prism.CachedVolume`), `RefreshVolumeCache`, and the
  create/destroy seeding are **content-identical to yours** — those hunks will auto-resolve;
  only the two hooks above show up as conflicts (take yours).
- **Caught & fixed the `Prism.Grow()` regression** (`b5cf0ca34`) — your cherry-pick
  `1c2022288` silently drops `public void Grow(float) => scaleAnimator.Grow(amount)` (its
  region auto-merges with no conflict marker), which breaks `Boid.cs`'s `Grow()` calls with
  CS1061. **You already fixed this on your lineage** ("restore Prism.Grow() accidentally
  dropped… (CS1061)"), so nothing to do — just be aware the two fixes are the same and the
  `Grow` line will merge clean.

---

## 5. Recommended merge order

**Land zen-volta first, then rebase rciwzl onto it.** Rationale: zen-volta is bigger,
touches scenes/prefabs/shaders, and needs the in-editor verification pass anyway; landing it
first freezes the harder surface, then rciwzl (mostly script-level) rebases and applies the
§3 resolutions. The reverse works too — whoever goes second does §3. Either way the recipe is
identical and written down here.

I (rciwzl workstream) am happy to **own the §3 reconciliation** on the rebase once your PR
lands — ping me and I'll execute it and re-run the merge-tree to prove it clean.

---

## 6. Decisions only you can make (blocking your merge, not mine)

1. **Run zen-volta's own verification protocol** — `Docs/PRISM_ECS_MIGRATION.md` §5 steps
   1–10 + §7 shader-reimport loop: isolated stress-scene color parity, HexRace visual A/B
   (bloom / wither / explode / implode / shield / danger / theme), Frame Debugger
   draws-decoupled-from-count, `ents ≈ prisms` probe, collider-LOD near-count parity. No
   post-fix gameplay capture exists on the branch yet — the 60 fps exit criterion is stated,
   not evidenced. **Verification, not more code, is what stands between zen-volta and merge.**
2. **Decide the shipping default.** The code defaults instancing **OFF**
   (`PrismRenderService.Enabled` → runtime override → `Resources/PrismRenderConfig` → default
   OFF), but the **committed `Resources/PrismRenderConfig.asset` ships it ON**
   (`useInstancedRendering: 1`). Merging as-is enables instancing for **every** platform —
   including any GLES3 Android target, where Entities Graphics has no BRG path and falls back
   to legacy. Either ship OFF and flip per-platform after device passes, or record the Vulkan
   min-spec decision (your doc §6 calls this "the biggest open product decision"). Also fix
   the stale `PrismRenderConfigSO` class docstring, which says the path defaults ON (contradicts
   both the field tooltip and the code).

---

## 7. Branch hygiene (safe to action now)

Per `Docs/PERF_BRANCH_MERGE_PLAN.md`. **Delete after zen-volta merges:**
`claude/kind-turing-3lh823` — `git log kind-turing --not zen-volta` is empty (fully contained
in your branch). **Delete now (superseded / dead, and two would REGRESS if merged):**
`beautiful-dirac-K5720` (its cherry-picked equivalents already landed; merging re-adds an
AudioListener removed by the Jun-29 spatialization fix), `loving-fermi-DVWsY` (carries the
reverted mass-conservation trail cap), `ecs-migration-guide-Db42i` (your own migration doc §2
rejects it by name), `density-partitioning-sync-6OXTO`, `add-mobile-performance-manager-IUiaA`,
and the merged ancestors `audit-density-partitioning-2EvgR` / `beautiful-bohr-wspnmf` /
`sweet-pascal-N3rny`.

---

## 8. Deferred (needs the editor; sequence AFTER both branches land)

- **Flora pooling port** (`claude/audit-flora-pooling-f5vmD`): `SpindlePoolManager`,
  `HealthPrismPoolManager`, pooled `LifeForm`/`Flora` paths, batched `SpindleAnimDriver`.
  Genuinely unlanded value, but its Spindle rewrite must be reconciled to the §3.2 union
  (shared materials at rest, MPB only while animating) — do it AFTER Spindle is settled.
- **AOEExplosion instance pooling** (rciwzl deferred it deliberately: `Instantiate` +
  reflection-DI + `Destroy` per detonation; reset-correctness needs the editor).
- **Mobile HyperSea skybox SubShader**, wired through `GraphicsSettingsApplier` quality tiers.
- **Fauna tick scheduler** — gate on ecology scaling (`/ecology` protocol).

---

## 9. TL;DR checklist for you

- [ ] Read §3 (the 4-file conflict recipe) — that's the whole interaction.
- [ ] Run the §6.1 in-editor verification protocol; capture a post-fix gameplay frame.
- [ ] Make the §6.2 shipping-default call (OFF-by-default + per-platform, or Vulkan min-spec).
- [ ] Merge PR #573 → `confident-clarke` → `bleeding-edge`, then ping the rciwzl workstream to
      rebase + apply §3 (or do it yourself with this doc).
- [ ] Delete `kind-turing` after you land; delete the §7 dead branches whenever.
