// Round 11f (Docs/ECOLOGY_LOD.md §2): the conserved stomach that replaces Fauna.starvationSeconds.
// Asserts the migration (an authored clock behaves the same), the conservation, and a negative control.
using System;
using CosmicShore.Gameplay;

public static class StomachHarness
{
    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("STOMACH (FaunaStomach replaces the starvation clock)");
        const float nominal = 16f;   // CellPhaseThresholds.NominalPrismVolume

        // 1. migration: with nominal meals, death comes exactly starvationSeconds after the last feed, as the clock did
        var rng = new Random(3);
        int agree = 0, trials = 2000; double worst = 0;
        for (int t = 0; t < trials; t++)
        {
            float T = 5f + (float)rng.NextDouble() * 120f;
            var st = FaunaStomach.FromStarvationClock(T, nominal);
            float now = 0f, lastFed = 0f;
            st.Fill(now);
            int feeds = rng.Next(0, 6);
            for (int f = 0; f < feeds; f++)
            {
                now += (float)rng.NextDouble() * T * 0.95f;         // always before the clock would have run out
                st.Feed(nominal * (1f + (float)rng.NextDouble()), now);   // a meal of at least one nominal prism
                lastFed = now;
            }
            float clockDeath = lastFed + T, stomachDeath = now + st.SecondsLeft(now);
            double err = Math.Abs(clockDeath - stomachDeath);
            worst = Math.Max(worst, err);
            if (err < 1e-3 * T) agree++;
        }
        Console.WriteLine($"  migration: {agree}/{trials} random feeding histories starve at the clock's time (worst {worst:E1} s)");
        check(agree == trials, "an authored starvationSeconds behaves identically for meals of at least one nominal prism");

        var half = FaunaStomach.FromStarvationClock(30f, nominal);
        half.Fill(0f);
        half.Settle(30f);                // empty at t = 30
        half.Feed(nominal * 0.5f, 30f);  // half a meal
        check(Math.Abs(half.SecondsLeft(30f) - 15f) < 1e-3f, "half a nominal meal buys half the clock (15 s of 30) - the conservation the clock lacked");
        var never = FaunaStomach.FromStarvationClock(0f, nominal);
        never.Fill(0f);
        check(!never.IsEmpty(1e6f), "starvationSeconds 0 keeps 'never starves'");

        // 2. conservation: in == out + held, over random feeds, settles and a predator's surrender
        double inSum = 0, outSum = 0, maxRel = 0;
        for (int t = 0; t < 500; t++)
        {
            var st = FaunaStomach.FromStarvationClock(10f + (float)rng.NextDouble() * 50f, nominal * (0.5f + (float)rng.NextDouble() * 3f));
            float now = 0f;
            double held0 = st.Fill(now); inSum = held0; outSum = 0;
            for (int k = 0; k < 40; k++)
            {
                now += (float)rng.NextDouble() * 8f;
                double r = rng.NextDouble();
                if (r < 0.5) { float v = (float)rng.NextDouble() * 40f; inSum += v; outSum += st.Feed(v, now); }
                else outSum += st.Settle(now);
            }
            outSum += st.SurrenderAll(now + 1f, out float burned) + burned;
            maxRel = Math.Max(maxRel, Math.Abs(inSum - outSum) / inSum);
        }
        Console.WriteLine($"  conservation: worst |in - out| / in = {maxRel:E1} over 500 histories (feeds, upkeep, overflow, surrender)");
        check(maxRel < 1e-5, "every volume that enters a stomach leaves it exactly once (soil, overflow or a predator)");

        // negative control: the clock's semantics (any feed refills to capacity) conjures mass
        double minted = 0;
        {
            var st = FaunaStomach.FromStarvationClock(30f, nominal);
            float now = 0f; st.Fill(now);
            double ins = nominal, outs = 0;
            for (int k = 0; k < 40; k++)
            {
                now += 5f; outs += st.Settle(now);
                float bite = 1f; ins += bite;
                outs += 0; st = FaunaStomach.FromStarvationClock(30f, nominal); st.Fill(now);   // "reset the clock"
            }
            outs += st.SurrenderAll(now, out float b) + b;
            minted = (outs - ins) / ins;
        }
        Console.WriteLine($"  negative control (clock reset on a 1-volume bite): {minted:P0} of the intake conjured");
        check(minted > 0.5, "negative control: a clock-reset stomach fails conservation");
    }
}
