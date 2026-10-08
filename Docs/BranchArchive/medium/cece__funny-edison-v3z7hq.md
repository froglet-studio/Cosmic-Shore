# Branch archive: `cece/funny-edison-v3z7hq`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-08-26 by Claude
- **Unmerged commits:** 4
- **Forked from:** `def29f5e1` (2026-08-25, Merge remote-tracking branch 'origin/bleeding-edge' into seven-sixteen)
- **Tip:** `458c36cbc`
- **Files touched (32):**
  - `Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl`
  - `Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl.meta`
  - `Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph`
  - `Assets/_Models/OmniCrystalExport1_8-21-25.fbx.meta`
  - `Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/SquirrelImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset`
  - `Assets/_SO_Assets/Effects/Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset.meta`
  - `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`
  - `Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Containers/VesselImpactorDataContainerSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Abstract Effect Types/VesselOmniCrystalRetirementSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Abstract Effect Types/VesselOmniCrystalRetirementSO.cs.meta`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/SquirrelCrystalMorphByCrystalEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Crystal Effects/SquirrelCrystalMorphByCrystalEffectSO.cs.meta`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs.meta`
  - `Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs`
  - `Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs.meta`
  - `Assets/_Scripts/Utility/CSDebug.cs`
  - `Assets/_Scripts/Utility/CrystalMorphMeshBuilder.cs`
  - `Assets/_Scripts/Utility/CrystalMorphMeshBuilder.cs.meta`
  - `CLAUDE.md`
  - `Tools/Build/measure_omni_crystal_morph.py`
  - `Tools/Build/omni_crystal_morph.png`
  - `Tools/Shaders/verify_crystal_morph.py`
  - `Tools/Shaders/wire_crystal_morph.py`

### `bb1e7a51e` — feat(squirrel): the omni crystal MORPHS into its boost ring instead of shattering

_Claude, 2026-08-26 15:23:35 +0000_

```text
A Squirrel that flies through an omni crystal lays a ring of eight shielded
prisms just ahead of its nose. Until now the crystal ALSO burst into the shared
husk spray, so the pickup read as two unrelated events. It now reads as one: the
cage opens, its plates fly outward, and they land as the eight octahedra of the
ring it just made.

THE CENSUS IS EXACTLY 1:1, and it is a measurement, not a design choice. The
crystal's cage is 122 disjoint solids whose NON-QUAD faces are 20x2 triangles +
12x2 pentagons = 64, and eight octahedron shields show 8x8 = 64. Every panel
becomes exactly one face, with nothing invented and nothing spare. The 660 quads
(struts and rims) collapse into the octahedron their own solid was assigned to
and are absorbed, stamped to go FIRST so nothing is left hanging when the panels
land.

The platform slot: VesselImpactorDataContainerSO.OmniCrystalRetirement, one per
vessel. OmniCrystalImpactor asks the collecting vessel whether it retires the
crystal itself and skips Crystal.Explode when it does; the animation runs from
VesselImpactor.ExecuteOmniCrystalImpact, which the owner already routes through
NetworkVesselImpactor and back out to every peer — so a bespoke retirement needs
NO new networking. A vessel that authors nothing keeps the husk, so the fleet
migrates one hull at a time.

Three things make the hand-off seamless, and they generalise to the next vessel:
 - it draws the CRYSTAL'S OWN renderers (mesh, shared materials, property block),
   so frame 0 IS the crystal — tint, shell bands and all;
 - it ends on the REAL prisms, read from what BoostRingBuilder actually laid
   (new RingLaid event), never re-derived from the same authored numbers;
 - the prisms are laid AT ONCE and only their PHOTONS wait (new
   Prism.SetVisualStandIn) — the ring is skimmable from frame 0 while the morph
   is still in flight, which is the clock law's gameplay-final-at-start division
   applied to a hand-off.

Two geometry traps are recorded in the code and the doc: a face is found
STRUCTURALLY, never by coplanarity (60 of this cage's quads are non-planar, and a
plane test reports 160 triangle panels where there are 40); and a panel must
BECOME its face, not sit inside it (a raw perimeter map put only 83 of 336 corners
on a target corner, leaving every octahedron as shrunken plates with gaps).

Verified headlessly, each gate negative-controlled:
 - Tools/Build/measure_omni_crystal_morph.py — the census against the shipped FBX
 - Tools/Shaders/verify_crystal_morph.py — compiles and RUNS the shipped HLSL
 - Tools/Shaders/wire_crystal_morph.py --check — the ShepardGraph splice
 - CrystalMorphMeshBuilderTests — 7 tests, run against the shipped builder

Verification status: NOT opened in Unity. Nothing here has been seen on screen;
the timings are a starting point for a playtest, not a tuned result.
```

