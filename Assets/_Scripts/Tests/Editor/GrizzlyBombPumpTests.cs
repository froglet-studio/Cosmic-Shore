using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Grizzly bomb pump's one rule: a bomb's size is the trigger PRESSURE, mapped through the
    /// shipped config. Tested against the shipped asset defaults (a fresh instance carries the same
    /// field initializers) so a retune that breaks monotonicity or the end points fails here.
    /// </summary>
    [TestFixture]
    public class GrizzlyBombPumpTests
    {
        GrizzlyBombPumpConfigSO _cfg;

        [SetUp] public void SetUp() => _cfg = ScriptableObject.CreateInstance<GrizzlyBombPumpConfigSO>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_cfg);

        [Test]
        public void LightPressIsTheSmallestBomb()
        {
            Assert.AreEqual(0f, _cfg.SizeForPressure(0f), 1e-5f);
            Assert.AreEqual(0f, _cfg.SizeForPressure(_cfg.PressureForMinBomb), 1e-5f);
        }

        [Test]
        public void FullSqueezeIsTheBiggestBomb()
        {
            Assert.AreEqual(1f, _cfg.SizeForPressure(1f), 1e-5f);
            Assert.AreEqual(1f, _cfg.SizeForPressure(_cfg.PressureForMaxBomb), 1e-5f);
        }

        [Test]
        public void SizeRisesMonotonicallyWithPressure()
        {
            float prev = -1f;
            for (float p = 0f; p <= 1.0001f; p += 0.01f)
            {
                float s = _cfg.SizeForPressure(p);
                Assert.GreaterOrEqual(s, prev, $"size fell at pressure {p}");
                prev = s;
            }
        }

        [Test]
        public void KickAndBlastSpanTheirAuthoredRanges()
        {
            Assert.AreEqual(_cfg.MinKick, _cfg.KickForSize(0f), 1e-4f);
            Assert.AreEqual(_cfg.MaxKick, _cfg.KickForSize(1f), 1e-4f);
            Assert.AreEqual(_cfg.MinBlastScale, _cfg.BlastScaleForSize(0f), 1e-4f);
            Assert.AreEqual(_cfg.MaxBlastScale, _cfg.BlastScaleForSize(1f), 1e-4f);
            Assert.Greater(_cfg.MaxKick, _cfg.MinKick, "a harder squeeze must kick harder");
        }

        // ── Autopilot drive (Grizzly Time: an AI Grizzly that cannot pump races at cruise) ──

        [Test]
        public void AutopilotPumpsFullOnAStraightStick()
        {
            Assert.Greater(_cfg.AiPumpStickBand, 0f,
                "the shipped band is 0, which disables the drive - every AI Grizzly races at cruise");
            Assert.AreEqual(1f, _cfg.AutopilotPumpSize(0f), 1e-5f);
            Assert.AreEqual(1f, _cfg.AutopilotPumpSize(_cfg.AiPumpStickBand), 1e-5f);
            Assert.AreEqual(1f, _cfg.AutopilotPumpSize(-_cfg.AiPumpStickBand), 1e-5f,
                "a left turn and a right turn of the same deflection must pump the same");
        }

        [Test]
        public void AutopilotEasesToTheSmallestBombAtFullDeflection()
        {
            Assert.AreEqual(0f, _cfg.AutopilotPumpSize(1f), 1e-5f);
            float prev = 2f;
            for (float s = 0f; s <= 1.0001f; s += 0.01f)
            {
                float size = _cfg.AutopilotPumpSize(s);
                Assert.LessOrEqual(size, prev, $"autopilot bomb grew at stick {s}");
                prev = size;
            }
        }
    }
}
