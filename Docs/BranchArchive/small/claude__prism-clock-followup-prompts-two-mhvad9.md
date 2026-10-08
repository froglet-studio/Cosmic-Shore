# Branch archive: `claude/prism-clock-followup-prompts-two-mhvad9`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-04 by Claude
- **Unmerged commits:** 1
- **Forked from:** `2ab828867` (2026-08-03, new song added)
- **Tip:** `bbb47c111`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Toys/Microscene.cs`
  - `Docs/PRISM_ANIMATION.md`
  - `Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md`

### `bbb47c111` — fix(toys): the conveyor recycle's kind-wipe missed the birth window

_Claude, 2026-08-04 06:16:31 +0000_

```text
Audit follow-up to the C13a fix (762dd2b6). That commit's birth rule -- a
shield engaged/disengaged while !IsCreationComplete SNAPS, because the grow-in
bloom already carries continuity of existence -- was applied to only one half of
the Wanderway recycle.

Microscene.RearrangeIntoAsync cleared the old kind BEFORE Prism.Initialize. But
IsCreationComplete is what opens the birth window and Initialize is what clears
it, so at the wipe the prism was still IsCreationComplete from its PREVIOUS
life: PrismStateManager.ApplyNormalState computed birth == false and ran a LIVE
disengage on every recycled shielded/supershielded prism -- a 0.6s shatter
overlay with a per-frame morph-mesh rebuild (PrismOctahedronShieldManager) and
one ShieldDeactivate SFX each, layered over the fresh grow-in bloom.

The grow stamp survived it (Disengage clears _exoticVisualActive
unconditionally), which is why C13a still stood and nothing screamed. Pure
invisible cost plus a wrong-looking recycle, on one of the two surfaces Prompt
2's own in-editor test names.

The asymmetry was visible in the file: the sibling PrismKinds.Apply already ran
after Initialize and carried an explicit birth-rule rationale; only the Clear
was left at the top. Collapsed both into PrismKinds.Retheme after Initialize --
the purpose-built clear-then-apply that Microscene's own class doc already
claimed it used, and which until now had zero callers.

Clearing early looked like the safe order and is exactly backwards. It also
never achieved the state-leak proofing it was written for: Initialize re-applies
baked prismProperties.IsShielded/IsDangerous afterwards regardless, so the wipe
has to run after it to stick. No frame boundary is crossed between the pose
write and the wipe (the slice yield is after Retheme, and CreateBlockCoroutine
yields before its first statement), so the old shield collider cannot outlive
the iteration.

Docs: PRISM_ANIMATION.md §4.5(b) gains the ordering corollary (wipe AFTER
Initialize, never before) and the C13a tracker row records the audit; the
follow-up prompts doc records the hole, the fix, and the outstanding play-test
debt -- C13a shipped code-complete and its in-editor test has never been run.

Also fixes the rank-vs-prompt-number collision in the follow-up prompts priority
table: "Prompt 2" (a permanent section name) and rank 2 (which was Prompt 9)
read as the same thing. Rank column is now labelled and the done item is out of
the numbered sequence.
```

```text
 Assets/_Scripts/Controller/Toys/Microscene.cs | 23 ++++++++++++-----------
 Docs/PRISM_ANIMATION.md                       | 18 +++++++++++++++++-
 Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md          | 56 ++++++++++++++++++++++++++++++++++++++++++++------------
 3 files changed, 73 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 160 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Toys/Microscene.cs b/Assets/_Scripts/Controller/Toys/Microscene.cs
index 9d11622d6..4cf1edf7c 100644
--- a/Assets/_Scripts/Controller/Toys/Microscene.cs
+++ b/Assets/_Scripts/Controller/Toys/Microscene.cs
@@ -243,11 +243,6 @@ namespace CosmicShore.Gameplay
 
                 var lay = plan.Prisms[i];
 
-                // Wipe any previous kind BACK to plain before re-init, so a shielded/supershielded/
-                // danger prism from the last arrangement can't leak its state (or its always-on
-                // convex MeshCollider) into a plain slot. Reversible by construction.
-                PrismKinds.Clear(block);
-
                 block.ChangeTeam(lay.Domain);
                 block.transform.localPosition = lay.Point.Position;
                 block.transform.localRotation = lay.Point.Rotation;
@@ -271,12 +266,18 @@ namespace CosmicShore.Gameplay
                 // so an earlier write would be silently dropped.
                 block.TargetScale = lay.Point.Scale;
 