```text
 Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl                   |  64 ++++
 Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl.meta              |   7 +
 Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph            | 568 +++++++++++++++++++++++++++++---
 .../VesselContainers/SquirrelImpactorDataContainer.asset              |   2 +
 .../Effects/Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset     |  21 ++
 .../Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset.meta        |   8 +
 Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs   |  40 ++-
 .../ImpactEffects/Containers/VesselImpactorDataContainerSO.cs         |  16 +
 .../EffectsSO/Abstract Effect Types/VesselOmniCrystalRetirementSO.cs  |  25 ++
 .../Abstract Effect Types/VesselOmniCrystalRetirementSO.cs.meta       |  11 +
 .../Vessel Crystal Effects/SquirrelCrystalMorphByCrystalEffectSO.cs   | 110 +++++++
 .../SquirrelCrystalMorphByCrystalEffectSO.cs.meta                     |  11 +
 .../_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs  |  21 ++
 .../Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs         |  10 +-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs  |  18 +
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |  44 ++-
 .../Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md       | 216 ++++++++++++
 .../Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md.meta  |   7 +
 .../Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs         | 348 +++++++++++++++++++
 .../Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs.meta    |  11 +
 Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs          | 343 +++++++++++++++++++
 Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs.meta     |  11 +
 Assets/_Scripts/Utility/CrystalMorphMeshBuilder.cs                    | 541 ++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/CrystalMorphMeshBuilder.cs.meta               |  11 +
 CLAUDE.md                                                             |   3 +
 Tools/Build/measure_omni_crystal_morph.py                             | 351 ++++++++++++++++++++
 Tools/Build/omni_crystal_morph.png                                    | Bin 0 -> 294882 bytes
 Tools/Shaders/verify_crystal_morph.py                                 | 156 +++++++++
 Tools/Shaders/wire_crystal_morph.py                                   | 476 ++++++++++++++++++++++++++
 29 files changed, 3393 insertions(+), 57 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2851 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl b/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl
new file mode 100644
index 000000000..099a7a83d
--- /dev/null
+++ b/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl
@@ -0,0 +1,64 @@
+// CrystalMorph.hlsl — a crystal's body carried onto another shape, on the clock.
+//
+// The GPU half of the Squirrel's omni-crystal morph
+// (_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md). The CPU bakes a
+// per-vertex TARGET into TEXCOORD2 and stamps three numbers once; the vertex stage runs
+// the whole animation from there, so it costs nothing per frame and nothing per vertex on
+// the CPU — the same contract Docs/PRISM_ANIMATION.md §4 puts on every prism visual.
+//
+// Shader Graph usage: Custom Function node, Source = this file, function name WITHOUT the
+// _float suffix. Wire the UNEXPOSED _PrismClock property (published once per frame by
+// PrismClock's publisher, from the SAME value the stamp uses) into Clock — never a Time
+// node, which is a different clock domain and renders every stamp pre-finished.
+//
+// Spliced onto the very END of the vertex-position chain, so `Position` arrives with every
+// other vertex effect already applied (on ShepardGraph, the shell's outward displacement).
+// That placement is load-bearing in BOTH directions: at t = 0 the output is that position
+// verbatim, so the morph starts EXACTLY as the crystal was drawing — displacement, shell
+// band and all — and at t = 1 it is the bare target, so the displacement is gone and the
+// shape lands on the geometry it was fitted to instead of hovering a shell above it.
+
+#ifndef CRYSTAL_MORPH_INCLUDED
+#define CRYSTAL_MORPH_INCLUDED
+
+// -----------------------------------------------------------------------------
+// Position -> Target, eased, with a per-face stagger.
+//
+//   Position  object-space vertex position, post every other vertex effect
+//   Target    xyz = this vertex's destination (object space), w = its face's PHASE [0,1]
+//   Clock     _PrismClock
+//   Morph     x = stamped start time, y = duration (seconds), z = stagger [0,1)
+//
+// PHASE is what lets one mesh carry two different jobs at once: the crystal's strut faces
+// are stamped phase 0 and are absorbed FIRST, while the panels that become the octahedra's
+// faces are stamped late and land LAST — so the leftovers are already gone by the time the
+// shape they were absorbed into is finished. Stagger 0 collapses that to one synchronised
+// move.
+//
+// Duration <= 0 means UNSTAMPED and returns Position untouched. Every crystal material in
+// the project carries this node with (0, 0, 0) and is therefore bit-identical to before it
+// existed; only an object the morph has stamped moves.
+// -----------------------------------------------------------------------------
+void CrystalMorph_float(float3 Position, float4 Target, float Clock, float3 Morph,
+    out float3 Out)
+{
+    float duration = Morph.y;
+    if (duration <= 0.0)
+    {
+        Out = Position;
+        return;
+    }
+
+    float t = saturate((Clock - Morph.x) / duration);
+
+    // Each face gets the same LENGTH of travel, offset by its phase, so a staggered face
+    // is not also a faster one. span is what is left of the window after the stagger.
+    float stagger = saturate(Morph.z);
+    float span = max(1e-4, 1.0 - stagger);
+    float e = saturate((t - saturate(Target.w) * stagger) / span);
+
+    e = e * e * (3.0 - 2.0 * e);   // smoothstep: zero end tangents, so it settles rather than arrives
+    Out = lerp(Position, Target.xyz, e);
+}
+
+#endif // CRYSTAL_MORPH_INCLUDED
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs b/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs
index 20b0cb2f4..3c5b1e5fe 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs
@@ -23,6 +23,23 @@ namespace CosmicShore.Gameplay
         }
     }
 
+    /// <summary>One ring that has just been laid: where it went, what it is, and its prisms.</summary>
+    public readonly struct BoostRingLay
+    {
+        public readonly Pose Pose;
+        public readonly BoostRingSpec Spec;
+        public readonly Domains Domain;
+        public readonly string OwnerPrefix;
+        /// <summary>The prisms, in ring order. Live mass — read it, never retire it.</summary>
+        public readonly IReadOnlyList<Prism> Prisms;
+
+        public BoostRingLay(Pose pose, in BoostRingSpec spec, Domains domain, string ownerPrefix,
+            IReadOnlyList<Prism> prisms)
+        {
+            Pose = pose; Spec = spec; Domain = domain; OwnerPrefix = ownerPrefix; Prisms = prisms;
+        }
+    }
+
     /// <summary>
     /// THE canonical "ring of prisms the skimmer WILL collide with" builder - shared by every
     /// feature that throws a fly-through boost ring around the flight path: the Squirrel
@@ -47,6 +64,19 @@ namespace CosmicShore.Gameplay
     /// </summary>
     public static class BoostRingBuilder
     {
+        /// <summary>
+        /// Raised the instant a ring finishes laying, on the machine that laid it.
+        ///
+        /// It exists so a visual that has to LAND on a ring can read the ring the builder
+        /// actually made instead of re-deriving it from the same authored numbers. Those two
+        /// can drift; the ring cannot drift from itself. The Squirrel's omni-crystal morph is
+        /// the first listener — it ends on the real octahedra of the real prisms, so retuning
+        /// `SpawnableRings` moves the animation with it and no second authority exists.
+        ///
+        /// Listeners must be one-shot and must not retire the mass: these prisms are conserved.
+        /// </summary>
+        public static event System.Action<BoostRingLay> RingLaid;
+
         /// <summary>
         /// Lays one ring of <see cref="BoostRingSpec.Segments"/> boost prisms around
         /// <paramref name="pose"/>'s forward axis. Pass a <paramref name="trail"/> to group them,
@@ -62,6 +92,10 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            // Collected unconditionally so RingLaid can hand listeners the ring it actually
+            // made; `collected` (the caller's own teardown list) still gets every prism too.
+            var laid = new List<Prism>(spec.Segments);
+
             for (int i = 0; i < spec.Segments; i++)
             {
                 float angle = i * (2f * Mathf.PI / spec.Segments);
@@ -70,9 +104,13 @@ namespace CosmicShore.Gameplay
                 // Long side runs along the ring axis; block "up" points outward radially.
                 Quaternion rotation = pose.rotation * Quaternion.LookRotation(Vector3.forward, radial);
 
-                LayOne(channel, position, rotation, spec.PrismScale, spec.Kind,
+                var prism = LayOne(channel, position, rotation, spec.PrismScale, spec.Kind,
                     domain, playerName, $"{ownerPrefix}::{i}", trail, collected);
+                if (prism) laid.Add(prism);
             }
+
+            if (laid.Count > 0)
+                RingLaid?.Invoke(new BoostRingLay(pose, spec, domain, ownerPrefix, laid));
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Containers/VesselImpactorDataContainerSO.cs b/Assets/_Scripts/Controller/ImpactEffects/Containers/VesselImpactorDataContainerSO.cs
index df11f1556..74bd275ef 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Containers/VesselImpactorDataContainerSO.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Containers/VesselImpactorDataContainerSO.cs
@@ -18,6 +18,18 @@ namespace CosmicShore.Gameplay
 
```

