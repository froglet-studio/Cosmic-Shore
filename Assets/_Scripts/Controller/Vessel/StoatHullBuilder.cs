using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Stoat's procedural hull (design: <c>R_VesselActions/STOAT.md</c> §2). The GEOMETRY
    /// is <see cref="StoatHullForm"/> — a pure function this component feeds its serialized
    /// proportions into, compiled and RUN offline by <c>Tools/Build/stoat_hull_harness</c>. This
    /// class owns only what needs the scene: emitting the parts as meshes on named children,
    /// mirroring its own materials onto them, blending the element morphs, and hiding the Squirrel
    /// model the prefab was cloned from. It is <see cref="ButterflyHullBuilder"/>'s shape on purpose.
    ///
    /// <para><b>Channel discipline.</b> This component writes MESH VERTICES (the element morph)
    /// and, once at emit, each child's rest pose. <see cref="StoatAnimation"/> owns every
    /// transform after that — the lope's body lift, pitch and stretch on this GameObject, the
    /// spine's arch on the plates, the swing of the legs and tail. No channel has two writers.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour
    /// onto slot <b>1</b> of the object in <c>VesselCustomization._shipGeometries</c> — this one —
    /// so the plates are submesh 0 and the accents (ridge fins, eyes, tail crystal) submesh 1.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class StoatHullBuilder : MonoBehaviour, IProceduralElementMorphSource, IProceduralHullSource
    {
        [Header("Body (world units, +Z forward)")]
        [Tooltip("Body plates from the chest to the hip. Topology: never driven by an element.")]
        [SerializeField, Range(2, 12)] int segments = 5;
        [Tooltip("Faces around the body. 4 = the diamond plate the user chose (a ridge along the spine).")]
        [SerializeField, Range(3, 8)] int sides = 4;
        [Tooltip("Front of the chest plate to the back of the hip plate.")]
        [SerializeField, Min(0.5f)] float bodyLength = 4f;
        [Tooltip("Half-width of the chest plate.")]
        [SerializeField, Min(0.05f)] float chestRadius = 0.45f;
        [Tooltip("Half-width of the hip plate. A stoat narrows to the hip.")]
        [SerializeField, Min(0.05f)] float hipRadius = 0.26f;
        [Tooltip("Each plate's back face as a fraction of its front — the overlap that makes plates read as plates.")]
        [SerializeField, Range(0.5f, 1f)] float plateTaper = 0.9f;

        [Header("Head")]
        [Tooltip("Half-width of the wedge head where it meets the chest.")]
        [SerializeField, Min(0.05f)] float headRadius = 0.43f;
        [Tooltip("Neck to snout tip.")]
        [SerializeField, Min(0.1f)] float headLength = 1.1f;
        [SerializeField, Min(0.01f)] float earSize = 0.16f;
        [Tooltip("The eyes are the face's one domain-coloured accent.")]
        [SerializeField, Min(0.01f)] float eyeSize = 0.075f;

        [Header("Tail")]
        [SerializeField, Range(1, 8)] int tailSegments = 4;
        [SerializeField, Min(0.1f)] float tailLength = 2.1f;
        [SerializeField, Min(0.01f)] float tailRootRadius = 0.16f;
        [SerializeField, Min(0.01f)] float tailTipRadius = 0.08f;
        [Tooltip("Half-width of the domain-coloured crystal at the tail tip.")]
        [SerializeField, Min(0.01f)] float crystalSize = 0.22f;
        [Tooltip("The crystal's length as a multiple of its half-width.")]
        [SerializeField, Min(0.5f)] float crystalStretch = 1.7f;

        [Header("Legs and ridge")]
        [SerializeField, Min(0.01f)] float legLength = 0.26f;
        [SerializeField, Min(0.01f)] float legThickness = 0.07f;
        [Tooltip("Dorsal fin height on every other plate, as a fraction of that plate's radius. Charge raises it.")]
        [SerializeField, Min(0.01f)] float finHeight = 0.9f;
        [Tooltip("Dorsal fin base half-width, as a fraction of the plate radius.")]
        [SerializeField, Min(0.01f)] float finWidth = 0.32f;

        [Header("Legacy model")]
        [Tooltip("Root of the inherited model (the Squirrel's) whose RENDERERS this component switches off. " +
                 "Never its GameObjects: the vessel's colliders live on that subtree.")]
        [SerializeField] Transform legacyModelRoot;

        StoatHullForm.MorphSet _morphSet;
        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;
        readonly List<Mesh> _partMeshes = new();
        readonly List<Vector3> _blendVerts = new();
        readonly List<Vector3> _blendNormals = new();
        readonly List<Material> _materialWatchScratch = new();
        readonly float[] _blendWeights = new float[4];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };

        // The animated parts, by kind, in emit order — what StoatAnimation drives.
        readonly List<Transform> _plates = new();      // every body plate INCLUDING the core, nose plate first
        readonly List<Transform> _tail = new();        // tail plates root first, then the crystal tip
        readonly List<Transform> _legs = new();        // FL, FR, BL, BR
        Transform _head;

        /// <summary>Every body plate, nose plate first; the core plate is this transform itself.</summary>
        public IReadOnlyList<Transform> Plates => _plates;
        /// <summary>Tail plates root first, the crystal tip last.</summary>
        public IReadOnlyList<Transform> Tail => _tail;
        /// <summary>Front-left, front-right, back-left, back-right.</summary>
        public IReadOnlyList<Transform> Legs => _legs;
        public Transform Head => _head;
        public int CoreIndex => StoatHullForm.CoreIndex(segments);
        public int SegmentCount => segments;
        /// <summary>The authored body length (chest plate front to hip plate back), world units.</summary>
        public float BodyLength => bodyLength;

        static readonly Element[] MorphedElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        public IReadOnlyList<Element> ProceduralMorphElements => MorphedElements;
        public Transform HiddenLegacyModelRoot => legacyModelRoot;

        public StoatHullForm.Settings CollectSettings() => new()
        {
            Segments = segments,
            Sides = sides,
            BodyLength = bodyLength,
            ChestRadius = chestRadius,
            HipRadius = hipRadius,
            PlateTaper = plateTaper,
            HeadRadius = headRadius,
            HeadLength = headLength,
            EarSize = earSize,
            EyeSize = eyeSize,
            TailSegments = tailSegments,
            TailLength = tailLength,
            TailRootRadius = tailRootRadius,
            TailTipRadius = tailTipRadius,
            CrystalSize = crystalSize,
            CrystalStretch = crystalStretch,
            LegLength = legLength,
            LegThickness = legThickness,
            FinHeight = finHeight,
            FinWidth = finWidth,
        };

        // IProceduralHullSource — what a harvester reading the prefab ASSET (the toybox's mini hulls,
        // the codex's portraits) sees instead of an empty MeshFilter: this ship at rest.
        public void BuildPreviewPieces(List<ProceduralHullPiece> into)
        {
            var parts = StoatHullForm.Generate(CollectSettings());
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

        /// <summary>Part 0 (the core plate) renders on THIS GameObject in hull space; every other
        /// part is a child re-seated on its pivot.</summary>
        static Vector3 PivotShift(int index, StoatHullForm.Part part) => index == 0 ? Vector3.zero : part.Pivot;

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        /// <summary>Right-click the component to preview the shape without entering play mode.
        /// Runtime always rebuilds in <see cref="Awake"/>, so a stale preview cannot ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            // The base hull and the four element extremes in one pass; the topology assert throws
            // here, loudly, if an element ever drives an int.
            _morphSet = StoatHullForm.BakeMorphSet(CollectSettings());
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            EmitParts(_morphSet.BaseParts);
            HideLegacyModel();
        }

        void EmitParts(List<StoatHullForm.Part> parts)
        {
            _partMeshes.Clear();
            _plates.Clear();
            _tail.Clear();
            _legs.Clear();
            _head = null;
            var plateByIndex = new Transform[segments];

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var local = new List<Vector3>(part.Verts.Count);
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - pivotShift);

                var mesh = new Mesh { name = "Stoat_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Body, StoatHullForm.BodySubmesh);
                mesh.SetTriangles(part.Accent, StoatHullForm.AccentSubmesh);
                // Pinned to the bake's union over every element-weight combination, so a morph
                // write never pays a recalculation and can never shrink culling.
                Vector3 bMin = _morphSet.BoundsMin[i] - pivotShift, bMax = _morphSet.BoundsMax[i] - pivotShift;
                mesh.bounds = new Bounds((bMin + bMax) * 0.5f, bMax - bMin);
                _partMeshes.Add(mesh);

                Transform t;
                if (i == 0)
                {
                    GetComponent<MeshFilter>().sharedMesh = mesh;
                    t = transform;
                }
                else
                {
                    t = transform.Find(part.Name);
                    if (!t)
                    {
                        var go = new GameObject(part.Name) { layer = gameObject.layer };
                        t = go.transform;
                        t.SetParent(transform, false);
                        go.AddComponent<MeshFilter>();
                        go.AddComponent<MeshRenderer>();
                    }
                    t.localPosition = part.Pivot;
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one;
                    t.GetComponent<MeshFilter>().sharedMesh = mesh;
                }

                switch (part.Kind)
                {
                    case StoatHullForm.PartKind.Core:
                    case StoatHullForm.PartKind.Segment:
                        if (part.Index >= 0 && part.Index < plateByIndex.Length) plateByIndex[part.Index] = t;
                        break;
                    case StoatHullForm.PartKind.Head: _head = t; break;
                    case StoatHullForm.PartKind.Tail:
                    case StoatHullForm.PartKind.TailTip: _tail.Add(t); break;
                    case StoatHullForm.PartKind.Leg: _legs.Add(t); break;
                }
            }
            _plates.AddRange(plateByIndex);
            PropagateMaterials(force: true);
        }

        /// <summary>
        /// Blend the hull to the given element weights (each 0..1: charge, mass, space, time).
        /// Cheap when nothing changed; only parts an element moves are rewritten, bounds untouched.
        /// Driven from <see cref="StoatAnimation"/>'s LateUpdate, whose tweens carry the fleet's
        /// morph feel.
        /// </summary>
        public void ApplyElementMorphWeights(float charge, float mass, float space, float time)
        {
            if (_morphSet == null) return;
            var w = _blendWeights;
            w[0] = Mathf.Clamp01(charge);
            w[1] = Mathf.Clamp01(mass);
            w[2] = Mathf.Clamp01(space);
            w[3] = Mathf.Clamp01(time);

            bool changed = false;
            for (int i = 0; i < w.Length; i++) changed |= !Mathf.Approximately(w[i], _appliedWeights[i]);
            if (!changed) return;
            for (int i = 0; i < w.Length; i++) _appliedWeights[i] = w[i];

            for (int p = 0; p < _partMeshes.Count && p < _morphSet.BaseParts.Count; p++)
            {
                if (!_morphSet.PartMorphs[p]) continue;
                var mesh = _partMeshes[p];
                if (!mesh) continue;
                StoatHullForm.BlendPart(_morphSet, p, w, _blendVerts, _blendNormals);
                Vector3 pivotShift = PivotShift(p, _morphSet.BaseParts[p]);
                if (pivotShift != Vector3.zero)
                    for (int i = 0; i < _blendVerts.Count; i++) _blendVerts[i] -= pivotShift;
                mesh.SetVertices(_blendVerts, 0, _blendVerts.Count, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetNormals(_blendNormals, 0, _blendNormals.Count, MeshUpdateFlags.DontRecalculateBounds);
            }
        }

        /// <summary>Keep every child part wearing this renderer's materials. The domain colour is
        /// painted onto THIS object (it is the one in <c>_shipGeometries</c>) at spawn and on any
        /// later domain change, neither of which raises an event, so it is watched — allocation
        /// free — rather than pushed.</summary>
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
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).TryGetComponent<MeshRenderer>(out var r)) r.sharedMaterials = mats;
        }

        void LateUpdate() => PropagateMaterials();

        /// <summary>Switch off the inherited model's RENDERERS — never its GameObjects, which carry
        /// the vessel's colliders and serialized references.</summary>
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
