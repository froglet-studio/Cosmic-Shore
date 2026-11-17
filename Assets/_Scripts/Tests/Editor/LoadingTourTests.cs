#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The loading screen into a game (Rev 91 F): which chapter of the tour a load value shows, and which step of
    /// the bar it is in.
    ///
    /// WHY THIS MATTERS: both follow the LOAD, not a clock. The chapter is the fifth of the bar the load has
    /// reached and the step is the measured share the value is in (laying to 0.60, growing to 0.90, pilots to 1),
    /// so the edges are where the screen would lie: a chapter index past the last at exactly 100, a step name that
    /// changes one frame before its share ends, or a marker filled for a step that is still running.
    /// </summary>
    [TestFixture]
    public class LoadingTourTests
    {
        // ArenaLoadProgress's shares, as the loading screen's bar carries them.
        static readonly float[] Ends = { ArenaLoadProgress.LayCeiling, ArenaLoadProgress.GrowCeiling, 1f };

        [Test]
        public void Chapter_IsTheFifthOfTheLoad()
        {
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(0f, 5));
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(0.19f, 5));
            Assert.AreEqual(1, ConnectingTourView.ChapterFor(0.21f, 5));
            Assert.AreEqual(2, ConnectingTourView.ChapterFor(0.5f, 5));
            Assert.AreEqual(3, ConnectingTourView.ChapterFor(0.79f, 5));
            Assert.AreEqual(4, ConnectingTourView.ChapterFor(0.81f, 5));
        }

        [Test]
        public void Chapter_HoldsTheLastAtTheEnd()
        {
            Assert.AreEqual(4, ConnectingTourView.ChapterFor(0.999f, 5));
            Assert.AreEqual(4, ConnectingTourView.ChapterFor(1f, 5), "exactly 100 must not index past the last chapter");
            Assert.AreEqual(4, ConnectingTourView.ChapterFor(7f, 5));
        }

        [Test]
        public void Chapter_SurvivesBadInput()
        {
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(-1f, 5));
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(float.NaN, 5));
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(0.7f, 0), "no chapters answers 0, never a negative index");
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(0.7f, -3));
            Assert.AreEqual(0, ConnectingTourView.ChapterFor(0.7f, 1));
        }

        [Test]
        public void Chapter_NeverGoesBackAsTheLoadRises()
        {
            int last = 0;
            for (int i = 0; i <= 1000; i++)
            {
                int chapter = ConnectingTourView.ChapterFor(i / 1000f, 5);
                Assert.GreaterOrEqual(chapter, last);
                Assert.LessOrEqual(chapter - last, 1, "a chapter is never skipped");
                last = chapter;
            }
            Assert.AreEqual(4, last);
        }

        [Test]
        public void Step_IsTheMeasuredShareTheValueIsIn()
        {
            Assert.AreEqual(0, KitStepBar.StepAt(Ends, 0f));
            Assert.AreEqual(0, KitStepBar.StepAt(Ends, 0.59f));
            Assert.AreEqual(1, KitStepBar.StepAt(Ends, 0.61f));
            Assert.AreEqual(1, KitStepBar.StepAt(Ends, 0.89f));
            Assert.AreEqual(2, KitStepBar.StepAt(Ends, 0.91f));
        }

        [Test]
        public void Step_ChangesExactlyWhereAShareEnds()
        {
            Assert.AreEqual(1, KitStepBar.StepAt(Ends, ArenaLoadProgress.LayCeiling), "laying is over once its share is full");
            Assert.AreEqual(2, KitStepBar.StepAt(Ends, ArenaLoadProgress.GrowCeiling));
            Assert.AreEqual(2, KitStepBar.StepAt(Ends, 1f), "the last step holds at 100");
            Assert.AreEqual(2, KitStepBar.StepAt(Ends, 2f));
        }

        [Test]
        public void Step_SurvivesAnEmptyBar()
        {
            Assert.AreEqual(0, KitStepBar.StepAt(null, 0.5f));
            Assert.AreEqual(0, KitStepBar.StepAt(new float[0], 0.5f));
            Assert.AreEqual(0, KitStepBar.StepAt(new[] { 1f }, 0.5f));
        }

        [Test]
        public void Markers_LightWhenAStepStarts_AndFillWhenItIsDone()
        {
            // The bar is in step 1 (growing): step 0 is finished, step 1 running, step 2 not started.
            Assert.IsTrue(KitStepBar.StepStarted(0, 1, false));
            Assert.IsTrue(KitStepBar.StepFinished(0, 1, false));
            Assert.IsTrue(KitStepBar.StepStarted(1, 1, false));
            Assert.IsFalse(KitStepBar.StepFinished(1, 1, false), "a running step's marker is lit, not filled");
            Assert.IsFalse(KitStepBar.StepStarted(2, 1, false));
            Assert.IsFalse(KitStepBar.StepFinished(2, 1, false));
        }

        [Test]
        public void Markers_AllFillWhenTheLoadIsDone()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(KitStepBar.StepStarted(i, 2, true));
                Assert.IsTrue(KitStepBar.StepFinished(i, 2, true));
            }
            Assert.IsFalse(KitStepBar.StepFinished(2, 2, false), "the last step is not done until the owner says the load is");
        }
    }
}
#endif