</details>

### `ddb65801b` — chore(squirrel): slow the crystal morph 20x for inspection (0.45s -> 9s)

_Claude, 2026-08-26 18:34:46 +0000_

```text
One field on SquirrelOmniCrystalMorph.asset. Every other timing is a FRACTION
of `duration` — stagger, handoff, panel phases — so this alone slows the whole
animation and nothing else needs touching. `ringGraceSeconds` deliberately does
NOT scale: it is how long to wait for the ring, not part of the animation.

Timeline at 9s:
  fillers absorb     0.00s -> 5.85s
  first panel        1.73s -> 7.58s
  last panel         3.15s -> 9.00s
  ring revealed      6.48s   (cross-dissolve 6.48s -> 9.00s)
  morph destroyed    9.00s

Two things to expect while it is slow — the design at 1/20 speed, not faults:
the ring is solid and skimmable but INVISIBLE for 6.5s, so flying through it
gives boost off prisms you cannot see; and the last panels land 2.5s after the
real ring appeared, so you watch the tail of the flight cross-dissolve over a
ring already in place (0.13s at the shipping duration).

`duration` carries no [Range], so it can be dialled straight in the inspector.
Intended shipping value is 0.45 — the platform's crystal-capture beat.
```

```text
 Assets/_SO_Assets/Effects/Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset |  2 +-
 Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md     | 12 +++++++++++-
 2 files changed, 12 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md
index f18551b70..4943d6dc9 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md
@@ -169,9 +169,19 @@ injected, the gate was confirmed to fire, the file restored) — a gate that has
 indistinguishable from one that cannot fail.
 
 **Not verified: how it looks.** Nothing here has been seen in the editor. Timings
-(`duration 0.45`, `stagger 0.35`, `handoffFraction 0.72`, panel phases 0.55–1.0) are authored on
+(`stagger 0.35`, `handoffFraction 0.72`, panel phases 0.55–1.0) are authored on
 `SquirrelOmniCrystalMorph.asset` and are a starting point for a playtest, not a tuned result.
 
+> ⚠ **`duration` is currently `9` — a 20× INSPECTION value, not a shipping one.** The intended
+> figure is `0.45`, the platform's crystal-capture beat (`CrystalCaptureConfigSO`), so a pickup
+> reads the same length whichever vessel took it. Every other timing is a FRACTION of `duration`,
+> so this one field slows the whole animation and nothing else needs touching. Two things to
+> expect while it is slow, both of which are the design running at 1/20 speed rather than faults:
+> the ring is **solid and skimmable but invisible** for `handoffFraction × duration` = 6.5 s, so
+> flying through it gives boost off prisms you cannot see; and the last panels land at 9.0 s while
+> the real ring appeared at 6.5 s, so for 2.5 s you watch the tail of the flight cross-dissolve
+> over a ring that is already there (0.13 s at the shipping duration).
+
 ---
 
 ## 7. Cost
```

