using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the Dolphin's Echo Sight: while the pilot holds the sight, every prism
    /// standing inside the volume the next crystal blast would sweep lights up.
    ///
    /// It publishes exactly FIVE global shader uniforms once per frame and does nothing else.
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
        static readonly int ParamsId = Shader.PropertyToID("_PrismSightParams");
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

            // Three direction/point vectors plus one params vector. The scalars ride their own
            // vector rather than the others' w channels because the prism graphs carry Vector3
            // property donors and no Vector4 one — synthesising a property type neither graph
            // contains is exactly the hand-authored schema the asset-surgery protocol forbids.
            //   Params = (height, core radius per unit depth, capsule half-length per unit depth)
            //   height <= 0 is the shader's "sight off" sentinel.
            Shader.SetGlobalVector(ApexId, volume.Apex);
            Shader.SetGlobalVector(AxisId, volume.Axis);
            Shader.SetGlobalVector(GapeId, volume.GapeAxis);
            Shader.SetGlobalVector(ParamsId,
                new Vector4(volume.Height, volume.TanCorePerUnit, volume.TanGapePerUnit, 0f));

            // Its own scalar rather than Params' spare slot: a fade sharing a vector with the
            // blast's geometry reads fine today and gets misinterpreted six months from now.
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
            // Everything is zeroed so nothing stale survives into a later frame; Params.x <= 0 is
            // the sentinel the shader actually branches on.
            Shader.SetGlobalVector(ApexId, Vector4.zero);
            Shader.SetGlobalVector(AxisId, Vector4.zero);
            Shader.SetGlobalVector(GapeId, Vector4.zero);
            Shader.SetGlobalVector(ParamsId, Vector4.zero); // x <= 0 is the shader's "off" sentinel
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
