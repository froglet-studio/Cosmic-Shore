using System.Collections.Generic;
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
    /// <para><b>The muzzles are the DRAWN barrel mouths.</b> Each muzzle sits on its <c>gun.N.mouth</c> bone (built at
    /// <see cref="BrittleStarHullForm.MuzzlePosition"/>), so a round leaves and a cord roots exactly where the drawn
    /// barrel ends, in whatever frame Unity imports the bones (Garrett, 2026-10-18: the guns and cords must be at the
    /// ring corners). The element shapes grow the barrels while the bones stay put, so each muzzle also follows its
    /// barrel's own blend-shape travel, read once from the mesh (the mean full-weight delta of the vertices skinned to
    /// the mouth bone, in the bone's bind frame). The ring's radius, thickness and plane stay the form's numbers.</para>
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
        float _ringPhaseDegrees;   // the firing rotation SetRingPose last applied: a thrown ring carries it in its pose

        readonly Transform[] _flames = new Transform[Slots];
        readonly Vector3[] _flameRestScale = new Vector3[Slots];
        readonly int[] _flameAxis = new int[Slots];
        readonly Transform[] _muzzles = new Transform[Slots];
        readonly Quaternion[] _muzzleRestRotation = new Quaternion[Slots];
        // per slot, per element (Charge, Mass, Space, Time): the barrel mouth's full-weight travel in its bone's frame
        readonly Vector3[,] _mouthTravel = new Vector3[Slots, 4];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };
        static readonly string[] ShapeNames = { "Charge", "Mass", "Space", "Time" };

        List<int>[] _ringTriangles;
        readonly List<Mesh> _thrownMeshes = new();
        readonly List<Vector3> _vertexScratch = new();
        readonly List<Vector3> _normalScratch = new();
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
                    // the muzzle rides the mouth bone (and so the ring's spin and presence), facing the hull's forward
                    _muzzleRestRotation[slot] = Quaternion.Inverse(mouth.rotation) * transform.rotation;
                    string muzzleName = i < 0 ? "Muzzle_C" : "Muzzle_" + i;
                    var m = mouth.Find(muzzleName);
                    if (!m)
                    {
                        m = new GameObject(muzzleName) { layer = gameObject.layer }.transform;
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
            ReadMouthTravel();
            PlaceMuzzles(0f, 0f, 0f, 0f);
            CollectRingTriangles();
        }

        /// <summary>Each barrel mouth's travel under each element shape, in its bone's bind frame: the mean full-weight
        /// delta of the vertices skinned to the mouth bone (its muzzle brake). Read once; the import keeps the mesh
        /// readable.</summary>
        void ReadMouthTravel()
        {
            if (!_model) return;
            var mesh = _model.sharedMesh;
            if (!mesh || !mesh.isReadable) return;
            var bones = _model.bones;
            var bindposes = mesh.bindposes;
            var weights = mesh.boneWeights;
            var deltas = new Vector3[mesh.vertexCount];
            var sums = new Vector3[Slots];
            var counts = new int[Slots];
            var slotOfBone = new int[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                slotOfBone[b] = -1;
                for (int slot = 0; slot < Slots; slot++)
                    if (_muzzles[slot] && bones[b] == _muzzles[slot].parent) slotOfBone[b] = slot;
            }
            for (int e = 0; e < ShapeNames.Length; e++)
            {
                int shape = FindShape(mesh, ShapeNames[e]);
                if (shape < 0) continue;
                int last = mesh.GetBlendShapeFrameCount(shape) - 1;
                if (last < 0) continue;
                mesh.GetBlendShapeFrameVertices(shape, last, deltas, null, null);
                System.Array.Clear(sums, 0, Slots);
                System.Array.Clear(counts, 0, Slots);
                for (int v = 0; v < weights.Length; v++)
                {
                    int b = weights[v].boneIndex0;
                    if (b < 0 || b >= slotOfBone.Length || slotOfBone[b] < 0 || weights[v].weight0 < 0.5f) continue;
                    sums[slotOfBone[b]] += deltas[v];
                    counts[slotOfBone[b]]++;
                }
                for (int b = 0; b < bones.Length; b++)
                {
                    int slot = slotOfBone[b];
                    if (slot < 0 || counts[slot] == 0 || b >= bindposes.Length) continue;
                    _mouthTravel[slot, e] = bindposes[b].MultiplyVector(sums[slot] / counts[slot]);
                }
            }
        }

        static int FindShape(Mesh mesh, string element)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                if (n.Equals(element, System.StringComparison.OrdinalIgnoreCase)
                    || n.EndsWith("." + element, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
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

        /// <summary>Each muzzle on its drawn barrel mouth, carried by the element weights' barrel travel.</summary>
        void PlaceMuzzles(float charge, float mass, float space, float time)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                var m = _muzzles[slot];
                if (!m) continue;
                m.localPosition = _mouthTravel[slot, 0] * charge + _mouthTravel[slot, 1] * mass
                                + _mouthTravel[slot, 2] * space + _mouthTravel[slot, 3] * time;
                m.localRotation = _muzzleRestRotation[slot];
            }
        }

        public override void SetRingPose(float phaseRadians, float presenceScale)
        {
            if (!_resolved) Resolve();
            if (!_ring) return;
            _ringPhaseDegrees = phaseRadians * Mathf.Rad2Deg;
            _ring.localRotation = Quaternion.AngleAxis(_ringPhaseDegrees, _ringAxisInParent) * _ringRestLocal;
            _ring.localScale = _ringRestScale * presenceScale;
        }

        public override void ResetRingPose()
        {
            _ringPhaseDegrees = 0f;
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
            // the muzzles on the barrel mouths the shapes move.
            if (!_resolved) Resolve();
            if (Mathf.Approximately(charge, _appliedWeights[0]) && Mathf.Approximately(mass, _appliedWeights[1])
                && Mathf.Approximately(space, _appliedWeights[2]) && Mathf.Approximately(time, _appliedWeights[3]))
                return;
            _appliedWeights[0] = charge;
            _appliedWeights[1] = mass;
            _appliedWeights[2] = space;
            _appliedWeights[3] = time;
            PlaceMuzzles(Mathf.Clamp01(charge), Mathf.Clamp01(mass), Mathf.Clamp01(space), Mathf.Clamp01(time));
        }

        /// <summary>Per submesh, the triangles skinned to the ring or a bone under it (the model is skinned rigidly,
        /// one bone per part): what a thrown ring draws - less the thruster plumes (<c>*.flame</c>), which belong to
        /// the mounted ring's engines, not to a still copy in flight. Read once; the import keeps the mesh readable.</summary>
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
            for (int b = 0; b < bones.Length; b++)
                inRing[b] = bones[b] && bones[b].IsChildOf(_ring) && !bones[b].name.EndsWith(".flame");
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
            if (!body) return;

            // The thrown ring's frame is the mounted ring's, as the procedural builder hands over `_ring.rotation`:
            // local Z along the NOSE, turned by the firing rotation. The executor spins the flight about that local Z
            // and faces the detonation disk along it. It is built from the hull (`transform`, whose forward is the
            // nose), NOT taken from the skinned mesh's transform: a Z-up FBX keeps its mesh node in the file frame
            // (export.py, THE AXIS STORY), so that node's local Z is the hull's UP.
            Quaternion bodyRotation = transform.rotation * Quaternion.AngleAxis(_ringPhaseDegrees, Vector3.forward);
            Vector3 pivot = _ring ? _ring.position : transform.position;
            // Posed before any guard below, so a body that cannot be dressed still launches from the ring.
            body.transform.SetPositionAndRotation(pivot, bodyRotation);
            body.transform.localScale = Vector3.one;
            if (!_model || !_ring || _ringTriangles == null) return;
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

            // The ring as it stands - its firing rotation and the element morphs - re-expressed in the body's frame
            // (world, then into bodyRotation about the pivot), so its vertices hold the UNTURNED ring and the pose
            // carries the turn; the normals turn with them (VesselGraph's rim reads them). Then only the ring's
            // triangles.
            var source = _model.transform;
            _model.BakeMesh(mesh, false);
            Quaternion toBody = Quaternion.Inverse(bodyRotation);
            Quaternion normalToBody = toBody * source.rotation;
            mesh.GetVertices(_vertexScratch);
            for (int v = 0; v < _vertexScratch.Count; v++)
                _vertexScratch[v] = toBody * (source.TransformPoint(_vertexScratch[v]) - pivot);
            mesh.SetVertices(_vertexScratch);
            mesh.GetNormals(_normalScratch);
            for (int n = 0; n < _normalScratch.Count; n++) _normalScratch[n] = normalToBody * _normalScratch[n];
            mesh.SetNormals(_normalScratch);
            int subMeshes = Mathf.Min(mesh.subMeshCount, _ringTriangles.Length);
            for (int s = 0; s < subMeshes; s++) mesh.SetTriangles(_ringTriangles[s], s, false);
            mesh.RecalculateBounds();

            renderer.sharedMaterials = _model.sharedMaterials;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _thrownMeshes.Count; i++)
                if (_thrownMeshes[i]) Destroy(_thrownMeshes[i]);
            _thrownMeshes.Clear();
        }
    }
}