</details>

### `538284385` — fix(squirrel): the crystal morph never ran — an unreadable source mesh

_Claude, 2026-08-26 19:56:29 +0000_

```text
The animation shipped dead. `Mesh.vertices` THROWS on a mesh imported without
Read/Write, and `OmniCrystalExport1_8-21-25.fbx` had `isReadable: 0`. The throw
happened inside the `BoostRingBuilder.RingLaid` listener — after the ring's
prisms were already laid — so the only symptom was the one reported: eight
shielded prisms appearing instantly and the crystal fading out on its own
give-up path. Nothing named the cause; every distinct failure of this animation
produces that same picture.

Root cause:
  - `OmniCrystalExport1_8-21-25.fbx.meta`: `isReadable: 0` -> `1`. The morph has
    to read the cage's vertices on the CPU to bake each face's target.
    (`ChargeCrystalExport1_7-11-25.fbx` already carried this for the same reason.)

So it can never fail silently again:
  - `CrystalMorphMeshBuilder.TryBuild` refuses an unreadable mesh BY NAME with a
    diagnosis that names the importer setting, instead of throwing from inside
    a `.vertices` read.
  - `BoostRingBuilder` isolates `RingLaid?.Invoke`. A listener is a VISUAL and
    must never be able to damage conserved mass: without this a throwing
    listener unwinds past the rest of the lay and is reported three frames from
    its cause.
  - Every rejection in `SquirrelCrystalMorph` is now a warning that names what
    mismatched (face census, missing crystal id, no ring inside the grace).
  - New `CSLogChannel.CrystalMorph` for the step trace (off by default,
    FrogletTools > Toolbox > Logging).

And the two design points the same report carried:
  - POSITION, COLOUR AND ORIENTATION. Each face now also carries the crystal's
    palette onto the shielded prism's: `_DarkColor`/`_BrightColor` are read off
    the material the laid prisms actually bound and blended into the crystal's
    `_DullCrystalColor`/`_BrightCrystalColor` across the flight
    (`colourBlendFraction`). No second authority for the target colour.
  - SWAP ONLY BETWEEN EQUIVALENT STATES. The hand-off moved to t=1
    (`handoffFraction` 0.72 -> 1). Below 1 the swap happens while panels are
    still arriving, which is the seam this design exists to remove; the dial
    survives only for seeing where the hand-off lands.
  - While waiting for the ring the crystal now HOLDS STATIC rather than
    drifting, and a late ring still wins (grace 1.5s).

Verification: 8/8 edit-mode tests (new: an unreadable mesh is refused by name),
geometry driver against the real cage (64/64 panels cover their face exactly,
1394.3 vs 1394.3), HLSL verifier, `wire_crystal_morph.py --check`,
`check_conditional_compilation.py`. All offline — THE ANIMATION HAS STILL NOT
BEEN SEEN RUNNING. Duration stays at the 20x inspection value of 9s.
```

