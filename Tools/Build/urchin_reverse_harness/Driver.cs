using System;
using UnityEngine;
using CosmicShore.Gameplay;

static class Driver
{
    // The brake EXACTLY as it stood before this change, as the negative control for the no-op claim.
    static float Legacy(float stepped, float current, float target, float rate, float dt)
    {
        if (target > 0f || current <= 0f || rate <= 0f || dt <= 0f) return stepped;
        return Mathf.Max(0f, Mathf.Min(stepped, current - rate * dt));
    }

    static int Main()
    {
        const float LERP = 1.5f;
        int fails = 0;

        // ---- T1. symmetric:false is BIT-IDENTICAL to the old brake over every shipped input ----
        //  target is swept over [0, ...] only, because no shipped transformer without CanReverse
        //  can produce a negative one (XDiff in [0,1], positive scalers, boost >= 1).
        long n = 0; int diff = 0;
        foreach (float cur in new[]{ -300f, -40f, -0.01f, 0f, 0.01f, 5f, 20f, 65f, 180f, 347f })
        foreach (float tgt in new[]{ 0f, 0.5f, 20f, 65f, 180f })
        foreach (float rate in new[]{ 0f, 32.5f, 90f })
        foreach (float dt in new[]{ 0f, 0.0166f, 0.033f, 0.1f })
        {
            float stepped = Mathf.Lerp(cur, tgt, LERP * dt);
            float now = MinimumThrottleBrake.Apply(stepped, cur, tgt, rate, dt, symmetric: false);
            float was = Legacy(stepped, cur, tgt, rate, dt);
            n++;
            if (BitConverter.SingleToInt32Bits(now) != BitConverter.SingleToInt32Bits(was)) { diff++;
                if (diff < 4) Console.WriteLine($"  DIFF cur={cur} tgt={tgt} rate={rate} dt={dt}: {was} -> {now}"); }
        }
        Console.WriteLine($"T1 no-op for non-reversing hulls: {n} cases, {diff} differ  {(diff==0?"PASS":"FAIL")}");
        if (diff != 0) fails++;

        // ---- T1b. NEGATIVE control, and a statement of WHERE the flag can matter. -------
        //  The brake's own contract is that the exponential owns the fast fall and the constant
        //  rate owns only the tail, the crossover being rate / LERP_AMOUNT. So symmetric:true can
        //  only differ BELOW it, and must differ everywhere below it — a control picked above the
        //  crossover comes back green while proving nothing (which is how this test first passed
        //  1 of 3).
        const float rateT = 32.5f, dtT = 1f / 60f;
        float crossover = rateT / LERP;                      // 21.67 u/s on an Urchin
        int below = 0, belowDiff = 0, above = 0, aboveSame = 0;
        for (float cur = -60f; cur < -0.005f; cur += 0.005f)
        {
            float stepped = Mathf.Lerp(cur, 0f, LERP * dtT);
            bool differs = BitConverter.SingleToInt32Bits(MinimumThrottleBrake.Apply(stepped, cur, 0f, rateT, dtT, true))
                        != BitConverter.SingleToInt32Bits(Legacy(stepped, cur, 0f, rateT, dtT));
            if (Mathf.Abs(cur) < crossover) { below++; if (differs) belowDiff++; }
            else                            { above++; if (!differs) aboveSame++; }
        }
        bool t1b = belowDiff == below && aboveSame == above && below > 0 && above > 0;
        Console.WriteLine($"T1b negative control: below crossover ({crossover:F2} u/s) {belowDiff}/{below} differ, " +
                          $"above {aboveSame}/{above} identical  {(t1b ? "PASS" : "FAIL")}");
        if (!t1b) fails++;

        // ---- T2. A REVERSING Urchin released to centre reaches an EXACT zero, and in a
        //  comparable time to the forward stop. Urchin ThrottleScaler 65, brakeSeconds 2 -> 32.5 u/s^2.
        foreach (int sign in new[]{ +1, -1 })
        {
            float v = sign * 65f, dt = 1f / 60f; int f = 0;
            while (f < 60 * 30 && v != 0f)
            { v = MinimumThrottleBrake.Apply(Mathf.Lerp(v, 0f, LERP * dt), v, 0f, 32.5f, dt, symmetric: true); f++; }
            string dir = sign > 0 ? "forward" : "reverse";
            bool ok = v == 0f && f < 60 * 10;
            Console.WriteLine($"T2 {dir} 65 u/s -> stop: {f / 60f:F2}s, final={v}  {(ok?"PASS":"FAIL")}");
            if (!ok) fails++;
        }

        // ---- T3. The brake never pushes a reversing vessel PAST the stop into forward motion. ----
        bool overshot = false;
        for (float v0 = -1f; v0 < 0f; v0 += 0.01f)
        {
            float r = MinimumThrottleBrake.Apply(Mathf.Lerp(v0, 0f, LERP * 0.1f), v0, 0f, 200f, 0.1f, true);
            if (r > 0f) overshot = true;
        }
        Console.WriteLine($"T3 reverse brake never overshoots into forward: {(!overshot?"PASS":"FAIL")}");
        if (overshot) fails++;

        // ---- T4. A REVERSE command (negative target) is never clamped to a stop. ----
        float rev = MinimumThrottleBrake.Apply(Mathf.Lerp(10f, -65f, LERP * 0.0166f), 10f, -65f, 32.5f, 0.0166f, true);
        float expect = Mathf.Lerp(10f, -65f, LERP * 0.0166f);
        Console.WriteLine($"T4 negative target passes through untouched: {rev}=={expect}  {(rev==expect?"PASS":"FAIL")}");
        if (rev != expect) fails++;

        Console.WriteLine(fails == 0 ? "\nALL PASS" : $"\n{fails} FAILED");
        return fails;
    }
}