-                // Apply the new kind AFTER Initialize (which may have re-applied a stale baked flag);
-                // additive on a now-plain prism. Initialize has already cleared IsCreationComplete,
-                // so PrismStateManager reads this as a BIRTH transition and the shield snaps
-                // silently instead of opening a 0.35s morph across the creation reveal
-                // (Docs/PRISM_ANIMATION.md §4.5).
-                PrismKinds.Apply(block, lay.Kind);
+                // Re-theme AFTER Initialize, clear AND apply together: Initialize has cleared
+                // IsCreationComplete, so PrismStateManager reads BOTH halves as a BIRTH transition
+                // and the shield snaps silently instead of opening a morph across the creation
+                // reveal (Docs/PRISM_ANIMATION.md §4.5). Clearing BEFORE Initialize looks safer but
+                // is the bug: the prism is still IsCreationComplete from its PREVIOUS life, so the
+                // disengage reads as a transition on live mass — a 0.6s shatter overlay with a
+                // per-frame morph-mesh rebuild and one ShieldDeactivate SFX per recycled shielded
+                // prism, layered over the fresh grow-in bloom. Clearing here also reverses any stale
+                // baked flag Initialize just re-applied (prismProperties.IsShielded/IsDangerous),
+                // which is why a plain slot cannot inherit the last arrangement's kind (or its
+                // always-on convex MeshCollider) despite the wipe now running later.
+                PrismKinds.Retheme(block, lay.Kind);
 
                 if (!SliceExhausted(slice)) continue;
                 await UniTask.Yield(PlayerLoopTiming.Update, ct);
diff --git a/Docs/PRISM_ANIMATION.md b/Docs/PRISM_ANIMATION.md
index 2398f5a33..b9948463f 100644
--- a/Docs/PRISM_ANIMATION.md
+++ b/Docs/PRISM_ANIMATION.md
@@ -825,6 +825,22 @@ its faces exactly as before. `SegmentSpawner.SuperShieldSpawnedPrisms`, which po
 shield component directly rather than going through `PrismStateManager`, honours the
 same rule explicitly.
 
+**The ordering corollary — wipe AFTER `Initialize`, never before it** (added 2026-08-04,
+from a hole the audit found on the Wanderway recycle). `IsCreationComplete` is what opens
+the birth window, and `Prism.Initialize` is what clears it. A recycled prism is therefore
+still `IsCreationComplete` from its PREVIOUS life until `Initialize` runs, so **clearing
+a kind/state before `Initialize` is a LIVE transition on a prism that is about to be
+reborn** — it buys the full 0.6 s shatter overlay, the per-frame morph-mesh rebuild and a
+state SFX, per prism, layered over the grow-in bloom that is about to start. Clearing
+early *looks* like the safe order (wipe before re-init) and is exactly backwards. It also
+does not even achieve the leak-proofing it was written for, because `Initialize` re-applies
+baked `prismProperties.IsShielded` / `IsDangerous` afterwards anyway. Use
+`PrismKinds.Retheme` (clear-then-apply, both inside the birth window) *after* `Initialize`
+— `Microscene.RearrangeIntoAsync` is the reference call site. This is a distinct failure
+from §3.8 #10: the grow stamp survives it (`Disengage` clears `_exoticVisualActive`
+unconditionally), so nothing screams — it is pure invisible cost plus a wrong-looking
+recycle.
+
 ## 5. Migration tracker (the deduplicated work list)
 
 Phase A — infrastructure (everything else rides on it):
@@ -860,7 +876,7 @@ Phase C — rogue paths & ecosystem visuals (each is standalone):
 | C10 | Worm segment make-room shift → stamped slide (locomotion stays mover-contract) | ☐ not started |
 | C11 | Spindle `_DeathAnimation` fade (prism-adjacent) → clock inputs on spindle material | ☐ not started |
 | C12 | `PrismImplosion` watchdog → scheduler; orphan cleanup; `SkimFxRunner` stretch beam review; `CloakSeedWall` dead code removal | ◐ 2026-08-01: `TrailBlockBufferManager` deleted; `TrailViewer` removed from Urchin.prefab + deleted (D2, 2026-08-02); watchdog / SkimFxRunner / CloakSeedWall pending |