```text
 Assets/_Models/OmniCrystalExport1_8-21-25.fbx.meta                    |   2 +-
 .../Effects/Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset     |   5 +-
 Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs   |  19 ++-
 .../Vessel Crystal Effects/SquirrelCrystalMorphByCrystalEffectSO.cs   |  36 ++++-
 .../Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md       |  51 ++++++-
 .../Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs         | 242 ++++++++++++++++++++++++++------
 Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs          |  19 +++
 Assets/_Scripts/Utility/CSDebug.cs                                    |  13 ++
 Assets/_Scripts/Utility/CrystalMorphMeshBuilder.cs                    |  16 ++-
 9 files changed, 341 insertions(+), 62 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 584 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs b/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs
index 3c5b1e5fe..f3a967ac2 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/BoostRingBuilder.cs
@@ -109,8 +109,25 @@ namespace CosmicShore.Gameplay
                 if (prism) laid.Add(prism);
             }
 
+            // ISOLATED: a listener here is a VISUAL, and a visual must never be able to damage
+            // conserved mass. Without this a throwing listener unwinds out of the lay — past the
+            // rest of this method and into whatever spawner called it — leaving the ring
+            // half-built and the exception reported three frames from its cause. (That is
+            // exactly how the Squirrel's morph first failed: an unreadable source mesh threw
+            // inside the listener, after the prisms were already laid, and the only visible
+            // symptom was the crystal fading out.)
             if (laid.Count > 0)
-                RingLaid?.Invoke(new BoostRingLay(pose, spec, domain, ownerPrefix, laid));
+            {
+                try
+                {
+                    RingLaid?.Invoke(new BoostRingLay(pose, spec, domain, ownerPrefix, laid));
+                }
+                catch (System.Exception e)
+                {
+                    CSDebug.LogError($"[BoostRingBuilder] a RingLaid listener threw; the ring is " +
+                                     $"laid and unaffected. {e}");
+                }
+            }
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md
index 4943d6dc9..6719a5a1e 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md
@@ -63,6 +63,16 @@ from frame 0 while the morph is still in flight* — and `Prism.SetVisualStandIn
 their rendering. That is the clock-material law's own division (`Docs/PRISM_ANIMATION.md` §4)
 applied to a hand-off: gameplay final at the start, only photons animate.
 
