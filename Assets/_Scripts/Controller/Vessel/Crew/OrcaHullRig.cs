using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Orca model's three turrets, aimed by the crew (<c>Orca.fbx</c>, <c>Tools/Build/vessel_model/specs/orca.py</c>;
    /// Garrett 2026-10-21: "use the vessel modeling tools and really step up the vessel"). Each gunner seat's turret is a
    /// YAW bone at its pivot, a PITCH bone under it, and a MUZZLE bone at the barrel's tip, found by name under the hull's
    /// nested model. Every frame:
    /// <list type="bullet">
    /// <item>the yaw bone stands where the crew's lagged outrigger frame puts that seat's mount (the lab's round-4
    /// <c>mountSmooth</c>), so the drawn turret and the point every round, beam and missile leaves from are one;</item>
    /// <item>the yaw bone turns about the outrigger frame's up and the pitch bone about the turret's right, so the barrel
    /// points along the seat's stabilized aim. Each is posed RELATIVE to its rest (the bone's local axes are the model's
    /// business; only "rest aims straight ahead" is assumed).</item>
    /// </list>
    /// The crew poses a seat again the moment it fires (<see cref="PoseSeat"/>), so a round leaves this frame's barrel tip
    /// and not last frame's: at 175 u/s a frame-old muzzle is 3 u behind the drawn one (round 7, Garrett 2026-10-21: "not
    /// shooting where they were aiming").
    /// A model without the bones (or with no model) leaves <see cref="Bound"/> false and the view draws its faceted pods
    /// instead, so the crew never depends on the art.
    /// </summary>
    public sealed class OrcaHullRig
    {
        public const string ModelName = "Orca";
        static readonly string[] YawNames = { null, "SniperYaw", "FastGunYaw", "BombYaw" };
        static readonly string[] PitchNames = { null, "SniperPitch", "FastGunPitch", "BombPitch" };
        static readonly string[] MuzzleNames = { null, "SniperMuzzle", "FastGunMuzzle", "BombMuzzle" };
        static readonly string[] MissileNames = { null, "SniperMissile", "FastGunMissile", "BombMissile" };

        readonly Transform _hull;
        readonly Transform[] _yaw = new Transform[OrcaCrew.Seats], _pitch = new Transform[OrcaCrew.Seats], _muzzle = new Transform[OrcaCrew.Seats],
            _missile = new Transform[OrcaCrew.Seats];
        // each bone's rest rotation relative to the hull
        readonly Quaternion[] _yawRest = new Quaternion[OrcaCrew.Seats], _pitchRest = new Quaternion[OrcaCrew.Seats];

        public bool Bound { get; }

        public OrcaHullRig(Transform hull)
        {
            _hull = hull;
            var model = hull ? FindDeep(hull, ModelName) : null;
            if (!model) return;
            bool all = true;
            for (int s = 1; s < OrcaCrew.Seats; s++)
            {
                _yaw[s] = FindDeep(model, YawNames[s]);
                _pitch[s] = FindDeep(model, PitchNames[s]);
                _muzzle[s] = FindDeep(model, MuzzleNames[s]);
                _missile[s] = FindDeep(model, MissileNames[s]);   // optional: missiles launch from the mount without it
                if (!_yaw[s] || !_pitch[s] || !_muzzle[s]) { all = false; continue; }
                var inv = Quaternion.Inverse(hull.rotation);
                _yawRest[s] = inv * _yaw[s].rotation;
                _pitchRest[s] = inv * _pitch[s].rotation;
            }
            Bound = all;
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

        /// <summary>Stand each turret at its lagged mount and point it along its seat's aim.</summary>
        public void Pose(OrcaCrew crew)
        {
            for (int s = 1; s < OrcaCrew.Seats; s++) PoseSeat(crew, s);
        }

        /// <summary>One seat's turret, posed now (the crew calls it as the seat fires).</summary>
        public void PoseSeat(OrcaCrew crew, int s)
        {
            if (!Bound || s <= OrcaCrew.Helm || s >= OrcaCrew.Seats || !UnityLiveness.Alive(_hull)) return;
            var t = crew.TurretOf(s);
            if (t == null) return;
            var cfg = crew.Config;
            var pod = crew.Pod;
            var podRot = Quaternion.LookRotation(OrcaCrew.U(pod.F), OrcaCrew.U(pod.U));
            var hullPos = OrcaCrew.N(_hull.position);
            {
                _yaw[s].position = OrcaCrew.U(OrcaCrewCore.MountPosition(hullPos, pod, s, cfg.HullRadius));
                // the aim in the outrigger frame: yaw about its up, then pitch about the turret's right
                var local = Quaternion.Inverse(podRot) * OrcaCrew.U(t.Aim);
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
                var yawRot = podRot * Quaternion.AngleAxis(yaw, Vector3.up);
                _yaw[s].rotation = yawRot * _yawRest[s];
                _pitch[s].rotation = yawRot * Quaternion.AngleAxis(-pitch, Vector3.right) * _pitchRest[s];
            }
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
