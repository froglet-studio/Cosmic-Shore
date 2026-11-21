using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Thresher's procedural hull (design: <c>R_VesselActions/THRESHER.md</c> §5b) — the
    /// thresher shark that replaced the Squirrel model the vessel was cloned from. The GEOMETRY is
    /// <see cref="ThresherHullForm"/>, a pure function this component feeds its serialized proportions
    /// into, compiled and RUN offline by <c>Tools/Build/thresher_hull_harness</c>. This class owns only
    /// what needs the scene: emitting the parts as meshes on named children, mirroring its own materials
    /// onto them, and blending the element morphs. It is <see cref="StoatHullBuilder"/>'s shape on purpose.
    ///
    /// <para><b>Channel discipline.</b> This component writes MESH VERTICES (the element morph) and, once
    /// at emit, each child's rest pose. <see cref="ThresherAnimation"/> owns every transform after that —
    /// the swim's sway on this GameObject, the pectorals' bank and the tail's sweep. No channel has two
    /// writers.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour onto
    /// slot <b>1</b> of the object in <c>VesselCustomization._shipGeometries</c> — this one — so the hide
    /// is submesh 0 and the accents (dorsal fin, eyes, the scythe's blade, the hitch) submesh 1.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ThresherHullBuilder : MonoBehaviour, IProceduralElementMorphSource, IProceduralHullSource
    {
        [Header("Body (world units, +Z forward)")]
        [Tooltip("Faces around the body. 6 = a ridge along the back for the dorsal and a keel underneath. Topology: never driven by an element.")]
        [SerializeField, Range(4, 10)] int sides = 6;
        [Tooltip("Snout tip to the caudal peduncle (the tail is extra).")]
        [SerializeField, Min(0.5f)] float bodyLength = 4.4f;
        [Tooltip("Half-width at the girthiest station, a third of the way back.")]
        [SerializeField, Min(0.05f)] float bodyRadius = 0.62f;
        [Tooltip("Height / width of the cross-section. Below 1 is a little flatter than round.")]
        [SerializeField, Range(0.4f, 1.5f)] float bodyFlatten = 0.86f;
        [Tooltip("The eyes are the face's domain-coloured accent.")]
        [SerializeField, Min(0.01f)] float eyeSize = 0.085f;

        [Header("Fins")]
        [Tooltip("Every fin's thickness at its root; the edges are knives.")]
        [SerializeField, Min(0.005f)] float finThickness = 0.08f;
        [Tooltip("The dorsal fin's height (domain colour). Charge raises it.")]
        [SerializeField, Min(0.05f)] float dorsalHeight = 0.95f;
        [SerializeField, Min(0.05f)] float dorsalChord = 1.05f;
        [Tooltip("How far back the dorsal's tip leans, per unit of height.")]
        [SerializeField, Min(0f)] float dorsalSweep = 0.55f;
        [Tooltip("Pectoral span from the flank. Time spreads them.")]
        [SerializeField, Min(0.05f)] float pectoralSpan = 1.35f;
        [SerializeField, Min(0.05f)] float pectoralChord = 0.9f;
        [Tooltip("How far back the pectoral tip sits, per unit of span.")]
        [SerializeField, Min(0f)] float pectoralSweep = 0.75f;
        [Tooltip("Degrees the pectorals droop below horizontal at rest.")]
        [SerializeField, Range(-30f, 60f)] float pectoralDroop = 18f;

        [Header("Tail")]
        [Tooltip("The tail base, body to the caudal fork, where the hitch hangs.")]
        [SerializeField, Min(0.05f)] float peduncleLength = 0.5f;
        [SerializeField, Min(0.05f)] float lowerLobeLength = 0.75f;
        [Tooltip("Hinged segments of the upper lobe (the scythe). Topology: never driven by an element.")]
        [SerializeField, Range(1, 6)] int lobeSegments = 3;
        [Tooltip("The scythe along its curve — a thresher's tail is nearly as long as its body. Space draws it out.")]
        [SerializeField, Min(0.1f)] float lobeLength = 3.6f;
        [Tooltip("The scythe's width at its root; it tapers to a point.")]
        [SerializeField, Min(0.05f)] float lobeChord = 0.62f;
        [Tooltip("Degrees above straight back at the root.")]
        [SerializeField, Range(0f, 80f)] float lobeRise = 24f;
        [Tooltip("Extra degrees of rise by the tip: the curl that makes it a scythe.")]
        [SerializeField, Range(0f, 80f)] float lobeCurl = 22f;
        [Tooltip("0..1: the trailing share of the scythe's chord drawn in the domain colour. Charge widens it.")]
        [SerializeField, Range(0f, 1f)] float lobeBlade = 0.4f;

        [Header("Hitch (where the chain leaves the tail)")]
        [SerializeField, Min(0.02f)] float hitchRadius = 0.2f;
        [SerializeField, Min(0.005f)] float hitchThickness = 0.055f;

        ThresherHullForm.MorphSet _morphSet;
        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;
        Transform _hitch;
        readonly List<Mesh> _partMeshes = new();
        readonly List<Vector3> _blendVerts = new();
        readonly List<Vector3> _blendNormals = new();
        readonly List<Material> _materialWatchScratch = new();
        readonly float[] _blendWeights = new float[4];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };

        // The animated parts, by kind, in emit order — what ThresherAnimation drives.
        readonly List<Transform> _pectorals = new();   // left, right
        readonly List<Transform> _lobes = new();       // root segment first
        Transform _tailBase;

        /// <summary>Left then right pectoral.</summary>
        public IReadOnlyList<Transform> Pectorals => _pectorals;
        /// <summary>The scythe's segments, root first; each is a child of THIS transform hinged where the
        /// previous ends (siblings, not a chain, so the animation composes the wave itself).</summary>
        public IReadOnlyList<Transform> Lobes => _lobes;
        /// <summary>The peduncle, lower lobe and hitch.</summary>
        public Transform TailBase => _tailBase;
        /// <summary>Where the chain is drawn from: the hitch ring's centre, riding the tail base's sway.
        /// Falls back to this transform before the first build.</summary>
        public Vector3 HitchWorldPosition => _hitch ? _hitch.position : transform.position;

        static readonly Element[] MorphedElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        public IReadOnlyList<Element> ProceduralMorphElements => MorphedElements;
        /// <summary>The Squirrel model this vessel was cloned from is GONE from the prefab (2026-10-13), so
        /// there is nothing hidden for an audit or a harvester to skip.</summary>
        public Transform HiddenLegacyModelRoot => null;

        public ThresherHullForm.Settings CollectSettings() => new()
        {
            Sides = sides,
            BodyLength = bodyLength,
            BodyRadius = bodyRadius,
            BodyFlatten = bodyFlatten,
            EyeSize = eyeSize,
            FinThickness = finThickness,
            DorsalHeight = dorsalHeight,
            DorsalChord = dorsalChord,
            DorsalSweep = dorsalSweep,
            PectoralSpan = pectoralSpan,
            PectoralChord = pectoralChord,
            PectoralSweep = pectoralSweep,
            PectoralDroop = pectoralDroop,
            PeduncleLength = peduncleLength,
            LowerLobeLength = lowerLobeLength,
            LobeSegments = lobeSegments,
            LobeLength = lobeLength,
            LobeChord = lobeChord,
            LobeRise = lobeRise,
            LobeCurl = lobeCurl,
            LobeBlade = lobeBlade,
            HitchRadius = hitchRadius,
            HitchThickness = hitchThickness,
        };

        // IProceduralHullSource — what a harvester reading the prefab ASSET (the toybox's mini hulls, the
        // codex's portraits) sees instead of an empty MeshFilter: this ship at rest.
        public void BuildPreviewPieces(List<ProceduralHullPiece> into)
        {
            var parts = ThresherHullForm.Generate(CollectSettings());
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

        /// <summary>Part 0 (the body) renders on THIS GameObject in hull space; every other part is a
        /// child re-seated on its pivot.</summary>
        static Vector3 PivotShift(int index, ThresherHullForm.Part part) => index == 0 ? Vector3.zero : part.Pivot;

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        /// <summary>Right-click the component to preview the shape without entering play mode. Runtime
        /// always rebuilds in <see cref="Awake"/>, so a stale preview cannot ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            // The base hull and the four element extremes in one pass; the topology assert throws here,
            // loudly, if an element ever drives an int.
            var settings = CollectSettings();
            _morphSet = ThresherHullForm.BakeMorphSet(settings);
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            EmitParts(_morphSet.BaseParts);
            SeatHitch(settings);
        }

        void EmitParts(List<ThresherHullForm.Part> parts)
        {
            _partMeshes.Clear();
            _pectorals.Clear();
            _lobes.Clear();
            _tailBase = null;

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var local = new List<Vector3>(part.Verts.Count);
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - pivotShift);

                var mesh = new Mesh { name = "Thresher_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Body, ThresherHullForm.BodySubmesh);
                mesh.SetTriangles(part.Accent, ThresherHullForm.AccentSubmesh);
                // Pinned to the bake's union over every element-weight combination, so a morph write never
                // pays a recalculation and can never shrink culling.
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
                    case ThresherHullForm.PartKind.Pectoral: _pectorals.Add(t); break;
                    case ThresherHullForm.PartKind.TailBase: _tailBase = t; break;
                    case ThresherHullForm.PartKind.Lobe: _lobes.Add(t); break;
                }
            }
            PropagateMaterials(force: true);
        }

        /// <summary>An empty marker at the hitch ring's centre, under the tail base so it sways with it —
        /// the executor draws the chain from it (<see cref="HitchWorldPosition"/>).</summary>
        void SeatHitch(in ThresherHullForm.Settings settings)
        {
            if (!_tailBase) return;
            if (!_hitch)
            {
                _hitch = _tailBase.Find("ThresherHitch");
                if (!_hitch)
                {
                    _hitch = new GameObject("ThresherHitch").transform;
                    _hitch.SetParent(_tailBase, false);
                }
            }
            _hitch.localPosition = ThresherHullForm.HitchCenter(settings) - _tailBase.localPosition;
            _hitch.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Blend the hull to the given element weights (each 0..1: charge, mass, space, time). Cheap when
        /// nothing changed; only parts an element moves are rewritten, bounds untouched. Driven from
        /// <see cref="ThresherAnimation"/>'s LateUpdate, whose tweens carry the fleet's morph feel.
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
                ThresherHullForm.BlendPart(_morphSet, p, w, _blendVerts, _blendNormals);
                Vector3 pivotShift = PivotShift(p, _morphSet.BaseParts[p]);
                if (pivotShift != Vector3.zero)
                    for (int i = 0; i < _blendVerts.Count; i++) _blendVerts[i] -= pivotShift;
                mesh.SetVertices(_blendVerts, 0, _blendVerts.Count, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetNormals(_blendNormals, 0, _blendNormals.Count, MeshUpdateFlags.DontRecalculateBounds);
            }
        }

        /// <summary>Keep every child part wearing this renderer's materials. The domain colour is painted
        /// onto THIS object (it is the one in <c>_shipGeometries</c>) at spawn and on any later domain
        /// change, neither of which raises an event, so it is watched — allocation free — rather than
        /// pushed.</summary>
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

        void OnDestroy()
        {
            // The meshes are this component's own (minted in EmitParts), so they go with it.
            for (int i = 0; i < _partMeshes.Count; i++)
                if (_partMeshes[i])
                {
                    if (Application.isPlaying) Destroy(_partMeshes[i]);
                    else DestroyImmediate(_partMeshes[i]);
                }
            _partMeshes.Clear();
        }
    }
}
