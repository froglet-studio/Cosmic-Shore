# The fauna-birth omni-crystal morph (Angler, Jacks)

**The crystal does not shatter. It becomes the creature's heart.**

Two hulls answer an omni crystal with a creature. The Angler's crystal "morphs into a random creature" that joins its
school (`AnglerCommandExecutor.OmniMorph`, `ANGLER.md` §1g). The Jacks' crystal produces an omnivorous worm in the
collector's domain (`VesselSpawnFaunaByCrystalEffectSO`, Garrett 2026-10-24). Until now the crystal also burst into the
shared husk spray, so the pickup read as a shatter with a creature appearing beside it. It now reads as one event: the
cage folds into the newborn's heart, and the body blooms around it as it always did.

This is a **per-vessel omni-crystal retirement** (`VesselImpactorDataContainerSO.OmniCrystalRetirement`), after the
Squirrel, Scarab, Dolphin, sphere-blast (`SPHERE_BLAST_CRYSTAL_MORPH.md`) and Shrike (`SHRIKE_CRYSTAL_MORPH.md`) morphs.
It shares everything but the target with them (`CrystalMorphRunner`); the feel is the fleet's (`Resources/CrystalMorphConfig`).

## 1. The target: the heart

Every creature carries its element in one embedded `Crystal`, its heart (`Fauna.ProvisionHeart`), sized to the one
elemental crystal size (`LifeFormCrystal.ApplyHeartSize`). So the morph is crystal into crystal at about the same size,
and the hull cast lands every vertex on the heart's own mesh (`CrystalMorphMeshBuilder.ConvexHullTarget`).

| hull | where the creature is born | heart |
|---|---|---|
| Angler | where the crystal was (clamped into the cell's fauna band) | the creature's own |
| Jacks | 30 u behind the hull (`spawnBehind`) | the worm colony is heartless at its root (`Docs/ECOSYSTEM.md` §23.3); each segment carries one, and the crystal takes the one nearest the collect (`FaunaHeartMorphGeometry.NearestHeart`) |

The heart is up to 30 u away and only the crystal's size, so the cast keeps each vertex's direction from the crystal's
own middle (`CrystalMorphRecast`): seen from the heart, the whole cage would lie in one direction and pile onto the face
that looks back at it. `FaunaBirthCrystalMorphGeometryTests` measures it: around the crystal, the landed points cover all
8 faces; the plain cast (the negative control) covers at most 4.

## 2. What makes the hand-off seamless

1. It draws the crystal's own renderers (`CrystalMorphRunner.Adopt`), so frame 0 is the crystal.
2. **It follows the heart.** A newborn swims from its first frame, so after the stamp the morph rides the heart's pose
   every frame. It is never parented under the creature: a creature that dies or is pooled cannot take the morph, or its
   hold, with it. A heart that disappears releases the hold and the plates finish where they are.
3. **Only the heart's photons wait** (`Renderer.forceRenderingOff` on the heart's renderers, and only on those that were
   drawing). The creature is born, registered, leashed and simulated from the pickup frame as before.
4. The colour pair converges on the heart's own `_DarkColor`/`_BrightColor` (property block first, then material).

**Not exact:** the body blooms in around the heart while the plates are still folding, exactly as a creature always
blooms in. Only the heart is held.

## 3. How it runs

```
Angler / Jacks touches an omni crystal
  └─ VesselImpactor.ExecuteOmniCrystalImpact                   [every peer]
        ├─ OmniCrystalRetirement (runs FIRST): FaunaBirthCrystalMorphByCrystalEffectSO
        │     └─ FaunaBirthCrystalMorph.Begin: adopt the crystal · listen to CrystalFaunaBirth.Born / Declined
        └─ VesselCrystalEffects
              ├─ Angler: AnglerTameByCrystalEffect → OmniMorph → SpawnFaunaWithDomain → CrystalFaunaBirth.Announce
              │          (no creature: CrystalFaunaBirth.Decline)
              └─ Jacks:  VesselSpawnFaunaByCrystalEffect → SpawnFaunaWithDomain → CrystalFaunaBirth.Announce
                         (replicated species on a non-simulating peer: CrystalFaunaBirth.Decline)
                    └─ FaunaBirthCrystalMorph.OnBorn: same vessel · born within ringCaptureRadius (60 u)
                          nearest drawn heart → hull cast around the crystal → hold the heart's photons · ONE stamp
  t = morphFraction · duration : the heart draws itself again
  t = duration                 : the crystal's body has dissolved off it; the morph is destroyed
```

`CrystalFaunaBirth` lives on the vessel side on purpose: the creature is born through the ecology's one spawn seam
exactly as before, and nothing in the ecology (LOCKED, `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`) knows the morph exists.

**The morph never spawns the creature.** A declined birth (the Angler's school is full, or a replicated species is born
on the simulating peer) fades the crystal quietly. A missing birth fades it with a warning naming the sibling effect.

## 4. Networking

A replicated species is born only where the ecology is simulated, so on other peers no creature is announced and the
crystal fades instead of morphing (quietly: it is declined). An unreplicated species is born on each peer and morphs on each.
Landing on the replicated puppet on the other peers is a follow-up.

## 5. Files

| file | role |
|---|---|
| `R_VesselActions/FaunaBirthCrystalMorph.cs` | the heart target: listen, pick the heart, cast, hold, follow, reveal |
| `R_VesselActions/CrystalFaunaBirth.cs` | `Born` / `Declined`, isolated, raised by the two vessel effects |
| `…/Vessel Crystal Effects/FaunaBirthCrystalMorphByCrystalEffectSO.cs` + `_SO_Assets/…/FaunaBirthCrystalMorphByCrystal.asset` | the retirement, in the Angler container and (written by `author_jacks_assets.py`) the Jacks container |
| `AnglerCommandExecutor.OmniMorph`, `AnglerTameByCrystalEffectSO`, `VesselSpawnFaunaByCrystalEffectSO` | one announce or decline line each |
| `Utility/FaunaHeartMorphGeometry.cs` | which heart, pure |
| `Tests/Editor/FaunaBirthCrystalMorphGeometryTests.cs` | five gates, run headlessly by `Tools/Build/crystal_morph_harness/` |

## 6. Verification

**Not opened in Unity.** Proven offline: the headless morph harness (all suites) and `unity_refcompile` (player and
editor, 0 errors in the changed files). Hand tests: `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Trace with
**FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph**.
