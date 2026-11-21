// --race: fly each rung under several pilots in one model (FlightSim) and report what READING THE COURSE is worth -
// the round-5 bar: STRICT gain = (best fixed habit - best reader) / best fixed habit (Docs/GrizzlyTime/POWDER_KEGS_SPEC.md:
// 5-15% is a real feature, under 3% is fake) - and how today's AI and the coached AI race.
//
//   habits  : a human (no nose lag, 0.25 s reaction) who reads nothing: aims at each ring's centre and lets go of
//             Speed Mode whenever the nose is more than A degrees off its steering point (A in 15/30/60/90/180).
//   readers : the same human flying BrittleStarTimeLine: down each funnel, and through each let-go gate (with the
//             same A set, plus "let go at the posts").
//   AI today: GateRaceController's steering, the hull's nose lag, the shipped Speed law, Creation whenever off Speed.
//   AI coach: BrittleStarTimeCoach (the mode's Speed wish) on the same hull model.
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using UnityEngine;

public static class RaceSweep
{
    const float Inner = 480f, Outer = 1080f;
    static readonly float[] LiftAngles = { 15f, 30f, 60f, 90f, 180f };

    public static List<RaceGate> Lap(int intensity, int seed)
    {
        if (BrittleStarTimeCourse.BuildLap(intensity, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out var lap))
            return lap;
        var s = BrittleStarTimeCourse.Gearbox();
        s.InnerRadius = Inner;
        s.OuterRadius = Outer;
        s.FirstGateDirection = Vector3.up;
        return HeadlongCircuit.Generate(seed, s);
    }

    public static float Median(List<float> v) { var c = new List<float>(v); c.Sort(); return c.Count == 0 ? float.NaN : c[c.Count / 2]; }

    public static float TopSpeed(int intensity) =>
        intensity >= 4 ? BrittleStarTimeCourse.SpeedModeSpeedAtTime5 : BrittleStarTimeCourse.SpeedModeSpeed;

    public static FlightSim.Options Human(int intensity, FlightSim.Steering steer) => new FlightSim.Options
    {
        TopMultiplier = TopSpeed(intensity) / BrittleStarTimeCourse.CruiseSpeed,
        Rolls = intensity >= 4, Steer = steer, ReactSeconds = 0.25f,
        Laps = BrittleStarTimeCourse.LapsFor(intensity), MaxSeconds = 300f,
    };

    public static FlightSim.Options Autopilot(int intensity, FlightSim.Steering steer) => new FlightSim.Options
    {
        TopMultiplier = TopSpeed(intensity) / BrittleStarTimeCourse.CruiseSpeed,
        Rolls = false, NoseLag = true, OffIsCreation = true, Steer = steer,
        Laps = BrittleStarTimeCourse.LapsFor(intensity), MaxSeconds = 420f,
    };

    static FlightSim.SpeedPolicy Lift(float deg) => (in FlightSim.State s, IReadOnlyList<RaceGate> l, int r) => s.AimAngle <= deg;

    static FlightSim.SpeedPolicy LiftAndLetGo(float deg, float top) => (in FlightSim.State s, IReadOnlyList<RaceGate> l, int r) =>
        s.AimAngle <= deg && (l[r].Position - s.Pos).magnitude > BrittleStarTimeLine.LetGoDistance(l, r, top);

    /// <summary>The shipped AI's Speed law (BrittleStarAIBoostPolicy.asset via BrittleStarSpeedModeAIPolicySO): wish Speed
    /// within 12 deg of the ring with 1.5 s of runway, let go past 25 deg or under 0.5 s, held 1.2 s, rested 0.8 s.</summary>
    public static FlightSim.SpeedPolicy ShippedAI()
    {
        bool wish = false; float changed = -99f;
        return (in FlightSim.State s, IReadOnlyList<RaceGate> lap, int ring) =>
        {
            Vector3 to = lap[ring].Position - s.Pos;
            float ang = Vector3.Angle(s.Fwd, to), runway = to.magnitude / Mathf.Max(1f, s.Speed);
            if (wish)
            {
                if (s.T - changed >= 1.2f && (ang > 25f || runway < 0.5f)) { wish = false; changed = s.T; }
            }
            else if (s.T - changed >= 0.8f && ang <= 12f && runway >= 1.5f) { wish = true; changed = s.T; }
            return wish;
        };
    }

