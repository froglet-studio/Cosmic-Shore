using NUnit.Framework;
using CosmicShore.Gameplay.Audio;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Squirrel's drift loop is quiet at a shallow slip and plays at its old (full) volume at a
    /// large one. <see cref="DriftAudioController.SlipVolume"/> is the whole mapping; the controller
    /// only feeds it the angle between Course and the nose. Numbers are the shipped defaults.
    /// </summary>
    [TestFixture]
    public class DriftSlipVolumeTests
    {
        const float Quiet = 0f, Full = 180f, QuietVolume = 0.15f;

        static float V(float degrees) => DriftAudioController.SlipVolume(degrees, Quiet, Full, QuietVolume);

        [Test]
        public void SmallSlip_IsQuiet()
        {
            Assert.AreEqual(QuietVolume, V(0f), 1e-6f);
            Assert.AreEqual(QuietVolume, V(Quiet), 1e-6f);
        }

        [Test]
        public void LargeSlip_IsTheUnscaledVolume()
        {
            Assert.AreEqual(1f, V(Full), 1e-6f);
            Assert.AreEqual(1f, V(200f), 1e-6f);
        }

        [Test]
        public void Volume_RisesMonotonicallyWithSlip()
        {
            float previous = V(0f);
            for (float d = 0.5f; d <= 180f; d += 0.5f)
            {
                float v = V(d);
                Assert.GreaterOrEqual(v, previous - 1e-6f, $"volume fell at {d} degrees");
                previous = v;
            }
        }

        [Test]
        public void Volume_IsLinearInSlip()
        {
            Assert.AreEqual(QuietVolume + (1f - QuietVolume) * 0.25f, V(45f), 1e-5f);
            Assert.AreEqual(QuietVolume + (1f - QuietVolume) * 0.5f, V(90f), 1e-5f);
            Assert.AreEqual(QuietVolume + (1f - QuietVolume) * 0.75f, V(135f), 1e-5f);
        }

        [Test]
        public void DegenerateBand_StepsAtFull()
        {
            Assert.AreEqual(QuietVolume, DriftAudioController.SlipVolume(10f, 20f, 20f, QuietVolume), 1e-6f);
            Assert.AreEqual(1f, DriftAudioController.SlipVolume(20f, 20f, 20f, QuietVolume), 1e-6f);
        }
    }
}