-| C13a | Environment-laid prisms miss the clock path (the live repro: `grow:SpawnablePrism (Clone)`) | ✅ FIXED 2026-08-02 — root cause was NOT the raw-`Instantiate` lay: the shield engage-morph held `_exoticVisualActive` across the creation reveal, so `EnsureRenderEntity` was skipped at the exact instant the one-shot grow stamp fired. Fixed by §4.5 (a) entity existence ⊥ visibility + stamp-site self-heal + fact-based diagnosis, and (b) the birth rule (spawn-time shields snap). §3.8 #10 has the full anatomy. Pooling is orthogonal — a pooled prism with a `Shielded` kind failed identically; `BoostRingBuilder` only escaped because it defers shield kinds to `onGrown` |
+| C13a | Environment-laid prisms miss the clock path (the live repro: `grow:SpawnablePrism (Clone)`) | ✅ FIXED 2026-08-02 — root cause was NOT the raw-`Instantiate` lay: the shield engage-morph held `_exoticVisualActive` across the creation reveal, so `EnsureRenderEntity` was skipped at the exact instant the one-shot grow stamp fired. Fixed by §4.5 (a) entity existence ⊥ visibility + stamp-site self-heal + fact-based diagnosis, and (b) the birth rule (spawn-time shields snap). §3.8 #10 has the full anatomy. Pooling is orthogonal — a pooled prism with a `Shielded` kind failed identically; `BoostRingBuilder` only escaped because it defers shield kinds to `onGrown`. **Audit 2026-08-04:** every claimed mechanism verified on disk (`Prism.cs:408`, `EffectiveRenderMesh`, `TryEnsureRenderEntityForStamp` at `PrismScaleAnimator.cs:165`, `IsBirthTransition` at `PrismStateManager.cs:58`), and one asymmetry closed — `Microscene.RearrangeIntoAsync` wiped the old kind BEFORE `Initialize`, so the *disengage* half missed the birth window on every recycled shielded conveyor prism (shatter overlay + per-frame morph rebuild + SFX, no stamp loss). Now one `PrismKinds.Retheme` after `Initialize`; see the ordering corollary in §4.5(b). **Code-complete but NOT play-tested** — the in-editor test (HexRace track build + Wanderway conveyor) is still owed |
 | C13b | Environment lay pooling: `PrismTrailBuilder.LayOne` → pooled pull with final domain material (kills the `Domains.Blue` → domain spawn repaint) | ☐ not started — still worth doing on its own merits (spawn repaint, alloc churn), but it is NOT a clock-path fix. Note the pools are `maxSize`-bounded and environment mass is never released, so a naive pool-through would either destroy conserved mass on release or instantiate forever; it needs its own environment-prefab pool design |
 
 Phase D — lock-in:
diff --git a/Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md b/Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md
index d8950d1a0..db1589b34 100644
--- a/Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md
+++ b/Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md
@@ -5,19 +5,25 @@ branch (`claude/prism-animation-audit-*`). Each is self-contained for a FRESH
 session: it names the docs to read first, the scope, the constraints, and the
 in-editor test that closes it. Run them as separate branches.
 
-**PRIORITY ORDER (set at ship, 2026-08-02) — work top-down:**
+**PRIORITY ORDER (set at ship, 2026-08-02) — work top-down.**
 
-| # | Prompt | Why this rank |
+> **Rank is NOT the prompt number.** The `Rank` column is the order to work in; the
+> prompt numbers are permanent section names and never change. "Prompt 2" always
+> means the C13 section below, never "the second-ranked item". Say "Prompt N" when
+> you mean a section and "rank N" when you mean priority — they have collided at
+> least once.
+
+| Rank | Prompt (section below) | Why this rank |
 |---|---|---|
