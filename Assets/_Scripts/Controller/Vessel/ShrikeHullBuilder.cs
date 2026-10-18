using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Shrike's procedural hull (design: <c>R_VesselActions/SHRIKE.md</c> §2). The GEOMETRY is
    /// <see cref="ShrikeHullForm"/>, a pure function; this component owns only what needs the scene: the hull mesh on
    /// this object, the engine flame on a child, the muzzle the lance fires from, the element morph, and its own
    /// materials mirrored onto the flame.
    ///
    /// <para><b>Channel discipline.</b> This component writes MESH VERTICES (the element morph: a cheap full rebuild of
    /// a few hundred vertices, only on a change of weights) and the muzzle's and flame's rest positions.
    /// <see cref="ShrikeAnimation"/> owns every other transform write (the flame's length, the hull's bank).</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour onto slot <b>1</b>, so
    /// the plating is submesh 0 and the accents submesh 1.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ShrikeHullBuilder : MonoBehaviour, IProceduralElementMorphSource, IProceduralHullSource
    {
        [Header("Fuselage (world units, +Z forward)")]
        [SerializeField, Min(0.1f)] float bodyRadius = 1.1f;
        [SerializeField, Min(0.5f)] float bodyLength = 7f;

        [Header("Void Lance barrel (under the nose)")]
        [Tooltip("Charge thickens it.")]
        [SerializeField, Min(0.02f)] float barrelRadius = 0.21f;
        [Tooltip("Space lengthens it.")]
        [SerializeField, Min(0.5f)] float barrelLength = 7.5f;

        [Header("Wings")]
        [Tooltip("Tip to root. Time spreads them.")]
        [SerializeField, Min(0.5f)] float wingSpan = 4.4f;
        [SerializeField, Min(0.1f)] float wingChord = 1.4f;

        [Header("Engine")]
        [SerializeField, Min(0.05f)] float flameRadius = 0.55f;
        [SerializeField, Min(0.1f)] float flameLength = 1.6f;

        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;
        Mesh _hullMesh, _flameMesh;
        Transform _flame, _muzzle;
        readonly List<Material> _materialWatchScratch = new();
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };

        static readonly Element[] MorphedElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        public IReadOnlyList<Element> ProceduralMorphElements => MorphedElements;
        /// <summary>The Shrike carries no inherited model.</summary>
        public Transform HiddenLegacyModelRoot => null;

        /// <summary>The engine flame (its root at the tail; its z scale is its length).</summary>
        public Transform Flame => _flame;
        /// <summary>The barrel's front, where the lance's beam starts.</summary>
        public Transform Muzzle => _muzzle;

        public ShrikeHullForm.Settings CollectSettings()
        {
            var s = ShrikeHullForm.Defaults;
            s.BodyRadius = bodyRadius;
            s.BodyLength = bodyLength;
            s.BarrelRadius = barrelRadius;
            s.BarrelLength = barrelLength;
            s.WingSpan = wingSpan;
            s.WingChord = wingChord;
            s.FlameRadius = flameRadius;
            s.FlameLength = flameLength;
            return s;
        }

        // IProceduralHullSource — what a harvester reading the prefab ASSET (the toybox's mini hulls, the codex's
        // portraits) sees instead of an empty MeshFilter: this ship at rest, flame out.
        public void BuildPreviewPieces(List<ProceduralHullPiece> into)
        {
            var m = ShrikeHullForm.Hull(CollectSettings());
            into.Add(new ProceduralHullPiece
            {
                Name = "ShrikeHull",
                LocalPosition = Vector3.zero,
                Vertices = m.Verts.ToArray(),
                Normals = m.Normals.ToArray(),
                Uvs = m.Uvs.ToArray(),
                Submeshes = new[] { m.Body.ToArray(), m.Accent.ToArray() },
            });
        }

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        /// <summary>Right-click the component to preview the shape without entering play mode. Runtime always rebuilds
        /// in <see cref="Awake"/>, so a stale preview cannot ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            Emit(CollectSettings());
            PropagateMaterials(force: true);
        }

        /// <summary>Blend the hull to the given element weights (each 0..1: charge, mass, space, time). Cheap when nothing
        /// changed. Driven from <see cref="ShrikeAnimation"/>'s LateUpdate, whose tweens carry the fleet's morph feel.</summary>
        public void ApplyElementMorphWeights(float charge, float mass, float space, float time)
        {
            if (Mathf.Approximately(charge, _appliedWeights[0]) && Mathf.Approximately(mass, _appliedWeights[1])
                && Mathf.Approximately(space, _appliedWeights[2]) && Mathf.Approximately(time, _appliedWeights[3]))
                return;
            _appliedWeights[0] = charge;
            _appliedWeights[1] = mass;
            _appliedWeights[2] = space;
            _appliedWeights[3] = time;
            Emit(ShrikeHullForm.Morph(CollectSettings(), charge, mass, space, time));
        }

        void Emit(ShrikeHullForm.Settings s)
        {
            if (!_hullMesh) _hullMesh = new Mesh { name = "Shrike_Hull" };
            Fill(_hullMesh, ShrikeHullForm.Hull(s));
            GetComponent<MeshFilter>().sharedMesh = _hullMesh;

            _flame = Child("ShrikeFlame", ShrikeHullForm.FlameRoot(s), withRenderer: true);
            if (!_flameMesh) _flameMesh = new Mesh { name = "Shrike_Flame" };
            Fill(_flameMesh, ShrikeHullForm.Flame(s));
            _flame.GetComponent<MeshFilter>().sharedMesh = _flameMesh;

            _muzzle = Child("Muzzle", ShrikeHullForm.Muzzle(s), withRenderer: false);
        }

        Transform Child(string childName, Vector3 localPosition, bool withRenderer)
        {
            var t = transform.Find(childName);
            if (!t)
            {
                var go = new GameObject(childName) { layer = gameObject.layer };
                t = go.transform;
                t.SetParent(transform, false);
                if (withRenderer)
                {
                    go.AddComponent<MeshFilter>();
                    go.AddComponent<MeshRenderer>();
                }
            }
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            return t;
        }

        static void Fill(Mesh mesh, ShrikeHullForm.MeshData m)
        {
            mesh.Clear();
            mesh.SetVertices(m.Verts);
            mesh.SetNormals(m.Normals);
            mesh.SetUVs(0, m.Uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(m.Body, ShrikeHullForm.BodySubmesh);
            mesh.SetTriangles(m.Accent, ShrikeHullForm.AccentSubmesh);
            mesh.RecalculateBounds();
        }

        /// <summary>Keep the flame wearing this renderer's materials. The domain colour is painted onto THIS object at
        /// spawn and on any later domain change, neither of which raises an event, so it is watched — allocation free.</summary>
        void PropagateMaterials(bool force = false)
        {
            var source = _sourceRenderer ? _sourceRenderer : (_sourceRenderer = GetComponent<MeshRenderer>());
            if (!source || !_flame) return;
            source.GetSharedMaterials(_materialWatchScratch);
            if (_materialWatchScratch.Count == 0) return;
            Material domain = _materialWatchScratch.Count > 1 ? _materialWatchScratch[1] : _materialWatchScratch[0];
            if (!force && domain == _lastDomainMaterial) return;
            _lastDomainMaterial = domain;
            if (_flame.TryGetComponent(out MeshRenderer flameRenderer)) flameRenderer.sharedMaterials = source.sharedMaterials;
        }

        void LateUpdate() => PropagateMaterials();

        void OnDestroy()
        {
            if (_hullMesh) Destroy(_hullMesh);
            if (_flameMesh) Destroy(_flameMesh);
        }
    }
}
