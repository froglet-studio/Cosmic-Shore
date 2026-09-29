#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The connecting screen's progress model.
    ///
    /// WHY THIS MATTERS: the signals it folds together genuinely go backwards -
    /// <c>PrismTrailBuilder.LayProgress</c> reads 1 while idle and drops to 0 the moment a batch
    /// starts, and a second arena build re-queues the counters from scratch. A bar that followed
    /// them faithfully would run backwards mid-load, which turns "this is taking a while" into
    /// "something is wrong". Monotonicity is the one property the whole model exists to provide,
    /// so it is the one asserted here rather than eyeballed on a loading screen.
    /// </summary>
    [TestFixture]
    public class ArenaLoadProgressTests
    {
        const float Dt = 1f / 60f;

        static float Step(ArenaLoadProgress p, bool laying, float lay, int grow, bool ready = false)
            => p.Tick(Dt, laying, lay, grow, ready);

        [Test]
        public void NeverGoesBackwards_AcrossTheWholeLoad()
        {
            var p = new ArenaLoadProgress();
            p.Reset();

            float last = 0f;
            void Assert_(float v, string where)
            {
                Assert.GreaterOrEqual(v, last - 1e-5f, $"progress went backwards during {where}");
                last = v;
            }

            // Dwell: nothing is building yet, and LayProgress reads 1 while idle.
            for (int i = 0; i < 120; i++) Assert_(Step(p, false, 1f, 0), "the dwell");

            // Laying starts - LayProgress collapses to 0.
            for (int i = 0; i < 100; i++) Assert_(Step(p, true, i / 100f, 0), "laying");

            // A SECOND build re-queues from scratch, so lay progress restarts at 0.
            for (int i = 0; i < 100; i++) Assert_(Step(p, true, i / 100f, 0), "a second lay batch");

            // Gap between phases: nothing measurable at all.
            for (int i = 0; i < 60; i++) Assert_(Step(p, false, 1f, 0), "the gap between phases");

            // Growing: the count climbs before it falls, which is what the peak denominator is for.
            for (int n = 100; n <= 400; n += 50) Assert_(Step(p, false, 1f, n), "the grow count rising");
            for (int n = 400; n >= 0; n -= 25) Assert_(Step(p, false, 1f, n), "the grow count falling");

            Assert_(Step(p, false, 1f, 0, ready: true), "the finish");
        }

        [Test]
        public void FinishesAtExactlyOne()
        {
            var p = new ArenaLoadProgress();
            p.Reset();
            Step(p, true, 0.4f, 0);

            // The bar must reach 1, not 0.95 - a bar that vanishes short reads as an abandoned
            // load rather than a completed one.
            Assert.AreEqual(1f, Step(p, false, 1f, 0, ready: true), 1e-6f);
        }

        [Test]
        public void IdleCreepMovesButNeverReachesItsCeiling()
        {
            // Sitting still on an unmeasurable span reads as a hang; jumping ahead lies. The creep
            // has to do both: always move, never arrive.
            var p = new ArenaLoadProgress();
            p.Reset();

            float first = Step(p, false, 1f, 0);
            Assert.Greater(first, 0f, "the creep must move on the very first frame");

            float v = first;
            for (int i = 0; i < 10000; i++) v = Step(p, false, 1f, 0);

            Assert.LessOrEqual(v, ArenaLoadProgress.DwellCeiling + 1e-4f,
                "the dwell creep must never pass its ceiling, however long the wait - the bar " +
                "must not claim a phase that has not started");
        }

        [Test]
        public void LayingSpansItsAuthoredBand()
        {
            var p = new ArenaLoadProgress();
            p.Reset();

            Assert.AreEqual(ArenaLoadProgress.LayFloor, Step(p, true, 0f, 0), 1e-4f);
            Assert.AreEqual(ArenaLoadProgress.LayCeiling, Step(p, true, 1f, 0), 1e-4f);
        }

        [Test]
        public void GrowingMeasuresAgainstThePeakCount()
        {
            var p = new ArenaLoadProgress();
            p.Reset();

            // First reading is the peak so far, so it reads as 0% of the grow band, not 100%.
            Assert.AreEqual(ArenaLoadProgress.GrowFloor, Step(p, false, 1f, 500), 1e-4f);

            // Half settled = half the band.
            float half = Step(p, false, 1f, 250);
            Assert.AreEqual((ArenaLoadProgress.GrowFloor + ArenaLoadProgress.GrowCeiling) / 2f,
                            half, 1e-3f);

            // One prism left of 500 is as good as done, but NOT the ready flag - the band's
            // ceiling is where growing tops out, and only `ready` takes it to 1.
            float nearlyDone = Step(p, false, 1f, 1);
            Assert.Greater(nearlyDone, ArenaLoadProgress.GrowCeiling - 0.01f);
            Assert.LessOrEqual(nearlyDone, ArenaLoadProgress.GrowCeiling + 1e-4f);
        }

        [Test]
        public void ResetIsTheOnlyWayDown()
        {
            var p = new ArenaLoadProgress();
            p.Reset();
            Step(p, true, 1f, 0);
            Assert.Greater(p.Value, 0.5f);

            p.Reset();
            Assert.AreEqual(0f, p.Value, 1e-6f);
        }
    }
}
#endif
