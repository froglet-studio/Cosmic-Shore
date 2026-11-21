#if UNITY_EDITOR
using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A restored prism grows again (Docs/PRISM_ANIMATION.md §3.3). <c>Prism.SetupDestruction</c> turns the prism's
    /// <see cref="PrismScaleAnimator"/> off, and until 2026-10-29 <c>Prism.Restore</c> never turned it back on, so every grow
    /// on a restored prism (<c>SetTargetScale</c>, <c>Grow</c>, <c>GrowAlong</c>) was a silent no-op: the Urchin's ride payoff
    /// (<c>GunVesselTransformer.ApplyPrismscapePayoff</c>) restores a prism and then grows it, and the growth never happened.
    /// <c>Restore</c> now calls <see cref="PrismScaleAnimator.RearmAfterRestore"/>; these tests hold that method (a full
    /// destroy-and-restore needs a live prism's Awake and its SOAP channels, which edit mode does not give) and that
    /// <c>Restore</c> calls it.
    /// </summary>
    public class PrismRestoreTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        /// <summary>An animator at a size, then switched off as SetupDestruction switches it off.</summary>
        PrismScaleAnimator DeadAnimatorAt(Vector3 size)
        {
            _go = new GameObject("restored prism");
            var animator = _go.AddComponent<PrismScaleAnimator>();
            animator.SetTargetScale(size);
            Assert.AreEqual(size, animator.TargetScale, "the control: a live animator takes a size");
            animator.enabled = false;
            return animator;
        }

        [Test]
        public void ADeadPrismDoesNotGrow_TheDefectThisFixes()
        {
            var animator = DeadAnimatorAt(new Vector3(1f, 1f, 2f));
            animator.SetTargetScale(new Vector3(1f, 1f, 4f));
            Assert.AreEqual(2f, animator.TargetScale.z, 1e-4f, "while switched off a grow is a no-op (what a restored prism used to be)");
        }

        [Test]
        public void ARestoredPrismKeepsItsSize_AndGrowsAgain()
        {
            var animator = DeadAnimatorAt(new Vector3(1f, 1f, 2f));
            animator.RearmAfterRestore();
            Assert.IsTrue(animator.enabled, "re-armed");
            Assert.AreEqual(2f, animator.TargetScale.z, 1e-4f, "at the size it died at");
            animator.SetTargetScale(new Vector3(1f, 1f, 4f));
            Assert.AreEqual(4f, animator.TargetScale.z, 1e-4f, "and it grows again");
            animator.RearmAfterRestore();
            Assert.AreEqual(4f, animator.TargetScale.z, 1e-4f, "re-arming a live animator changes nothing");
        }

        [Test]
        public void RestoreReArmsTheScaleAnimator()
        {
            string source = File.ReadAllText("Assets/_Scripts/Controller/Vessel/Prism.cs");
            int start = source.IndexOf("public void Restore()", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Prism.Restore exists");
            int end = source.IndexOf("public void NotifyPositionChanged()", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start, "Restore's body is followed by NotifyPositionChanged");
            StringAssert.Contains("RearmAfterRestore()", source.Substring(start, end - start),
                "Prism.Restore re-arms the scale animator SetupDestruction turned off");
        }
    }
}
#endif
