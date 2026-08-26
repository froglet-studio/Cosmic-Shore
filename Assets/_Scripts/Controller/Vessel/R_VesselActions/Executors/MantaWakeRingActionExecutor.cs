using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lays the Manta's SOAR wake rings — the Time element's qualitative surface (level 5 =
    /// "Wake Highway": rings twice as often, harder surge, rideable by the whole domain).
    ///
    /// PASSIVE: rides <c>IVesselStatus.IsBoosting</c> (Soar's own state), so it is bound to no
    /// input event and its config is wired directly here (the Dolphin crystal-seeding rule).
    /// The owner machine decides when a ring is laid and relays the pose so every peer lays an
    /// identical ring — conserved mass an ally's machine must contain for the ally to ride it.
    /// </summary>
    public sealed class MantaWakeRingActionExecutor : ShipActionExecutorBase
    {
        [Header("Config (wired directly — passive, no input event)")]
        [SerializeField] MantaWakeRingConfigSO config;

        IVesselStatus _status;
        MantaBombNetworkRelay _relay;
        float _nextRingTime;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _relay = GetComponentInParent<MantaBombNetworkRelay>();
            _nextRingTime = 0f;

            if (!config)
                CSDebug.LogWarning("[MantaWakeRing] No MantaWakeRingConfigSO wired — Soar lays " +
                                   "no wake rings. Wire it on Manta.prefab's " +
                                   "MantaWakeRingActionExecutor.");
        }

        void Update()
        {
            if (!config || _status == null) return;
            if (!MantaStingActionExecutor.IsSimAuthority(_status)) return;
            if (!_status.IsBoosting) { return; }

            var handler = _status.ElementalAbilityHandler;
            bool highway = handler && handler.IsUpgradeActive(Element.Time);
            float period = highway ? config.SpawnPeriodAtTime5 : config.SpawnPeriodSeconds;

            if (Time.time < _nextRingTime)
                return;
            _nextRingTime = Time.time + period;

            var prismController = _status.VesselPrismController;
            if (!prismController || !prismController.PrismSpawnChannel) return;

            Vector3 axis = _status.Course.sqrMagnitude > 1e-4f
                ? _status.Course.normalized
                : _status.ShipTransform.forward;
            var pose = new Pose(_status.ShipTransform.position - axis * config.BehindOffset,
                                Quaternion.LookRotation(axis, _status.ShipTransform.up));

            LayRingAt(config, pose, _status, prismController.PrismSpawnChannel, registerSwitch: true);
            if (_relay && _relay.IsSpawned)
                _relay.BroadcastWakeRing(pose.position, pose.rotation);
        }

        /// <summary>
        /// Lays one wake ring + its switch at <paramref name="pose"/>. Shared verbatim by the
        /// owner machine and every peer's relayed copy (<see cref="MantaBombNetworkRelay"/>),
        /// so the two cannot drift. The prisms are ordinary conserved mass in the Manta's
        /// domain — grazeable, stealable, rideable as a 1D loop — laid through the canonical
        /// <see cref="BoostRingBuilder"/> (full-size colliders from frame 0).
        /// </summary>
        public static void LayRingAt(MantaWakeRingConfigSO cfg, Pose pose, IVesselStatus layer,
                                     CosmicShore.ScriptableObjects.PrismEventChannelWithReturnSO channel,
                                     bool registerSwitch)
        {
            if (cfg == null || layer == null || !channel) return;

            var handler = layer.ElementalAbilityHandler;
            bool highway = handler && handler.IsUpgradeActive(Element.Time);

            var collected = new List<Prism>();
            var ringTrail = new Trail(true);
            BoostRingBuilder.LayRing(channel, pose,
                new BoostRingSpec(cfg.Segments, cfg.RingRadius, cfg.PrismScale, PrismKind.Plain),
                layer.Domain, layer.PlayerName, $"{layer.PlayerName}::wakeRing",
                ringTrail, collected);

            if (collected.Count == 0 || !registerSwitch) return;

            MantaWakeRingSwitch.Create(cfg, pose, layer.PlayerName, layer.Domain, highway, collected);
        }
    }

    /// <summary>
    /// The SWITCH in a wake ring's hole — thread it and it pays a forward surge. Per the
    /// switch law the trigger volume IS the ring's own radius (a ring may never advertise a
    /// volume its trigger lacks). Eligibility is a LAY-time snapshot: below Time 5 only the
    /// laying pilot is paid; with Wake Highway any own-domain vessel is. Enemies are never
    /// paid — it is a wake, not a trap — and the surge applies only on the machine that
    /// simulates the rider (exactly once per rider, wherever they are hosted).
    ///
    /// The switch retires itself once most of its ring is gone (identity-tested against pool
    /// reuse via each prism's TimeCreated): an invisible booster with no ring is a lie. The
    /// prisms are conserved mass and are never culled by this.
    /// </summary>
    public class MantaWakeRingSwitch : MonoBehaviour
    {
        MantaWakeRingConfigSO _cfg;
        string _layerName;
        Domains _domain;
        bool _allyRideable;
        Vector3 _axis;
        readonly List<(Prism prism, float laidAt)> _ringPrisms = new();
        readonly Dictionary<int, float> _lastRideByVessel = new();
        float _nextUpkeep;

        public static MantaWakeRingSwitch Create(MantaWakeRingConfigSO cfg, Pose pose,
            string layerName, Domains domain, bool allyRideable, List<Prism> ringPrisms)
        {
            var go = new GameObject("MantaWakeRingSwitch");
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = cfg.RingRadius;

            var sw = go.AddComponent<MantaWakeRingSwitch>();
            sw._cfg = cfg;
            sw._layerName = layerName;
            sw._domain = domain;
            sw._allyRideable = allyRideable;
            sw._axis = pose.rotation * Vector3.forward;
            for (int i = 0; i < ringPrisms.Count; i++)
                if (ringPrisms[i])
                    sw._ringPrisms.Add((ringPrisms[i], ringPrisms[i].prismProperties.TimeCreated));
            sw._nextUpkeep = Time.time + 2f;
            return sw;
        }

        void Update()
        {
            if (Time.time < _nextUpkeep) return;
            _nextUpkeep = Time.time + 2f;

            // Same object AND same life: a pool-recycled prism keeps its reference alive but
            // re-stamps TimeCreated on its next issue, which is exactly the tell.
            int live = 0;
            for (int i = 0; i < _ringPrisms.Count; i++)
            {
                var (prism, laidAt) = _ringPrisms[i];
                if (prism && !prism.destroyed
                    && Mathf.Approximately(prism.prismProperties.TimeCreated, laidAt))
                    live++;
            }

            if (_ringPrisms.Count == 0 ||
                live < _ringPrisms.Count * _cfg.RetireBelowPrismFraction)
                Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<ImpactCollider>(out var ic)) return;
            if (ic.Impactor is not VesselImpactor vesselImpactor) return;

            var status = vesselImpactor.Vessel?.VesselStatus;
            if (status == null) return;

            // Pay each vessel exactly once, on the machine that simulates its motion.
            if (!MantaStingActionExecutor.IsSimAuthority(status)) return;

            bool eligible = _allyRideable
                ? status.Domain == _domain
                : status.PlayerName == _layerName;
            if (!eligible) return;

            int id = status.Vessel.Transform.GetInstanceID();
            if (_lastRideByVessel.TryGetValue(id, out float last)
                && Time.time - last < _cfg.PerVesselRideCooldown)
                return;
            _lastRideByVessel[id] = Time.time;

            // The surge flings the rider along the highway, whichever way they entered it.
            Vector3 course = status.Course.sqrMagnitude > 1e-4f ? status.Course : _axis;
            Vector3 direction = Vector3.Dot(_axis, course) >= 0f ? _axis : -_axis;
            float surge = _allyRideable ? _cfg.SurgeSpeedAtTime5 : _cfg.SurgeSpeed;
            status.VesselTransformer.ModifyVelocity(direction * surge, _cfg.SurgeSeconds);
        }
    }
}