+**4. The swap happens ONLY between equivalent states.** The prisms are revealed at `t = 1` — not
+before — when the morph's geometry *is* their octahedra (same corners, same orientation) and its
+colour has been carried onto their shielded palette. There is no cross-fade between two
+different-looking things, because there is no moment at which they look different. The colour
+target is read off the material the laid prisms actually bound (`_DarkColor`/`_BrightColor`, the
+prism's base face and fresnel rim), lerped from what each crystal shell is drawing
+(`_DullCrystalColor`/`_BrightCrystalColor` — the same two roles). `handoffFraction` can reveal
+them earlier, but only as a debugging aid: below 1 it swaps while panels are still arriving,
+which is the seam this design exists to remove.
+
 ---
 
 ## 3. How it runs
@@ -177,10 +187,9 @@ indistinguishable from one that cannot fail.
 > reads the same length whichever vessel took it. Every other timing is a FRACTION of `duration`,
 > so this one field slows the whole animation and nothing else needs touching. Two things to
 > expect while it is slow, both of which are the design running at 1/20 speed rather than faults:
-> the ring is **solid and skimmable but invisible** for `handoffFraction × duration` = 6.5 s, so
-> flying through it gives boost off prisms you cannot see; and the last panels land at 9.0 s while
-> the real ring appeared at 6.5 s, so for 2.5 s you watch the tail of the flight cross-dissolve
-> over a ring that is already there (0.13 s at the shipping duration).
+> the ring is **solid and skimmable but invisible for the whole 9 s**, so flying through it gives
+> boost off prisms you cannot see; and the morph holds the crystal static for up to 1.5 s first
+> while it waits for the ring to be laid.
 
 ---
 
@@ -210,7 +219,39 @@ per-vessel flourishes belong in `vesselCrystalEffects` alongside it.
 
 ---
 
-## 9. Known limitations
+## 9. The failure that shipped first, and what now catches it
+
+**`isReadable: 0` on the omni crystal's FBX.** `CrystalMorphMeshBuilder` reads the cage's
+vertices on the CPU to bake each face's target, and an IMPORTED mesh without Read/Write does not
+return empty vertices — it **throws**. The throw escaped through `BoostRingBuilder.RingLaid`
+*after* the eight prisms were already laid, so the visible result was: the ring appears normally,
+and the crystal fades out. Which is indistinguishable from "the retirement never ran", "the ring
+never arrived", and "the ring was rejected".
+
+Three things came out of it, and the third is the general one:
+
+1. **The fix** — `isReadable: 1` on `OmniCrystalExport1_8-21-25.fbx`, matching
+   `ChargeCrystalExport1_7-11-25.fbx`, which this project already reads at runtime for the same
+   class of reason. Cost is a CPU copy of a 2,880-vertex mesh.
+2. **The guard** — `TryBuild` checks `Mesh.isReadable` before touching a vertex and refuses with
+   a diagnosis naming the importer setting. `AnUnreadableSourceMeshIsRefusedByName` pins it.
+3. **A listener must never be able to damage conserved mass.** `RingLaid` is now invoked inside a
+   try/catch: a *visual* listener that throws must not unwind out of the lay it is watching. The
+   exception is reported where it happened rather than three frames away.
+
+And because that animation's one hard dependency (a ring laid by a SIBLING effect) is invisible
+to it, **every rejection is now a warning that names what mismatched** — wrong domain, wrong
+prism kind, a prism whose shield had not engaged, an unreadable mesh, no ring at all — and the
+whole path traces under `CSLogChannel.CrystalMorph` (FrogletTools ▸ Toolbox ▸ Logging). While it
+waits for the ring the crystal now holds **static**, not fading, and a ring that arrives during
+the give-up fade still takes over.
+
+*A silent fallback is worse than no fallback: it converts every distinct cause into the same
+symptom.*
+
+---
+
+## 10. Known limitations
 
 - **`FadeIn` cannot reach a ShepardGraph crystal.** `FadeIn` drives `_opacity` (lowercase) while
   ShepardGraph's property is `_Opacity`, so the bloom-in every crystal is supposed to play is a
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs
index b1e01661b..50e601ac4 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs
@@ -13,27 +13,43 @@ namespace CosmicShore.Gameplay
     /// The crystal's cage has 40 triangular and 24 pentagonal faces — 64 — and eight octahedron
     /// shields show 8 × 8 = 64. So every panel of the crystal becomes exactly one face of a
     /// prism, 1:1, and the 660 leftover quads (the cage's struts and the panels' rims) collapse
-    /// into whichever octahedron their own solid was assigned to and are absorbed. Nothing is
-    /// invented and nothing is spare.
+    /// into whichever octahedron their own solid was assigned to and are absorbed.
     ///
-    /// ── The three things that make it seamless ────────────────────────────────────────────
+    /// ── The transition is between EQUIVALENT STATES ───────────────────────────────────────
+    /// The real prisms are revealed only at t = 1, when the morph's geometry IS their octahedra
+    /// — same corners, same orientation — and its colour has been carried onto their shielded
+    /// palette. Until that instant the prisms are held invisible; after it the morph is gone.
+    /// There is no cross-fade between two different-looking things, because there is no moment
+    /// at which they look different. (`HandoffFraction` can reveal them earlier for debugging;
+    /// at anything below 1 the swap happens while the panels are still arriving, which is
+    /// exactly the seam this design exists to remove.)
+    ///
+    /// ── What makes each half seamless ─────────────────────────────────────────────────────
     /// 1. **It draws the crystal's own renderers.** Mesh, materials and MaterialPropertyBlock
     ///    are copied off the live crystal, so frame 0 of the morph is the crystal, including
     ///    the Shepard shells' band animation. A rebuilt look-alike would pop.
     /// 2. **It ends ON the real prisms.** The targets are read from the prisms the ring builder
-    ///    actually laid — their own shield semi-axes, their own final pose — so the last frame
-    ///    of the morph and the first frame of the ring are the same geometry. There is no
-    ///    second authority to drift: retune `SpawnableRings` and the animation follows.
+    ///    actually laid — their own shield semi-axes, their own final pose — and the colour it
+    ///    converges to is read off the very material those prisms bound. There is no second
+    ///    authority to drift: retune `SpawnableRings` and the animation follows.
     /// 3. **The prisms are laid at once and only their PHOTONS wait.** Colliders, mass, shield
     ///    state and spatial index all go final the instant the ring is laid — the ring is
     ///    skimmable from frame 0 while the morph is still in flight — and
     ///    <see cref="Prism.SetVisualStandIn"/> holds nothing but their rendering. That is the
     ///    clock-material law's own division (Docs/PRISM_ANIMATION.md §4) applied to a hand-off.
     ///
-    /// Cost: one Mesh build per collect (~4.3k vertices; the face partition itself is cached
-    /// per source mesh), and ONE stamp. The animation runs entirely in the vertex stage off
-    /// `_PrismClock` — no per-frame, per-vertex or per-prism CPU work. The only per-frame write
-    /// is the tail cross-dissolve's `_Opacity`, one uniform per shell renderer.
```

</details>

### `458c36cbc` — fix(squirrel): the morph starts where the crystal WAS, and carries its normals

_Claude, 2026-08-26 22:22:27 +0000_

```text
Two defects, one in each half of the animation.

## It started at the crystal's NEXT home

A pickup is serviced by TWO trigger callbacks in the same physics step — the
crystal's `OmniCrystalImpactor` and the vessel's `VesselImpactor` — and Unity
does not define which fires first. The crystal's ends in `Crystal.Respawn()`,
which on a host writes the slot list and re-poses the crystal SYNCHRONOUSLY. So
in one of the two orders `CrystalImpactData.FromCrystal` read the crystal's next
home; `MoveToNewPos` also resets rotation to identity, so the orientation went
with it. Across the wire it is not even order-dependent: collection and respawn
are independent RPC chains, so a remote peer nearly always sees the moved
crystal.

  - `Crystal.CollectPose` — the pose the crystal had BEFORE it was moved this
    frame, else its live pose. Exact by construction (the only way the pose can
    be new at read time is that `MoveToNewPos` ran this very frame, which is the
    move being serviced), no polling, no assumption about callback order.
  - `CrystalImpactData` carries the whole pose — position, rotation, world
    scale — and `SquirrelCrystalMorph.Begin` takes it as a parameter and never
    touches `crystal.transform`. The crystal's APPEARANCE is still read live;
    its POSE is not.

The general one: two PhysX trigger callbacks cannot be ordered from inside
either of them, and any arrangement that appeared to work would hold only until
the next collider was added. Report the pose that WAS.

## The seam at the hand-off

Four differences between the morph's last frame and the prisms' first. Two were
real:

  - NORMALS. The morph carried only position, so it arrived with the cage's
    normals sitting on the octahedron's faces — and both shaders derive base
    colour from (1-N.V)^4 through the same `FresnelColors` subgraph, so the
    shape was right and the shading was nonsense. The builder now bakes
    TEXCOORD3 = (target face outward normal, the same phase) and
    `CrystalMorphNormal` blends it on the position's exact schedule. Measured on
    the real cage: 64/64 panels wear their face's outward normal, worst dot
    1.000000. A leftover collapses to a point and keeps its own normal.
  - RENDER MODE. `ShepardGraph` is transparent, alpha-blended, `_ZWrite 0`, four
    stacked shells; `BlockGraph` is opaque, z-writing, alpha-clip dithered. No
    uniform closes that, so the structure changed instead: `morphFraction` (0.85)
    of the window is the geometry — the shader's window, so the last staggered
    panel lands before the boundary — and at the boundary the REAL prisms take
    the surface while the crystal's shells dissolve off them. The object that
    wins is the real one.

Two were measured and deliberately left, both recorded: the fresnel power is
already identical (the shared subgraph's hardcoded 4; `_Fresnel` feeds
ShepardGraph's alpha chain, not its colour), and `BlockGraph`'s distance fade is
0.3-2.5% of its range at pickup distance (`_SqrDistance 100000` = 316u).

## Verification

10/10 edit-mode tests (2 new: panels wear their face's outward normal; a
leftover keeps its own), geometry driver on the real cage, HLSL verifier
(normal runs the position's schedule to 1.2e-7, always unit, near-antipodal
guarded), `wire_crystal_morph.py --check` over BOTH splices incl. that they read
one clock and one stamp, census, conditional-compilation. Every new gate was
negative-controlled. Still nothing seen in the editor; `duration` stays at the
20x inspection value of 9s.
```

```text
 Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl                   |  51 ++++++
 Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph            | 284 +++++++++++++++++++++++++++++-
 .../Effects/Vessel Crystal Effects/SquirrelOmniCrystalMorph.asset     |   2 +-
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs           |  33 ++++
 .../Vessel Crystal Effects/SquirrelCrystalMorphByCrystalEffectSO.cs   |  29 +--
 .../_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs  |  29 ++-
 .../Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md       | 145 +++++++++++----
 .../Controller/Vessel/R_VesselActions/SquirrelCrystalMorph.cs         | 123 +++++++++----
 Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs          | 135 +++++++++++++-
 Assets/_Scripts/Utility/CrystalMorphMeshBuilder.cs                    |  62 +++++--
 Tools/Shaders/verify_crystal_morph.py                                 |  54 ++++++
 Tools/Shaders/wire_crystal_morph.py                                   | 301 ++++++++++++++++++++------------
 12 files changed, 1033 insertions(+), 215 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1382 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl b/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl
index 099a7a83d..39bd70e06 100644
--- a/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl
@@ -11,6 +11,11 @@
 // PrismClock's publisher, from the SAME value the stamp uses) into Clock — never a Time
 // node, which is a different clock domain and renders every stamp pre-finished.
 //
+// There are TWO functions here and they are one animation: `CrystalMorph` moves the vertex and
+// `CrystalMorphNormal` turns its normal, both off the same stamp and the same per-face phase.
+// Splice both or neither — a shape that arrives without its shading is the seam this exists to
+// remove, wearing a different costume.
+//
 // Spliced onto the very END of the vertex-position chain, so `Position` arrives with every
 // other vertex effect already applied (on ShepardGraph, the shell's outward displacement).
 // That placement is load-bearing in BOTH directions: at t = 0 the output is that position
@@ -61,4 +66,50 @@ void CrystalMorph_float(float3 Position, float4 Target, float Clock, float3 Morp
     Out = lerp(Position, Target.xyz, e);
 }
 
+// -----------------------------------------------------------------------------
+// Normal -> Target normal, on EXACTLY the same schedule as the position above.
+//
+//   Normal    object-space vertex normal, post every other vertex effect
+//   Target    xyz = this vertex's destination NORMAL, w = its face's PHASE (the same
+//             value baked into the position target, duplicated so the two cannot drift)
+//   Clock     _PrismClock
+//   Morph     the same (start, duration, stagger) the position node is given
+//
+// This is not a nicety. Both shaders involved in the hand-off derive their base colour from
+// `(1 - N.V)^4` (the shared FresnelColors subgraph), so a morph that carried only POSITION
+// would land the crystal cage's normals on the octahedron's faces: the right shape wearing
+// the wrong surface, which reads as a lighting pop at the exact instant the animation is
+// supposed to be invisible.
+//
+// The lerp is between two unit vectors and is renormalised, which is a chord rather than an
+// arc — it is not slerp, and near-antipodal endpoints pass close to zero. That is acceptable
+// here and deliberately not "fixed": a face's own normal and its target face's normal are
+// close for most of the assignment (the panel was chosen by angular fit), the traversal is a
+// fraction of a second, and slerp on a per-vertex path would cost a trig pair per vertex for
+// a difference nobody can see. The degenerate case is guarded, not ignored.
+//
+// Duration <= 0 means UNSTAMPED and returns Normal untouched, exactly like the position node.
+// -----------------------------------------------------------------------------
+void CrystalMorphNormal_float(float3 Normal, float4 Target, float Clock, float3 Morph,
+    out float3 Out)
+{
+    float duration = Morph.y;
+    if (duration <= 0.0)
+    {
+        Out = Normal;
+        return;
+    }
+
+    float t = saturate((Clock - Morph.x) / duration);
+
+    float stagger = saturate(Morph.z);
+    float span = max(1e-4, 1.0 - stagger);
+    float e = saturate((t - saturate(Target.w) * stagger) / span);
+
+    e = e * e * (3.0 - 2.0 * e);
+    float3 n = lerp(Normal, Target.xyz, e);
+    float len = length(n);
+    Out = len > 1e-5 ? n / len : Target.xyz;
+}
+
 #endif // CRYSTAL_MORPH_INCLUDED
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index 0ff28883d..f8eed1371 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -509,8 +509,41 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        /// <summary>
+        /// The pose this crystal had BEFORE it was moved this frame — or its live pose if it has
+        /// not been moved this frame.
+        ///
+        /// A collect is serviced by TWO trigger callbacks in the same physics step (the crystal's
+        /// own <see cref="OmniCrystalImpactor"/> and the vessel's <see cref="VesselImpactor"/>),
+        /// and Unity does not define which fires first. The crystal's ends in
+        /// <see cref="Respawn"/>, which on a host writes the slot list and re-poses the crystal
+        /// SYNCHRONOUSLY — so in one of the two orders anything that reads
+        /// <c>transform.position</c> for "where the crystal was collected" reads its NEXT HOME
+        /// instead, and every retirement animation starts in the wrong place. (That is exactly
+        /// how the Squirrel's morph shipped: it began at the crystal's new position, tens of
+        /// units away, with the respawn's identity rotation.)
+        ///
+        /// Rather than order the two callbacks — which cannot be done from here, and would only
+        /// hold until the next collider is added — this reports the pose the crystal HAD, and is
+        /// exact by construction: the only way the pose can be "new" at read time is that
+        /// <see cref="MoveToNewPos"/> ran this very frame, which is the move being serviced.
+        /// </summary>
+        public Pose CollectPose => _movedFrame == Time.frameCount
+            ? _poseBeforeMove
+            : new Pose(transform.position, transform.rotation);
+
+        /// <summary>World scale to match <see cref="CollectPose"/>. A respawn moves a crystal, it
+        /// does not resize it, so this is simply the live scale — carried alongside the pose so a
+        /// consumer never has to touch the transform at all.</summary>
+        public Vector3 CollectScale => transform.lossyScale;
+
+        Pose _poseBeforeMove;
+        int _movedFrame = -1;
+
         public void MoveToNewPos(Vector3 newPos)
         {
+            _poseBeforeMove = new Pose(transform.position, transform.rotation);
+            _movedFrame = Time.frameCount;
             transform.SetPositionAndRotation(newPos, Quaternion.identity);
         }
 
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs
index 973baabd3..0343bfc89 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactData.cs
@@ -19,23 +19,38 @@ namespace CosmicShore.Gameplay
         public int CrystalId;
 
         /// <summary>
-        /// Where the crystal was WHEN IT WAS COLLECTED. Carried rather than read back off the
-        /// crystal because collection and respawn are two independent RPC chains: on a remote
-        /// peer the crystal may already have been moved to its next home by the time these
-        /// effects arrive, and a retirement animation that starts there starts in the wrong
-        /// place.
+        /// The crystal's full POSE when it was collected — position, orientation and world scale.
+        ///
+        /// Carried rather than read back off the crystal for two independent reasons, and both
+        /// have bitten:
+        ///   • <b>Same frame.</b> A collect is serviced by two trigger callbacks in one physics
+        ///     step and Unity does not order them; the crystal's own ends in a respawn that
+        ///     re-poses it synchronously on a host. See <see cref="Crystal.CollectPose"/>.
+        ///   • <b>Across the wire.</b> Collection and respawn are independent RPC chains, so on
+        ///     a remote peer the crystal has usually already moved on by the time these effects
+        ///     arrive.
+        /// A retirement animation that reads the live transform therefore starts at the
+        /// crystal's NEXT home, wearing the respawn's identity rotation.
         /// </summary>
         public Vector3 Position;
+        /// <summary>Orientation to match <see cref="Position"/>. A respawn resets this to
+        /// identity, so it cannot be recovered afterwards.</summary>
+        public Quaternion Rotation;
+        /// <summary>World scale to match <see cref="Position"/>.</summary>
+        public Vector3 Scale;
 
         // 🔥 The factory method
         public static CrystalImpactData FromCrystal(Crystal crystal)
         {
+            var pose = crystal.CollectPose;
```

</details>
