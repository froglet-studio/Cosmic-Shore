#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The boot splash's progress model (Rev 94 C).
    ///
    /// WHY THIS MATTERS: the boot's statuses arrive out of order and more than once (the profile line and the
    /// lobby line race; a retry starts again at "Signing in"), and the lay counter reads 1 while idle and drops
    /// to 0 when a batch starts. A bar that followed them would run backwards on the first screen of the game.
    /// The model exists to be monotonic, to hold where nothing is measured and to end at exactly 1, so those are
    /// asserted here.
    /// </summary>
    [TestFixture]
    public class BootLoadProgressTests
    {
        static BootLoadProgress Fresh()
        {
            var p = new BootLoadProgress();
            p.Reset();
            return p;
        }

        [Test]
        public void NeverGoesBackwards_AcrossAWholeBoot()
        {
            var p = Fresh();
            float last = 0f;
            void Rising(float v, string where)
            {
                Assert.GreaterOrEqual(v, last - 1e-6f, $"progress went backwards during {where}");
                last = v;
            }

            Rising(p.Report(BootStep.Connecting), "connecting");
            for (int i = 0; i < 60; i++) Rising(p.Tick(false, false, 1f, 0), "the wait to connect");
            Rising(p.Report(BootStep.SigningIn), "signing in");
            Rising(p.Report(BootStep.LoadingProfile), "the profile");
            Rising(p.Report(BootStep.JoiningLobby), "the lobby");
            Rising(p.Report(BootStep.CreatingSession), "the session");
            Rising(p.Report(BootStep.HostReady), "host ready");

            // The menu scene: the veil covers, then the world lays in two batches and grows.
            for (int i = 0; i < 30; i++) Rising(p.Tick(true, false, 1f, 0), "the cover before the build");
            for (int i = 0; i <= 50; i++) Rising(p.Tick(true, true, i / 50f, 0), "the first lay batch");
            for (int i = 0; i < 10; i++) Rising(p.Tick(true, false, 1f, 0), "the gap between batches");
            for (int i = 0; i <= 50; i++) Rising(p.Tick(true, true, i / 50f, 40), "the second lay batch");
            for (int g = 400; g > 0; g -= 8) Rising(p.Tick(true, false, 1f, g), "growing");
            for (int i = 0; i < 10; i++) Rising(p.Tick(true, false, 1f, 0), "settled, still covered");
            Rising(p.Tick(false, false, 1f, 0), "the release");

            Assert.AreEqual(1f, p.Value, "the boot did not end at exactly 1");
            Assert.IsTrue(p.Done);
        }

        [Test]
        public void StatusesOutOfOrderOrRepeated_NeverLowerTheBar()
        {
            var p = Fresh();
            p.Report(BootStep.Connecting);
            p.Report(BootStep.JoiningLobby);
            float atLobby = p.Value;
            Assert.AreEqual(BootLoadProgress.JoiningLobbyFloor, atLobby, 1e-6f);

            Assert.AreEqual(atLobby, p.Report(BootStep.LoadingProfile), "an earlier step arriving late lowered the bar");
            Assert.AreEqual(atLobby, p.Report(BootStep.SigningIn), "a retry's Signing in lowered the bar");
            Assert.AreEqual(atLobby, p.Report(BootStep.JoiningLobby), "a repeated status moved the bar");
            Assert.AreEqual(atLobby, p.Report(BootStep.None), "a status that is no milestone moved the bar");

            Assert.Greater(p.Report(BootStep.HostReady), atLobby);
        }

        [Test]
        public void EachStatus_CompletesTheStepsBeforeIt()
        {
            var p = Fresh();
            Assert.AreEqual(0f, p.Report(BootStep.Connecting));
            Assert.AreEqual(BootLoadProgress.StepEnds[0], p.Report(BootStep.SigningIn), 1e-6f, "Connecting is done when sign-in starts");
            Assert.AreEqual(BootLoadProgress.StepEnds[1], p.Report(BootStep.LoadingProfile), 1e-6f, "Signing in is done when the profile starts");
            Assert.AreEqual(BootLoadProgress.StepEnds[2], p.Report(BootStep.JoiningLobby), 1e-6f, "the profile is done when the lobby starts");
            Assert.Less(p.Report(BootStep.HostReady), BootLoadProgress.WorldFloor, "the session's statuses reach into the world's share");
        }

        [Test]
        public void HoldsStill_WhereNothingIsMeasured()
        {
            var p = Fresh();
            p.Report(BootStep.SigningIn);
            float held = p.Value;
            // No world, no cover, no new status: a thousand frames change nothing. There is no clock in the model.
            for (int i = 0; i < 1000; i++) Assert.AreEqual(held, p.Tick(false, false, 1f, 0));

            // Covered but not building yet (the scene is settling): still held, and not done.
            for (int i = 0; i < 1000; i++) Assert.AreEqual(held, p.Tick(true, false, 1f, 0));
            Assert.IsFalse(p.Done);
        }

        [Test]
        public void TheWorld_OwnsTheBarsSecondHalf_OnABoot()
        {
            var p = Fresh();
            p.Report(BootStep.HostReady);
            Assert.AreEqual(BootLoadProgress.WorldFloor, p.Tick(true, true, 0f, 0), 1e-6f, "the lay starts at the world's floor");
            float layDone = BootLoadProgress.WorldFloor + (1f - BootLoadProgress.WorldFloor) * BootLoadProgress.LayPartOfWorld;
            Assert.AreEqual(layDone, p.Tick(true, true, 1f, 0), 1e-6f, "a finished lay is the lay's share");
            Assert.AreEqual(layDone, p.Tick(true, false, 1f, 100), 1e-6f, "growing starts where laying ended");
            Assert.Less(p.Tick(true, false, 1f, 1), 1f, "one prism still growing is not done");
            Assert.AreEqual(1f, p.Tick(false, false, 1f, 0));
        }

        [Test]
        public void ALoadThatIsNotABoot_IsTheWorldAlone_OverTheWholeBar()
        {
            var p = Fresh();
            Assert.IsFalse(p.IsBoot);
            Assert.AreEqual(0f, p.Tick(true, false, 1f, 0), "a return to the menu started above 0");
            Assert.AreEqual(BootLoadProgress.LayPartOfWorld * 0.5f, p.Tick(true, true, 0.5f, 0), 1e-6f);
            Assert.AreEqual(BootLoadProgress.LayPartOfWorld, p.Tick(true, false, 1f, 50), 1e-6f);
            Assert.AreEqual(1f, p.Tick(false, false, 1f, 0));
            Assert.IsTrue(p.Done);
        }

        [Test]
        public void IsNotDone_BeforeAnyWorldWasSeen()
        {
            var p = Fresh();
            p.Report(BootStep.HostReady);
            // Idle counters read "nothing laying, nothing growing, nothing covering": that is not a finished world.
            for (int i = 0; i < 100; i++) p.Tick(false, false, 1f, 0);
            Assert.IsFalse(p.Done);
            Assert.AreEqual(BootLoadProgress.HostReadyFloor, p.Value, 1e-6f);
        }

        [Test]
        public void GrowingCount_ThatRisesFirst_TakesItsPeakAsTheTotal()
        {
            var p = Fresh();
            float a = p.Tick(true, false, 1f, 10);
            float b = p.Tick(true, false, 1f, 200);     // more prisms queued as others settle
            float c = p.Tick(true, false, 1f, 100);
            Assert.GreaterOrEqual(b, a);
            Assert.Greater(c, b, "half of the peak settled did not move the bar");
        }

        [Test]
        public void Reset_StartsANewLoad_AndABootInProgressIsNotOne()
        {
            var p = Fresh();
            p.Report(BootStep.LoadingProfile);
            Assert.IsTrue(p.BootInProgress, "a boot that is filling must not be wiped by the splash coming back into sight");
            p.Tick(true, true, 1f, 0);
            p.Tick(false, false, 1f, 0);
            Assert.IsFalse(p.BootInProgress, "a finished boot must let the next load start");

            p.Reset();
            Assert.AreEqual(0f, p.Value);
            Assert.IsFalse(p.IsBoot);
            Assert.IsFalse(p.Done);
        }

        [Test]
        public void TheBarsSteps_AreTheModelsShares()
        {
            var ends = BootLoadProgress.StepEnds;
            Assert.AreEqual(4, ends.Length);
            Assert.AreEqual(BootLoadProgress.SigningInFloor, ends[0]);
            Assert.AreEqual(BootLoadProgress.LoadingProfileFloor, ends[1]);
            Assert.AreEqual(BootLoadProgress.JoiningLobbyFloor, ends[2]);
            Assert.AreEqual(1f, ends[3]);

            // The step shown over the bar is the one the value is in.
            Assert.AreEqual(0, KitStepBar.StepAt(ends, BootLoadProgress.FloorOf(BootStep.Connecting)));
            Assert.AreEqual(1, KitStepBar.StepAt(ends, BootLoadProgress.FloorOf(BootStep.SigningIn)));
            Assert.AreEqual(2, KitStepBar.StepAt(ends, BootLoadProgress.FloorOf(BootStep.LoadingProfile)));
            Assert.AreEqual(3, KitStepBar.StepAt(ends, BootLoadProgress.FloorOf(BootStep.JoiningLobby)));
            Assert.AreEqual(3, KitStepBar.StepAt(ends, BootLoadProgress.FloorOf(BootStep.HostReady)));
            Assert.AreEqual(3, KitStepBar.StepAt(ends, 1f));
        }
    }
}
#endif
