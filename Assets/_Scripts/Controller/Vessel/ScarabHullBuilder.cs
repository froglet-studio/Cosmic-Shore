using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Scarab's procedural hull (design: R_VesselActions/SCARAB.md §3.0). The Scarab
    /// is a dung beetle, and its silhouette is the read: a wide domed carapace split down the
    /// middle by an elytra seam, a raised pronotum shield ahead of it, a flat clypeus, the
    /// signature curved horn, and six jointed legs under the shell.
    ///
    /// The GEOMETRY lives in <see cref="ScarabHullForm"/> — a pure static function this
    /// component feeds its serialized proportions into. This class owns only what needs the
    /// scene: emitting the parts as meshes on named child GameObjects, mirroring the core's
    /// materials onto them, and hiding the legacy model. That split is what lets the exact
    /// shipped geometry be compiled and RUN offline (the 2026-08-15 NaN was caught only that
    /// way) and is what the elemental morphs build on.
    ///
    /// WHY PROCEDURAL RATHER THAN A DIFFERENT FBX. The model hangs off the vessel as a
    /// <c>PrefabInstance</c> of the Sparrow FBX carrying ~40 per-child modifications plus stripped
    /// references from the vessel root (the hull GameObject that owns the ImpactCollider and the
    /// vessel's hull SphereCollider, the Animator, several transforms). Repointing that instance's
    /// guid at another FBX dangles every one of them — the exact failure `Docs/GAMECANVAS.md`
    /// records for hard-copied prefabs. So the legacy instance stays, keeping its colliders and
    /// rig wiring intact, and only its RENDERERS are switched off; this component draws the ship.
    /// When real Scarab art lands it replaces this component, not the scaffolding.
    ///
    /// MATERIAL CONTRACT (`ShipHelper.ApplyShipMaterial`): a MeshRenderer hull is painted on slot
    /// <b>1</b>. The mesh is therefore built with two submeshes — 0 = chassis (belly, clypeus,
    /// legs) on the shared body material, 1 = carapace + pronotum + horn, which is what takes the
    /// domain colour. Authoring them the other way round would paint the underside and leave the
    /// shell grey.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ScarabHullBuilder : MonoBehaviour, IProceduralElementMorphSource
    {
        [Header("Proportions (world units, +Z forward)")]
        [Tooltip("Nose-to-tail length of the CARAPACE. Horn and legs extend past this — the fit " +
                 "pass measures the carapace only, so appendages cannot squash the body.")]
        [SerializeField, Min(1f)] float length = 9f;
        [Tooltip("Full width at the widest point of the carapace.")]
        [SerializeField, Min(1f)] float width = 7.4f;
        [Tooltip("Height of the carapace dome above the centreline.")]
        [SerializeField, Min(0.2f)] float domeHeight = 2.15f;
        [Tooltip("Depth of the belly plate below the centreline. A beetle is domed on top and " +
                 "nearly flat underneath — keep this well under domeHeight.")]
        [SerializeField, Min(0.05f)] float bellyDepth = 0.8f;

        [Header("Elytra & pronotum")]
        [Tooltip("Half-width of the seam gap between the two wing cases, as a fraction of the " +
                 "half-width. This groove is most of what reads as 'beetle' at speed.")]
        [SerializeField, Range(0.01f, 0.25f)] float seamFraction = 0.055f;
        [Tooltip("Where the wing cases end and the pronotum begins, along the hull (0 = tail).")]
        [SerializeField, Range(0.4f, 0.85f)] float elytraFront = 0.63f;
        [Tooltip("Where the pronotum ends and the head begins.")]
        [SerializeField, Range(0.6f, 0.98f)] float pronotumFront = 0.90f;
        [Tooltip("How far the pronotum stands proud of the elytra profile. This step is the " +
                 "second-strongest beetle read after the seam.")]
        [SerializeField, Range(1f, 1.3f)] float pronotumSwell = 1.09f;
        [Tooltip("Number of raised ridges (striae) running the length of each wing case. Zero " +
                 "leaves a bald dome, which reads as a pebble rather than a shell.")]
        [SerializeField, Range(0, 8)] int striaeCount = 4;
        [Tooltip("Height of the striae as a fraction of the dome height.")]
        [SerializeField, Range(0f, 0.15f)] float striaeDepth = 0.045f;
        [Tooltip("Mesh resolution along the length. Higher = smoother shell, more verts.")]
        [SerializeField, Range(6, 40)] int lengthSegments = 22;
        [Tooltip("Mesh resolution across one wing case.")]
        [SerializeField, Range(3, 20)] int widthSegments = 10;

        [Header("Horn")]
        [Tooltip("Length of the clypeal horn as a fraction of the hull length. 0 removes it.")]
        [SerializeField, Range(0f, 0.8f)] float hornLength = 0.42f;
        [Tooltip("How far the horn curves upward over its span, in radians of total sweep. Enough " +
                 "of it that the tip finishes ABOVE the dome — a horn that ends level with the " +
                 "shell is read as a snout, and the horn is the whole silhouette.")]
        [SerializeField, Range(0f, 1.6f)] float hornCurve = 1.25f;
        [SerializeField, Range(3, 12)] int hornSides = 7;

        [Header("Legs")]
        [Tooltip("Total leg reach as a fraction of the half-width. Beetle legs are SHORT — at 0.6 " +
                 "the hull measures wider across the legs than across the shell and reads as a " +
                 "spider.")]
        [SerializeField, Range(0f, 1.2f)] float legLength = 0.34f;
        [Tooltip("Thickness of the femur as a fraction of the half-width. The tibia is thinner.")]
        [SerializeField, Range(0.01f, 0.2f)] float legThickness = 0.055f;

        [Header("Abdomen & antennae")]
        [Tooltip("Height of the abdomen dorsum under the wing cases, as a fraction of the dome " +
                 "profile. This is the body an open-elytra pose reveals — 0 re-opens the " +
                 "see-through seam. Keep well under 1 or it fuses with the closed shell.")]
        [SerializeField, Range(0f, 0.9f)] float abdomenHeight = 0.55f;
        [Tooltip("Antenna reach as a fraction of the half-width. Swept up and back so the " +
                 "lamellate clubs break the dome's silhouette from the chase camera astern.")]
        [SerializeField, Range(0f, 1.2f)] float antennaLength = 0.55f;
        [Tooltip("Antenna shaft thickness as a fraction of the half-width.")]
        [SerializeField, Range(0.01f, 0.1f)] float antennaThickness = 0.032f;

        [Header("Legacy model")]
        [Tooltip("Root of the inherited FBX model instance. Its RENDERERS are disabled at build " +
                 "time so only this hull draws; its colliders, Animator and transforms are left " +
                 "alone because the vessel's impact collider and rig references live on them.")]
        [SerializeField] Transform legacyModelRoot;

        readonly List<Mesh> _partMeshes = new();
        readonly List<Transform> _partTransforms = new();
        readonly List<Material> _materialWatchScratch = new();   // reused: the per-frame compare must not allocate
        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;

        // ---- elemental morph state (baked by Rebuild, driven by ScarabAnimation) -----------
        ScarabHullForm.MorphSet _morphSet;
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };   // force the first apply
        readonly List<Vector3> _blendVerts = new();
        readonly List<Vector3> _blendNormals = new();

        // IProceduralElementMorphSource — the audit's honesty surface.
        public IReadOnlyList<Element> ProceduralMorphElements => ScarabHullForm.MorphElements;
        public Transform HiddenLegacyModelRoot => legacyModelRoot;

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        /// <summary>The proportions as the pure form consumes them. The serialized fields are
        /// the authored home; this is how they travel into <see cref="ScarabHullForm.Generate"/>.</summary>
        public ScarabHullForm.Settings CollectSettings() => new()
        {
            Length = length,
            Width = width,
            DomeHeight = domeHeight,
            BellyDepth = bellyDepth,
            SeamFraction = seamFraction,
            ElytraFront = elytraFront,
            PronotumFront = pronotumFront,
            PronotumSwell = pronotumSwell,
            StriaeCount = striaeCount,
            StriaeDepth = striaeDepth,
            LengthSegments = lengthSegments,
            WidthSegments = widthSegments,
            HornLength = hornLength,
            HornCurve = hornCurve,
            HornSides = hornSides,
            LegLength = legLength,
            LegThickness = legThickness,
            AbdomenHeight = abdomenHeight,
            AntennaLength = antennaLength,
            AntennaThickness = antennaThickness,
        };

        /// <summary>Right-click the component to preview the shape in the editor without entering
        /// play mode. Runtime always rebuilds in <see cref="Awake"/>, so a stale preview mesh can
        /// never ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            // Bake the base hull AND the four element extremes in one pass (topology asserted
            // inside — a float channel that flips a feature gate throws loudly here rather than
            // corrupting the blend). The extremes cost three extra Generate calls at build time
            // and nothing per frame.
            _morphSet = ScarabHullForm.BakeMorphSet(CollectSettings());
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            EmitParts(_morphSet.BaseParts);
            HideLegacyModel();
        }

        void EmitParts(List<ScarabHullForm.Part> parts)
        {
            _partMeshes.Clear();
            _partTransforms.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                // Geometry arrives in hull space; for a CHILD part the pivot is subtracted here
                // and becomes its localPosition, so it hinges where it should. Part 0 (Core)
                // lives on THIS GameObject, whose transform belongs to the prefab author — it
                // cannot re-seat itself — so its mesh stays in hull space outright. (It used to
                // get the same subtraction with no compensating transform, which displaced the
                // whole core +0.70u up / +0.41u forward of the shell: the fit pass centres the
                // hull, so 'pivot zero' comes back as minus the carapace centre. The offline
                // renders composite hull-space verts and were structurally blind to it.)
                var local = new List<Vector3>(part.Verts.Count);
                Vector3 pivotShift = i == 0 ? Vector3.zero : part.Pivot;
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - pivotShift);

                var mesh = new Mesh { name = "Scarab_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Chassis, ScarabHullForm.ChassisSubmesh);
                mesh.SetTriangles(part.Shell, ScarabHullForm.ShellSubmesh);
                // Bounds are pinned to the bake's interval union — the box that contains EVERY
                // element-weight combination — so the animated morph writes below never pay a
                // recalculation and can never shrink culling under a blended pose. The bake's
                // interval is in the part's LOCAL (pivot-relative) frame; the Core renders in
                // hull frame, so its interval carries the pivot's own reachable range too.
                var boundsMin = _morphSet.BoundsMin[i];
                var boundsMax = _morphSet.BoundsMax[i];
                if (i == 0)
                {
                    Vector3 pivotLo = part.Pivot, pivotHi = part.Pivot;
                    for (int e = 0; e < ScarabHullForm.MorphElements.Length; e++)
                    {
                        var pd = _morphSet.Deltas[e][i].PivotDelta;
                        pivotLo += Vector3.Min(Vector3.zero, pd);
                        pivotHi += Vector3.Max(Vector3.zero, pd);
                    }
                    boundsMin += pivotLo;
                    boundsMax += pivotHi;
                }
                mesh.bounds = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
                _partMeshes.Add(mesh);

                if (i == 0)
                {
                    GetComponent<MeshFilter>().sharedMesh = mesh;   // Core, on our own renderer
                    _partTransforms.Add(transform);
                    continue;
                }

                var child = transform.Find(part.Name);
                if (!child)
                {
                    var go = new GameObject(part.Name) { layer = gameObject.layer };
                    child = go.transform;
                    child.SetParent(transform, false);
                    go.AddComponent<MeshFilter>();
                    go.AddComponent<MeshRenderer>();
                }
                child.localPosition = part.Pivot;
                child.localRotation = Quaternion.identity;
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                _partTransforms.Add(child);
            }

            PropagateMaterials(force: true);
        }

        /// <summary>
        /// Blend the hull to the given element weights (each 0..1, in
        /// <see cref="ScarabHullForm.MorphElements"/> order: charge, mass, space, time).
        /// Idempotent and cheap when nothing changed; when a weight moved, only parts the morphs
        /// actually touch are rewritten (verts + renormalized normals, bounds untouched — they
        /// were pinned to the whole-lattice union at emit). The part's blended pivot lands on
        /// <c>localPosition</c>, which composes cleanly with <see cref="ScarabAnimation"/>'s
        /// puppetry: the animation owns localRotation, the morph owns localPosition, and no
        /// channel has two writers. Driven from ScarabAnimation's LateUpdate, whose tweens carry
        /// the fleet's morph feel (VesselElementalMorphConfigSO) — this method is deliberately
        /// feel-free.
        /// </summary>
        public void ApplyElementMorphWeights(float charge, float mass, float space, float time)
        {
            if (_morphSet == null) return;

            var weights = _blendWeights;
            weights[0] = Mathf.Clamp01(charge);
            weights[1] = Mathf.Clamp01(mass);
            weights[2] = Mathf.Clamp01(space);
            weights[3] = Mathf.Clamp01(time);

            bool changed = false;
            for (int i = 0; i < weights.Length; i++)
                changed |= !Mathf.Approximately(weights[i], _appliedWeights[i]);
            if (!changed) return;
            for (int i = 0; i < weights.Length; i++) _appliedWeights[i] = weights[i];

            for (int p = 0; p < _partMeshes.Count && p < _morphSet.BaseParts.Count; p++)
            {
                if (!_morphSet.PartMorphs[p]) continue;
                var mesh = _partMeshes[p];
                if (!mesh) continue;

                Vector3 pivot = ScarabHullForm.BlendPart(_morphSet, p, weights,
                                                         _blendVerts, _blendNormals);

                // The Core renders in hull frame (it cannot re-seat this GameObject), so its
                // blended pivot folds back into the vertices; a child re-seats via localPosition.
                if (p == 0)
                    for (int i = 0; i < _blendVerts.Count; i++) _blendVerts[i] += pivot;
                else if (_partTransforms[p])
                    _partTransforms[p].localPosition = pivot;

                mesh.SetVertices(_blendVerts, 0, _blendVerts.Count,
                                 MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetNormals(_blendNormals, 0, _blendNormals.Count,
                                MeshUpdateFlags.DontRecalculateBounds);
            }
        }

        readonly float[] _blendWeights = new float[4];

        /// <summary>
        /// Keep the movable parts wearing the same materials as the core. The fleet paints the
        /// DOMAIN colour onto slot 1 of the object listed in `VesselCustomization._shipGeometries`
        /// — which is this GameObject — and that list is serialized, so parts created at runtime
        /// can never be in it. Rather than fight that, the parts simply mirror the core, which also
        /// means they share its material instance instead of minting one each.
        /// </summary>
        void PropagateMaterials(bool force = false)
        {
            var source = _sourceRenderer ? _sourceRenderer : (_sourceRenderer = GetComponent<MeshRenderer>());
            if (!source) return;

            // The WATCH must be allocation-free: this runs every frame on every live Scarab, and
            // the sharedMaterials array getter mints a managed copy per access — "one reference
            // compare per frame" was hiding one Material[] per frame (review finding). The
            // non-allocating list read feeds the compare; the array is only materialized on the
            // rare force/changed path, where the children need it for assignment anyway.
            source.GetSharedMaterials(_materialWatchScratch);
            if (_materialWatchScratch.Count == 0) return;

            Material domain = _materialWatchScratch.Count > 1 ? _materialWatchScratch[1] : _materialWatchScratch[0];
            if (!force && domain == _lastDomainMaterial) return;
            _lastDomainMaterial = domain;

            var mats = source.sharedMaterials;
            for (int i = 0; i < transform.childCount; i++)
            {
                var r = transform.GetChild(i).GetComponent<MeshRenderer>();
                if (r) r.sharedMaterials = mats;
            }
        }

        // One reference compare per frame. The domain material is swapped by ShipHelper at spawn
        // AND on any later domain change (the domain-changer toy), and neither raises an event this
        // component could bind to, so it is watched rather than pushed.
        void LateUpdate() => PropagateMaterials();

        /// <summary>
        /// Switch off the inherited model's renderers — never its GameObjects. The vessel's
        /// ImpactCollider and hull SphereCollider live on that subtree, and `VesselCustomization` /
        /// `VesselStatus` hold references into it; deactivating it would silently drop the ship
        /// out of the collision world. Disabled renderers are also excluded from the occlusion
        /// corridor's hull measurement, so the corridor sizes itself to THIS hull automatically.
        /// </summary>
        void HideLegacyModel()
        {
            if (!legacyModelRoot) return;
            var renderers = legacyModelRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].transform.IsChildOf(transform)) continue;   // never hide ourselves
                renderers[i].enabled = false;
            }
        }
    }
}
