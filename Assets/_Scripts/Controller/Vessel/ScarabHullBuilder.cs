using System.Collections.Generic;
using UnityEngine;

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
    public class ScarabHullBuilder : MonoBehaviour
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

        [Header("Legacy model")]
        [Tooltip("Root of the inherited FBX model instance. Its RENDERERS are disabled at build " +
                 "time so only this hull draws; its colliders, Animator and transforms are left " +
                 "alone because the vessel's impact collider and rig references live on them.")]
        [SerializeField] Transform legacyModelRoot;

        readonly List<Mesh> _partMeshes = new();
        Material _lastDomainMaterial;

        void Awake() => Rebuild();

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
        };

        /// <summary>Right-click the component to preview the shape in the editor without entering
        /// play mode. Runtime always rebuilds in <see cref="Awake"/>, so a stale preview mesh can
        /// never ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            var parts = ScarabHullForm.Generate(CollectSettings());
            EmitParts(parts);
            HideLegacyModel();
        }

        void EmitParts(List<ScarabHullForm.Part> parts)
        {
            _partMeshes.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                // Geometry arrives in hull space; the pivot is subtracted here and becomes the
                // child's localPosition, so each part hinges where it should.
                var local = new List<Vector3>(part.Verts.Count);
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - part.Pivot);

                var mesh = new Mesh { name = "Scarab_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Chassis, ScarabHullForm.ChassisSubmesh);
                mesh.SetTriangles(part.Shell, ScarabHullForm.ShellSubmesh);
                mesh.RecalculateBounds();
                _partMeshes.Add(mesh);

                if (i == 0)
                {
                    GetComponent<MeshFilter>().sharedMesh = mesh;   // Core, on our own renderer
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
            }

            PropagateMaterials(force: true);
        }

        /// <summary>
        /// Keep the movable parts wearing the same materials as the core. The fleet paints the
        /// DOMAIN colour onto slot 1 of the object listed in `VesselCustomization._shipGeometries`
        /// — which is this GameObject — and that list is serialized, so parts created at runtime
        /// can never be in it. Rather than fight that, the parts simply mirror the core, which also
        /// means they share its material instance instead of minting one each.
        /// </summary>
        void PropagateMaterials(bool force = false)
        {
            var source = GetComponent<MeshRenderer>();
            if (!source) return;
            var mats = source.sharedMaterials;
            if (mats == null || mats.Length == 0) return;

            Material domain = mats.Length > 1 ? mats[1] : mats[0];
            if (!force && domain == _lastDomainMaterial) return;
            _lastDomainMaterial = domain;

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
