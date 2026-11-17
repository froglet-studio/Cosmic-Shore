#if UNITY_EDITOR
using System.IO;
using CosmicShore.ECS;
using NUnit.Framework;
using Unity.Mathematics;
using Q = CosmicShore.ECS.PrismGrowStampClearQueue;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The entity grow stamp clears itself once the bloom is exactly final (Docs/PRISM_ANIMATION.md C23): every
    /// <c>PrismRenderService.StampGrow</c> pushes one entry on the shared <see cref="PrismGrowStampClearQueue"/>,
    /// drained by the render service's LateUpdate flush. Before that, nothing cleared it on the entity path, so
    /// every prism that ever bloomed ran PrismGrowScale's exp() and three per-instance loads per vertex for life.
    /// The clear must be bit-invisible: at the delay, the shader's float32 factor is exactly 1.0f on every axis.
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

        // ---------------- the exact time ----------------

        [Test]
        public void AtTheDelay_TheShaderFactorIsExactlyOne_OnEveryAxis()
        {
            foreach (float k in Rates)
            foreach (float a in StartFracs)
            foreach (float b in StartFracs)
            {
                var s0 = new float3(a, b, 0.5f * (a + b));
                float t = Q.ExactDelay(k, in s0);
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
            Assert.Greater(Q.ExactDelay(k, in fromZero), settle);
        }

        [Test]
        public void AnUnstampedOrAlreadyFinalStamp_HasNoDelay()
        {
            var final = new float3(1f);
            var fromZero = new float3(0f);
            Assert.AreEqual(0f, Q.ExactDelay(2.5f, in final));
            Assert.AreEqual(0f, Q.ExactDelay(0f, in fromZero));
        }

        // ---------------- the queue ----------------

        static readonly PrismRenderHandle H = PrismRenderHandle.Invalid;

        [Test]
        public void Pops_InFireTimeOrder_WhateverThePushOrder()
        {
            var q = new Q(initialCapacity: 4);
            var rng = new Random(12345);
            const int n = 5000;
            for (int i = 0; i < n; i++) Assert.IsTrue(q.TryPush(rng.NextFloat(0f, 100f), i, in H));
            Assert.AreEqual(n, q.Count);
            Assert.GreaterOrEqual(q.Capacity, n, "the array grows past its initial size");

            var into = new Q.Entry[n];
            Assert.AreEqual(n, q.PopDue(float.MaxValue, into, n));
            for (int i = 1; i < n; i++)
                Assert.LessOrEqual(into[i - 1].FireTime, into[i].FireTime, $"pop {i}");
            Assert.AreEqual(0, q.Count);
        }

        [Test]
        public void PopDue_TakesOnlyDueEntries_AndLeavesTheRest()
        {
            var q = new Q();
            q.TryPush(5f, 1f, in H);
            q.TryPush(1f, 2f, in H);
            q.TryPush(3f, 3f, in H);
            var into = new Q.Entry[8];

            Assert.AreEqual(0, q.PopDue(0.5f, into, 8), "nothing is due yet");
            Assert.AreEqual(3, q.Count);

            Assert.AreEqual(2, q.PopDue(3f, into, 8), "fire time <= now is due");
            Assert.AreEqual(1f, into[0].FireTime);
            Assert.AreEqual(2f, into[0].StampTime, "the stamp time travels with its entry");
            Assert.AreEqual(3f, into[1].FireTime);
            Assert.AreEqual(1, q.Count);
        }

        [Test]
        public void PopDue_IsBoundedByTheBudget_TheRestStayDue()
        {
            var q = new Q();
            for (int i = 0; i < 5000; i++) q.TryPush(i * 0.001f, 0f, in H);
            var into = new Q.Entry[PrismRenderService.GrowClearBudgetPerFrame];
            int budget = PrismRenderService.GrowClearBudgetPerFrame;

            Assert.AreEqual(budget, q.PopDue(100f, into, budget));
            Assert.AreEqual(5000 - budget, q.Count);
            Assert.AreEqual((budget - 1) * 0.001f, into[budget - 1].FireTime, 1e-6f, "the earliest went first");
            // A buffer smaller than the budget bounds it too.
            Assert.AreEqual(10, q.PopDue(100f, new Q.Entry[10], budget));
        }

        [Test]
        public void AFullQueue_RefusesThePush_InsteadOfGrowingPastItsCap()
        {
            var q = new Q(initialCapacity: 2, maxCapacity: 8);
            for (int i = 0; i < 8; i++) Assert.IsTrue(q.TryPush(i, 0f, in H));
            Assert.IsFalse(q.TryPush(9f, 0f, in H));
            Assert.AreEqual(8, q.Count);
            Assert.AreEqual(8, q.Capacity);
            q.Clear();
            Assert.AreEqual(0, q.Count);
            Assert.IsTrue(q.TryPush(1f, 0f, in H), "a cleared queue takes pushes again");
        }

        // ---------------- the stale-entry rule ----------------

        [Test]
        public void AStaleEntry_IsANoOp()
        {
            var s0 = new float3(0f);
            // Re-stamped (a retarget, a restore, a pool reuse that bloomed again): a newer start time owns it.
            Assert.AreEqual(Q.Verdict.Skip, Q.Decide(10f, 12f, 2.5f, in s0, 100f, out _));
            // Already cleared (pool reuse, CompleteImmediately's MeshRenderer twin, or this queue earlier).
            var cleared = new float3(1f);
            Assert.AreEqual(Q.Verdict.Skip, Q.Decide(0f, 0f, 0f, in cleared, 100f, out _));
        }

        [Test]
        public void ItsOwnStamp_ClearsWhenExact_AndRefiresWhenASameInstantRestampEndsLater()
        {
            const float start = 10f, k = 2.5f;
            var near = new float3(0.9f);
            var far = new float3(0f);
            float nearDue = start + Q.ExactDelay(k, in near);
            float farDue = start + Q.ExactDelay(k, in far);
            Assert.Less(nearDue, farDue);

            Assert.AreEqual(Q.Verdict.Clear, Q.Decide(start, start, k, in near, nearDue, out _));
            // The entry was pushed for the near bloom, but the entity now carries a far one stamped the same
            // instant (same start time): it must not cut that bloom short.
            Assert.AreEqual(Q.Verdict.Refire, Q.Decide(start, start, k, in far, nearDue, out float refire));
            Assert.AreEqual(farDue, refire);
            Assert.AreEqual(Q.Verdict.Clear, Q.Decide(start, start, k, in far, refire, out _));
        }

        [Test]
        public void EveryEntityStamp_PushesItsClear_AndTheFlushHostDrainsIt()
        {
            string src = File.ReadAllText("Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs");
            StringAssert.Contains("EnqueueGrowStampClear(in handle, startTime, rate, in startFrac);", src);
            StringAssert.Contains("FlushGrowStampClears();", src);
        }
    }
}
#endif
