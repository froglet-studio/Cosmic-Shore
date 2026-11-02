using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>The any-prism sense (SWITCHBACK.md "AI: any prism in the way"): blocked lines, corridors, blasting a
    /// blocker from a crystal, per-pilot lanes, and a point-mass flight down a rival's trail per difficulty.</summary>
    public class SwitchbackPrismAvoidanceTests
    {
        /// <summary>A rival's trail: prisms every 4 u straight down the line the pilot wants to fly.</summary>
        static int Trail(Vector3[] pts, Vector3 from, Vector3 dir, int count, float spacing = 4f)
        {
            for (int i = 0; i < count; i++) pts[i] = from + dir * (i * spacing);
            return count;
        }

        [Test]
        public void TrailOnTheLine_IsBlocked_AndACorridorClearsIt([Values(1, 2, 3, 4)] int level)
        {
            var skill = SwitchbackAvoidSkill.ForLevel(level);
            var pts = new Vector3[64];
            int n = Trail(pts, new Vector3(0, 0, 30), Vector3.forward, 40);
            Assert.IsTrue(SwitchbackPrismAvoidance.Blocked(Vector3.zero, Vector3.forward, skill.SenseRange, skill.Clearance, pts, n, out float along));
            Assert.That(along, Is.EqualTo(30f).Within(1f));
            var d = SwitchbackPrismAvoidance.ChooseCorridor(Vector3.zero, Vector3.forward, skill.SenseRange, pts, n, in skill, out float clear);
            Assert.That(clear, Is.GreaterThanOrEqualTo(skill.Clearance), "corridor keeps the skill's air");
            Assert.That(Vector3.Angle(d, Vector3.forward), Is.LessThanOrEqualTo(61f));
        }

        [Test]
        public void ClearLine_IsNotTouched()
        {
            var skill = SwitchbackAvoidSkill.ForLevel(4);
            var pts = new Vector3[64];
            int n = Trail(pts, new Vector3(40, 0, 0), Vector3.forward, 40);   // a trail 40 u to the side
            Assert.IsFalse(SwitchbackPrismAvoidance.Blocked(Vector3.zero, Vector3.forward, 200f, skill.Clearance, pts, n, out _));
            var d = SwitchbackPrismAvoidance.ChooseCorridor(Vector3.zero, Vector3.forward, 200f, pts, n, in skill, out _);
            Assert.That(Vector3.Angle(d, Vector3.forward), Is.LessThan(0.01f));
        }

        [Test]
        public void BigPrismRadius_Counts()
        {
            var pts = new[] { new Vector3(15, 0, 60) };
            var r = new[] { 12f };
            Assert.IsFalse(SwitchbackPrismAvoidance.Blocked(Vector3.zero, Vector3.forward, 100f, 8f, pts, 1, out _));
            Assert.IsTrue(SwitchbackPrismAvoidance.Blocked(Vector3.zero, Vector3.forward, 100f, 8f, pts, 1, out _, r));
        }

        /// <summary>A wall of prisms across the line, a crystal ahead: the slot fired from the crystal at the wall
        /// covers it, so a skilled pilot may blast; Easy never does; a crystal behind is never used.</summary>
        [Test]
        public void Blast_FromCrystalAhead_CoversABlockingWall()
        {
            var pts = new Vector3[64]; int n = 0;
            for (int y = -3; y <= 3; y++) pts[n++] = new Vector3(0, y * 4f, 150f);   // a vertical wall across the line
            var slot = SwitchbackBlastSlot.Default;
            var crystals = new[] { new Vector3(0, 0, 60f), new Vector3(0, 0, -50f) };
            Vector3 blocker = SwitchbackPrismAvoidance.BlockerCentroid(Vector3.zero, Vector3.forward, 200f, 20f, pts, n, out int count);
            Assert.AreEqual(n, count);
            var legend = SwitchbackAvoidSkill.ForLevel(4);
            Assert.IsTrue(SwitchbackPrismAvoidance.TryBlast(Vector3.zero, Vector3.forward, blocker, 20f, pts, n, crystals, 2, in slot,
                in legend, out Vector3 c, out Vector3 f, out Vector3 up, out float cov));
            Assert.AreEqual(crystals[0], c);
            Assert.That(cov, Is.GreaterThanOrEqualTo(legend.BlastMinCoverage));
            Assert.That(Mathf.Abs(Vector3.Dot(up, Vector3.up)), Is.GreaterThan(0.9f), "gape along the wall");
            var easy = SwitchbackAvoidSkill.ForLevel(1);
            Assert.IsFalse(SwitchbackPrismAvoidance.TryBlast(Vector3.zero, Vector3.forward, blocker, 20f, pts, n, crystals, 2, in slot,
                in easy, out _, out _, out _, out _));
            Assert.IsFalse(SwitchbackPrismAvoidance.TryBlast(Vector3.zero, Vector3.forward, blocker, 20f, pts, n, new[] { crystals[1] }, 1,
                in slot, in legend, out _, out _, out _, out _), "a crystal behind is not a blast");
        }

        [Test]
        public void Lanes_AreInsideTheMouth_AndDifferPerPilot()
        {
            var a = SwitchbackPrismAvoidance.LaneOffset(11u, Vector3.forward, 40f, 0.5f);
            var b = SwitchbackPrismAvoidance.LaneOffset(12u, Vector3.forward, 40f, 0.5f);
            Assert.That(a.magnitude, Is.InRange(0.5f * 0.5f * 40f - 0.01f, 20.01f));
            Assert.That(Mathf.Abs(Vector3.Dot(a, Vector3.forward)), Is.LessThan(1e-3f));
            Assert.That((a - b).magnitude, Is.GreaterThan(1f));
            Assert.AreEqual(a, SwitchbackPrismAvoidance.LaneOffset(11u, Vector3.forward, 40f, 0.5f), "deterministic");
        }

        /// <summary>
        /// Point-mass flight down a rival's trail (the reported bug): 100 u/s, 110 deg/s turn, the trail laid exactly on
        /// the line to the goal 400 u ahead. Without the sense the pilot hits nearly every trail prism; with it, at every
        /// difficulty, it hits far fewer, and the better pilots hit fewer still.
        /// </summary>
        [Test]
        public void FlightDownARivalTrail_HitsFewerPrisms()
        {
            int none = Fly(null);
            int[] hits = new int[5];
            for (int l = 1; l <= 4; l++) { var s = SwitchbackAvoidSkill.ForLevel(l); hits[l] = Fly(s); }
            System.Console.WriteLine($"  trail hits: none={none} easy={hits[1]} medium={hits[2]} hard={hits[3]} legendary={hits[4]}");
            Assert.That(none, Is.GreaterThan(50));
            for (int l = 1; l <= 4; l++) Assert.That(hits[l], Is.LessThan(none / 3), $"level {l}");
            Assert.That(hits[4], Is.LessThanOrEqualTo(hits[1]));
        }

        /// <summary>A rival that WEAVED across the line (its trail crosses it again and again): harder to dodge.</summary>
        [Test]
        public void FlightThroughAWeavingTrail_HitsFewerPrisms()
        {
            int none = Fly(null, true);
            int[] hits = new int[5];
            for (int l = 1; l <= 4; l++) hits[l] = Fly(SwitchbackAvoidSkill.ForLevel(l), true);
            System.Console.WriteLine($"  weave hits: none={none} easy={hits[1]} medium={hits[2]} hard={hits[3]} legendary={hits[4]}");
            for (int l = 1; l <= 4; l++) Assert.That(hits[l], Is.LessThanOrEqualTo(none), $"level {l}");
            Assert.That(hits[4], Is.LessThan(Mathf.Max(none, 1)));
        }

        public static int Fly(SwitchbackAvoidSkill? skill, bool weave = false)
        {
            var pts = new Vector3[128];
            int n = Trail(pts, new Vector3(0, 0, 40), Vector3.forward, 90);   // 40..396 u
            if (weave) for (int i = 0; i < n; i++) pts[i].x = 25f * Mathf.Sin(pts[i].z / 45f);   // a rival weaving across the line
            var hit = new bool[n];
            Vector3 pos = Vector3.zero, vel = Vector3.forward, goal = new Vector3(0, 0, 420);
            const float dt = 1f / 60f, speed = 100f, turn = 110f, hull = 3f;
            float nextSense = 0f; Vector3 aimDir = Vector3.forward; bool avoiding = false;
            var buf = new Vector3[128];
            for (float t = 0f; t < 6f && pos.z < 410f; t += dt)
            {
                Vector3 want = (goal - pos).normalized;
                if (skill.HasValue)
                {
                    var s = skill.Value;
                    if (t >= nextSense)
                    {
                        nextSense = t + s.ReactionSeconds;
                        float range = Mathf.Min(s.SenseRange, (goal - pos).magnitude);
                        int m = 0;   // what the capsule overlap would return
                        for (int i = 0; i < n; i++)
                        {
                            Vector3 w = pts[i] - pos; float a = Vector3.Dot(w, want);
                            if (a > 6f && a < range && (w - want * a).magnitude < s.Clearance * 2.5f) buf[m++] = pts[i];
                        }
                        avoiding = m > 0 && SwitchbackPrismAvoidance.Blocked(pos, want, range, s.Clearance, buf, m, out _);
                        if (avoiding) aimDir = SwitchbackPrismAvoidance.ChooseCorridor(pos, want, range, buf, m, in s, out _);
                    }
                    if (avoiding) want = aimDir;
                }
                float ang = Vector3.Angle(vel, want);
                float step = turn * dt;
                vel = ang <= step ? want : Vector3.Lerp(vel, want, step / ang).normalized;
                pos += vel * speed * dt;
                for (int i = 0; i < n; i++) if (!hit[i] && (pts[i] - pos).magnitude < hull + 1f) hit[i] = true;
            }
            int c = 0; foreach (var h in hit) if (h) c++;
            return c;
        }
    
        [Test]
        public void Emergency_ReachScalesWithSpeedAndLevel_PresentAtEveryLevel()
        {
            for (int l = 1; l <= 4; l++) Assert.Greater(SwitchbackAvoidSkill.EmergencySeconds(l), 0.3f);
            Assert.Less(SwitchbackAvoidSkill.EmergencySeconds(1), SwitchbackAvoidSkill.EmergencySeconds(4));
            Assert.AreEqual(SwitchbackPrismAvoidance.EmergencyMinReach, SwitchbackPrismAvoidance.EmergencyReach(10f, 0.35f), 1e-4f);
            Assert.AreEqual(180f, SwitchbackPrismAvoidance.EmergencyReach(200f, 0.9f), 1e-3f);
        }

        [Test]
        public void Emergency_EscapeLeavesTheLineAwayFromTheObstacle()
        {
            Vector3 dir = Vector3.forward;
            // Obstacle ahead and slightly right: break left, mostly sideways when close.
            Vector3 e = SwitchbackPrismAvoidance.EscapeDirection(dir, new Vector3(-2f, 0f, -20f), Vector3.back, Vector3.up, 1f);
            Assert.Less(e.x, -0.8f);
            Assert.AreEqual(1f, e.magnitude, 1e-4f);
            // Dead centre and head-on normal: still leaves the line (falls back to up).
            Vector3 c = SwitchbackPrismAvoidance.EscapeDirection(dir, new Vector3(0f, 0f, -20f), Vector3.back, Vector3.up, 0f);
            Assert.Greater(c.y, 0.5f);
        }
    }
}
