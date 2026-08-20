using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the pure functions behind the Urchin's chain reaction and its Slip window.
    ///
    /// These live under an Editor/ folder deliberately (see CLAUDE.md): a test anywhere else
    /// compiles into Assembly-CSharp and ships into the player, where the IL2CPP linker hits
    /// its NUnit attributes and kills the Windows build - a failure the compile tier and the
    /// edit-mode suite are both structurally blind to.
    /// </summary>
    public class UrchinChainReactionTests
    {
        // ---------------------------------------------------------------- depth curve

        [Test]
        public void GenerationsForLevel_AnchorsOnTheAuthoredPair()
        {
            // The authored endpoints ARE the shipped feel: level 0 and level 10 must return
            // exactly what the asset says, with no curve of their own in between.
            Assert.AreEqual(1, UrchinSpikeActionSO.GenerationsForLevel(0, 1, 3));
            Assert.AreEqual(3, UrchinSpikeActionSO.GenerationsForLevel(10, 1, 3));
        }

        [Test]
        public void GenerationsForLevel_ExtrapolatesAcrossTheFullElementBand()
        {
            // The element system runs [-5, 15], not [0, 10] - a starved level and an overcharged
            // one both extrapolate rather than clamping to the authored pair.
            Assert.AreEqual(0, UrchinSpikeActionSO.GenerationsForLevel(-5, 1, 3),
                "A starved Charge level should shorten the cascade below its resting depth.");
            Assert.AreEqual(4, UrchinSpikeActionSO.GenerationsForLevel(15, 1, 3),
                "Full overcharge should deepen it past the authored level-10 value.");
        }

        [Test]
        public void GenerationsForLevel_ClampsToWhatThePoolsAndBudgetCanCarry()
        {
            // 0..4 is not cosmetic: ProjectileFactory has three tiers and the per-frame volley
            // budget is sized for this range. An asset authored past it must not escape.
            Assert.AreEqual(4, UrchinSpikeActionSO.GenerationsForLevel(15, 4, 9));
            Assert.AreEqual(0, UrchinSpikeActionSO.GenerationsForLevel(-5, 0, 0));
        }

        [Test]
        public void GenerationsForLevel_ZeroIsReachable()
        {
            // Zero is TERMINAL - it is what stops the cascade. A curve that could never reach
            // it would make the depth cap unreachable and leave only the emergent brake.
            Assert.AreEqual(0, UrchinSpikeActionSO.GenerationsForLevel(0, 0, 1),
                "The barrage is authored with no chain at rest; that must survive the curve.");
        }

        // ---------------------------------------------------------------- ghost window

        [Test]
        public void GhostSecondsForLevel_AnchorsAndExtrapolates()
        {
            Assert.AreEqual(0.6f, UrchinSlipActionSO.GhostSecondsForLevel(0, 0.6f, 1.6f), 1e-4f);
            Assert.AreEqual(1.6f, UrchinSlipActionSO.GhostSecondsForLevel(10, 0.6f, 1.6f), 1e-4f);
            Assert.AreEqual(2.1f, UrchinSlipActionSO.GhostSecondsForLevel(15, 0.6f, 1.6f), 1e-4f);
        }

        [Test]
        public void GhostSecondsForLevel_NeverGoesNegative()
        {
            // A negative ghost would skip the intangibility entirely and re-latch the vessel
            // onto the trail it just left - the ability doing the opposite of its purpose.
            Assert.GreaterOrEqual(UrchinSlipActionSO.GhostSecondsForLevel(-5, 0.2f, 1.6f), 0f);
        }

        // ---------------------------------------------------------------- determinism

        [Test]
        public void DeterministicOrientation_IsRepeatable()
        {
            var a = Gun.DeterministicOrientation(new Vector3(10.02f, 3.1f, -7.4f), 2);
            var b = Gun.DeterministicOrientation(new Vector3(10.02f, 3.1f, -7.4f), 2);
            Assert.AreEqual(a, b, "The same volley must orient identically on every peer.");
        }

        [Test]
        public void DeterministicOrientation_AbsorbsSmallPositionalDisagreement()
        {
            // Two peers simulating the same spike will not agree to the last float. The quantum
            // is what makes their cascades re-converge instead of drifting further apart with
            // every generation.
            var a = Gun.DeterministicOrientation(new Vector3(10.02f, 3.10f, -7.40f), 2);
            var b = Gun.DeterministicOrientation(new Vector3(10.11f, 3.14f, -7.44f), 2);
            Assert.AreEqual(a, b, "Positions inside one quantum must produce one pattern.");
        }

        [Test]
        public void DeterministicOrientation_VariesWithOriginAndDepth()
        {
            var baseline = Gun.DeterministicOrientation(new Vector3(10.02f, 3.1f, -7.4f), 2);

            Assert.AreNotEqual(baseline,
                Gun.DeterministicOrientation(new Vector3(19.02f, 3.1f, -7.4f), 2),
                "A volley fired somewhere else must not reuse the same pattern.");

            Assert.AreNotEqual(baseline,
                Gun.DeterministicOrientation(new Vector3(10.02f, 3.1f, -7.4f), 1),
                "Successive generations at one point must not stack identical spikes.");
        }
    }
}
