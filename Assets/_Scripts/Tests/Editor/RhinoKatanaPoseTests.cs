using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's Charge card is a katana that follows the real sword
    /// (<see cref="RhinoVesselHUDView.KatanaPose"/>): it turns with the swipe, MIRRORS so its edge leads the
    /// swing, keeps the side it last faced at rest, and shows the straight blade (from behind) once both
    /// triggers are in the chop. Pinned because a sign slip draws a plausible wrong card - the spine
    /// leading the cut.
    /// </summary>
    public class RhinoKatanaPoseTests
    {
        const float Card = 45f, Straight = 0.75f, Short = 0.8f;

        static RhinoVesselHUDView.KatanaPoseResult Pose(float swipe, float chop, float facing = 1f) =>
            RhinoVesselHUDView.KatanaPose(swipe, chop, facing, Card, Straight, Short);

        [Test]
        public void SwipeRight_TurnsClockwise_AndMirrors()
        {
            var p = Pose(1f, 0.5f);
            Assert.AreEqual(-45f, p.AngleZ, 1e-4f);
            Assert.AreEqual(-1f, p.Facing);
        }

        [Test]
        public void SwipeLeft_TurnsCounterclockwise_EdgeAsDrawn()
        {
            var p = Pose(-1f, 0.5f, -1f);
            Assert.AreEqual(45f, p.AngleZ, 1e-4f);
            Assert.AreEqual(1f, p.Facing);
        }

        [Test]
        public void AtRest_KeepsTheSideItLastFaced()
        {
            Assert.AreEqual(-1f, Pose(0f, 0f, -1f).Facing);
            Assert.AreEqual(1f, Pose(0f, 0f, 1f).Facing);
        }

        [Test]
        public void SingleTriggerPull_StaysCurved() => Assert.IsFalse(Pose(1f, 0.5f).Straight);

        [Test]
        public void BothTriggers_ShowTheStraightBlade_Shortened()
        {
            var p = Pose(0f, 1f);
            Assert.IsTrue(p.Straight);
            Assert.AreEqual(0.8f, p.Length01, 1e-4f);
        }

        [Test]
        public void AFlourishPastFullTravel_Clamps() => Assert.AreEqual(-45f, Pose(3f, 0f).AngleZ, 1e-4f);
    }
}
