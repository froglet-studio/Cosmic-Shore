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
        public void AutopilotDriveIsOnAndDetonatesInsideItsOwnBlast()
        {
            Assert.Greater(_cfg.AiFireStickBand, 0f,
                "the shipped band is 0, which disables the drive - every AI Grizzly flies at cruise");
            Assert.Less(_cfg.AiFreezeDistance, _cfg.MaxBlastScale * 0.5f,
                "an autopilot that freezes its bomb outside the blast radius is never launched by it");
            Assert.Greater(_cfg.AiDetonateBehindDistance, 0f,
                "the launch is AWAY from the bomb, so the autopilot must blow it once it is BEHIND the hull");
            Assert.Less(_cfg.AiDetonateBehindDistance, _cfg.MaxBlastScale * 0.5f,
                "an autopilot that waits until its bomb is outside the blast is never launched by it");
        }

        // ── Small before, big after (design ask, 2026-10-08) ──

        [Test]
        public void TheBombIsSmallAndItsBlastIsHuge()
        {
            // A blast's SCALE is its diameter (the AOE sphere's collider radius is 0.5), as is the
            // projectile's. "Smaller before it explodes, bigger explosion": every bomb's blast must
            // be at least ten times the bomb, and a full squeeze's blast the biggest Grizzly blast.
            for (float size = 0f; size <= 1.0001f; size += 0.1f)
                Assert.GreaterOrEqual(_cfg.BlastScaleForSize(size), 10f * _cfg.ProjectileScaleForSize(size),
                    $"at size {size:F1} the blast is not ten times the bomb");
            Assert.Greater(_cfg.MaxBlastScale, 120f,
                "a full squeeze must out-blast the charged cannon's full charge (120)");
        }

        // ── Launch 3x, trigger-only detonation, danger shades (design ask, 2026-10-08) ──

        [Test]
        public void TheLaunchThrowsThreeTimesHarderThanAnyOtherShove()
        {
            // Every shove a vessel takes shares VesselTransformer's 100 u/s ceiling; the bomb
            // launch raises it for its own lifetime only. Read off the source so a change to the
            // shared cap names this test.
            const string transformer = "Assets/_Scripts/Controller/Vessel/VesselTransformer.cs";
            var m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(transformer),
                @"float velocityModifierMax = ([0-9.]+)f;");
            Assert.IsTrue(m.Success, "VesselTransformer.velocityModifierMax not found");
            float shared = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(3f * shared, _cfg.SelfLaunchCeiling, 1e-3f,
                "the bomb launch was asked to throw 3x as hard as the shared ceiling it used to sit on");
            Assert.AreEqual(4.5f, _cfg.SelfLaunchMultiplier, 1e-4f,
                "3x the 1.5 the launch first shipped at, so a launch under the ceiling is tripled too");
            // 1.2 s = AOEGrizzlyExplosion's ExplosionDuration; 0.5 = the launch's weakest weight
            // (cos ease 1.5 -> 0.5), so this is its LAST frame.
            Assert.GreaterOrEqual(_cfg.MaxBlastScale / 1.2f * _cfg.SelfLaunchMultiplier * 0.5f, _cfg.SelfLaunchCeiling,
                "a full squeeze ridden at the bomb must hold the raised ceiling for its whole second");
        }

        [Test]
        public void BothBombsAreDangerColouredAndToldApart()
        {
            var danger = new Color(1f, 0.004f, 0.005f, 1f);   // the shipped palette's rim, normalised
            var left = _cfg.BombColor(true, danger);
            var right = _cfg.BombColor(false, danger);
            Color.RGBToHSV(danger, out float hd, out _, out _);
            Color.RGBToHSV(left, out float hl, out float sl, out float vl);
            Color.RGBToHSV(right, out float hr, out float sr, out float vr);

            float Wrap(float d) => Mathf.Abs(Mathf.Repeat(d + 0.5f, 1f) - 0.5f);
            Assert.Greater(Wrap(hl - hr), 0.05f, "LT's and RT's bombs must read as two colours");
            Assert.LessOrEqual(Wrap(hl - hd), _cfg.SideHueShift + 1e-3f, "LT's bomb must stay a DANGER shade");
            Assert.LessOrEqual(Wrap(hr - hd), _cfg.SideHueShift + 1e-3f, "RT's bomb must stay a DANGER shade");
            Assert.Less(_cfg.SideHueShift, 1f / 12f, "past 30 degrees a shade stops reading as danger red");
            Assert.Greater(sl, 0.9f); Assert.Greater(sr, 0.9f);
            Assert.AreEqual(1f, vl, 1e-3f); Assert.AreEqual(1f, vr, 1e-3f);
        }

        [Test]
        public void APaletteWithNoDangerColourFallsBackToDangerRed()
        {
            // GetDangerSignalColor answers alpha 0 when the palette authors none.
            var left = _cfg.BombColor(true, new Color(0f, 0f, 0f, 0f));
            Assert.AreEqual(1f, left.a);
            Assert.Greater(left.r, 0.5f, "the fallback must still be a red, not black");
            Assert.AreNotEqual(_cfg.BombColor(true, default), _cfg.BombColor(false, default));
        }

        [Test]
        public void TheLaunchIsStrongestAtTheBombAndNeverInverts()
        {
            Assert.Greater(_cfg.SelfLaunchMultiplier, 0f, "a self-launch of 0 launches nobody");
            Assert.Greater(_cfg.SelfLaunchSeconds, 0f);
            Assert.GreaterOrEqual(_cfg.SelfLaunchEdgeStrength, 0f, "a negative edge strength would pull you INTO the bomb");
            Assert.LessOrEqual(_cfg.SelfLaunchEdgeStrength, 1f, "the edge of a blast must not throw harder than its heart");
        }
    }
}
