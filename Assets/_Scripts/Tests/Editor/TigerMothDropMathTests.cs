using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Tiger Moth's Drop (<see cref="TigerMothDropMath"/>, <c>R_VesselActions/TIGER_MOTH.md</c>): the hull falls along
    /// its own down, the camera rides the frame pitched 90 degrees nose-down, and the landing turns the hull into that
    /// frame. The promises these hold are the ones the A/B page made on screen (<c>Docs/Studios/ABTests/butterfly-drop</c>),
    /// plus the one change Garrett asked for when it won: the landing never moves the camera.
    /// </summary>
    [TestFixture]
    public class TigerMothDropMathTests
    {
        const float Tol = 1e-4f;
        static readonly Quaternion Tilted = Quaternion.Euler(20f, -35f, 50f);   // an arbitrary attitude: nothing is world-up

        static void AreClose(Vector3 want, Vector3 got, string what) =>
            Assert.Less((want - got).magnitude, Tol, $"{what}: wanted {want}, got {got}");

        [Test]
        public void TheDropFrame_LooksAlongTheHullsDown_WithItsForwardAsUp()
        {
            var frame = TigerMothDropMath.DropFrame(Tilted);
            AreClose(Tilted * Vector3.down, frame * Vector3.forward, "the frame's forward");
            AreClose(Tilted * Vector3.forward, frame * Vector3.up, "the frame's up");
            AreClose(Tilted * Vector3.right, frame * Vector3.right, "the frame's right (a pure pitch keeps it)");
        }

        [Test]
        public void TheDropLine_IsTheHullsOwnDown_AndTheLandedHullFliesAlongIt()
        {
            AreClose(Tilted * Vector3.down, TigerMothDropMath.DropDirection(Tilted), "the drop line");
            // the whole point of the landing: forward after the turn is the line the hull fell on, so it flies straight on
            AreClose(TigerMothDropMath.DropDirection(Tilted), TigerMothDropMath.Pitched(Tilted, 1f) * Vector3.forward, "landed forward");
        }

        [Test]
        public void TheLanding_MovesNoCamera_WhileTheHullTurnsIntoTheFrame()
        {
            var frame = TigerMothDropMath.DropFrame(Tilted);
            for (int i = 0; i <= 20; i++)
            {
                var hull = TigerMothDropMath.Pitched(Tilted, i / 20f);
                var camera = TigerMothDropMath.CameraFrame(hull, frame, 1f);
                Assert.Less(Quaternion.Angle(frame, camera), 0.01f, $"the camera moved at {i * 5}% of the landing");
            }
        }

        [Test]
        public void ReleasingTheFrameAfterTheLanding_IsNotACut()
        {
            // the executor hands the camera back to the hull at the end; the two must be the same frame
            var frame = TigerMothDropMath.DropFrame(Tilted);
            var landed = TigerMothDropMath.Pitched(Tilted, TigerMothDropMath.Landing(1f, 0.25f, 900f, 55f).turn01);
            Assert.Less(Quaternion.Angle(frame, landed), 0.01f);
        }

        [Test]
        public void TheCameraSwingsUpOverTheHull_AndNeverAtTheHullsExpense()
        {
            var frame = TigerMothDropMath.DropFrame(Tilted);
            Assert.Less(Quaternion.Angle(Tilted, TigerMothDropMath.CameraFrame(Tilted, frame, TigerMothDropMath.CameraWeight(0f, 0.35f))), 0.01f,
                "at the press the camera is still behind the hull");
            float half = Quaternion.Angle(Tilted, TigerMothDropMath.CameraFrame(Tilted, frame, TigerMothDropMath.CameraWeight(0.175f, 0.35f)));
            Assert.AreEqual(45f, half, 0.5f, "half way through the swing is half the pitch (SmoothStep is symmetric)");
            Assert.Less(Quaternion.Angle(frame, TigerMothDropMath.CameraFrame(Tilted, frame, TigerMothDropMath.CameraWeight(0.35f, 0.35f))), 0.01f,
                "after the swing the camera rides the drop frame");
            Assert.AreEqual(1f, TigerMothDropMath.CameraWeight(0f, 0f), "no swing time means the frame at once");
        }

        [Test]
        public void TheFall_EasesIn_ThenHoldsTheAuthoredSpeed()
        {
            Assert.AreEqual(0f, TigerMothDropMath.DropSpeed(0f, 900f, 0.12f), Tol);
            Assert.AreEqual(450f, TigerMothDropMath.DropSpeed(0.06f, 900f, 0.12f), 1e-2f);
            Assert.AreEqual(900f, TigerMothDropMath.DropSpeed(0.12f, 900f, 0.12f), Tol);
            Assert.AreEqual(900f, TigerMothDropMath.DropSpeed(5f, 900f, 0.12f), Tol);
            Assert.AreEqual(900f, TigerMothDropMath.DropSpeed(0f, 900f, 0f), Tol, "no ease means full speed at the press");
        }

        [Test]
        public void TheLanding_BringsBackTheCruise_AsTheTurnFinishes()
        {
            var start = TigerMothDropMath.Landing(0f, 0.25f, 900f, 55f);
            Assert.AreEqual(0f, start.turn01, Tol); Assert.AreEqual(900f, start.speed, Tol);
            var end = TigerMothDropMath.Landing(0.25f, 0.25f, 900f, 55f);
            Assert.AreEqual(1f, end.turn01, Tol); Assert.AreEqual(55f, end.speed, Tol);
            var later = TigerMothDropMath.Landing(9f, 0.25f, 900f, 55f);
            Assert.AreEqual(1f, later.turn01, Tol); Assert.AreEqual(55f, later.speed, Tol);
        }

        [Test]
        public void TheRecharge_IsPaidByDistance_UpToTheMediumDrop_AndNoMore()
        {
            // Garrett, 2026-10-29: "a little hop has a small cooldown. a medium distance should be the full 20. anything
            // after medium distance is free"
            Assert.AreEqual(0f, TigerMothDropMath.Cooldown(0f, 900f, 20f), Tol, "no drop, no wait");
            Assert.AreEqual(2f, TigerMothDropMath.Cooldown(90f, 900f, 20f), Tol, "a hop pays its share");
            Assert.AreEqual(10f, TigerMothDropMath.Cooldown(450f, 900f, 20f), Tol, "half the distance, half the wait");
            Assert.AreEqual(20f, TigerMothDropMath.Cooldown(900f, 900f, 20f), Tol, "the medium drop pays the full 20");
            Assert.AreEqual(20f, TigerMothDropMath.Cooldown(5000f, 900f, 20f), Tol, "past it the drop is free");
            Assert.AreEqual(0f, TigerMothDropMath.Cooldown(-5f, 900f, 20f), Tol);
        }

        [Test]
        public void OnlyAFullRechargeDrop_LeavesTheWormhole()
        {
            Assert.IsFalse(TigerMothDropMath.PaysFullCooldown(899f, 900f));
            Assert.IsTrue(TigerMothDropMath.PaysFullCooldown(900f, 900f));
            Assert.IsTrue(TigerMothDropMath.PaysFullCooldown(4000f, 900f));
            // Far Drop doubles the medium distance: the same 900 u drop then pays half and leaves no pair
            Assert.IsFalse(TigerMothDropMath.PaysFullCooldown(900f, 1800f));
            Assert.AreEqual(10f, TigerMothDropMath.Cooldown(900f, 1800f, 20f), Tol);
        }

        [Test]
        public void NegativeControl_ARollInsteadOfAPitch_WouldLandOffTheLine()
        {
            // proves the landed-forward test can fail: turning about forward (a roll) leaves the nose where it was
            var rolled = Tilted * Quaternion.AngleAxis(TigerMothDropMath.TurnDegrees, Vector3.forward);
            Assert.Greater((rolled * Vector3.forward - TigerMothDropMath.DropDirection(Tilted)).magnitude, 1f);
        }
    }
}
