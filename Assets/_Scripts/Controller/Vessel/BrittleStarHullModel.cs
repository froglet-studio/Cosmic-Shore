using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's hull as a MODEL: <c>Assets/_Models/Vessel Models/BrittleStar.fbx</c>, built headlessly by
    /// <c>Tools/Build/vessel_model/specs/brittlestar.py</c> and nested under the prefab's <c>BrittleStarHull</c>
    /// (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §2). It replaced the procedural
    /// <see cref="BrittleStarHullBuilder"/> on 2026-10-18; that class stays in the tree until the model is accepted.
    ///
    /// <para><b>Found by NAME, never wired.</b> An FBX's sub-object fileIDs exist only after Unity imports it, so the
    /// prefab holds the model root and this component finds the parts in <see cref="Awake"/>: the bone <c>ring</c>
    /// (the firing rotation turns it, Ring Detonation throws everything under it), <c>gun.mouth</c> /
    /// <c>gun.1.mouth</c>..<c>gun.5.mouth</c> (the muzzles), <c>jet.flame</c> / <c>jet.1.flame</c>..<c>jet.5.flame</c>
    /// (the plumes) and the <see cref="SkinnedMeshRenderer"/>. <c>arm.N</c> / <c>.N</c> is the placeholder's point N-1.</para>
    ///
    /// <para><b>The gameplay numbers stay the form's.</b> The ring's radius, thickness and plane and every muzzle's
    /// position come from <see cref="BrittleStarHullForm"/> (the same numbers the placeholder emitted), and the muzzles are
    /// re-seated on the model's mouth bones from those positions, so where a round leaves, where a cord roots and how
    /// thick the explosion's disk is cannot drift with the drawing. The model's element shapes travel the form's own
    /// muzzle distances, so the drawn barrels end where the muzzles are at every weight.</para>
    ///
    /// <para><b>Rest-relative.</b> The model's bones rest at orientations of the FBX's choosing, so the ring turns about
    /// the vessel's nose axis expressed in its parent's frame and composed onto its captured rest, and each flame
    /// lengthens along whichever of its local axes runs down the exhaust. Captured once, in <see cref="Awake"/>, at the
    /// imported rest pose and before anything animates.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BrittleStarHullModel : BrittleStarHullSource
    {
        const int Slots = BrittleStarHullForm.Points + 1;   // [0] the cockpit's, [1..5] the points'

        SkinnedMeshRenderer _model;
        Transform _ring;
        Quaternion _ringRestLocal = Quaternion.identity;
        Vector3 _ringRestScale = Vector3.one;
        Vector3 _ringAxisInParent = Vector3.forward;

        readonly Transform[] _flames = new Transform[Slots];
        readonly Vector3[] _flameRestScale = new Vector3[Slots];
        readonly int[] _flameAxis = new int[Slots];
        readonly Transform[] _muzzles = new Transform[Slots];
        readonly Matrix4x4[] _mouthFromHull = new Matrix4x4[Slots];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };

        List<int>[] _ringTriangles;
        readonly List<Mesh> _thrownMeshes = new();
        readonly List<Vector3> _vertexScratch = new();
        bool _resolved;

        // Every entry point resolves first: a hull that starts inactive has not run Awake when the executor asks.
        public override Transform Ring { get { if (!_resolved) Resolve(); return _ring; } }
        public override float RingRadius => BrittleStarHullForm.Defaults.RingRadius;
        public override float RingHalfThickness => BrittleStarHullForm.Defaults.StrutThickness;
        public override float RingPlaneZ => BrittleStarHullForm.Defaults.RingZ;
        public override Transform Muzzle(int index)
        {
            if (!_resolved) Resolve();
            return _muzzles[index + 1];
        }

        /// <summary>The model's renderer: slot 0 Domain, 1 Body, 2 Window, 3 Engine.</summary>
        public SkinnedMeshRenderer Model { get { if (!_resolved) Resolve(); return _model; } }

        public override Material DomainMaterial
        {
            get
            {
                if (!_resolved) Resolve();
                if (!_model) return null;
                var mats = _model.sharedMaterials;
                return mats is { Length: > 0 } ? mats[0] : null;
            }
        }

        void Awake() => Resolve();

        void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _model = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (_model) _model.updateWhenOffscreen = true;   // the ring spins and the barrels grow past the bind bounds
            _ring = FindPart(transform, "ring");
            if (_ring)
            {
                _ringRestLocal = _ring.localRotation;
                _ringRestScale = _ring.localScale;
                Vector3 noseWorld = transform.TransformDirection(Vector3.forward);
                _ringAxisInParent = _ring.parent ? _ring.parent.InverseTransformDirection(noseWorld).normalized : noseWorld;
            }

            Vector3 exhaustWorld = transform.TransformDirection(Vector3.back);
            var settings = BrittleStarHullForm.Defaults;
            int missing = 0;
            for (int i = -1; i < BrittleStarHullForm.Points; i++)
            {
                int slot = i + 1;
                string suffix = i < 0 ? "" : "." + (i + 1);

                var flame = FindPart(transform, "jet" + suffix + ".flame");
                _flames[slot] = flame;
                if (flame)
                {
                    _flameRestScale[slot] = flame.localScale;
                    Vector3 local = flame.InverseTransformDirection(exhaustWorld);
                    float ax = Mathf.Abs(local.x), ay = Mathf.Abs(local.y), az = Mathf.Abs(local.z);
                    _flameAxis[slot] = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;
                }
                else missing++;

                var mouth = FindPart(transform, "gun" + suffix + ".mouth");
                if (mouth)
                {
                    // hull space -> the mouth bone's local space, at rest: the muzzle rides the bone (and so the ring's
                    // spin and presence) but is placed by the form's numbers
                    _mouthFromHull[slot] = mouth.worldToLocalMatrix * transform.localToWorldMatrix;
                    string name = i < 0 ? "Muzzle_C" : "Muzzle_" + i;
                    var m = mouth.Find(name);
                    if (!m)
                    {
                        m = new GameObject(name) { layer = gameObject.layer }.transform;
                        m.SetParent(mouth, false);
                    }
                    _muzzles[slot] = m;
                }
                else missing++;
            }
            if (!_model || !_ring || missing > 0)
                CSDebug.LogWarning($"[BrittleStarHullModel] '{name}': the model under the hull is missing " +
                                   $"{(!_model ? "its SkinnedMeshRenderer, " : "")}{(!_ring ? "the 'ring' bone, " : "")}{missing} " +
                                   "muzzle/flame bone(s). Rebuild it with Tools/Build/vessel_model/build.py brittlestar.");
            PlaceMuzzles(settings);
            CollectRingTriangles();
        }

        static Transform FindPart(Transform root, string partName)
        {
            if (root.name == partName) return root;
            for (int c = 0; c < root.childCount; c++)
            {
                var hit = FindPart(root.GetChild(c), partName);
                if (hit) return hit;
            }
            return null;
        }

        void PlaceMuzzles(in BrittleStarHullForm.Settings s)
        {
            for (int i = -1; i < BrittleStarHullForm.Points; i++)
            {
                var m = _muzzles[i + 1];
                if (!m) continue;
                var fromHull = _mouthFromHull[i + 1];
                m.localPosition = fromHull.MultiplyPoint3x4(BrittleStarHullForm.MuzzlePosition(s, i));
                m.localRotation = fromHull.rotation;
            }
        }

        public override void SetRingPose(float phaseRadians, float presenceScale)
        {
            if (!_resolved) Resolve();
            if (!_ring) return;
            _ring.localRotation = Quaternion.AngleAxis(phaseRadians * Mathf.Rad2Deg, _ringAxisInParent) * _ringRestLocal;
            _ring.localScale = _ringRestScale * presenceScale;
        }

        public override void ResetRingPose()
        {
            if (!_ring) return;
            _ring.localRotation = _ringRestLocal;
            _ring.localScale = _ringRestScale;
        }

        public override void SetFlame(int index, float girth, float length)
        {
            if (!_resolved) Resolve();
            int slot = index + 1;
            var flame = _flames[slot];
            if (!flame) return;
            // the model's plumes are one unit long and the placeholder's width at rest, so the numbers are the same
            Vector3 rest = _flameRestScale[slot];
            Vector3 s = new(rest.x * girth, rest.y * girth, rest.z * girth);
            s[_flameAxis[slot]] = rest[_flameAxis[slot]] * length;
            flame.localScale = s;
        }

        public override void ApplyElementMorphWeights(float charge, float mass, float space, float time)
        {
            // The shapes themselves are VesselAnimation's (blend shapes Charge / Mass / Space / Time); this only keeps
            // the muzzles on the barrels the shapes grow, at the form's own distances.
            if (!_resolved) Resolve();
            if (Mathf.Approximately(charge, _appliedWeights[0]) && Mathf.Approximately(mass, _appliedWeights[1])
                && Mathf.Approximately(space, _appliedWeights[2]))
                return;
            _appliedWeights[0] = charge;
            _appliedWeights[1] = mass;
            _appliedWeights[2] = space;
            _appliedWeights[3] = time;
            var s = BrittleStarHullForm.Defaults;
            var spaceX = BrittleStarHullForm.Extreme(s, Element.Space);
            var chargeX = BrittleStarHullForm.Extreme(s, Element.Charge);
            var massX = BrittleStarHullForm.Extreme(s, Element.Mass);
            s.BarrelLength += (spaceX.BarrelLength - s.BarrelLength) * Mathf.Clamp01(space);
            s.PodLength += (chargeX.PodLength - s.PodLength) * Mathf.Clamp01(charge);
            s.CockpitLength += (massX.CockpitLength - s.CockpitLength) * Mathf.Clamp01(mass);
            PlaceMuzzles(s);
        }

        /// <summary>Per submesh, the triangles skinned to the ring or a bone under it (the model is skinned rigidly,
        /// one bone per part): what a thrown ring draws. Read once; the import keeps the mesh readable.</summary>
        void CollectRingTriangles()
        {
            _ringTriangles = null;
            if (!_model || !_ring) return;
            var mesh = _model.sharedMesh;
            if (!mesh || !mesh.isReadable)
            {
                CSDebug.LogWarning($"[BrittleStarHullModel] '{name}': the model's mesh is not readable, so a thrown ring " +
                                   "cannot be copied from it. Keep isReadable on in BrittleStar.fbx.meta.");
                return;
            }
            var bones = _model.bones;
            var inRing = new bool[bones.Length];
            for (int b = 0; b < bones.Length; b++) inRing[b] = bones[b] && bones[b].IsChildOf(_ring);
            var weights = mesh.boneWeights;
            var tris = new List<int>();
            _ringTriangles = new List<int>[mesh.subMeshCount];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var keep = new List<int>();
                mesh.GetTriangles(tris, s);
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    int v = tris[t];
                    int bone = v < weights.Length ? weights[v].boneIndex0 : -1;
                    if (bone < 0 || bone >= inRing.Length || !inRing[bone]) continue;
                    keep.Add(tris[t]);
                    keep.Add(tris[t + 1]);
                    keep.Add(tris[t + 2]);
                }
                _ringTriangles[s] = keep;
            }
        }

        public override void DressThrownRing(GameObject body)
        {
            if (!_resolved) Resolve();
            if (!body || !_model || !_ring || _ringTriangles == null) return;
            var filter = body.GetComponent<MeshFilter>();
            var renderer = body.GetComponent<MeshRenderer>();
            if (!filter || !renderer) return;

            // One mesh per pooled body, owned here: a ring in flight keeps its own copy while the next is thrown.
            var mesh = filter.sharedMesh;
            if (!mesh || !_thrownMeshes.Contains(mesh))
            {
                mesh = new Mesh { name = "BrittleStarThrownRing" };
                mesh.MarkDynamic();
                _thrownMeshes.Add(mesh);
                filter.sharedMesh = mesh;
            }

            // The ring as it stands - its firing rotation and the element morphs - then only the ring's triangles,
            // recentred on its pivot (on the nose axis) so the thrown copy spins about its own centre.
            var source = _model.transform;
            _model.BakeMesh(mesh, false);
            Vector3 pivot = source.InverseTransformPoint(_ring.position);
            mesh.GetVertices(_vertexScratch);
            for (int v = 0; v < _vertexScratch.Count; v++) _vertexScratch[v] -= pivot;
            mesh.SetVertices(_vertexScratch);
            int subMeshes = Mathf.Min(mesh.subMeshCount, _ringTriangles.Length);
            for (int s = 0; s < subMeshes; s++) mesh.SetTriangles(_ringTriangles[s], s, false);
            mesh.RecalculateBounds();

            renderer.sharedMaterials = _model.sharedMaterials;
            body.transform.SetPositionAndRotation(_ring.position, source.rotation);
            body.transform.localScale = source.lossyScale;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _thrownMeshes.Count; i++)
                if (_thrownMeshes[i]) Destroy(_thrownMeshes[i]);
            _thrownMeshes.Clear();
        }
    }
}
