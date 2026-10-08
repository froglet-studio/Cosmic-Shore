using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Grizzly trigger bombs' one rule: a pull's PEAK pressure is the commitment - it sets the
    /// bomb's size, the blast's size and the ammo spent together - and a pull the pool cannot pay
    /// for in full fires the biggest bomb it can. Tested against a fresh config instance (its
    /// field initializers are the shipped asset's values) so a retune that breaks monotonicity,
    /// the end points or affordability fails here.
    /// </summary>
    [TestFixture]
    public class GrizzlyTriggerBombTests
    {
        GrizzlyTriggerBombConfigSO _cfg;

        [SetUp] public void SetUp() => _cfg = ScriptableObject.CreateInstance<GrizzlyTriggerBombConfigSO>();
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
        public void AHarderSqueezeCostsMoreAndBlowsBigger()
        {
            Assert.Greater(_cfg.MaxAmmoCost, _cfg.MinAmmoCost, "a full squeeze must cost more ammo than a tap");
            Assert.Greater(_cfg.MaxBlastScale, _cfg.MinBlastScale, "a full squeeze must blow a bigger blast");
            Assert.AreEqual(_cfg.MinAmmoCost, _cfg.AmmoCostForSize(0f), 1e-5f);
            Assert.AreEqual(_cfg.MaxAmmoCost, _cfg.AmmoCostForSize(1f), 1e-5f);
            Assert.AreEqual(_cfg.MinBlastScale, _cfg.BlastScaleForSize(0f), 1e-4f);
            Assert.AreEqual(_cfg.MaxBlastScale, _cfg.BlastScaleForSize(1f), 1e-4f);
            Assert.Greater(_cfg.ProjectileScaleForSize(1f), _cfg.ProjectileScaleForSize(0f),
                "the visible bomb must say how big the blast will be");
        }

        [Test]
        public void APoolThatCanPayFiresTheRequestedBomb()
        {
            Assert.AreEqual(1f, _cfg.AffordableSize(1f, 1f), 1e-5f);
            Assert.AreEqual(0.4f, _cfg.AffordableSize(0.4f, 1f), 1e-5f);
            Assert.AreEqual(1f, _cfg.AffordableSize(1f, _cfg.MaxAmmoCost), 1e-5f,
                "exactly enough ammo is enough");
        }

        [Test]
        public void AShortPoolFiresTheBiggestBombItCanPayFor()
        {
            float half = Mathf.Lerp(_cfg.MinAmmoCost, _cfg.MaxAmmoCost, 0.5f);
            float size = _cfg.AffordableSize(1f, half);
            Assert.AreEqual(0.5f, size, 1e-4f);
            Assert.LessOrEqual(_cfg.AmmoCostForSize(size), half + 1e-5f,
                "the bomb actually fired must never cost more than the pool holds");
        }

        [Test]
        public void APoolBelowTheSmallestBombFizzles()
        {
            Assert.Less(_cfg.AffordableSize(1f, _cfg.MinAmmoCost * 0.5f), 0f);
            Assert.Less(_cfg.AffordableSize(0f, 0f), 0f);
        }

        [Test]
        public void AFullPoolBuysMoreThanOneFullBomb()
        {
            // Two triggers, two bombs: a full pool that bought only one full squeeze would make
            // the second trigger dead on arrival every time.
            Assert.GreaterOrEqual(1f / _cfg.MaxAmmoCost, 2f);
        }

        // ── Autopilot (an AI Grizzly that cannot bomb never launches) ──

        [Test]
        public void AutopilotDriveIsOnAndFreezesInsideItsOwnBlast()
        {
            Assert.Greater(_cfg.AiFireStickBand, 0f,
                "the shipped band is 0, which disables the drive - every AI Grizzly flies at cruise");
            Assert.Less(_cfg.AiFreezeDistance, _cfg.MaxBlastScale * 0.5f,
                "an autopilot that freezes its bomb outside the blast radius is never launched by it");
            Assert.LessOrEqual(_cfg.AiDetonateDistance, _cfg.AiFreezeDistance);
        }
    }
}
