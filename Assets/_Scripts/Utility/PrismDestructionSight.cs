using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the Dolphin's Echo Sight: while the pilot holds the sight, every prism
    /// standing inside the volume the next crystal blast would sweep lights up.
    ///
    /// It publishes exactly THREE global shader uniforms once per frame and does nothing else.
    /// There is no per-prism work of any kind — no trigger volumes, no spatial query, no material
    /// swaps, no per-instance overrides, no tracking list. The containment test runs per fragment
    /// in <c>PrismDestructionSight.hlsl</c>, wired into the prism graphs.
    ///
    /// <para><b>Why a global uniform and not a query.</b> "Is this prism in the blast" is
    /// camera-and-vessel-relative LIVE data: the answer changes every frame for every prism as the
    /// ship turns and the energy meter fills. So it can never be a per-prism stamp — and the
    /// clock-material law's escape hatch for exactly this case (Docs/PRISM_ANIMATION.md §1,
    /// "animation vs. live gameplay data"; §4.7, the ONE sanctioned shape for a view-dependent
    /// prism visual) is a global uniform: one O(1) write per frame that every prism reads. This is
    /// the sibling of <see cref="PrismOcclusionCorridor"/> and earns its per-frame write the same
    /// way. Running <c>PrismSpatialIndex</c>'s conic sweep every frame just to tint would be the
    /// per-prism CPU pass the law exists to prevent.</para>
    ///
    /// <para><b>The volume is not re-derived here.</b> It comes from
    /// <c>VesselExplosionByCrystalEffectSO.TryResolveBlastVolume</c>, which reads the same authored
    /// scales, the same energy resource and the same Space multiplier the detonation itself uses.
    /// A sight that computed its own cone would be a lie the first time anyone retuned a scale —
    /// and a targeting aid that lies is worse than none.</para>
    ///
    /// Unlike the occlusion corridor and the speed tunnel this is NOT a platform law: it is one
    /// vessel's ability, engaged only while its trigger is held, and it is off for everyone else.
    /// </summary>
    public static class PrismDestructionSight
    {
        static readonly int ApexId = Shader.PropertyToID("_PrismSightApex");
        static readonly int AxisId = Shader.PropertyToID("_PrismSightAxis");
        static readonly int GapeId = Shader.PropertyToID("_PrismSightGape");
        static readonly int StrengthId = Shader.PropertyToID("_PrismSightStrength");

        static bool _publishedActive;

        /// <summary>True while a sight is publishing a live volume.</summary>
        public static bool IsActive => _publishedActive;

        /// <summary>
        /// Publish the volume to highlight. <paramref name="strength01"/> fades the highlight in
        /// and out so the sight never pops on — continuity of existence applies to a targeting
        /// overlay as much as to mass.
        ///
        /// Called every frame by the engaged sight executor; call <see cref="Clear"/> on release.
        /// </summary>
        public static void Publish(in BlastVolume volume, float strength01)
        {
            strength01 = Mathf.Clamp01(strength01);
            if (!volume.IsValid || volume.Height <= 0f || strength01 <= 0.001f)
            {
                Clear();
                return;
            }

            // w channels carry the scalars so the whole volume costs three vectors:
            //   Apex.w = height        (<= 0 is the shader's "sight off" sentinel)
            //   Axis.w = core radius per unit depth
            //   Gape.w = capsule half-length per unit depth
            Shader.SetGlobalVector(ApexId,
                new Vector4(volume.Apex.x, volume.Apex.y, volume.Apex.z, volume.Height));
            Shader.SetGlobalVector(AxisId,
                new Vector4(volume.Axis.x, volume.Axis.y, volume.Axis.z, volume.TanCorePerUnit));
            Shader.SetGlobalVector(GapeId,
                new Vector4(volume.GapeAxis.x, volume.GapeAxis.y, volume.GapeAxis.z, volume.TanGapePerUnit));

            // Kept as its own scalar rather than squeezed into a spare w: all three w channels are
            // already carrying geometry, and a fade that shares a slot with a tangent is the kind
            // of packing that reads fine today and is misinterpreted six months from now.
            Shader.SetGlobalFloat(StrengthId, strength01);

            _publishedActive = true;
        }

        /// <summary>Turn the sight off. Idempotent — safe to call every frame while disengaged.</summary>
        public static void Clear()
        {
            if (!_publishedActive) return;
            PublishOff();
        }

        static void PublishOff()
        {
            // Apex.w <= 0 is the shader's "off" sentinel; the rest are zeroed so nothing stale
            // survives into a later frame.
            Shader.SetGlobalVector(ApexId, Vector4.zero);
            Shader.SetGlobalVector(AxisId, Vector4.zero);
            Shader.SetGlobalVector(GapeId, Vector4.zero);
            Shader.SetGlobalFloat(StrengthId, 0f);
            _publishedActive = false;
        }

        /// <summary>
        /// Shader globals survive play-mode exit in the editor, so a sight left engaged when play
        /// stopped would otherwise keep highlighting around a vessel that no longer exists. Publish
        /// the off state before anything renders — the same guard the occlusion corridor installs.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            _publishedActive = true; // force PublishOff to actually write
            PublishOff();
        }
    }
}