-| ~~1~~ | ~~**Prompt 2** — C13 environment-lay prisms miss the clock path~~ | ✅ **DONE 2026-08-02** — the cause was the shield engage-morph straddling the creation reveal (not the raw-`Instantiate` lay). Residual **C13b** (pooled lay / spawn repaint) is not a clock fix — re-rank it with the rest. |
-| 2 | **Prompt 9** — batched entity debris remainder | Completes the proven, playtest-loved carrier: implosions on the batch path + the measured next bottleneck (`AOE.ResolveDamage` 0.43 ms/kill self, per-kill `PrismEventData` alloc). The benchmark rig is already built to measure it. |
-| 3 | **Prompt 1** — transparent-prism occlusion restore | Pre-existing broken system (predates this branch) + it gates the one deferred wiring verification (transparent color fades). |
-| 4 | **Prompt 3** — fauna/flora on the clock (ecology) | Per-frame CPU prism writes in every cell scene; wither/devour are ecology-locked visuals that must ride the law. |
-| 5 | **Prompt 4** — conveyor + cell-swap suction | The two biggest world-scale per-frame CPU flows left. |
-| 6 | **Prompt 6** — B4 shield morphs on the GPU | Retires the last sanctioned CPU ticker (`PrismOctahedronShieldManager`). |
-| 7 | **Prompt 5** — projectile prism paths | Real but lower-traffic (Sparrow volleys, fire trails). |
-| 8 | **Prompt 7** — C12/B1 cleanup sweep | Simplifications unblocked by the migration; no player-visible change. |
-| 9 | **Prompt 8** — HUD/validator upkeep | Ride-along with any of the above, not its own branch. |
+| ✅ | ~~**Prompt 2** — C13 environment-lay prisms miss the clock path~~ | **DONE 2026-08-02** — the cause was the shield engage-morph straddling the creation reveal (not the raw-`Instantiate` lay). Residual **C13b** (pooled lay / spawn repaint) is not a clock fix — re-rank it with the rest. **Code-complete, NOT play-tested** — the section's in-editor test still owes a run. |
+| 1 | **Prompt 9** — batched entity debris remainder | Completes the proven, playtest-loved carrier: implosions on the batch path + the measured next bottleneck (`AOE.ResolveDamage` 0.43 ms/kill self, per-kill `PrismEventData` alloc). The benchmark rig is already built to measure it. |
+| 2 | **Prompt 1** — transparent-prism occlusion restore | Pre-existing broken system (predates this branch) + it gates the one deferred wiring verification (transparent color fades). |
+| 3 | **Prompt 3** — fauna/flora on the clock (ecology) | Per-frame CPU prism writes in every cell scene; wither/devour are ecology-locked visuals that must ride the law. |
+| 4 | **Prompt 4** — conveyor + cell-swap suction | The two biggest world-scale per-frame CPU flows left. |
+| 5 | **Prompt 6** — B4 shield morphs on the GPU | Retires the last sanctioned CPU ticker (`PrismOctahedronShieldManager`). |
+| 6 | **Prompt 5** — projectile prism paths | Real but lower-traffic (Sparrow volleys, fire trails). |
+| 7 | **Prompt 7** — C12/B1 cleanup sweep | Simplifications unblocked by the migration; no player-visible change. |
+| 8 | **Prompt 8** — HUD/validator upkeep | Ride-along with any of the above, not its own branch. |
 
 Shared context every prompt inherits (do not restate in the session):
 `Docs/PRISM_ANIMATION.md` is the LOCKED law — one stamp → GPU clock → one
@@ -57,7 +63,7 @@ surgery, machine validation) are captured in the `/asset-surgery` skill — use
 > already wired into ExplodingBlockGraph; this test was deferred solely on this
 > system being down).
 
-## Prompt 2 — C13: environment-lay / SegmentSpawner prisms miss the clock path (live repro)
+## Prompt 2 — ✅ DONE — C13: environment-lay / SegmentSpawner prisms miss the clock path (live repro)
 
 > **✅ DONE 2026-08-02** (branch `claude/prismclock-render-entity-bug-fe3z2d`). The
 > prompt's three suspects were all wrong, and so was its preferred fix: the raw
@@ -79,6 +85,32 @@ surgery, machine validation) are captured in the `/asset-surgery` skill — use
 > spawn repaint). Worth doing, but it is not a clock fix and it needs its own
 > environment-prefab pool design (the existing pools are `maxSize`-bounded and
 > environment mass is never released).
+>
+> **Audit 2026-08-04 — one birth-rule hole found and closed.** A verification pass
+> over the shipped fix confirmed every claimed mechanism is on disk (entity
+> existence ⊥ visibility at `Prism.cs:408`, `EffectiveRenderMesh` keeping the morph
+> mesh out of Entities Graphics, `TryEnsureRenderEntityForStamp` at
+> `PrismScaleAnimator.cs:165`, `IsBirthTransition` at `PrismStateManager.cs:58`) and
+> that C13b is accurately scoped. It also found the birth rule applied
+> ASYMMETRICALLY on the Wanderway recycle — one of the two surfaces this prompt's
+> own test names. `Microscene.RearrangeIntoAsync` cleared the old kind BEFORE
+> `Prism.Initialize`, so `IsCreationComplete` was still true from the prism's
+> PREVIOUS life and the disengage ran as a live transition: a 0.6 s shatter overlay
+> with a per-frame morph-mesh rebuild plus one `ShieldDeactivate` SFX per recycled
+> shielded prism, layered over its fresh grow-in bloom. It did not break the grow
+> stamp (`Disengage` clears the exotic flag unconditionally), which is why C13a
+> still stood. Fixed by collapsing the split clear/apply into the purpose-built
+> `PrismKinds.Retheme` AFTER `Initialize` — which is what `Microscene`'s own class
+> doc already claimed it used. **Generalise the lesson:** the birth rule keys off
+> `IsCreationComplete`, so ANY kind/state wipe on a recycled prism must run after
+> `Initialize`, never before it.
```

</details>
