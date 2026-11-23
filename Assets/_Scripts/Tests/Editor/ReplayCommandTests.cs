#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// What each replay control does, written once in <see cref="ReplayCommands"/> and run here
    /// against a fake transport - no scene, no Theater. A HUD button, a pad button and a key all
    /// build these commands, so a wrong skip or a speed that runs away is caught here for all three.
    /// </summary>
    public class ReplayCommandTests
    {
        sealed class FakeTransport : IReplayTransport
        {
            public bool IsPlaying { get; set; } = true;
            public bool IsPaused { get; set; }
            public float Position { get; set; }
            public float Duration { get; set; } = 120f;
            public float Speed { get; set; } = 1f;
            public TheaterShot Shot { get; set; } = TheaterShot.Orbit;
            public string FollowName { get; set; } = "Pilot";
            public int ShotIndex = -1, ShotSteps, FollowSteps, Stops;

            public void TogglePause() => IsPaused = !IsPaused;
            public void Seek(float seconds) => Position = seconds < 0f ? 0f : seconds > Duration ? Duration : seconds;
            public void SetShot(int index) => ShotIndex = index;
            public void CycleShot(int step) => ShotSteps += step;
            public void CycleFollow(int step) => FollowSteps += step;
            public void Stop() { Stops++; IsPlaying = false; }
        }

        [Test]
        public void TogglePause_FlipsBackAndForth()
        {
            var t = new FakeTransport();
            ReplayCommands.TogglePause.Execute(t);
            Assert.IsTrue(t.IsPaused);
            ReplayCommands.TogglePause.Execute(t);
            Assert.IsFalse(t.IsPaused);
        }

        [Test]
        public void Nudge_SkipsFromWhereThePlayheadIs()
        {
            var t = new FakeTransport { Position = 30f };
            ReplayCommands.Nudge(5f).Execute(t);
            Assert.AreEqual(35f, t.Position, 1e-4f);
            ReplayCommands.Nudge(-5f).Execute(t);
            ReplayCommands.Nudge(-5f).Execute(t);
            Assert.AreEqual(25f, t.Position, 1e-4f);
        }

        [Test]
        public void Nudge_PastEitherEnd_StopsAtTheEnd()
        {
            var t = new FakeTransport { Position = 2f };
            ReplayCommands.Nudge(-5f).Execute(t);
            Assert.AreEqual(0f, t.Position, 1e-4f);
            t.Position = 118f;
            ReplayCommands.Nudge(5f).Execute(t);
            Assert.AreEqual(120f, t.Position, 1e-4f);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.25f, 30f)]
        [TestCase(1f, 120f)]
        [TestCase(1.5f, 120f)]
        [TestCase(-0.5f, 0f)]
        public void SeekFraction_IsAShareOfTheReplay(float fraction, float expected)
        {
            var t = new FakeTransport();
            ReplayCommands.SeekFraction(fraction).Execute(t);
            Assert.AreEqual(expected, t.Position, 1e-4f);
        }

        [Test]
        public void ScaleSpeed_StepsAndStaysInsideItsLimits()
        {
            var t = new FakeTransport();
            var faster = ReplayCommands.ScaleSpeed(2f, 0.25f, 4f);
            var slower = ReplayCommands.ScaleSpeed(0.5f, 0.25f, 4f);
            faster.Execute(t);
            Assert.AreEqual(2f, t.Speed, 1e-4f);
            for (int i = 0; i < 5; i++) faster.Execute(t);
            Assert.AreEqual(4f, t.Speed, 1e-4f, "faster must stop at the maximum");
            for (int i = 0; i < 10; i++) slower.Execute(t);
            Assert.AreEqual(0.25f, t.Speed, 1e-4f, "slower must stop at the minimum");
        }

        [Test]
        public void ScaleSpeed_SwappedLimits_StillClamp()
        {
            var t = new FakeTransport { Speed = 10f };
            ReplayCommands.ScaleSpeed(1f, 4f, 0.25f).Execute(t);
            Assert.AreEqual(4f, t.Speed, 1e-4f);
        }

        [Test]
        public void ShotAndPilotCommands_ReachTheTransport()
        {
            var t = new FakeTransport();
            ReplayCommands.SetShot(3).Execute(t);
            ReplayCommands.CycleShot(1).Execute(t);
            ReplayCommands.CycleShot(-1).Execute(t);
            ReplayCommands.CycleShot(1).Execute(t);
            ReplayCommands.CycleFollow(1).Execute(t);
            ReplayCommands.CycleFollow(1).Execute(t);
            ReplayCommands.CycleFollow(-1).Execute(t);
            Assert.AreEqual(3, t.ShotIndex);
            Assert.AreEqual(1, t.ShotSteps);
            Assert.AreEqual(1, t.FollowSteps);
        }

        [Test]
        public void Stop_StopsOnce()
        {
            var t = new FakeTransport();
            ReplayCommands.Stop.Execute(t);
            Assert.AreEqual(1, t.Stops);
            Assert.IsFalse(t.IsPlaying);
        }

        [TestCase(0f, "0:00")]
        [TestCase(5.9f, "0:05")]
        [TestCase(65f, "1:05")]
        [TestCase(600f, "10:00")]
        [TestCase(-3f, "0:00")]
        public void Clock_IsMinutesAndSeconds(float seconds, string expected)
        {
            Assert.AreEqual(expected, ReplaySummary.Clock(seconds));
        }
    }
}
#endif
