# QA Dev Tasks — failures converted to handoff work

The handoff surface for engineering. **Owner: the `/qa-backlog` skill — do not hand-edit.**

Each failed QA item becomes one task below, keyed by its QA item ID. The definition of
done is always literally "the QA item passes" — when the fix merges, the QA item is still
marked 🔴 on the backlog, so it returns to the top of the next list automatically.

The `<!-- devtask:QA-... -->` markers let the apply engine refresh a task in place instead
of duplicating it.

<!-- devtask:QA-EDITMODE-TESTS -->
### QA-EDITMODE-TESTS — run the test suites that were written but never executed
- **Failed on:** claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-07, andrew)
- **Symptom:** None of the required tests were even there for me to test.
- **Definition of done:** QA item `QA-EDITMODE-TESTS` passes.
<!-- /devtask:QA-EDITMODE-TESTS -->

<!-- devtask:QA-SPARROW-PROJECTILE-POOL -->
### QA-SPARROW-PROJECTILE-POOL — async-refilled pooled projectiles are injected
- **Failed on:** bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-10, andrew)
- **Symptom:** Launch SFX play and there are no NREs, but normal shots sometimes pass straight through prisms, and the Console throws "Projectile already released! Should not call twice!".
- **Definition of done:** QA item `QA-SPARROW-PROJECTILE-POOL` passes.
<!-- /devtask:QA-SPARROW-PROJECTILE-POOL -->

<!-- devtask:QA-FLORA-LEAFSIZE -->
### QA-FLORA-LEAFSIZE — garden flora still grow leaves at the authored size
- **Failed on:** bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-10, andrew)
- **Symptom:** No reference for what the leaf size should be, and nothing that looked like a leaf to analyze in the first place.
- **Definition of done:** QA item `QA-FLORA-LEAFSIZE` passes.
<!-- /devtask:QA-FLORA-LEAFSIZE -->

<!-- devtask:QA-AUDIT-TOOLS -->
### QA-AUDIT-TOOLS — run every FrogletTools auditor and record its verdict
- **Failed on:** bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-11, andrew)
- **Symptom:** Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK).
- **Definition of done:** QA item `QA-AUDIT-TOOLS` passes.
<!-- /devtask:QA-AUDIT-TOOLS -->

<!-- devtask:QA-VESSEL-SPARROW-ROLL -->
### QA-VESSEL-SPARROW-ROLL — Sparrow rolls on prism hit
- **Failed on:** bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-17, andrew)
- **Symptom:** Hitting a prism as the Sparrow shifts my movement (course is redirected) rather than rolling the vessel in place — matches the item's "still being deflected off-course" FAIL criterion.
- **Definition of done:** QA item `QA-VESSEL-SPARROW-ROLL` passes.
<!-- /devtask:QA-VESSEL-SPARROW-ROLL -->

<!-- devtask:QA-CRYSTAL-EFFECTS -->
### QA-CRYSTAL-EFFECTS — elemental crystal capture effect + omni-crystal bloom
- **Failed on:** bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-18, andrew)
- **Symptom:** In HexRace: the crystal model and its breaking/collection effect look normal, but when a new crystal spawns in it POPS into existence instead of blooming in — the omni/crystal bloom-in (PR #724) is not playing on spawn.
- **Definition of done:** QA item `QA-CRYSTAL-EFFECTS` passes.
<!-- /devtask:QA-CRYSTAL-EFFECTS -->
