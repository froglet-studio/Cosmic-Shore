using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Tests
{
    /// <summary><see cref="RealtimePacer"/> on a fake clock: early ticks wait, late ticks catch up, debt is capped.</summary>
    public class RealtimePacerTests
    {
        const double Step = 1.0 / 60.0;

        [Fact]
        public void EarlyTick_SleepsOutTheRestOfItsStep()
        {
            double now = 0;
            var slept = new List<int>();
            var p = new RealtimePacer(Step, () => now, ms => { slept.Add(ms); now += ms; });
            now = 2; // the tick took 2 ms
            p.AfterTick();
            Assert.Equal(new[] { 14 }, slept); // 16.67 - 2, truncated
        }

        [Fact]
        public void SteadyFastTicks_HoldGameTimeToTheWallClock()
        {
            double now = 0;
            var p = new RealtimePacer(Step, () => now, ms => now += ms);
            for (int i = 0; i < 600; i++) { now += 0.5; p.AfterTick(); }
            // 600 ticks = 10 s of game time; the wall clock may trail by under one step.
            Assert.InRange(now, 10000 - 1000.0 / 60.0, 10000);
        }

        [Fact]
        public void LateTick_IsCaughtUpWithoutSleeping()
        {
            double now = 0;
            var slept = new List<int>();
            var p = new RealtimePacer(Step, () => now, ms => { slept.Add(ms); now += ms; });
            now = 40; // one slow tick, 2.4 steps
            Assert.Equal(0, p.AfterTick());
            now += 1; // the next tick is quick but still behind (2 steps = 33.3 ms of game time vs 41 ms)
            Assert.Equal(0, p.AfterTick());
            now += 1; // 3 steps = 50 ms vs 42 ms: ahead again
            Assert.Equal(8, p.AfterTick());
            Assert.Equal(new[] { 8 }, slept);
        }

        [Fact]
        public void DebtPastAQuarterSecond_IsDropped()
        {
            double now = 0;
            var p = new RealtimePacer(Step, () => now, ms => now += ms);
            now = 5000; // a 5 s load inside one tick
            p.AfterTick();
            // Without the cap the next ~300 ticks would run unpaced. With it, at most a quarter second
            // of catch-up remains: 15 steps, then the pacer sleeps again.
            int unpaced = 0;
            while (p.AfterTick() == 0) { unpaced++; now += 0.1; Assert.True(unpaced < 30, "debt was not capped"); }
            Assert.InRange(unpaced, 13, 16);
        }
    }
}
