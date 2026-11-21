using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Orca model posed by its crew (<c>Orca.fbx</c>, <c>Tools/Build/vessel_model/specs/orca.py</c>). The model is a
    /// HOVERING ASSEMBLY (Garrett 2026-10-21: "free floating parts that are associated and animated together but clearly
    /// distinct parts of a hovering assembly"): a hull core, and a fin, outrigger plates, engines and turrets that float
    /// clear of it, each on its own bone, found by name under the hull's nested model. Every frame:
    /// <list type="bullet">
    /// <item><b>The turrets.</b> Each seat's YAW bone stands where the crew's lagged outrigger frame puts that seat's mount
    /// (the lab's round-4 <c>mountSmooth</c>), turns about that frame's up, and its PITCH bone about the turret's right, so
    /// the barrel points along the seat's stabilized aim. The crew poses a seat again the moment it fires
    /// (<see cref="PoseSeat"/>), so a round leaves this frame's barrel tip.</item>
    /// <item><b>The outrigger chain.</b> The floating plates between the hull and a side turret ride part-way between the
    /// hull's frame and the lagged outrigger frame (by how far out they float), so through a hard turn the chain swings
    /// out as one soft arc, hull to turret: the parts stay associated without touching.</item>
    /// <item><b>The hover.</b> Every floating part bobs a little on its own phase, and the engines lean into turns.</item>
    /// <item><b>The jets</b> burn from the model's engine mouths: the prefab's <see cref="VesselJet"/>s name the hidden
    /// Sparrow's tail bones, so on bind each is remounted on the nearer mouth (<see cref="VesselJet.Remount"/>), its plume
    /// still pointing where it pointed relative to the hull.</item>
    /// </list>
    /// <para><b>Rest comes from the mesh's bind poses</b>, never from the bones as found: a crew that re-binds to a hull it
    /// already posed (an AI crew, then a person boarding) would otherwise take an aimed turret as its rest and draw it at
    /// that angle off its aim for good (Garrett 2026-10-21: "the minigun model was still at an odd angle compared to the
    /// shot"). The bones go back to rest when the crew lets go (<see cref="Restore"/>).</para>
    /// A model without the turret bones (or with no model) leaves <see cref="Bound"/> false and the view draws its faceted
    /// pods instead, so the crew never depends on the art. A missing floating-part bone is skipped.
    /// </summary>
    public sealed class OrcaHullRig
    {
        public const string ModelName = "Orca";
        static readonly string[] YawNames = { null, "SniperYaw", "FastGunYaw", "BombYaw" };
        static readonly string[] PitchNames = { null, "SniperPitch", "FastGunPitch", "BombPitch" };
        static readonly string[] MuzzleNames = { null, "SniperMuzzle", "FastGunMuzzle", "BombMuzzle" };
        static readonly string[] MissileNames = { null, "SniperMissile", "FastGunMissile", "BombMissile" };

        enum Motion { Hover = 0, Chain = 1, Engine = 2 }
        // The floating parts the model names (specs/orca.py): how each moves and, for an outrigger plate, how far out
        // along its seat's outrigger it floats (0 = the hull, 1 = the turret).
        static readonly (string name, Motion motion, float along)[] Parts =
        {
            ("fin", Motion.Hover, 0f),
            // the chain's plates sit 8.3 / 12.8 / 17.4 u out on the 21.6 u outrigger, and the pod under each turret at it
            ("wing.L.1", Motion.Chain, 0.38f), ("wing.L.2", Motion.Chain, 0.59f), ("wing.L.3", Motion.Chain, 0.80f), ("pod.L", Motion.Chain, 1f),
            ("wing.R.1", Motion.Chain, 0.38f), ("wing.R.2", Motion.Chain, 0.59f), ("wing.R.3", Motion.Chain, 0.80f), ("pod.R", Motion.Chain, 1f),
            ("engine.L", Motion.Engine, 0f), ("engine.R", Motion.Engine, 0f),
            ("fluke.L", Motion.Engine, 0f), ("fluke.R", Motion.Engine, 0f),
        };

        // A bone's rest pose in the HULL's frame, read from the bind poses.
        struct Rest { public Vector3 Position; public Quaternion Rotation; }

        readonly Transform _hull;
        readonly Transform[] _yaw = new Transform[OrcaCrew.Seats], _pitch = new Transform[OrcaCrew.Seats],
            _muzzle = new Transform[OrcaCrew.Seats], _missile = new Transform[OrcaCrew.Seats];
        readonly Rest[] _yawRest = new Rest[OrcaCrew.Seats], _pitchRest = new Rest[OrcaCrew.Seats];
        readonly Transform[] _parts = new Transform[Parts.Length];
        readonly Rest[] _partRest = new Rest[Parts.Length];
        readonly float _hullRadius;
        Quaternion _lastHullRot;
        Vector2 _turn;   // the hull's yaw and pitch rates (deg/s), eased: what the engines lean into

        public bool Bound { get; }

        public OrcaHullRig(Transform hull, float hullRadius)
        {
            _hull = hull;
            _hullRadius = Mathf.Max(1f, hullRadius);
            var model = hull ? FindDeep(hull, ModelName) : null;
            if (!model) return;
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            bool all = true;
            for (int s = 1; s < OrcaCrew.Seats; s++)
            {
                _yaw[s] = FindDeep(model, YawNames[s]);
                _pitch[s] = FindDeep(model, PitchNames[s]);
                _muzzle[s] = FindDeep(model, MuzzleNames[s]);
                _missile[s] = FindDeep(model, MissileNames[s]);   // optional: missiles launch from the mount without it
                if (!_yaw[s] || !_pitch[s] || !_muzzle[s]) { all = false; continue; }
                _yawRest[s] = RestOf(skin, _yaw[s]);
                _pitchRest[s] = RestOf(skin, _pitch[s]);
            }
            for (int i = 0; i < Parts.Length; i++)
            {
                _parts[i] = FindDeep(model, Parts[i].name);
                if (_parts[i]) _partRest[i] = RestOf(skin, _parts[i]);
            }
            _lastHullRot = hull.rotation;
            Bound = all;
            if (Bound) RemountJets(model);
        }

        // Each jet onto the mouth on its side of the hull, spread on a small ring round it so a side's jets read as one
        // engine's plume rather than three stacked on a point.
        void RemountJets(Transform model)
        {
            var left = FindDeep(model, "jet.L.mouth");
            var right = FindDeep(model, "jet.R.mouth");
            if (!left || !right) return;
            var jets = _hull.GetComponentsInChildren<VesselJet>(true);
            int nl = 0, nr = 0;
            float ring = _hullRadius * 0.07f;
            foreach (var jet in jets)
            {
                bool isLeft = _hull.InverseTransformPoint(jet.transform.position).x < 0f;
                var mouth = isLeft ? left : right;
                int k = isLeft ? nl++ : nr++;
                float a = k * 2.0944f;   // 120 degrees apart
                var offset = _hull.right * Mathf.Cos(a) * ring + _hull.up * Mathf.Sin(a) * ring;
                jet.Remount(mouth, mouth.position + (k == 0 ? Vector3.zero : offset));
            }
        }

        // bindposes[i] = bones[i].worldToLocalMatrix * skin.transform.localToWorldMatrix, taken at bind, so a bone's rest
        // pose is skin.localToWorld * inverse(bindpose) whatever it has been posed to since; expressed in the hull's frame
        // (the skinned mesh rides the hull rigidly). A bone the skin does not list falls back to where it is now.
        Rest RestOf(SkinnedMeshRenderer skin, Transform bone)
        {
            var inv = Quaternion.Inverse(_hull.rotation);
            if (skin && skin.sharedMesh)
            {
                var bones = skin.bones;
                var binds = skin.sharedMesh.bindposes;
                for (int i = 0; i < bones.Length && i < binds.Length; i++)
                {
                    if (bones[i] != bone) continue;
                    var world = skin.transform.localToWorldMatrix * binds[i].inverse;
                    return new Rest { Position = _hull.InverseTransformPoint(world.GetColumn(3)), Rotation = inv * world.rotation };
                }
            }
            return new Rest { Position = _hull.InverseTransformPoint(bone.position), Rotation = inv * bone.rotation };
        }

        /// <summary>Where a seat's rounds leave from: its barrel's tip (posed this frame), or false with no rig.</summary>
        public bool TryMuzzle(int seat, out Vector3 position)
        {
            position = default;
            if (!Bound || seat <= OrcaCrew.Helm || seat >= OrcaCrew.Seats) return false;
            position = _muzzle[seat].position;
            return true;
        }

        /// <summary>Where a seat's missiles leave from: its missile tube's mouth (posed this frame), or false.</summary>
        public bool TryMissileTube(int seat, out Vector3 position)
        {
            position = default;
            if (!Bound || seat <= OrcaCrew.Helm || seat >= OrcaCrew.Seats || !_missile[seat]) return false;
            position = _missile[seat].position;
            return true;
        }

        /// <summary>The whole assembly: the turrets, the outrigger chains, the hover and the engines' lean.</summary>
        public void Pose(OrcaCrew crew)
        {
            if (!Bound || !UnityLiveness.Alive(_hull)) return;
            for (int s = 1; s < OrcaCrew.Seats; s++) PoseSeat(crew, s);
            PoseParts(crew);
        }

        /// <summary>One seat's turret, posed now (the crew calls it as the seat fires).</summary>
        public void PoseSeat(OrcaCrew crew, int s)
        {
            if (!Bound || s <= OrcaCrew.Helm || s >= OrcaCrew.Seats || !UnityLiveness.Alive(_hull)) return;
            var t = crew.TurretOf(s);
            if (t == null) return;
            var pod = crew.Pod;
            var podRot = Quaternion.LookRotation(OrcaCrew.U(pod.F), OrcaCrew.U(pod.U));
            _yaw[s].position = OrcaCrew.U(OrcaCrewCore.MountPosition(OrcaCrew.N(_hull.position), pod, s, crew.Config.HullRadius));
            // the aim in the outrigger frame: yaw about its up, then pitch about the turret's right
            var local = Quaternion.Inverse(podRot) * OrcaCrew.U(t.Aim);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            var yawRot = podRot * Quaternion.AngleAxis(yaw, Vector3.up);
            _yaw[s].rotation = yawRot * _yawRest[s].Rotation;
            _pitch[s].rotation = yawRot * Quaternion.AngleAxis(-pitch, Vector3.right) * _pitchRest[s].Rotation;
        }

        void PoseParts(OrcaCrew crew)
        {
            float dt = Mathf.Max(1e-4f, Time.deltaTime), now = Time.time;
            var hullRot = _hull.rotation;
            var pod = crew.Pod;
            var podRot = Quaternion.LookRotation(OrcaCrew.U(pod.F), OrcaCrew.U(pod.U));
            // the hull's turn rates in its own frame, eased
            (Quaternion.Inverse(_lastHullRot) * hullRot).ToAngleAxis(out float angle, out var axis);
            _lastHullRot = hullRot;
            if (angle > 180f) angle -= 360f;
            var rate = float.IsFinite(axis.x) ? axis * (angle / dt) : Vector3.zero;
            _turn = Vector2.Lerp(_turn, new Vector2(rate.y, rate.x), 1f - Mathf.Exp(-dt / 0.25f));
            float bob = _hullRadius * 0.025f;   // a breath of hover: ~0.2 u on the 9 u hull
            var lean = Quaternion.Euler(Mathf.Clamp(_turn.y * 0.12f, -12f, 12f), Mathf.Clamp(_turn.x * 0.12f, -12f, 12f), 0f);
            for (int i = 0; i < Parts.Length; i++)
            {
                var bone = _parts[i];
                if (!bone) continue;
                var rest = _partRest[i];
                var (_, motion, along) = Parts[i];
                var frame = motion == Motion.Chain ? Quaternion.Slerp(hullRot, podRot, along) : hullRot;
                var pos = _hull.position + frame * rest.Position + hullRot * Vector3.up * (bob * Mathf.Sin(now * 2.1f + i * 1.7f));
                var rot = motion == Motion.Engine ? hullRot * lean * rest.Rotation : frame * rest.Rotation;
                bone.SetPositionAndRotation(pos, rot);
            }
        }

        /// <summary>Every bone back at rest (the crew let go of the hull).</summary>
        public void Restore()
        {
            if (!Bound || !UnityLiveness.Alive(_hull)) return;
            for (int s = 1; s < OrcaCrew.Seats; s++)
            {
                _yaw[s].SetPositionAndRotation(_hull.TransformPoint(_yawRest[s].Position), _hull.rotation * _yawRest[s].Rotation);
                _pitch[s].SetPositionAndRotation(_hull.TransformPoint(_pitchRest[s].Position), _hull.rotation * _pitchRest[s].Rotation);
            }
            for (int i = 0; i < Parts.Length; i++)
                if (_parts[i]) _parts[i].SetPositionAndRotation(_hull.TransformPoint(_partRest[i].Position), _hull.rotation * _partRest[i].Rotation);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit) return hit;
            }
            return null;
        }
    }
}