    /// <summary>The mode's coach, exactly as the controller asks it (BrittleStarTimeCoach.WantsSpeed).</summary>
    public static FlightSim.SpeedPolicy Coach(int intensity, int seat, BrittleStarTimeCoach.Skill skill) =>
        (in FlightSim.State s, IReadOnlyList<RaceGate> lap, int ring) =>
            BrittleStarTimeCoach.WantsSpeed(lap, ring, s.Pos, s.Fwd, TopSpeed(intensity), seat, skill, intensity, s.SpeedOn);

    public struct Row { public float Median; public int Finished, Seeds; public float Rings, Misses, CarriedShare; }

    public static Row Fly(int intensity, int seeds, Func<FlightSim.SpeedPolicy> make, FlightSim.Options o)
    {
        var ts = new List<float>(); int fin = 0; float rings = 0, miss = 0, carried = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var r = FlightSim.Fly(Lap(intensity, seed), make(), o);
            ts.Add(r.Seconds); if (r.Finished) fin++; rings += r.Threaded; miss += r.Misses; carried += r.Carried;
        }
        return new Row { Median = Median(ts), Finished = fin, Seeds = seeds, Rings = rings / seeds, Misses = miss / seeds,
                         CarriedShare = rings > 0 ? carried / rings : 0f };
    }

    static string Show(Row r) => $"{r.Median,6:0.0} s, finished {r.Finished}/{r.Seeds}, misses {r.Misses:0.0}, carried {r.CarriedShare * 100f:0}%";

    /// <summary>The numbers the sweep and the self-test read: per rung, the best habit, the best reader, the gain, and
    /// the AI today against the coached AI (per skill).</summary>
    public sealed class RungResult
    {
        public int Intensity;
        public Row BestHabit, BestReader, AIToday, BestLineNoGates;
        public string HabitName, ReaderName;
        public Row[] Coached = new Row[4];
        public float Gain => (BestHabit.Median - BestReader.Median) / BestHabit.Median;
        /// <summary>What the let-go gates alone are worth: the best line flown with them against the best without.</summary>
        public float GateGain => (BestLineNoGates.Median - BestReader.Median) / BestLineNoGates.Median;
    }

    public static RungResult Measure(int intensity, int seeds, bool verbose)
    {
        var res = new RungResult { Intensity = intensity };
        float top = TopSpeed(intensity);
        res.BestHabit.Median = float.MaxValue; res.BestReader.Median = float.MaxValue; res.BestLineNoGates.Median = float.MaxValue;
        if (verbose) Console.WriteLine($"I{intensity} ({BrittleStarTimeCourse.LapsFor(intensity)} laps):");
        foreach (var a in LiftAngles)
        {
            var h = Fly(intensity, seeds, () => Lift(a), Human(intensity, FlightSim.Steering.Centre));
            if (verbose) Console.WriteLine($"  habit  centre, lift past {a,3:0} deg: {Show(h)}");
            if (h.Finished == seeds && h.Median < res.BestHabit.Median) { res.BestHabit = h; res.HabitName = $"centre, lift past {a:0}"; }
            var app = Fly(intensity, seeds, () => Lift(a), Human(intensity, FlightSim.Steering.Approach));
            if (verbose) Console.WriteLine($"  habit  approach every ring, lift past {a,3:0} deg: {Show(app)}");
            if (app.Finished == seeds && app.Median < res.BestHabit.Median) { res.BestHabit = app; res.HabitName = $"approach every ring, lift past {a:0}"; }
            foreach (bool letGo in new[] { false, true })
            {
                var rd = Fly(intensity, seeds, () => letGo ? LiftAndLetGo(a, top) : Lift(a), Human(intensity, FlightSim.Steering.Line));
                if (verbose) Console.WriteLine($"  reader line{(letGo ? " + let-go" : "")}, lift past {a,3:0} deg: {Show(rd)}");
                if (!letGo && rd.Finished == seeds && rd.Median < res.BestLineNoGates.Median) res.BestLineNoGates = rd;
                if (rd.Finished == seeds && rd.Median < res.BestReader.Median) { res.BestReader = rd; res.ReaderName = $"line{(letGo ? " + let-go" : "")}, lift past {a:0}"; }
            }
        }
        res.AIToday = Fly(intensity, seeds, ShippedAI, Autopilot(intensity, FlightSim.Steering.GateRaceAI));
        for (int k = 0; k < 4; k++)
        {
            var skill = (BrittleStarTimeCoach.Skill)k;
            var o = Autopilot(intensity, FlightSim.Steering.Line);
            o.NoseFollow = BrittleStarTimeCoach.NoseFollowFor(intensity, skill);
            res.Coached[k] = Fly(intensity, seeds, () => Coach(intensity, 1, skill), o);
        }
        if (verbose)
        {
            Console.WriteLine($"  best habit  ({res.HabitName}): {Show(res.BestHabit)}");
            Console.WriteLine($"  best reader ({res.ReaderName}): {Show(res.BestReader)}");
            Console.WriteLine($"  reading the course saves {res.Gain * 100f:0.0}% against the best habit; the let-go gates alone {res.GateGain * 100f:0.0}%");
            Console.WriteLine($"  AI today: {Show(res.AIToday)}, rings {res.AIToday.Rings:0.0}");
            for (int k = 0; k < 4; k++)
                Console.WriteLine($"  AI coach, {(BrittleStarTimeCoach.Skill)k}: {Show(res.Coached[k])}, rings {res.Coached[k].Rings:0.0}");
        }
        return res;
    }

    /// <summary>The coach's pace against the reading human, per skill: Easy 2x, Medium 1.5x, Hard 1.25x, Legendary 1.05x.
    /// A person at the controls flies ~1.6x the model's reader (the recorded I1 race at Speed: 5.5 s a ring against 3.4),
    /// so a first race sits between Easy and Medium and a practised one between Medium and Hard.</summary>
    public static readonly float[] PaceTarget = { 2.0f, 1.5f, 1.25f, 1.05f };

    /// <summary>The nose follow range searched: the fleet's 1.5/s is the floor's neighbour; past 8/s an AI's nose is all
    /// but lag-free, which no human-feeling rival should be.</summary>
    public const float MinFollow = 0.8f, MaxFollow = 8f;

    /// <summary>--tune: for each rung and skill, the AI nose follow (per second) whose median race lands on
    /// PaceTarget x the reading human, by bisection (a faster nose flies faster).</summary>
    public static int Tune(string[] args)
    {
        int seeds = args.Length > 1 ? int.Parse(args[1]) : 16;
        for (int intensity = 1; intensity <= 4; intensity++)
        {
            float reader = Measure(intensity, seeds, false).BestReader.Median;
            var row = new List<string>();
            for (int k = 0; k < 4; k++)
            {
                var skill = (BrittleStarTimeCoach.Skill)k;
                float target = PaceTarget[k] * reader;
                float bestErr = float.MaxValue, bestShare = 0f, bestFollow = 0f, bestRatio = 0f;
                foreach (float share in new[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f })
                {
                    BrittleStarTimeCoach.CornerShareByIntensity[intensity - 1][k] = share;
                    float lo = MinFollow, hi = MaxFollow;
                    for (int it = 0; it < 10; it++)
                    {
                        float mid = 0.5f * (lo + hi);
                        var o = Autopilot(intensity, FlightSim.Steering.Line);
                        o.NoseFollow = mid;
                        var r = Fly(intensity, seeds, () => Coach(intensity, 1, skill), o);
                        if (r.Finished < seeds || r.Median > target) lo = mid; else hi = mid;
                    }
                    var check = Autopilot(intensity, FlightSim.Steering.Line);
                    check.NoseFollow = hi;
                    var got = Fly(intensity, seeds, () => Coach(intensity, 1, skill), check);
                    if (got.Finished < seeds) continue;
                    float err = Mathf.Abs(got.Median / reader - PaceTarget[k]);
                    if (err < bestErr - 0.02f) { bestErr = err; bestShare = share; bestFollow = hi; bestRatio = got.Median / reader; }
                }
                BrittleStarTimeCoach.CornerShareByIntensity[intensity - 1][k] = bestShare;
                BrittleStarTimeCoach.NoseFollowByIntensity[intensity - 1][k] = bestFollow;
                row.Add($"{(BrittleStarTimeCoach.Skill)k}: share {bestShare:0.00}, follow {bestFollow:0.00} ({bestRatio:0.00}x)");
            }
            Console.WriteLine($"I{intensity}: reader {reader:0.0} s -> {string.Join("; ", row)}");
        }
        return 0;
    }

    /// <summary>The bar a feature must clear to be real (Docs/GrizzlyTime/POWDER_KEGS_SPEC.md: under 3% is fake).</summary>
    public const float RealFeatureBar = 0.03f;

    /// <summary>
    /// --check-race: the measured claims of BRITTLESTARTIME.md section 0, as a gate (exit 0 = they hold). The let-go gates
    /// are worth at least <see cref="RealFeatureBar"/> on every rung that stands one; every coached AI, at every skill,
    /// finishes every rung; Easy is slower than Legendary on every rung; and the coached Medium beats today's AI.
    /// </summary>
    public static int CheckRace(string[] args)
    {
        int seeds = args.Length > 1 ? int.Parse(args[1]) : 16;
        int fails = 0;
        void Fail(string what) { fails++; Console.WriteLine($"FAIL {what}"); }
        for (int intensity = 1; intensity <= 4; intensity++)
        {
            var r = Measure(intensity, seeds, false);
            bool gates = Enumerable.Range(1, seeds).Any(seed =>
            {
                var lap = Lap(intensity, seed);
                return Enumerable.Range(0, lap.Count).Any(k => BrittleStarTimeLine.LetGoDistance(lap, k, TopSpeed(intensity)) > 0f);
            });
            bool expectGates = intensity >= 3;
            if (gates != expectGates) Fail($"I{intensity}: let-go gates {(gates ? "stand" : "are missing")}");
            if (expectGates && !(r.GateGain >= RealFeatureBar))
                Fail($"I{intensity}: the let-go gates are worth {r.GateGain * 100f:0.0}% (< {RealFeatureBar * 100f:0}%)");
            for (int k = 0; k < 4; k++)
                if (r.Coached[k].Finished < seeds)
                    Fail($"I{intensity}: the coached {(BrittleStarTimeCoach.Skill)k} AI finished {r.Coached[k].Finished}/{seeds}");
            if (!(r.Coached[0].Median > r.Coached[3].Median))
                Fail($"I{intensity}: Easy ({r.Coached[0].Median:0.0} s) is not slower than Legendary ({r.Coached[3].Median:0.0} s)");
            if (!(r.Coached[1].Median < r.AIToday.Median))
                Fail($"I{intensity}: the coached Medium ({r.Coached[1].Median:0.0} s) does not beat today's AI ({r.AIToday.Median:0.0} s)");
            Console.WriteLine($"I{intensity}: gates {r.GateGain * 100f:0.0}%, reader {r.BestReader.Median:0.0} s, AI today " +
                              $"{r.AIToday.Median:0.0} s, coached {string.Join(" / ", r.Coached.Select(c => c.Median.ToString("0.0")))} s");
        }
        Console.WriteLine(fails == 0 ? "the measured claims hold" : $"{fails} measured claim(s) fail");
        return fails == 0 ? 0 : 1;
    }

    public static int Run(string[] args)
    {
        int seeds = args.Length > 1 ? int.Parse(args[1]) : 24;
        for (int i = 1; i <= 4; i++)
            for (int seed = 1; seed <= 2; seed++)
            {
                var lap = Lap(i, seed);
                Console.WriteLine($"I{i} seed {seed}: turn/facing/flyable/let-go " + string.Join("  ", Enumerable.Range(0, lap.Count).Select(k =>
                    $"{BrittleStarTimeLine.TurnDegrees(lap, k):0}/{BrittleStarTimeLine.FacingDegrees(lap, k):0}/{BrittleStarTimeLine.FlyableRadius(lap, k):0}/{BrittleStarTimeLine.LetGoDistance(lap, k, TopSpeed(i)):0}")));
            }
        for (int intensity = 1; intensity <= 4; intensity++) Measure(intensity, seeds, true);
        return 0;
    }
}
