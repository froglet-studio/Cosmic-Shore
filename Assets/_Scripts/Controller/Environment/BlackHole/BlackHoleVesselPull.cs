using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pulls VESSELS toward live black holes (Docs/BLACK_HOLE.md §4).
    ///
    /// A vessel is not a body: it has an engine that works against the pull, and its position
    /// belongs to the peer that drives it. So the hole does not integrate the vessel — it carries
    /// a per-vessel GRAVITATIONAL velocity (<c>v_g += a·dt</c>, the same acceleration a prism
    /// feels, scaled by the config) and hands it to the vessel's own transformer every frame
    /// through <see cref="VesselTransformer.ModifyVelocity"/>, the one sanctioned way anything
    /// outside a vessel moves it. The engine's own travel adds on top, which is exactly the
    /// escape rule: a vessel whose engine speed beats the local escape speed gets away (bent — the
    /// pull is still a lateral push on its path), one slower is drawn in. The channel is clamped
    /// at 100 u/s by the transformer and the config keeps the hole's share under that, so a
    /// knockback still registers on a falling ship, and inside the horizon the ship is HELD at
    /// the ceiling rather than destroyed — a simple version's stated limit.
    ///
    /// The shift is applied only on the machine that drives the vessel (the owner, or a
    /// non-networked vessel): a replica's transformer is inactive and its transform is the
    /// owner's to write. Vessels are enumerated once a second (there is no vessel registry; the
    /// pull is live only while a hole is), never per frame.
    ///
    /// <c>ModifyVelocity</c>'s weight curve starts at 1.5× for a fresh entry, and an entry given
    /// this frame's dt as its duration is consumed exactly once on steady frame times — so the
    /// shift handed over is <c>v_g / 1.5</c>. On a jittery frame an entry can survive into a
    /// second application at ~0.5×; that is a bounded noise and the price of using the sanctioned
    /// channel instead of writing the vessel's position directly.
    /// </summary>
    public static class BlackHoleVesselPull
    {
        /// <summary>The weight <c>VesselTransformer.ApplyVelocityModifiers</c> gives a fresh entry.</summary>
        const float FreshModifierWeight = 1.5f;
        const float RefreshSeconds = 1f;

        static readonly List<VesselStatus> _vessels = new();
        static readonly Dictionary<int, Vector3> _pullByVessel = new();
        static readonly List<int> _stale = new();
        static float _nextRefresh;

        /// <summary>Vessels this machine applied a pull to on the last tick (diagnostics).</summary>
        public static int PulledVesselCount { get; private set; }

        /// <summary>The gravitational velocity a vessel is currently carrying (diagnostics, tests).</summary>
        public static bool TryGetPull(VesselStatus vessel, out Vector3 pull)
        {
            pull = Vector3.zero;
            return vessel != null && _pullByVessel.TryGetValue(vessel.GetInstanceID(), out pull);
        }

        public static void Tick(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config, float dt)
        {
            PulledVesselCount = 0;
            if (!config.PullVessels || dt <= 0f) { _pullByVessel.Clear(); return; }
            if (holes.Count == 0 && _pullByVessel.Count == 0) return;

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                _vessels.Clear();
                // Not a hot path: once a second, only while a hole is live or a pull is decaying.
                _vessels.AddRange(Object.FindObjectsByType<VesselStatus>(FindObjectsSortMode.None));
            }

            var wells = new BlackHolePhysics.NativeWells();
            for (int i = 0; i < holes.Count && i < BlackHolePhysics.NativeWells.Capacity; i++)
            {
                var h = holes[i];
                if (h == null) continue;
                wells.Add(h.ToWell(config));
            }

            float scale = config.VesselPullScale;
            float maxSpeed = config.MaxVesselPullSpeed;
            _stale.Clear();
            foreach (var kv in _pullByVessel) _stale.Add(kv.Key);

            for (int i = 0; i < _vessels.Count; i++)
            {
                var vessel = _vessels[i];
                if (vessel == null) continue;
                int key = vessel.GetInstanceID();
                _stale.Remove(key);

                // Only the machine that drives this vessel may move it; a replica's transform is
                // the owner's. A stationary vessel's transformer does not age its modifiers, so
                // entries would pile up — skip it.
                if (vessel.IsNetworkClient || vessel.IsStationary) { _pullByVessel.Remove(key); continue; }
                var transformer = vessel.VesselTransformer;
                if (transformer == null) { _pullByVessel.Remove(key); continue; }

                _pullByVessel.TryGetValue(key, out var pull);
                var p = vessel.transform.position;
                var pos = new float3(p.x, p.y, p.z);

                float3 a = float3.zero;
                bool inside = false;
                for (int w = 0; w < wells.Count; w++)
                {
                    var well = wells[w];
                    float d = math.length(pos - well.Position);
                    if (d <= well.InfluenceRadius) inside = true;
                    a += BlackHolePhysics.Acceleration(pos, well);
                }

                var dv = new Vector3(a.x, a.y, a.z) * (scale * dt);
                pull += dv;
                if (!inside && config.ReleaseDamping > 0f)
                    pull *= Mathf.Exp(-config.ReleaseDamping * dt);
                pull = Vector3.ClampMagnitude(pull, maxSpeed);

                if (pull.sqrMagnitude < 1e-4f)
                {
                    _pullByVessel.Remove(key);
                    continue;
                }

                _pullByVessel[key] = pull;
                transformer.ModifyVelocity(pull / FreshModifierWeight, dt);
                PulledVesselCount++;
            }

            for (int i = 0; i < _stale.Count; i++) _pullByVessel.Remove(_stale[i]);
        }

        internal static void ResetOnLoad()
        {
            _vessels.Clear();
            _pullByVessel.Clear();
            _stale.Clear();
            _nextRefresh = 0f;
            PulledVesselCount = 0;
        }
    }
}
