using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Manta's DROP and the blasts that set bombs off (Caleb, 2026-11-16): a dropped bomb keeps the Manta's velocity
    /// and falls along the Manta's own down; its contact test sweeps the segment it flew so a fast bomb cannot step
    /// through a prism; and a blast sets off only the bombs that were already there when it started, which is what
    /// keeps Contagion from setting off the bombs a bloom plants inside that same bloom.
    /// </summary>
    public class MantaBombDropTests
    {
        const string StingConfigPath = "Assets/_SO_Assets/VesselActions/Manta/MantaStingConfig.asset";
        const string MantaPrefabPath = "Assets/_Prefabs/Spacevessels/Manta.prefab";
        const string DropActionMetaPath = "Assets/_SO_Assets/VesselActions/Manta/MantaDropBombAction.asset.meta";

        [TearDown]
        public void TearDown() => MantaBlastField.Clear();

        // ── Motion ──────────────────────────────────────────────────────────

        [Test]
        public void ADropWithNoFallFliesTheMantasLine()
        {
            Vector3 pos = Vector3.zero, vel = new Vector3(0f, 0f, 50f);
            for (int i = 0; i < 60; i++) MantaDropMath.Step(ref pos, ref vel, Vector3.down, 0f, 1f / 60f);
            Assert.That(pos.z, Is.EqualTo(50f).Within(1e-3f));
            Assert.That(pos.y, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ADropFallsAlongTheGivenDownNotWorldDown()
        {
            // A Manta rolled onto its side: its down is world +X.
            Vector3 down = Vector3.right;
            Vector3 pos = Vector3.zero, vel = new Vector3(0f, 0f, 40f);
            for (int i = 0; i < 60; i++) MantaDropMath.Step(ref pos, ref vel, down, 30f, 1f / 60f);

            Assert.That(pos.x, Is.GreaterThan(14f), "one second at 30 u/s^2 falls ~15 units along the Manta's down");
            Assert.That(pos.y, Is.EqualTo(0f).Within(1e-5f), "world Y plays no part");
            Assert.That(pos.z, Is.EqualTo(40f).Within(1e-3f), "the inherited velocity is kept, not decayed");
        }

        // ── Contact sweep ───────────────────────────────────────────────────

        [Test]
        public void TheSweepCatchesAPrismBetweenTwoFrames()
        {
            // 300 u/s at 60 fps is 5 units a frame: a prism halfway along is caught by the segment, not the endpoints.
            Vector3 a = Vector3.zero, b = new Vector3(0f, 0f, 5f), prism = new Vector3(0.5f, 0f, 2.5f);
            Assert.That(Vector3.Distance(a, prism), Is.GreaterThan(2f));
            Assert.That(Vector3.Distance(b, prism), Is.GreaterThan(2f));
            Assert.That(MantaDropMath.SegmentDistance(a, b, prism), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(MantaDropMath.ClosestOnSegment(a, b, prism), Is.EqualTo(new Vector3(0f, 0f, 2.5f)));
        }

        [Test]
        public void TheSweepClampsToTheSegmentEnds()
        {
            Vector3 a = Vector3.zero, b = new Vector3(0f, 0f, 5f);
            Assert.That(MantaDropMath.SegmentDistance(a, b, new Vector3(0f, 0f, -3f)), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(MantaDropMath.SegmentDistance(a, b, new Vector3(0f, 0f, 9f)), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(MantaDropMath.SegmentDistance(a, a, new Vector3(3f, 4f, 0f)), Is.EqualTo(5f).Within(1e-4f),
                        "a bomb that did not move is a point");
        }

        // ── Blasts set each other off ───────────────────────────────────────

        [Test]
        public void TheFrontGrowsOnTheBloomsEase()
        {
            Assert.That(MantaBlastField.FrontRadius(35f, 2.5f, -0.1f), Is.EqualTo(0f));
            Assert.That(MantaBlastField.FrontRadius(35f, 2.5f, 0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(MantaBlastField.FrontRadius(35f, 2.5f, 1.25f), Is.EqualTo(35f * Mathf.Sin(Mathf.PI * 0.25f)).Within(1e-3f));
            Assert.That(MantaBlastField.FrontRadius(35f, 2.5f, 2.5f), Is.EqualTo(35f).Within(1e-3f));
            Assert.That(MantaBlastField.FrontRadius(35f, 2.5f, 9f), Is.EqualTo(35f).Within(1e-3f));
            Assert.That(MantaBlastField.FrontRadius(6f, 0f, 0f), Is.EqualTo(6f), "a prism beat is full at once");
        }

        [Test]
        public void ABlastReachesABombOnlyOnceItsFrontGetsThere()
        {
            MantaBlastField.Register(Vector3.zero, 35f, startTime: 10f, growSeconds: 2.5f);
            var bomb = new Vector3(30f, 0f, 0f);
            Assert.That(MantaBlastField.Reaches(bomb, bornAt: 5f, now: 10.5f), Is.False, "the front is still small");
            Assert.That(MantaBlastField.Reaches(bomb, bornAt: 5f, now: 12.4f), Is.True);
            Assert.That(MantaBlastField.Reaches(new Vector3(36f, 0f, 0f), bornAt: 5f, now: 12.5f), Is.False, "outside");
        }

        [Test]
        public void ABlastSparesTheBombsBornInsideIt()
        {
            MantaBlastField.Register(Vector3.zero, 35f, startTime: 10f, growSeconds: 2.5f);
            // A Contagion bomb the bloom planted as it started, and one planted later in the same bloom.
            Assert.That(MantaBlastField.Reaches(new Vector3(5f, 0f, 0f), bornAt: 10f, now: 12f), Is.False);
            Assert.That(MantaBlastField.Reaches(new Vector3(5f, 0f, 0f), bornAt: 11f, now: 12f), Is.False);
            // A bomb jousted on earlier goes off.
            Assert.That(MantaBlastField.Reaches(new Vector3(5f, 0f, 0f), bornAt: 9.9f, now: 12f), Is.True);
        }

        [Test]
        public void SpentBlastsAreForgotten()
        {
            MantaBlastField.Register(Vector3.zero, 35f, startTime: 10f, growSeconds: 2.5f);
            MantaBlastField.Register(Vector3.zero, 6f, startTime: 10f, growSeconds: 0f);
            Assert.That(MantaBlastField.Count, Is.EqualTo(2));
            Assert.That(MantaBlastField.Reaches(new Vector3(1f, 0f, 0f), bornAt: 0f, now: 13f), Is.False,
                        "past full size plus the linger, a blast reaches nothing");
            Assert.That(MantaBlastField.Count, Is.EqualTo(0));
        }

        [Test]
        public void AZeroRadiusBlastIsNotRegistered()
        {
            MantaBlastField.Register(Vector3.zero, 0f, 0f, 1f);
            Assert.That(MantaBlastField.Count, Is.EqualTo(0));
        }

        // ── The shipped assets ──────────────────────────────────────────────

        [Test]
        public void TheShippedConfigCarriesCalebsDropRules()
        {
            string text = File.ReadAllText(StingConfigPath);
            StringAssert.Contains("maxDroppedBombs: 1\n", text, "one dropped loose bomb at a time");
            StringAssert.Contains("dropCooldownSeconds: 0\n", text, "no cooldown until playtests ask for one");
            StringAssert.Contains("blastsSetOffBombs: 1\n", text);
        }

        [Test]
        public void PadADropsABombOnTheManta()
        {
            string guid = null;
            foreach (var line in File.ReadAllLines(DropActionMetaPath))
                if (line.StartsWith("guid: ")) guid = line.Substring(6).Trim();
            Assert.That(guid, Is.Not.Null.And.Not.Empty);

            string prefab = File.ReadAllText(MantaPrefabPath);
            StringAssert.Contains(
                "  _inputEventShipActions:\n  - InputEvent: 6\n    ShipActions:\n" +
                $"    - {{fileID: 11400000, guid: {guid}, type: 2}}\n", prefab,
                "InputEvents.Button1Action (pad A) is the drop");
        }
    }
}
