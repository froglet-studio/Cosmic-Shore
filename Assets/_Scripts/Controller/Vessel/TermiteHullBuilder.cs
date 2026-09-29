using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Termite queen's procedural hull (design: <c>R_VesselActions/TERMITE.md</c> §2).
    /// The GEOMETRY lives in <see cref="TermiteHullForm"/> — a pure static function this component
    /// feeds its serialized proportions into, compiled and RUN offline by
    /// <c>Tools/Build/termite_hull_harness</c>. This class owns only what needs the scene: emitting
    /// the parts as meshes on named children, mirroring the core's materials onto them, and hiding
    /// whatever legacy model the prefab carries.
    ///
    /// <para>The shape of this class is <see cref="ButterflyHullBuilder"/>'s, which is
    /// <see cref="ScarabHullBuilder"/>'s. The bake/blend machinery is duplicated a third time
    /// rather than extracted inside a new-vessel branch — extracting it is a refactor with its own
    /// review, and is logged as such in TERMITE.md's follow-ups.</para>
    ///
    /// <para><b>Why procedural.</b> The shipped <c>Termite.prefab</c> wore the Manta's model, and
    /// there is no termite art in the project. A generated hull gets REAL element morphs (the four
    /// extremes of its own pure function) and can be authored headlessly; when real art lands it
    /// replaces this component, not the scaffolding.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour
    /// onto slot <b>1</b>: the wings and the pale abdominal membrane are submesh 1, the dark plates,
    /// head, thorax and legs submesh 0.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TermiteHullBuilder : MonoBehaviour, IProceduralElementMorphSource, IProceduralHullSource
    {
        [Header("Head (world units, +Z forward)")]
        [SerializeField, Min(0.1f)] float headRadius = 1.55f;
        [SerializeField, Min(0.1f)] float headLength = 2.7f;
        [Tooltip("Length of each mandible. Charge lengthens them into sickles.")]
        [SerializeField, Min(0.05f)] float mandibleLength = 1.7f;
        [SerializeField, Min(0.01f)] float mandibleThickness = 0.24f;
        [Tooltip("Length of each beaded antenna.")]
        [SerializeField, Min(0.05f)] float antennaLength = 4.2f;
        [SerializeField, Min(0.005f)] float antennaThickness = 0.1f;

        [Header("Thorax + legs")]
        [Tooltip("NOT morphed by any element: the wing hinges and the waist sit on the thorax, and " +
                 "an element morph writes vertices only — a moving hinge would tear a wing off.")]
        [SerializeField, Min(0.1f)] float thoraxRadius = 1.15f;
        [SerializeField, Min(0.1f)] float thoraxLength = 3.1f;
        [SerializeField, Min(0.1f)] float legLength = 3.3f;
        [SerializeField, Min(0.01f)] float legThickness = 0.17f;

        [Header("Abdomen — the queen's silhouette")]
        [Tooltip("Fattest half-width of the abdomen. A queen is mostly abdomen; Mass swells it.")]
        [SerializeField, Min(0.1f)] float abdomenRadius = 2.7f;
        [SerializeField, Min(0.5f)] float abdomenLength = 11.5f;
        [Tooltip("How far each dark plate stands proud of the profile, as a fraction of radius.")]
        [SerializeField, Range(0f, 0.4f)] float plateBulge = 0.08f;
        [Tooltip("How far the pale (domain-coloured) membrane between plates sinks, as a fraction " +
                 "of radius.")]
        [SerializeField, Range(0f, 0.5f)] float membraneRecess = 0.12f;
        [SerializeField, Range(2, 16)] int abdomenSegments = 7;
        [SerializeField, Range(2, 8)] int ringsPerSegment = 4;

        [Header("Wings (four, nearly equal — Isoptera)")]
        [Tooltip("Forewing length, hinge to tip. Longer than the whole body, as a winged " +
                 "reproductive's are.")]
        [SerializeField, Min(0.5f)] float wingLength = 15f;
        [SerializeField, Min(0.1f)] float wingWidth = 2.7f;
        [SerializeField, Min(0f)] float wingLift = 0.4f;
        [SerializeField, Range(0.5f, 1.2f)] float hindScale = 0.94f;
        [SerializeField, Range(2, 24)] int wingSpanSegments = 12;
        [SerializeField, Range(1, 8)] int wingChordSegments = 4;

        [Header("Resolution")]
        [SerializeField, Range(6, 24)] int sides = 12;

        [Header("Legacy model")]
        [Tooltip("Root of an inherited model whose RENDERERS this component switches off. Never " +
                 "its GameObjects: colliders and serialized references can live on that subtree.")]
        [SerializeField] Transform legacyModelRoot;

        TermiteHullForm.MorphSet _morphSet;
        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;
        readonly List<Mesh> _partMeshes = new();
        readonly List<Transform> _partTransforms = new();
        readonly List<Vector3> _blendVerts = new();
        readonly List<Vector3> _blendNormals = new();
        readonly List<Material> _materialWatchScratch = new();
        readonly float[] _blendWeights = new float[4];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };
        readonly List<Transform> _wings = new();
        Transform _abdomen;

        static readonly Element[] MorphedElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public IReadOnlyList<Element> ProceduralMorphElements => MorphedElements;
        public Transform HiddenLegacyModelRoot => legacyModelRoot;

        /// <summary>The four wing transforms in emit order (fore L, fore R, hind L, hind R) — what
        /// <see cref="TermiteAnimation"/> folds and buzzes. Empty until <see cref="Rebuild"/>.</summary>
        public IReadOnlyList<Transform> WingTransforms => _wings;

        /// <summary>The abdomen transform (pivot at the waist) — what the animation breathes and
        /// sways. Null until <see cref="Rebuild"/>.</summary>
        public Transform AbdomenTransform => _abdomen;

        public TermiteHullForm.Settings CollectSettings() => new()
        {
            HeadRadius = headRadius,
            HeadLength = headLength,
            MandibleLength = mandibleLength,
            MandibleThickness = mandibleThickness,
            AntennaLength = antennaLength,
            AntennaThickness = antennaThickness,
            ThoraxRadius = thoraxRadius,
            ThoraxLength = thoraxLength,
            LegLength = legLength,
            LegThickness = legThickness,
            AbdomenRadius = abdomenRadius,
            AbdomenLength = abdomenLength,
            PlateBulge = plateBulge,
            MembraneRecess = membraneRecess,
            AbdomenSegments = abdomenSegments,
            RingsPerSegment = ringsPerSegment,
            WingLength = wingLength,
            WingWidth = wingWidth,
            WingLift = wingLift,
            HindScale = hindScale,
            WingSpanSegments = wingSpanSegments,
            WingChordSegments = wingChordSegments,
            Sides = sides,
        };

        // IProceduralHullSource — what a harvester reading the prefab ASSET sees instead of an
        // empty MeshFilter. Same parts and the same pivot rule as EmitParts. The wings are shown
        // in their GEOMETRY pose (spread), which is also how a mini hull in a toy station reads.
        public void BuildPreviewPieces(List<ProceduralHullPiece> into)
        {
            var parts = TermiteHullForm.Generate(CollectSettings());
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var verts = new Vector3[part.Verts.Count];
                for (int v = 0; v < verts.Length; v++) verts[v] = part.Verts[v] - pivotShift;
                into.Add(new ProceduralHullPiece
                {
                    Name = part.Name,
                    LocalPosition = pivotShift,
                    Vertices = verts,
                    Normals = part.Normals.ToArray(),
                    Uvs = part.Uvs.ToArray(),
                    Submeshes = new[] { part.Body.ToArray(), part.Accent.ToArray() },
                });
            }
        }

        static Vector3 PivotShift(int index, TermiteHullForm.Part part)
            => index == TermiteHullForm.CorePart ? Vector3.zero : part.Pivot;

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            _morphSet = TermiteHullForm.BakeMorphSet(CollectSettings());
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            EmitParts(_morphSet.BaseParts);
            HideLegacyModel();
        }

        void EmitParts(List<TermiteHullForm.Part> parts)
        {
            _partMeshes.Clear();
            _partTransforms.Clear();
            _wings.Clear();
            _abdomen = null;

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var local = new List<Vector3>(part.Verts.Count);
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - pivotShift);

                var mesh = new Mesh { name = "Termite_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Body, TermiteHullForm.BodySubmesh);
                mesh.SetTriangles(part.Accent, TermiteHullForm.AccentSubmesh);

                // Bounds pinned to the bake's interval union, so the per-frame morph writes never
                // pay a recalculation and can never shrink culling under a blended pose.
                Vector3 boundsMin = _morphSet.BoundsMin[i] - pivotShift;
                Vector3 boundsMax = _morphSet.BoundsMax[i] - pivotShift;
                mesh.bounds = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
                _partMeshes.Add(mesh);

                if (i == TermiteHullForm.CorePart)
                {
                    GetComponent<MeshFilter>().sharedMesh = mesh;
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
                child.localScale = Vector3.one;
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                _partTransforms.Add(child);

                if (i == TermiteHullForm.AbdomenPart) _abdomen = child;
                else _wings.Add(child);
            }

            PropagateMaterials(force: true);
        }

        /// <summary>
        /// Blend the hull to the given element weights (each 0..1, charge/mass/space/time). Cheap
        /// and idempotent when nothing moved. Writes VERTICES only — the animation owns
        /// <c>localRotation</c> and <c>localScale</c> of the parts, so no channel has two writers.
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

                TermiteHullForm.BlendPart(_morphSet, p, weights, _blendVerts, _blendNormals);

                Vector3 pivotShift = PivotShift(p, _morphSet.BaseParts[p]);
                if (pivotShift != Vector3.zero)
                    for (int i = 0; i < _blendVerts.Count; i++) _blendVerts[i] -= pivotShift;

                mesh.SetVertices(_blendVerts, 0, _blendVerts.Count, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetNormals(_blendNormals, 0, _blendNormals.Count, MeshUpdateFlags.DontRecalculateBounds);
            }
        }

        /// <summary>
        /// Keep every part wearing the core's materials. The fleet paints the domain colour onto
        /// slot 1 of the object listed in <c>VesselCustomization._shipGeometries</c> — this one —
        /// and that list is serialized, so parts created at runtime can never be in it.
        /// Allocation-free watch (the <c>sharedMaterials</c> getter mints an array per access).
        /// </summary>
        void PropagateMaterials(bool force = false)
        {
            var source = _sourceRenderer ? _sourceRenderer : (_sourceRenderer = GetComponent<MeshRenderer>());
            if (!source) return;

            source.GetSharedMaterials(_materialWatchScratch);
            if (_materialWatchScratch.Count == 0) return;

            Material domain = _materialWatchScratch.Count > 1 ? _materialWatchScratch[1] : _materialWatchScratch[0];
            if (!force && domain == _lastDomainMaterial) return;
            _lastDomainMaterial = domain;

            var mats = source.sharedMaterials;
            for (int i = 0; i < _partTransforms.Count; i++)
            {
                var t = _partTransforms[i];
                if (!t || t == transform) continue;
                var r = t.GetComponent<MeshRenderer>();
                if (r) r.sharedMaterials = mats;
            }
        }

        // The domain material is swapped by ShipHelper at spawn AND on any later domain change,
        // neither of which raises an event this component can bind to, so it is watched.
        void LateUpdate() => PropagateMaterials();

        void HideLegacyModel()
        {
            if (!legacyModelRoot) return;
            var renderers = legacyModelRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].transform.IsChildOf(transform)) continue;
                renderers[i].enabled = false;
            }
        }
    }
}
