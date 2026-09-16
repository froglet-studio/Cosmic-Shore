using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected ELEMENTAL crystal's BODY onto the vessel that took it — the crystal's
    /// cage opens out onto a shell the size of the hull as it flies in, then dissolves off the
    /// ship. Full record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/VESSEL_CRYSTAL_ABSORB.md</c>.
    ///
    /// This is the THIRD instance of a per-vessel crystal retirement and the first on the HULL
    /// path. The Squirrel's ring morph and the Scarab's ball morph
    /// (<see cref="ScarabCrystalMorph"/>) both retire an OMNI crystal onto a thing the pickup
    /// MADE; here there is no new object, so the crystal is morphed IN PLACE and the vessel is
    /// the target. Everything under the animation is reused: the GPU half
    /// (<c>CrystalMorph.hlsl</c>), the mesh bake (<see cref="CrystalMorphMeshBuilder"/>) and the
    /// FEEL (<see cref="CrystalMorphConfigSO"/>, one asset for the whole fleet).
    ///
    /// ── OPT-IN BY PRESENCE, and that is the per-vessel wiring ─────────────────────────────────
    /// A vessel takes the morph by CARRYING this component, the same way it takes a tail or a jet
    /// (<c>Docs/VESSEL_TAIL_AND_JETS.md</c>). There is no container entry and no class check: a
    /// hardcoded <c>VesselClassType</c> test would be a second authority on which hulls opt in,
    /// and an impact-effect container cannot carry it because the capture is run by the CRYSTAL's
    /// impactor, not by a vessel effect. A missing component is therefore a visible, inspectable
    /// state and the fleet default is the shipped husk-and-shrink absorb, unchanged.
    ///
    /// ── THE TARGET IS A SPHERE, AND THAT IS WHAT MAKES IT ONE BAKE ────────────────────────────
    /// <see cref="CrystalMorphMeshBuilder.ConvexHullTarget"/> is exact "for any CONVEX target",
    /// and a vessel hull is not convex — a ray from the hull centre toward a wingtip exits the
    /// fuselage first, so the cage would land INSIDE the ship. It is also not stable: five of the
    /// eight hulls are SKINNED (so <c>sharedMesh</c> is the bind pose in ROOT-BONE space, the trap
    /// <c>Docs/VESSEL_CONSTRUCTION.md</c> records twice) and every hull MORPHS on the very element
    /// the crystal just raised, so the surface is deforming while the cage lands on it.
    ///
    /// So the target is the sphere that CIRCUMSCRIBES the hull, measured by the corridor's own
    /// <see cref="PrismOcclusionCorridor.MeasureCircumscribedRadius"/> (hull only, rotation-
    /// invariant, skimmers and disabled renderers excluded) — one measurement, correct on any
    /// hull, convex by construction.
    ///
    /// Its load-bearing property is WHERE it is centred: on the CRYSTAL'S OWN LOCAL ORIGIN, not
    /// on the vessel. A sphere about the local origin is invariant under the crystal's own
    /// rotation AND its own motion, so the bake survives the whole flight — the capture's suction
    /// lerp carries the crystal to the ship and the morph independently opens its cage, and the
    /// two compose with nothing to re-bake and nothing to double-count. Centre it on the vessel
    /// instead and the targets sit hundreds of units away in local space, so the cage smears
    /// across the gap the suction is already closing. A hull MESH would need re-baking every
    /// frame as the ship turned; this needs one bake for the life of the capture.
    ///
    /// ── THE GEOMETRY DOES THE SIZE CHANGE, SO THE TRANSFORM MUST STOP ────────────────────────
    /// The shipped absorb drives <c>ScaleMultiplier</c> to ZERO — the crystal shrinks into the
    /// hull. Left running, that scales the baked targets with it and the shell collapses to a
    /// point whatever the morph does. So a morphing capture HOLDS the crystal's scale at its
    /// snatch-end value and lets the cage carry the entire size change, which is the same
    /// division the Scarab makes (the geometry lands, the transform does not move) and is the
    /// better read anyway: a cage folding open is more than a mesh getting smaller.
    ///
    /// ── THE WINDOW IS THE SUCTION, SO THE DISSOLVE TAKES A FINISHED SHELL ────────────────────
    /// The morph lands at ABSORB ENTRY, so the absorb's existing opacity ramp dissolves a shell
    /// that is fully formed. Running the geometry through the absorb as well would land it exactly
    /// as the opacity reached zero and nobody would ever see the shape — the Scarab's
    /// <c>morphFraction</c> split, expressed here in the phases the capture already has.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VesselCrystalAbsorbMorph : MonoBehaviour
    {
        static readonly int MorphId = Shader.PropertyToID("_CrystalMorph");

        [Header("Landing shell")]
        [Tooltip("Radius of the shell the cage lands on, as a fraction of this vessel's own " +
                 "CIRCUMSCRIBING HULL radius. 1 = the sphere that just contains the hull, so the " +
                 "crystal wraps the whole ship; lower values land it closer to the body. Sized off " +
                 "the hull rather than in world units so one number is right on every vessel.")]
        [SerializeField, Range(0.05f, 1.5f)] float targetHullFraction = 1f;

        [Tooltip("Icosphere subdivision for the landing shell. 2 = 320 facets, the same count the " +
                 "Scarab's ball morph lands on. Higher is smoother and costs bake time only — the " +
                 "shell is never rendered, it is only the surface the cage is cast onto.")]
        [SerializeField, Range(1, 3)] int subdivisions = 2;

        [Tooltip("Optional per-vessel override of the crystal-morph FEEL. Leave empty — " +
                 "Resources/CrystalMorphConfig is the fleet's single source, exactly as the " +
                 "capture beat is. A per-prefab duration is how the old capture drifted to 1s on " +
                 "two fauna and 3s on eleven flora.")]
        [SerializeField] CrystalMorphConfigSO configOverride;

        // A UNIT icosphere, built once for the whole fleet and scaled by the bake matrix. The
        // target hull is read out into plain arrays, so nothing renders this and one instance
        // serves every capture — a Generate() per pickup would mint a Mesh per crystal, and a
        // runtime Mesh that nobody destroys is the leak Docs/PRISM_ANIMATION-adjacent code keeps
        // paying for.
        static Mesh s_unitSphere;

        CrystalMorphConfigSO Config => configOverride ? configOverride : CrystalMorphConfigSO.Instance;

        /// <summary>
        /// The morph this vessel opts into, or null when it has not. Resolved off the vessel's own
        /// transform rather than its class, so the answer is the prefab's wiring.
        /// </summary>
        public static VesselCrystalAbsorbMorph For(IVesselStatus vesselStatus)
        {
            var root = vesselStatus?.VesselTransformer ? vesselStatus.VesselTransformer.transform : null;
            return root ? root.GetComponentInChildren<VesselCrystalAbsorbMorph>(true) : null;
        }

        /// <summary>
        /// True when <paramref name="crystal"/> can actually be morphed — i.e. its shells draw
        /// with a shader carrying the <c>_CrystalMorph</c> splice.
        ///
        /// This is a CAPABILITY test, not an element test, and that is deliberate: only
        /// <c>ShepardGraph</c> carries the splice today, which is Mass (and the omni cage). Charge
        /// (<c>ChargeCrystal.shader</c>), Space (<c>CrystalGraph</c>) and Time
        /// (<c>DynamicFresnelGraph</c>) are four different shader families and each needs its own
        /// splice; asking the material means they switch on the day their graph is wired and
        /// nothing here has to learn their names.
        ///
        /// <c>HasProperty</c> is a valid gate here specifically because <c>_CrystalMorph</c> is
        /// EXPOSED (<c>m_GeneratePropertyBlock: true</c>) — it could never see an UNEXPOSED
        /// ShaderGraph property, which is why the prism clock's validator reads graph text instead.
        /// </summary>
        public static bool CanMorph(Crystal crystal)
        {
            var models = crystal ? crystal.CrystalModels : null;
            if (models == null) return false;

            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (!model) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                var mat = renderer.sharedMaterial;
                if (mat && mat.HasProperty(MorphId)) return true;
            }
            return false;
        }

        /// <summary>
        /// Bakes the landing shell into <paramref name="crystal"/>'s own cage and writes the ONE
        /// stamp that runs the whole animation. Returns the morph mesh the caller now OWNS (it
        /// must <c>Destroy</c> it — the crystal's GameObject is destroyed at the end of the
        /// capture, but a runtime Mesh is not collected with it), or null with a
        /// <paramref name="diagnosis"/> naming the fix.
        ///
        /// Every refusal is NAMED because they all look identical on screen — the crystal flies in
        /// and vanishes — and after the fact there is no telling "this hull has no renderers to
        /// measure" from "the cage is not Read/Write enabled". A null return leaves the shipped
        /// husk-and-shrink absorb to play, so a refusal costs a flourish and never a pickup.
        /// </summary>
        public Mesh TryBegin(Crystal crystal, Transform vesselTransform, float morphSeconds,
                             out string diagnosis)
        {
            diagnosis = null;
            if (!crystal || !vesselTransform) { diagnosis = "no crystal or no vessel transform"; return null; }
            if (morphSeconds <= 0f) { diagnosis = "the morph window is zero-length"; return null; }

            float hullRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(vesselTransform);
            if (hullRadius <= 1e-3f)
            {
                diagnosis = "the vessel measured a zero hull radius — MeasureCircumscribedRadius " +
                            "counts only enabled MeshFilter/SkinnedMeshRenderer pairs outside a " +
                            "Skimmer, so this hull draws nothing it can see";
                return null;
            }

            var cage = FirstCageMesh(crystal, out var shells);
            if (!cage)
            {
                diagnosis = "the crystal exposed no drawable shell (a CrystalModels entry with " +
                            "both a MeshFilter and a MeshRenderer)";
                return null;
            }

            // The shell in WORLD units, brought into the crystal's own local space. The full
            // matrix (not a scalar) is what keeps this exact under a non-uniform parent: a sphere
            // in world space becomes an ellipsoid in local space, which maps back to the sphere
            // the eye sees.
            float worldRadius = hullRadius * targetHullFraction;
            Matrix4x4 toLocal = crystal.transform.worldToLocalMatrix
                              * Matrix4x4.Scale(Vector3.one * worldRadius);

            if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    UnitSphere(subdivisions), toLocal, Vector3.zero, out var target, out string hullDiagnosis))
            {
                diagnosis = $"cannot read the landing shell: {hullDiagnosis}";
                return null;
            }

            var cfg = Config;

            // ── The STAGGER is deliberately OFF on this path, and that is a MEASUREMENT ───────
            // CrystalMorphMeshBuilder phases each welded SOLID by its distance from the centre and
            // normalises across the observed range: phase carries information only when the cage
            // has solids at DIFFERENT radii. The omni cage does (struts and panels, which is why
            // "the outermost struts leave first" reads there). An elemental cage is a SHELL — the
            // Mass crystal welds into 60 connected solids whose mean radii are identical to within
            // 1.4e-6 — and the builder's own `span = Max(1e-5, maxR - minR)` floor then divides
            // that float noise by the epsilon and emits a phase band of [0.857, 1.000] across 16
            // distinct values. Measured by running the shipped builder on the shipped cage.
            //
            // So a staggered shell here is not a cascade, it is float noise wearing one: nothing
            // authored it, it differs from the omni look it would be borrowing, and it is not
            // reproducible in principle. Passing ONE phase for every solid makes the shell move as
            // one exactly, which is the honest motion for a shape that has no outermost part.
            // A future cage WITH real radial spread should pass cfg.phaseNear/phaseFar instead.
            var morphMesh = CrystalMorphMeshBuilder.TryBuild(cage, in target,
                                                             0f, 0f, out diagnosis);
            if (!morphMesh) return null;

            var morph = new Vector3(PrismClock.Now, morphSeconds, 0f);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < shells.Count; i++)
            {
                shells[i].filter.sharedMesh = morphMesh;

                // Read-modify-write, never a bare Set: Crystal.WriteTint owns the collectability
                // colour and the absorb's opacity on this same block, and a replacing write would
                // erase whichever of us went second.
                shells[i].renderer.GetPropertyBlock(block);
                block.SetVector(MorphId, morph);
                shells[i].renderer.SetPropertyBlock(block);
            }

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] hull: '{crystal.name}' opening onto a {worldRadius:F2}u shell " +
                $"({targetHullFraction:F2} of {vesselTransform.name}'s {hullRadius:F2}u hull, " +
                $"{target.FaceCount} facets) over {morphSeconds:F2}s — {shells.Count} shells, " +
                $"{morphMesh.vertexCount} morph vertices.");
            return morphMesh;
        }

        readonly struct Shell
        {
            public readonly MeshFilter filter;
            public readonly MeshRenderer renderer;
            public Shell(MeshFilter f, MeshRenderer r) { filter = f; renderer = r; }
        }

        /// <summary>
        /// The cage every shell shares, plus those shells. ONE morph mesh drives all of them, so
        /// they have to be the same mesh — which the Mass crystal's four are by construction
        /// (four coincident copies of one cage at one scale, differing only in which band of the
        /// travelling wave each renders). A shell drawing something else is dropped and NAMED
        /// rather than silently redrawn as shell 0's geometry.
        /// </summary>
        static Mesh FirstCageMesh(Crystal crystal, out List<Shell> shells)
        {
            shells = new List<Shell>();
            var models = crystal.CrystalModels;
            if (models == null) return null;

            Mesh first = null;
            for (int i = 0; i < models.Count; i++)
            {
                var model = models[i]?.model;
                if (!model) continue;
                if (!model.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) continue;
                if (!model.TryGetComponent<MeshRenderer>(out var renderer)) continue;

                if (!first) first = filter.sharedMesh;
                else if (filter.sharedMesh != first)
                {
                    CSDebug.LogWarning($"[VesselCrystalAbsorbMorph] '{crystal.name}' shell {i} draws " +
                                       $"'{filter.sharedMesh.name}', not the first shell's " +
                                       $"'{first.name}' — the morph carries one mesh, so this shell " +
                                       "is left out of it.");
                    continue;
                }
                shells.Add(new Shell(filter, renderer));
            }
            return shells.Count > 0 ? first : null;
        }

        static Mesh UnitSphere(int subdivisions)
        {
            if (!s_unitSphere)
            {
                s_unitSphere = IcosphereMeshGenerator.Generate(subdivisions, 1f, flatShaded: true);
                s_unitSphere.name = "CrystalAbsorbShell";
                // Never unloaded: one mesh for the whole session, and it is CPU-read only.
                s_unitSphere.hideFlags = HideFlags.HideAndDontSave;
            }
            return s_unitSphere;
        }
    }
}
