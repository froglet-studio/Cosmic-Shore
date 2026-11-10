#if UNITY_EDITOR
using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Mathematics;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The entity grow stamp clears itself once the bloom is exactly final (<c>PrismScaleAnimator</c>, the
    /// scheduled <c>ClearGrowStampIfStartedAt</c>). Before that, nothing cleared it on the entity path, so every
    /// prism that ever bloomed ran PrismGrowScale's exp() and three per-instance loads per vertex for life. The
    /// clear must be bit-invisible: at the delay, the shader's float32 factor is exactly 1.0f on every axis.
    /// </summary>
    public class PrismGrowStampClearTests
    {
        /// <summary>PrismGrowScale_float (PrismClockAnimation.hlsl) in float32, one axis.</summary>
        static float ShaderFactor(float t, float rate, float startFrac)
        {
            float decay = (float)System.Math.Exp((float)(-rate * math.max(t, 0f)));
            return math.max((float)(1f - (float)((1f - startFrac) * decay)), 0f);
        }

        static readonly float[] Rates = { 1.25f, 1.6f, 1.875f, 2.2f, 2.5f }; // ClockRateK spans [1.25, 2.5]
        static readonly float[] StartFracs = { 0f, 1e-6f, 0.25f, 0.5f, 0.99f, 0.9999f, 1.0001f, 1.5f, 3f, 10f, 60f };

        [Test]
        public void AtTheDelay_TheShaderFactorIsExactlyOne_OnEveryAxis()
        {
            foreach (float k in Rates)
            foreach (float a in StartFracs)
            foreach (float b in StartFracs)
            {
                var s0 = new float3(a, b, 0.5f * (a + b));
                float t = PrismScaleAnimator.GrowStampExactDelay(k, in s0);
                foreach (float late in new[] { t, t + 0.016f, t * 2f + 1f })
                {
                    Assert.AreEqual(1f, ShaderFactor(late, k, s0.x), $"k={k} s0={s0} t={late}");
                    Assert.AreEqual(1f, ShaderFactor(late, k, s0.y), $"k={k} s0={s0} t={late}");
                    Assert.AreEqual(1f, ShaderFactor(late, k, s0.z), $"k={k} s0={s0} t={late}");
                }
            }
        }

        [Test]
        public void TheLegacySettleTime_IsNotYetExact_SoTheClearWaitsLonger()
        {
            // Negative control: the 0.1-unit legacy settle (PrismScaleAnimator._clockSettleTime) is visibly short
            // of 1 for a from-zero bloom of a 1-unit prism, which is why the clear has its own, later time.
            const float k = 2.5f;
            float settle = math.log(1f / 0.1f) / k;
            Assert.AreNotEqual(1f, ShaderFactor(settle, k, 0f));
            var fromZero = new float3(0f);
            Assert.Greater(PrismScaleAnimator.GrowStampExactDelay(k, in fromZero), settle);
        }

        [Test]
        public void AnUnstampedOrAlreadyFinalStamp_ClearsImmediately()
        {
            var final = new float3(1f);
            var fromZero = new float3(0f);
            Assert.AreEqual(0f, PrismScaleAnimator.GrowStampExactDelay(2.5f, in final));
            Assert.AreEqual(0f, PrismScaleAnimator.GrowStampExactDelay(0f, in fromZero));
        }

        [Test]
        public void TheScheduledClear_IsGuardedByItsStampTime()
        {
            // A stale timer (re-stamped, or the prism pooled) must not clear a newer bloom: the clear goes through
            // the stamp-time-guarded entry point, never the unconditional ClearGrowStamp.
            string src = File.ReadAllText("Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs");
            StringAssert.Contains("ClearGrowStampIfStartedAt(in prism.RenderHandle, _growClearStampTime)", src);
            StringAssert.Contains("ScheduleGrowStampClear(now, k, in startFrac)", src);
            StringAssert.DoesNotContain("PrismRenderService.ClearGrowStamp(", src);
        }
    }
}
#endif
