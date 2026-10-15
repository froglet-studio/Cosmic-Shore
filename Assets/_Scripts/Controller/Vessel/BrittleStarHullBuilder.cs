using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Brittle Star's procedural hull (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §2). The GEOMETRY is
    /// <see cref="BrittleStarHullForm"/>, a pure function this component feeds its serialized proportions into. This
    /// class owns only what needs the scene: emitting the parts as meshes on named children, mirroring its own
    /// materials onto them, blending the element morphs, and keeping the six muzzles where the guns are.
    /// It is <see cref="StoatHullBuilder"/>'s shape on purpose.
    ///
    /// <para><b>Channel discipline.</b> This component writes MESH VERTICES (the element morph), each child's rest
    /// pose once at emit, and the muzzles' local positions (they ride the barrels the morph lengthens).
    /// <see cref="BrittleStarAnimation"/> owns every other transform write — the ring's spin, the flames' length,
    /// the ring's presence (scale) — so no channel has two writers.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour onto slot <b>1</b>
    /// of the object in <c>VesselCustomization._shipGeometries</c> — this one — so the plating is submesh 0 and the
    /// accents (nozzles, canopy, flames) submesh 1.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BrittleStarHullBuilder : MonoBehaviour, IProceduralElementMorphSource, IProceduralHullSource
    {
        [Header("Cockpit (world units, +Z forward)")]
        [Tooltip("Half-width of the cockpit hub. Mass grows it (the Fabricator's larger cockpit).")]
        [SerializeField, Min(0.05f)] float cockpitRadius = 0.62f;
        [Tooltip("Tail face to nose tip.")]
        [SerializeField, Min(0.1f)] float cockpitLength = 2.1f;
        [Tooltip("Where the hub's tail sits along the body axis; the cockpit flame roots here.")]
        [SerializeField] float cockpitTailZ = -0.9f;

        [Header("Support ring")]
        [Tooltip("Centre to each point of the pentagon.")]
        [SerializeField, Min(0.5f)] float ringRadius = 6f;
        [Tooltip("The ring's plane along the body axis.")]
        [SerializeField] float ringZ = -0.35f;
        [SerializeField, Min(0.01f)] float strutThickness = 0.22f;
        [Tooltip("Beams per side of the pentagon. Topology: Time curves them into an arc (the Zephyr's circle).")]
        [SerializeField, Range(1, 12)] int strutSegments = 6;
        [SerializeField, Min(0.01f)] float armThickness = 0.14f;

        [Header("Engines and guns (one per point)")]
        [Tooltip("Engine pod half-width. Charge grows the pods and guns (Rolling Thunder).")]
        [SerializeField, Min(0.02f)] float podRadius = 0.26f;
        [SerializeField, Min(0.1f)] float podLength = 1.0f;
        [Tooltip("The pods' tail plane; every point's flame roots here.")]
        [SerializeField] float podTailZ = -0.85f;
        [SerializeField, Min(0.01f)] float barrelRadius = 0.07f;
        [Tooltip("Pod front to muzzle. Space elongates it (the Hunter's long barrels).")]
        [SerializeField, Min(0.05f)] float barrelLength = 0.75f;
        [SerializeField, Min(0.01f)] float flameRadius = 0.2f;

        BrittleStarHullForm.MorphSet _morphSet;
        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;
        readonly List<Mesh> _partMeshes = new();
        readonly List<Vector3> _blendVerts = new();
        readonly List<Vector3> _blendNormals = new();
        readonly List<Material> _materialWatchScratch = new();
        readonly float[] _blendWeights = new float[4];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };

        Transform _ring;
        readonly Transform[] _flames = new Transform[BrittleStarHullForm.Points + 1];   // [0] cockpit, [1..5] points
        readonly Transform[] _muzzles = new Transform[BrittleStarHullForm.Points + 1];  // [0] cockpit, [1..5] points
        Mesh _ringMesh;

        /// <summary>The support ring (struts, arms, pods, guns). Spins about its local Z with the firing rotation;
        /// Ring Detonation hides it and throws a copy of <see cref="RingMesh"/>.</summary>
        public Transform Ring => _ring;
        /// <summary>The ring's live (morphed) mesh, in the ring's local space.</summary>
        public Mesh RingMesh => _ringMesh;
        /// <summary>The cockpit flame (index -1) or point <paramref name="index"/>'s flame.</summary>
        public Transform Flame(int index) => _flames[index + 1];
        /// <summary>The cockpit gun's muzzle (index -1) or point <paramref name="index"/>'s, which ride the ring.</summary>
        public Transform Muzzle(int index) => _muzzles[index + 1];
        public float RingRadius => ringRadius;
        /// <summary>Half the support ring's thickness across its plane (a strut's half-width) — what Ring Detonation's
        /// disk is as thick as.</summary>
        public float RingHalfThickness => strutThickness;

        static readonly Element[] MorphedElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        public IReadOnlyList<Element> ProceduralMorphElements => MorphedElements;
        /// <summary>The Brittle Star carries no inherited model.</summary>
        public Transform HiddenLegacyModelRoot => null;

        public BrittleStarHullForm.Settings CollectSettings() => new()
        {
            CockpitRadius = cockpitRadius,
            CockpitLength = cockpitLength,
            CockpitTailZ = cockpitTailZ,
            RingRadius = ringRadius,
            RingZ = ringZ,
            StrutThickness = strutThickness,
            StrutSegments = strutSegments,
            Roundness = 0f,
            ArmThickness = armThickness,
            PodRadius = podRadius,
            PodLength = podLength,
            PodTailZ = podTailZ,
            BarrelRadius = barrelRadius,
            BarrelLength = barrelLength,
            FlameRadius = flameRadius,
        };

        // IProceduralHullSource — what a harvester reading the prefab ASSET (the toybox's mini hulls, the codex's
        // portraits) sees instead of an empty MeshFilter: this ship at rest, flames out.
        public void BuildPreviewPieces(List<ProceduralHullPiece> into)
        {
            var parts = BrittleStarHullForm.Generate(CollectSettings());
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.Kind == BrittleStarHullForm.PartKind.Flame) continue;
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

        /// <summary>Part 0 (the cockpit) renders on THIS GameObject in hull space; every other part is a child
        /// re-seated on its pivot.</summary>
        static Vector3 PivotShift(int index, BrittleStarHullForm.Part part) => index == 0 ? Vector3.zero : part.Pivot;

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        /// <summary>Right-click the component to preview the shape without entering play mode. Runtime always
        /// rebuilds in <see cref="Awake"/>, so a stale preview cannot ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            // The base hull and the four element extremes in one pass; the topology assert throws here, loudly.
            _morphSet = BrittleStarHullForm.BakeMorphSet(CollectSettings());
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            EmitParts(_morphSet.BaseParts);
            PlaceMuzzles(CollectSettings());
        }

        void EmitParts(List<BrittleStarHullForm.Part> parts)
        {
            _partMeshes.Clear();
            _ring = null;

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var local = new List<Vector3>(part.Verts.Count);
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - pivotShift);

                var mesh = new Mesh { name = "BrittleStar_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Body, BrittleStarHullForm.BodySubmesh);
                mesh.SetTriangles(part.Accent, BrittleStarHullForm.AccentSubmesh);
                // Pinned to the bake's union over every element-weight combination, so a morph write never pays
                // a recalculation and can never shrink culling.
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
                    // Flames on the points ride the ring (it spins and is thrown); the cockpit's sits on the hull.
                    Transform parent = part.Kind == BrittleStarHullForm.PartKind.Flame && part.Index >= 0 && _ring
                        ? _ring : transform;
                    t = parent.Find(part.Name);
                    if (!t)
                    {
                        var go = new GameObject(part.Name) { layer = gameObject.layer };
                        t = go.transform;
                        t.SetParent(parent, false);
                        go.AddComponent<MeshFilter>();
                        go.AddComponent<MeshRenderer>();
                    }
                    // Geometry is in hull space about the pivot; under the ring the pivot is ring-relative.
                    t.localPosition = parent == transform ? part.Pivot : part.Pivot - _ring.localPosition;
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one;
                    t.GetComponent<MeshFilter>().sharedMesh = mesh;
                }

                switch (part.Kind)
                {
                    case BrittleStarHullForm.PartKind.Ring:
                        _ring = t;
                        _ringMesh = mesh;
                        break;
                    case BrittleStarHullForm.PartKind.Flame:
                        _flames[part.Index + 1] = t;
                        break;
                }
            }
            PropagateMaterials(force: true);
        }

        /// <summary>Empty transforms at the six muzzles: the cockpit's on the hull, the points' on the ring.</summary>
        void PlaceMuzzles(BrittleStarHullForm.Settings s)
        {
            for (int i = -1; i < BrittleStarHullForm.Points; i++)
            {
                Transform parent = i < 0 || !_ring ? transform : _ring;
                string name = i < 0 ? "Muzzle_C" : "Muzzle_" + i;
                var m = parent.Find(name);
                if (!m)
                {
                    m = new GameObject(name) { layer = gameObject.layer }.transform;
                    m.SetParent(parent, false);
                }
                Vector3 hullPos = BrittleStarHullForm.MuzzlePosition(s, i);
                m.localPosition = parent == transform ? hullPos : hullPos - _ring.localPosition;
                m.localRotation = Quaternion.identity;
                _muzzles[i + 1] = m;
            }
        }

        /// <summary>
        /// Blend the hull to the given element weights (each 0..1: charge, mass, space, time). Cheap when nothing
        /// changed; only parts an element moves are rewritten, bounds untouched. Driven from
        /// <see cref="BrittleStarAnimation"/>'s LateUpdate, whose tweens carry the fleet's morph feel.
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
                BrittleStarHullForm.BlendPart(_morphSet, p, w, _blendVerts, _blendNormals);
                Vector3 pivotShift = PivotShift(p, _morphSet.BaseParts[p]);
                if (pivotShift != Vector3.zero)
                    for (int i = 0; i < _blendVerts.Count; i++) _blendVerts[i] -= pivotShift;
                mesh.SetVertices(_blendVerts, 0, _blendVerts.Count, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetNormals(_blendNormals, 0, _blendNormals.Count, MeshUpdateFlags.DontRecalculateBounds);
            }

            // The muzzles ride the barrels: Space lengthens them, Charge lengthens the pods they sit on.
            var s = CollectSettings();
            var spaceX = BrittleStarHullForm.Extreme(s, Element.Space);
            var chargeX = BrittleStarHullForm.Extreme(s, Element.Charge);
            var massX = BrittleStarHullForm.Extreme(s, Element.Mass);
            s.BarrelLength += (spaceX.BarrelLength - s.BarrelLength) * w[2];
            s.PodLength += (chargeX.PodLength - s.PodLength) * w[0];
            s.CockpitLength += (massX.CockpitLength - s.CockpitLength) * w[1];
            PlaceMuzzles(s);
        }

        /// <summary>Keep every child part wearing this renderer's materials. The domain colour is painted onto THIS
        /// object (it is the one in <c>_shipGeometries</c>) at spawn and on any later domain change, neither of which
        /// raises an event, so it is watched — allocation free — rather than pushed.</summary>
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
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != source) renderers[i].sharedMaterials = mats;
        }

        void LateUpdate() => PropagateMaterials();
    }
}
