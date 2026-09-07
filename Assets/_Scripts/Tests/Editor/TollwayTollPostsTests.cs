#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tollway's ring-placement admission rule.
    ///
    /// WHY THIS MATTERS: a ring may only be planted in a toll post, and the ability puts the ring
    /// centre 150u AHEAD of the ship. The shipped rule tested that offset point alone, which is
    /// an ANNULUS rather than a radius: flying straight at a post from range d, the centre sits
    /// |d - 150| away, so a press was admitted only while 80 &lt;= d &lt;= 220 and refused at every
    /// range inside 80. The HUD arrow points AT a post, so a pilot who followed it flew through
    /// the only window that worked and then pressed, from close range, into a silent refusal —
    /// playtest reported it as "the AI placed rings at the right points, I could not place any at
    /// all". The AI was unaffected because it presses on a pacing timer while still approaching.
    ///
    /// These tests pin the corrected rule (nearest free post to the whole SEGMENT the pilot is
    /// aiming down) and keep the point-blank case — the one that shipped broken — explicit.
    /// </summary>
    public class TollwayTollPostsTests
    {
        // The shipped numbers: PlaceSwitchAction.asset placementDistance 150, TollwaySettings
        // postClaimRadius 70.
        const float PlacementDistance = 150f;
        const float ClaimRadius = 70f;

        [SetUp]
        public void BuildCourt()
        {
            // markerParent null => positions and claim radius only, no GameObjects. ScarabSwitch
            // .Live is empty in an edit-mode test, so every post reads as free.
            TollwayTollPosts.Build(seed: 12345, centre: Vector3.zero, courtRadius: 480f, count: 10,
                                   innerFraction: 0.4f, outerFraction: 0.85f,
                                   claimRadius: ClaimRadius, socketRadius: 24f,
                                   markerParent: null, theme: null);
        }

        [TearDown]
        public void ClearCourt() => TollwayTollPosts.Clear();

        [Test]
        public void Build_ProducesTheAuthoredNumberOfPosts()
        {
            Assert.IsTrue(TollwayTollPosts.HasLayout, "no layout was built");
            Assert.AreEqual(10, TollwayTollPosts.Positions.Count);
            Assert.AreEqual(ClaimRadius, TollwayTollPosts.ClaimRadius, 1e-3f);
        }

        /// <summary>
        /// The regression. A pilot flying straight at a post is admitted at EVERY range the
        /// ability can reach — most of all point-blank, where the arrow puts them and where the
        /// endpoint-only rule refused.
        /// </summary>
        [TestCase(5f)]      // sitting in the socket — the case that shipped broken
        [TestCase(40f)]
        [TestCase(79f)]     // one unit inside the old rule's dead zone
        [TestCase(150f)]    // the ring centre lands exactly on the post
        [TestCase(210f)]
        public void AimedAtAPost_IsAdmittedAtEveryRangeInReach(float range)
        {
            Vector3 post = TollwayTollPosts.Positions[0];
            Vector3 course = Vector3.Normalize(new Vector3(0.3f, 0.2f, 1f));
            Vector3 ship = post - course * range;

            Assert.IsTrue(
                TollwayTollPosts.TryResolve(ship, ship + course * PlacementDistance, out var placed),
                $"a press aimed at a post from {range}u was refused");
            Assert.AreEqual(0f, (placed - post).magnitude, 1e-3f,
                            "an admitted ring must snap exactly onto the post");
        }

        [Test]
        public void PointBlank_WasRefusedByTheOldEndpointOnlyRule()
        {
            // A negative control for the diagnosis itself: at 5u the OFFSET POINT really is ~145u
            // from the post, far outside the claim radius. The rule had to change; widening it
            // would not have been enough (145 > 70) and would have admitted half the court.
            Vector3 post = TollwayTollPosts.Positions[0];
            Vector3 course = Vector3.forward;
            Vector3 centre = (post - course * 5f) + course * PlacementDistance;
            Assert.Greater((centre - post).magnitude, ClaimRadius,
                           "the endpoint-only rule would have admitted this, so the test proves nothing");
        }

        [Test]
        public void AimedAwayFromEveryPost_IsRefused()
        {
            // Far outside the court, aiming further out: nothing is within reach.
            Vector3 ship = new Vector3(0f, 0f, 4000f);
            Vector3 course = Vector3.forward;
            Assert.IsFalse(
                TollwayTollPosts.TryResolve(ship, ship + course * PlacementDistance, out _),
                "a press with no post anywhere near the line must be refused");
        }

        [Test]
        public void BeyondReach_IsRefused()
        {
            // Lined up perfectly, but the post is further away than the ability can place.
            Vector3 post = TollwayTollPosts.Positions[0];
            Vector3 course = Vector3.Normalize(new Vector3(0f, 0f, 1f));
            Vector3 ship = post - course * (PlacementDistance + ClaimRadius + 50f);
            Assert.IsFalse(
                TollwayTollPosts.TryResolve(ship, ship + course * PlacementDistance, out _),
                "a post past placementDistance + claimRadius must stay out of reach");
        }

        [Test]
        public void EveryPostSitsInsideTheAuthoredBand()
        {
            const float court = 480f;
            foreach (var p in TollwayTollPosts.Positions)
            {
                float r = p.magnitude;
                Assert.GreaterOrEqual(r, court * 0.4f - 1f, "a post fell inside the inner band edge");
                Assert.LessOrEqual(r, court * 0.85f + 1f, "a post fell outside the outer band edge");
            }
        }
    }
}
#endif
