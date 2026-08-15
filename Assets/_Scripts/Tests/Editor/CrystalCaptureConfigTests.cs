using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The capture sequence's pure math. These guard the two properties that made the OLD capture
    /// read badly: it ran for seconds, and it moved at a constant rate toward a stale point.
    /// </summary>
    public class CrystalCaptureConfigTests
    {
        CrystalCaptureConfigSO _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<CrystalCaptureConfigSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void DefaultCapture_IsUnderHalfASecond()
        {
            // A crystal pickup is a flourish over a reward that has already landed; if it outlasts
            // its own payoff it reads as lag. The shipped asset is ~0.44s.
            Assert.Less(_config.TotalDuration, 0.5f);
        }

        [Test]
        public void ResolvePhase_WalksSnatchThenSuctionThenAbsorbThenDone()
        {
            Assert.AreEqual(CrystalCaptureConfigSO.Phase.Snatch, _config.ResolvePhase(0f, out _));
            Assert.AreEqual(CrystalCaptureConfigSO.Phase.Snatch,
                _config.ResolvePhase(_config.SnatchDuration * 0.5f, out _));
            Assert.AreEqual(CrystalCaptureConfigSO.Phase.Suction,
                _config.ResolvePhase(_config.SnatchDuration + _config.SuctionDuration * 0.5f, out _));
            Assert.AreEqual(CrystalCaptureConfigSO.Phase.Absorb,
                _config.ResolvePhase(_config.SnatchDuration + _config.SuctionDuration + _config.AbsorbDuration * 0.5f, out _));
            Assert.AreEqual(CrystalCaptureConfigSO.Phase.Done, _config.ResolvePhase(_config.TotalDuration, out _));
        }

        [Test]
        public void ResolvePhase_ProgressSpansZeroToOneWithinEachPhase()
        {
            _config.ResolvePhase(0f, out float atStart);
            Assert.AreEqual(0f, atStart, 1e-5f);

            _config.ResolvePhase(_config.SnatchDuration, out float suctionStart);
            Assert.AreEqual(0f, suctionStart, 1e-5f);

            _config.ResolvePhase(_config.SnatchDuration + _config.SuctionDuration * 0.999f, out float suctionEnd);
            Assert.Greater(suctionEnd, 0.99f);
        }

        [Test]
        public void FlightProgress_Accelerates_AndLandsExactlyOnTheVessel()
        {
            // Endpoints are exact, so the crystal starts at the recoil anchor and ends ON the hull.
            Assert.AreEqual(0f, _config.FlightProgress01(0f), 1e-5f);
            Assert.AreEqual(1f, _config.FlightProgress01(1f), 1e-5f);

            // The defining property: the first half of the flight covers less ground than a
            // straight lerp would. A linear ramp against a moving vessel is what made the old
            // capture read as the crystal being dragged rather than pulled in.
            Assert.Less(_config.FlightProgress01(0.5f), 0.5f);

            float previous = -1f;
            for (float u = 0f; u <= 1f; u += 0.05f)
            {
                float value = _config.FlightProgress01(u);
                Assert.GreaterOrEqual(value, previous, $"flight progress went backwards at u={u}");
                previous = value;
            }
        }

        [Test]
        public void ArcOffset_IsZeroAtBothEnds_AndPeaksInTheMiddle()
        {
            // The swing must close completely, or the crystal lands beside the ship.
            Assert.AreEqual(0f, CrystalCaptureConfigSO.ArcOffset01(0f), 1e-5f);
            Assert.AreEqual(0f, CrystalCaptureConfigSO.ArcOffset01(1f), 1e-5f);
            Assert.AreEqual(1f, CrystalCaptureConfigSO.ArcOffset01(0.5f), 1e-5f);
        }

        [Test]
        public void Scale_PopsOnTheSnatch_ThenCollapsesToNothing()
        {
            Assert.AreEqual(1f, _config.ScaleMultiplier(CrystalCaptureConfigSO.Phase.Snatch, 0f), 1e-5f);

            float peak = _config.ScaleMultiplier(CrystalCaptureConfigSO.Phase.Snatch, 1f);
            Assert.Greater(peak, 1f, "the snatch must overshoot - the pop is the grab");
            Assert.AreEqual(_config.SnatchScale, peak, 1e-5f);

            Assert.AreEqual(_config.SuctionEndScale,
                _config.ScaleMultiplier(CrystalCaptureConfigSO.Phase.Suction, 1f), 1e-5f);

            // Continuity of existence: the crystal is gone by geometry, never by a pop-out.
            Assert.AreEqual(0f, _config.ScaleMultiplier(CrystalCaptureConfigSO.Phase.Absorb, 1f), 1e-5f);
            Assert.AreEqual(0f, _config.ScaleMultiplier(CrystalCaptureConfigSO.Phase.Done, 0f), 1e-5f);
        }

        [Test]
        public void Opacity_HoldsUntilTheAbsorb_ThenDissolvesOut()
        {
            Assert.AreEqual(1f, CrystalCaptureConfigSO.Opacity(CrystalCaptureConfigSO.Phase.Snatch, 1f), 1e-5f);
            Assert.AreEqual(1f, CrystalCaptureConfigSO.Opacity(CrystalCaptureConfigSO.Phase.Suction, 1f), 1e-5f);
            Assert.AreEqual(1f, CrystalCaptureConfigSO.Opacity(CrystalCaptureConfigSO.Phase.Absorb, 0f), 1e-5f);
            Assert.AreEqual(0f, CrystalCaptureConfigSO.Opacity(CrystalCaptureConfigSO.Phase.Absorb, 1f), 1e-5f);
        }

        [Test]
        public void Spin_IsMonotonicAcrossPhaseBoundaries()
        {
            float snatchEnd = _config.SpinDegrees(CrystalCaptureConfigSO.Phase.Snatch, 1f);
            float suctionStart = _config.SpinDegrees(CrystalCaptureConfigSO.Phase.Suction, 0f);
            float suctionEnd = _config.SpinDegrees(CrystalCaptureConfigSO.Phase.Suction, 1f);

            // A discontinuity here is a visible snap in the tumble at a phase boundary.
            Assert.AreEqual(snatchEnd, suctionStart, 1e-4f);
            Assert.Greater(suctionEnd, suctionStart);
            Assert.AreEqual(_config.SpinRevolutions * 360f, suctionEnd, 1e-3f);
        }

        [Test]
        public void Flare_RisesMonotonicallyToTheAuthoredGain()
        {
            float snatchEnd = _config.FlareMultiplier(CrystalCaptureConfigSO.Phase.Snatch, 1f);
            float suctionStart = _config.FlareMultiplier(CrystalCaptureConfigSO.Phase.Suction, 0f);

            Assert.AreEqual(1f, _config.FlareMultiplier(CrystalCaptureConfigSO.Phase.Snatch, 0f), 1e-5f);
            Assert.AreEqual(snatchEnd, suctionStart, 1e-4f);
            Assert.AreEqual(_config.FlareGain, _config.FlareMultiplier(CrystalCaptureConfigSO.Phase.Suction, 1f), 1e-4f);
            Assert.AreEqual(_config.FlareGain, _config.FlareMultiplier(CrystalCaptureConfigSO.Phase.Absorb, 1f), 1e-4f);
        }

        [Test]
        public void ShippedAsset_MatchesTheDefaults()
        {
            var shipped = Resources.Load<CrystalCaptureConfigSO>(CrystalCaptureConfigSO.ResourcePath);
            Assert.IsNotNull(shipped,
                $"Resources/{CrystalCaptureConfigSO.ResourcePath} is missing - every elemental " +
                "crystal capture in the game reads its feel from that one asset.");
            Assert.Less(shipped.TotalDuration, 0.5f);
            Assert.Greater(shipped.SuctionAcceleration, 1f, "the shipped flight must accelerate");
        }
    }
}
