using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
    /// <b>Ownership.</b> A hole a vessel slung (<see cref="BlackHole.OwnerVessel"/> — the Stoat's
    /// wormhole pair) pulls only that vessel: a vessel may not move an opposing vessel
    /// (Docs/ELEMENTAL_ECONOMY.md §9, LOCKED). An environmental hole (the tool, the console, a cell)
    /// pulls every vessel.
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
        static readonly Transform[] _wellOwners = new Transform[BlackHolePhysics.NativeWells.Capacity];
        static float _nextRefresh;

        /// <summary>Vessels this machine applied a pull to on the last tick (diagnostics).</summary>
        public static int PulledVesselCount { get; private set; }

        /// <summary>Vessels carried through a pair since the session began (diagnostics, tests).</summary>
        public static int VesselTransitsTotal { get; private set; }

        /// <summary>
        /// The vessel half of the tunnel (the prisms' is in <see cref="BlackHoleGravityField"/>): a vessel
        /// whose centre is inside the horizon of a black hole that has a white partner is moved to the
        /// point reflection of its entry just outside the white horizon (<c>BlackHolePairMath.ExitPosition</c>),
        /// keeping its heading — which points outward there, the way it went in — through
        /// <c>VesselTransformer.SetPose</c>, so its trail and camera are carried across the jump
        /// (TeleportContinuity) and a gate watcher sees a teleport, not a fast frame. Its drawn
        /// spaghettification relaxes as it leaves (§11). A smooth well (the crystal style) carries pilots
        /// through its mouths instead.
        /// </summary>
        static bool TryCarryThrough(VesselStatus vessel, VesselTransformer transformer, BlackHolePhysics.NativeWells wells, Vector3 p)
        {
            for (int w = 0; w < wells.Count; w++)
            {
                var hole = _wellHoles[w];
                if (hole == null || hole.IsSource || hole.IsSmooth || hole.IsDespawning) continue;
                var exitHole = hole.Throat;
                if (exitHole == null || exitHole.IsDespawning) continue;
                var centre = hole.transform.position;
                if ((p - centre).sqrMagnitude > hole.HorizonRadius * hole.HorizonRadius) continue;
                var white = exitHole.transform.position;
                var exit = BlackHolePairMath.ExitPosition(p, centre, white, exitHole.HorizonRadius, white - centre);
                transformer.SetPose(new Pose(exit, vessel.transform.rotation));
                VesselTransitsTotal++;
                CSDebug.LogVerbose(CSLogChannel.BlackHole, $"[BlackHole] {vessel.name} carried through #{hole.Id} → #{exitHole.Id}");
                return true;
            }
            return false;
        }

        /// <summary>The gravitational velocity a vessel is currently carrying (diagnostics, tests).</summary>
        public static bool TryGetPull(VesselStatus vessel, out Vector3 pull)
        {
            pull = Vector3.zero;
            return vessel != null && _pullByVessel.TryGetValue(vessel.GetInstanceID(), out pull);
        }

        /// <summary>Floor on the cruise speed the felt law is measured in (a hull with no throttle speed).</summary>
        const float MinCruise = 20f;

        // The hole behind each well this tick, in well order (felt law parameters live on the hole).
        static readonly BlackHole[] _wellHoles = new BlackHole[BlackHolePhysics.NativeWells.Capacity];

        /// <summary>
        /// The felt acceleration of a vessel at <paramref name="p"/> toward (attractor) or away from
        /// (repulsor) a well at <paramref name="centre"/>:
        /// <c>sign · k · cruise² · R_t · s · r / (d² + R_t²)^1.5</c> — inverse-square far out when nothing
        /// is warped (s = 1), 1/d under a radial warp (s ∝ d), and SMOOTH through the throat: Plummer-
        /// softened by the throat radius, so it rises, peaks just inside the throat and falls to zero at
        /// the centre with no edge anywhere (Docs/CRYSTAL_WORMHOLE.md). In FELT units (the transformer
        /// scales the velocity channel by s); pure, and tested.
        /// </summary>
        public static float3 FeltAcceleration(float3 p, float3 centre, float sign, float k, float cruise,
            float throatRadius, float warp)
        {
            float3 r = p - centre;
            float rt = math.max(throatRadius, 1e-3f);
            float q = math.lengthsq(r) + rt * rt;
            float mag = k * cruise * cruise * rt * warp / (q * math.sqrt(q));
            return r * (-sign * mag);
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
            for (int i = 0; i < _wellHoles.Length; i++) _wellHoles[i] = null;
            for (int i = 0; i < holes.Count && i < BlackHolePhysics.NativeWells.Capacity; i++)
            {
                var h = holes[i];
                if (h == null) continue;
                _wellOwners[wells.Count] = h.OwnerVessel;
                _wellHoles[wells.Count] = h;
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

                // Through the pair: a vessel that crosses a PAIRED black hole's horizon — anyone's, by
                // its own flying — comes out of the white hole (Docs/BLACK_HOLE.md §11).
                if (TryCarryThrough(vessel, transformer, wells, p))
                {
                    _pullByVessel.Remove(key);
                    continue;
                }

                float3 a = float3.zero;
                bool inside = false;
                float cap = maxSpeed;
                float cruise = Mathf.Max(MinCruise, transformer.CruiseSpeed);
                float warp = WarpFieldRuntime.ScaleAt(p);
                for (int w = 0; w < wells.Count; w++)
                {
                    var well = wells[w];
                    var hole = _wellHoles[w];
                    // An owned hole (a pilot's slung pair) never moves an opposing vessel
                    // (Docs/ELEMENTAL_ECONOMY.md §9). An owned DRIFT pair does not pull its owner either:
                    // its owner flies the orbit (StoatSlingExecutor), which IS that pull, held to a circle.
                    // An owned crystal pair keeps charming-cerf's felt pull on its owner. Environmental
                    // holes move everyone.
                    if (_wellOwners[w] != null && (_wellOwners[w] != vessel.transform || hole == null || !hole.IsSmooth)) continue;
                    float d = math.length(pos - well.Position);
                    if (hole != null && hole.VesselFeltStrength > 0f)
                    {
                        // The FELT law (Docs/BLACK_HOLE.md §12): measured in the hull's own cruise
                        // speed and the hole's throat, in the vessel's own frame — so a source is a
                        // headwind every hull has to boost through and a sink a current that carries
                        // every hull in, and under a warp it is felt across the whole approach. The
                        // config's vesselPullScale is for the physical pull only, so it is divided out.
                        if (d > hole.VesselFeltReach) continue;
                        inside = true;
                        cap = Mathf.Max(cap, hole.VesselFeltCap * cruise);
                        a += FeltAcceleration(pos, well.Position, hole.Sign, hole.VesselFeltStrength * hole.Amplitude,
                            cruise, hole.FeltThroatRadius, warp) / math.max(scale, 1e-4f);
                        continue;
                    }
                    if (d <= well.InfluenceRadius) inside = true;
                    a += BlackHolePhysics.Acceleration(pos, well);
                }

                // In a warped world (Docs/WARP_FIELD.md) the transformer multiplies this whole
                // velocity channel by the vessel's local scale, as it does the engine's speed, so
                // the pull is felt in the vessel's own units and the escape rule above holds in the
                // player's frame — while the hole, measured in their shrunken lengths, is enormous.
                var dv = new Vector3(a.x, a.y, a.z) * (scale * dt);
                pull += dv;
                if (!inside && config.ReleaseDamping > 0f)
                    pull *= Mathf.Exp(-config.ReleaseDamping * dt);
                pull = Vector3.ClampMagnitude(pull, cap);

                if (pull.sqrMagnitude < 1e-4f)
                {
                    _pullByVessel.Remove(key);
                    continue;
                }

                _pullByVessel[key] = pull;
                // A felt pull above the transformer's own ceiling raises it for this entry, or a fast
                // hull's headwind would be clipped to the 100 u/s every shove shares.
                transformer.ModifyVelocity(pull / FreshModifierWeight, dt, false,
                    cap > transformer.VelocityModifierCeiling ? cap : 0f);
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
