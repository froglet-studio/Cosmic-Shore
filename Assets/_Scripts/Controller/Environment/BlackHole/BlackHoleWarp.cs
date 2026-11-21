using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CPU half of SPAGHETTIFICATION (Docs/BLACK_HOLE.md §5, Docs/PRISM_ANIMATION.md §4.7.4):
    /// every prism near a horizon is drawn stretched along the line to the hole and squeezed across
    /// it by the general-relativistic tidal tensor, in <c>PrismGravityWarp.hlsl</c>, in the vertex
    /// stage of both live prism graphs. Photons only: nothing here changes where any prism is or
    /// how big it is to gameplay; the gravity field moves mass, this draws what tides do to it.
    ///
    /// <para><b>One job: the bank.</b> One slot per live hole — centre + horizon in one float4,
    /// the tidal coefficient <c>GM·τ²</c> (eased by the hole's weight) + the reach in another —
    /// published once per frame as file-scope shader globals, with a master sentinel carrying the
    /// live count so an unpublished bank reads as "the loop does not execute". "Where is the hole
    /// relative to this prism" is live data no stamp could carry, so it is a §4.7 global: O(1)
    /// writes that every prism reads.</para>
    ///
    /// <para><b>No residency.</b> The stretch is one affine map per prism, which the authored
    /// 24-triangle prism represents exactly, so — unlike the first version's per-vertex slide —
    /// nothing is swapped to a high-poly mesh and no spatial query runs per frame.</para>
    ///
    /// A despawning hole keeps publishing with a falling weight until its ease completes, so the
    /// stretch lets go instead of snapping; the field has already stopped pulling by then.
    /// </summary>
    public static class BlackHoleWarp
    {
        /// <summary>
        /// Mirrors <c>PRISM_GRAVITY_WARP_SLOTS</c> in <c>PrismGravityWarp.hlsl</c> — change both together. Fewer than
        /// the hole budget (<see cref="BlackHolePhysics.NativeWells.Capacity"/>): the bank holds the holes NEAREST THE
        /// CAMERA, whose tides you can see; a far hole still pulls its prisms, it just does not stretch them on screen.
        /// </summary>
        public const int Slots = 4;

        static readonly int[] s_pick = new int[BlackHolePhysics.NativeWells.Capacity];
        static readonly float[] s_pickDistance = new float[BlackHolePhysics.NativeWells.Capacity];

        static readonly int CentreId = Shader.PropertyToID("_PrismGravityWarpCentre");
        static readonly int WeightId = Shader.PropertyToID("_PrismGravityWarpWeight");
        static readonly int ParamsId = Shader.PropertyToID("_PrismGravityWarpParams");
        static readonly int WaveCentreId = Shader.PropertyToID("_PrismGravityWaveCentre");
        static readonly int WaveAxisId = Shader.PropertyToID("_PrismGravityWaveAxis");
        static readonly int WaveParamsId = Shader.PropertyToID("_PrismGravityWaveParams");

        /// <summary>Mirrors <c>PRISM_GRAVITY_WAVE_SLOTS</c>: the newest gravitational waves the prisms feel (§5.2).</summary>
        public const int WaveSlots = 4;
        static readonly Vector4[] _waveCentre = new Vector4[WaveSlots];
        static readonly Vector4[] _waveAxis = new Vector4[WaveSlots];

        // Always sent at full length: Unity binds an array global at the length of its first
        // write, so a short write later would silently leave the tail of the previous bank live.
        static readonly Vector4[] _centre = new Vector4[Slots];
        static readonly Vector4[] _weight = new Vector4[Slots];
        static int _publishedCount;
        static int _publishedWaves;

        /// <summary>True while any hole is publishing a live slot.</summary>
        public static bool IsActive => _publishedCount > 0;

        /// <summary>How many holes are stretching prisms this frame (diagnostics).</summary>
        public static int LiveSlotCount => _publishedCount;

        /// <summary>
        /// The tide a VESSEL's drawn hull feels at <paramref name="position"/> — the CPU twin of the prisms'
        /// (<c>PrismGravityWarp.hlsl</c>): the strongest hole's log-stretch <c>|GM|·τ²/r³</c> (r floored at
        /// the horizon, faded by the warp weight), scaled by <see cref="BlackHoleConfigSO.VesselTideScale"/>
        /// and eased into the same <c>ln(maxTidalStretch)</c> ceiling. Even under time reversal, so a hull
        /// leaving a white hole is stretched exactly as one falling into a black hole and relaxes as it goes
        /// (§11). Horizon holes only; <paramref name="towardHole"/> is the unit line to the strongest one.
        /// 0 with no hole near.
        /// </summary>
        public static float VesselLogStretch(Vector3 position, out Vector3 towardHole)
        {
            towardHole = Vector3.forward;
            var config = BlackHoleRegistry.Config;
            // The Mobile look never stretches a hull (Docs/BLACK_HOLE.md §5.4): no space warping.
            if (!config.SpaceWarp || !config.WarpEnabled || config.VesselTideScale <= 0f || config.TidalResponseSeconds <= 0f) return 0f;
            float tau2 = config.TidalResponseSeconds * config.TidalResponseSeconds;
            float best = 0f;
            var holes = BlackHoleRegistry.Holes;
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                if (h == null || h.IsSmooth) continue;
                var d = h.transform.position - position;
                float r = CosmicShore.Utility.MathfNoAlloc.Max(d.magnitude, h.HorizonRadius, 1e-3f); // 3-value Mathf.Max binds to params float[] and allocates per call
                if (r > h.WarpReach) continue;
                float eps = Mathf.Abs(h.GM) * tau2 * h.WarpWeight / (r * r * r);
                if (eps <= best) continue;
                best = eps;
                towardHole = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
            }
            if (best <= 0f) return 0f;
            float ceiling = Mathf.Log(config.MaxTidalStretch);
            float x = Mathf.Min(best * config.VesselTideScale / ceiling, 1e4f);
            return ceiling * x / Mathf.Sqrt(Mathf.Sqrt(1f + x * x * x * x));
        }

        /// <summary>
        /// Pack this frame's holes into the bank. <paramref name="holes"/> is every hole with a
        /// non-zero warp weight — live ones and those easing out.
        /// </summary>
        public static void Flush(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config)
        {
            // The Mobile look (the default) publishes an empty bank: no prism is stretched, and the prisms' vertex
            // stage takes its first branch (the sentinel is 0) — no per-vertex loop over holes or waves.
            bool enabled = config.SpaceWarp && config.WarpEnabled && config.IsSane && config.TidalResponseSeconds > 0f;
            float tau2 = config.TidalResponseSeconds * config.TidalResponseSeconds;
            int count = 0;
            if (enabled)
            {
                int picked = PickNearest(holes);
                for (int k = 0; k < picked && count < Slots; k++)
                {
                    var h = holes[s_pick[k]];
                    float w = h.WarpWeight;
                    var p = h.transform.position;
                    _centre[count] = new Vector4(p.x, p.y, p.z, h.HorizonRadius);
                    // GM·τ², eased by the weight: the tidal log-stretch at distance r is this / r³.
                    // z: a smooth well's Plummer core (0 = a black hole's tide, floored at the horizon).
                    // Tides are EVEN under time reversal: a white hole stretches what it emits exactly as
                    // a black hole stretches what it swallows, so a body leaving it starts a needle and
                    // relaxes — the capture run backwards (§11). A horizon hole therefore publishes |GM|;
                    // a smooth well keeps charming-cerf's signed tide (a source flattens, §12).
                    float tideGM = h.IsSmooth ? h.GM : Mathf.Abs(h.GM);
                    _weight[count] = new Vector4(tideGM * tau2 * w, h.WarpReach, h.Softening, 0f);
                    count++;
                }
            }
            for (int i = count; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _weight[i] = Vector4.zero;
            }

            // The gravitational waves (BlackHoleWaves, the studio's gwPrism): the newest four, by age.
            int waves = 0;
            if (config.SpaceWarp && config.WarpEnabled && config.GwPrism > 0f)
            {
                float now = BlackHoleWaves.Now;
                var live = BlackHoleWaves.Live;
                for (int n = live.Count - 1; n >= 0 && waves < WaveSlots; n--)
                {
                    var w = live[n];
                    if (w.Kind != BlackHoleWaves.Kind.Gravitational) continue;
                    _waveCentre[waves] = new Vector4(w.Centre.x, w.Centre.y, w.Centre.z, now - w.T0);
                    _waveAxis[waves] = new Vector4(w.Axis.x, w.Axis.y, w.Axis.z, w.Mass);
                    waves++;
                }
            }
            for (int i = waves; i < WaveSlots; i++) { _waveCentre[i] = Vector4.zero; _waveAxis[i] = Vector4.zero; }

            // Nothing to say and nothing said last frame: the write is skipped entirely, so a
            // match with no hole in it costs this system nothing per frame.
            if (count == 0 && waves == 0 && _publishedCount == 0 && _publishedWaves == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            Shader.SetGlobalVectorArray(WaveCentreId, _waveCentre);
            Shader.SetGlobalVectorArray(WaveAxisId, _waveAxis);
            Shader.SetGlobalVector(WaveParamsId, new Vector4(config.GwSpeed, config.GwPeriod, config.GwRingdown, config.GwPrism));
            // x: the stretch ceiling as a log; y: the holes (the shader's sentinel is y + z: 0 = the map does not run);
            // z: how many waves the prisms feel.
            Shader.SetGlobalVector(ParamsId, new Vector4(Mathf.Log(config.MaxTidalStretch), count, waves, 0f));
            _publishedCount = count;
            _publishedWaves = waves;
        }

        /// <summary>
        /// The live (weighted) holes, nearest the view camera first, into <c>s_pick</c>. Returns how many.
        /// With no camera the list order stands.
        /// </summary>
        static int PickNearest(IReadOnlyList<BlackHole> holes)
        {
            var cam = BlackHoleLens.ViewCamera();
            Vector3 eye = cam ? cam.transform.position : Vector3.zero;
            int n = 0;
            for (int i = 0; i < holes.Count && n < s_pick.Length; i++)
            {
                var h = holes[i];
                if (h == null || h.WarpWeight <= 0.001f) continue;
                float d = cam ? (h.transform.position - eye).sqrMagnitude : n;
                int j = n++;
                while (j > 0 && s_pickDistance[j - 1] > d) { s_pick[j] = s_pick[j - 1]; s_pickDistance[j] = s_pickDistance[j - 1]; j--; }
                s_pick[j] = i;
                s_pickDistance[j] = d;
            }
            return n;
        }

        // ---------------- Lifecycle ----------------

        internal static void PublishOff()
        {
            for (int i = 0; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _weight[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            _publishedCount = 0;
            _publishedWaves = 0;
        }

        /// <summary>Play-mode (re)entry: publish the off state before anything renders.</summary>
        internal static void ResetOnLoad() => PublishOff();
    }
}
