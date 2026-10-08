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
        /// Mirrors <c>PRISM_GRAVITY_WARP_SLOTS</c> in <c>PrismGravityWarp.hlsl</c> and
        /// <see cref="BlackHolePhysics.NativeWells.Capacity"/> — change all three together.
        /// </summary>
        public const int Slots = 4;

        static readonly int CentreId = Shader.PropertyToID("_PrismGravityWarpCentre");
        static readonly int WeightId = Shader.PropertyToID("_PrismGravityWarpWeight");
        static readonly int ParamsId = Shader.PropertyToID("_PrismGravityWarpParams");

        // Always sent at full length: Unity binds an array global at the length of its first
        // write, so a short write later would silently leave the tail of the previous bank live.
        static readonly Vector4[] _centre = new Vector4[Slots];
        static readonly Vector4[] _weight = new Vector4[Slots];
        static int _publishedCount;

        /// <summary>True while any hole is publishing a live slot.</summary>
        public static bool IsActive => _publishedCount > 0;

        /// <summary>How many holes are stretching prisms this frame (diagnostics).</summary>
        public static int LiveSlotCount => _publishedCount;

        /// <summary>
        /// Pack this frame's holes into the bank. <paramref name="holes"/> is every hole with a
        /// non-zero warp weight — live ones and those easing out.
        /// </summary>
        public static void Flush(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config)
        {
            bool enabled = config.WarpEnabled && config.IsSane && config.TidalResponseSeconds > 0f;
            float tau2 = config.TidalResponseSeconds * config.TidalResponseSeconds;
            int count = 0;
            if (enabled)
            {
                for (int i = 0; i < holes.Count && count < Slots; i++)
                {
                    var h = holes[i];
                    if (h == null) continue;
                    float w = h.WarpWeight;
                    if (w <= 0.001f) continue;
                    var p = h.transform.position;
                    _centre[count] = new Vector4(p.x, p.y, p.z, h.HorizonRadius);
                    // GM·τ², eased by the weight: the tidal log-stretch at distance r is this / r³.
                    // z: a smooth well's Plummer core (0 = a black hole's tide, floored at the horizon).
                    _weight[count] = new Vector4(h.GM * tau2 * w, h.WarpReach, h.Softening, 0f);
                    count++;
                }
            }
            for (int i = count; i < Slots; i++)
            {
                _centre[i] = Vector4.zero;
                _weight[i] = Vector4.zero;
            }

            // Nothing to say and nothing said last frame: the write is skipped entirely, so a
            // match with no hole in it costs this system nothing per frame.
            if (count == 0 && _publishedCount == 0) return;

            Shader.SetGlobalVectorArray(CentreId, _centre);
            Shader.SetGlobalVectorArray(WeightId, _weight);
            // x: the stretch ceiling as a log; y: the shader's MASTER sentinel (0 = the loop does not run).
            Shader.SetGlobalVector(ParamsId, new Vector4(Mathf.Log(config.MaxTidalStretch), count, 0f, 0f));
            _publishedCount = count;
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
        }

        /// <summary>Play-mode (re)entry: publish the off state before anything renders.</summary>
        internal static void ResetOnLoad() => PublishOff();
    }
}
