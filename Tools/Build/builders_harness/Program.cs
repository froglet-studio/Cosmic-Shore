// Headless proof of the SHIPPED builder and thief cores (Assets/.../FloraAndFauna/Builders/*Core.cs) against a C# port
// of the research arena (Arena.cs). Every test prints its numbers next to the research's and ASSERTS the result;
// the run exits non-zero on any failure.
//   bash Tools/Build/builders_harness/run.sh            # everything (~1-2 min)
//   bash Tools/Build/builders_harness/run.sh fortress   # just the fortress block
//   bash Tools/Build/builders_harness/run.sh thieves    # just the thieves block
//   bash Tools/Build/builders_harness/run.sh emotion <jobs.txt>   # the emotion-probe export (EmotionBuilders.cs, SWARM_FAUNA.md §27)
// What this does NOT prove: anything about Unity - prisms, colliders, crystals, the GPU draw (Docs/BUILDERS_AND_THIEVES.md §7).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class Program
{
    static int _fail;
    const float Dt = 0.1f;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static float Median(List<float> v)
    {
        var s = v.Where(x => !float.IsNaN(x)).OrderBy(x => x).ToList();
        if (s.Count == 0) return float.NaN;
        return s.Count % 2 == 1 ? s[s.Count / 2] : 0.5f * (s[s.Count / 2 - 1] + s[s.Count / 2]);
    }

    static int Main(string[] args)
    {
        string only = args.Length > 0 ? args[0] : "";
        var sw = Stopwatch.StartNew();
        if (only == "exp") { Experiment(); return 0; }
        if (only == "emotion") return Emotion(args[1]);   // round 11d-2 (SWARM_FAUNA.md §27): the emotion-probe export
        if (only == "" || only == "fortress") Fortress();
        if (only == "" || only == "thieves") Thieves();
        Console.WriteLine($"\n{(_fail == 0 ? "OK" : $"FAILED ({_fail})")} - builders harness, {sw.Elapsed.TotalSeconds:F1} s");
        return _fail == 0 ? 0 : 1;
    }

    // ═════════════════════════════════════════════════════════════════════════════════ FORTRESS

    sealed class CutResult
    {
        public List<(int cut, int refilled, float t50, float t90)> Cuts = new();
        public int Built, Repairs, RepairTrail, Kills, Starved, Placed, PlacedTrail, Births, Eaten;
        public double Audit, Created;
        public float MovesPerS, QueriesPerS, MsPerStep, MsP95;
        public List<int> LineMass = new();
        public int ShieldedTaken, Stings;
    }

    static readonly float[] CutTimes = { 150f, 200f, 250f };

    /// <summary>run_fortress.cut_run: 150 s of building, then three straight cutting passes through the core.</summary>
    static CutResult CutRun(int seed, BuilderMendRule mend, bool game, float scar = 0f, bool sameLine = false,
                            float shielded = 0f, bool lieAboutShields = false, float minutes = 5f, string raid = "ram",
                            Action<BuilderColonyParams> tweak = null)
    {
        var ar = new Arena(seed);
        ar.Scatter(1500, shieldedFrac: shielded);
        var arng = new BuilderRng(seed + 5);
        var A = arng.OnSphere() * 450f;
        var p = new BuilderColonyParams { Mend = mend, ScarGain = scar, Containment = ar.R * 0.95f };
        if (!game) p.Stomach = null;
        tweak?.Invoke(p);
        IBuilderWorld world = lieAboutShields ? new ShieldLiar(ar) : ar;
        var col = new BuilderColonyCore(world, p, A, 2, 1, seed);
        var far = new[] { new Vector3(400, 0, 0), new Vector3(0, 400, 0), new Vector3(-400, 0, 0), new Vector3(0, -400, 0) }
            .Select(v => { var f = A + v; return f * MathF.Min(1f, 1000f / f.Length()); }).ToList();
        var pilot = ar.AddPilot(new Pilot { Policy = "circuit", Name = "cutter", Speed = 140f, TrailEvery = 0.25f, TrailVol = 6f, Domain = 1 });
        pilot.Waypoints = new List<Vector3>(far);
        pilot.Pos = far[0];
        var rng = new BuilderRng(seed + 77);
        var plan = new List<(int step, Vector3 a, Vector3 b)>();
        var d0 = rng.OnSphere();
        foreach (var tc in CutTimes)
        {
            var d = rng.OnSphere();
            if (sameLine) d = d0;
            plan.Add(((int)MathF.Round((tc - 3f) / Dt), A - d * 420f, A + d * 420f));
        }
        int steps = (int)(minutes * 60 / Dt);
        var vessels = new BuilderVessel[8];
        var res = new CutResult();
        var times = new List<double>(steps);
        var pts = new List<Vector3>();
        for (int s = 0; s < steps; s++)
        {
            foreach (var (st, a, b) in plan)
            {
                if (s == st)
                {
                    pilot.Waypoints = new List<Vector3> { a, b }; pilot.Waypoints.AddRange(far);
                    pilot.Wp = 0; pilot.Pos = a; pilot.Vel = BuilderMath.Unit(b - a) * pilot.Speed;
                    if (raid == "steal") pilot.Thief = true; else pilot.Ram = true;
                    // wall mass on the cut line just before the pass (scar thickness)
                    col.BuiltPositions(pts);
                    var u = BuilderMath.Unit(b - a);
                    res.LineMass.Add(pts.Count(q => { var w = q - a; return (w - u * Vector3.Dot(w, u)).Length() < 20f; }));
                }
                if (s == st + 90) { pilot.Ram = false; pilot.Thief = false; }
            }
            int nv = ar.Vessels(vessels);
            long t0 = Stopwatch.GetTimestamp();
            col.Step(Dt, vessels, nv);
            times.Add((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            ar.RamStructures();
            ar.Step(Dt);
        }
        foreach (var tc in CutTimes) res.Cuts.Add(col.RepairStats(tc - 3f));
        res.Built = col.Built; res.Repairs = col.Repairs; res.RepairTrail = col.RepairTrail;
        res.Kills = col.Kills; res.Starved = col.Starved; res.Placed = col.Placed; res.PlacedTrail = col.PlacedTrail;
        res.Births = col.Births; res.Eaten = col.Eaten; res.Stings = col.StingCount;
        res.Audit = ar.Audit(); res.Created = ar.Scattered + ar.LaidVol;
        res.MovesPerS = col.CarryMoves / (minutes * 60f);
        res.QueriesPerS = col.Queries / (minutes * 60f);
        times.Sort();
        res.MsPerStep = (float)times.Average(); res.MsP95 = (float)times[(int)(times.Count * 0.95)];
        // the shield law: no shielded prism ever changed domain, was built, or was carried
        for (int i = 0; i < ar.Count; i++)
            if (ar.ShieldedL[i] && (ar.Dom[i] != 0 || ar.Built.ContainsKey(i))) res.ShieldedTaken++;
        return res;
    }

    /// <summary>The negative control: a world that reports every shield as off (test_rules.py's planted failure).</summary>
    sealed class ShieldLiar : IBuilderWorld
    {
        readonly Arena _a;
        public ShieldLiar(Arena a) { _a = a; }
        public int QuerySphere(Vector3 c, float r, List<int> res) => _a.QuerySphere(c, r, res);
        public bool Alive(int h) => _a.Alive(h);
        public Vector3 Position(int h) => _a.Position(h);
        public int Domain(int h) => _a.Domain(h);
        public bool Shielded(int h) => false;
        public bool IsTrail(int h) => _a.IsTrail(h);
        public float Age(int h) => _a.Age(h);
        public float Volume(int h) => _a.Volume(h);
        public bool Loose(int h) => _a.Loose(h);
        public bool Steal(int h, int d)
        {
            // the liar's steal ignores the shield too (the bug the control plants)
            if (!_a.AliveL[h]) return false;
            _a.Dom[h] = d; return true;
        }
        public void Carry(int h, Vector3 p) => _a.Carry(h, p);
        public void Drop(int h) => _a.Drop(h);
        public bool TryReserve(Vector3 s, float c) => _a.TryReserve(s, c);
        public void Settle(int h, Vector3 f, Vector3 s, float t) => _a.Settle(h, f, s, t);
        public void SetBuilt(int h, int c, int site, bool b) => _a.SetBuilt(h, c, site, b);
        public float Consume(int h, Vector3 m) => _a.Consume(h, m);
        public void GiveBack(int h) => _a.GiveBack(h);
        public void Reclaim(int h, int v) => _a.Reclaim(h, v);
    }

    static (float t50, float t90, string healed, float trail, int sites) Summarise(List<CutResult> rows)
    {
        var t50 = rows.SelectMany(r => r.Cuts).Select(c => c.t50).ToList();
        var t90 = rows.SelectMany(r => r.Cuts).Select(c => c.t90).ToList();
        int healed = rows.SelectMany(r => r.Cuts).Count(c => !float.IsNaN(c.t90));
        int tot = rows.SelectMany(r => r.Cuts).Count(c => c.cut > 0);
        int sites = rows.SelectMany(r => r.Cuts).Sum(c => c.cut);
        float trail = (float)rows.Sum(r => r.RepairTrail) / Math.Max(1, rows.Sum(r => r.Repairs));
        return (Median(t50), Median(t90), $"{healed}/{tot}", trail, sites / Math.Max(1, tot));
    }

    static void Experiment()
    {
        int[] seeds = { 7, 23, 41, 101, 202, 303 };
        var variants = new (string, Action<BuilderColonyParams>)[]
        {
            ("def-on r0", p => { p.ReserveClear = 0f; p.SkipOwnDomain = false; }),
            ("def-off r0", p => { p.ReserveClear = 0f; p.SkipOwnDomain = false; p.AlarmRadius = 0f; }),
            ("caste30 r0", p => { p.ReserveClear = 0f; p.SkipOwnDomain = false; p.DefendCaste = 0.3f; }),
            ("caste30 r1.5", p => { p.ReserveClear = 1.5f; p.DefendCaste = 0.3f; }),
            ("caste30 r3.6", p => { p.ReserveClear = 3.6f; p.DefendCaste = 0.3f; }),
        };
        foreach (var (name, tw) in variants)
            foreach (var mend in new[] { BuilderMendRule.None, BuilderMendRule.Both })
            {
                var rows = seeds.Select(s => CutRun(s, mend, false, tweak: tw)).ToList();
                var m = Summarise(rows);
                Console.WriteLine($"{name,-12} {mend,-5} t50 {m.t50,5:F1} t90 {m.t90,5:F1} healed {m.healed} per-cut " +
                    string.Join(" ", rows.SelectMany(r => r.Cuts).Select(c => c.t50.ToString("F0"))));
            }
    }

    static void Fortress()
    {
        int[] seeds = { 7, 23, 41, 101, 202, 303 };
        Console.WriteLine("B1. fortress repair after a cut (run_fortress.py: 150 s of building, 3 cutting passes, 5 min; the shipped");
        Console.WriteLine("    defaults: a 30% defender caste, TryReserve 3.6 u, own-domain mass skipped). Research t50 / t90:");
        Console.WriteLine("    round 1: none 39.5 / 90.4, gap 15.8 / 43.6, alarm 6.8 / 41.1, both 6.4 / 61.6 (trail 97-98%);");
        Console.WriteLine("    round 3 (run_defend_vs_mend.py, both): defence on 17.8 / 70.0, off 5.6 / 38.5, caste 30% 5.6 / 41.3");
        var sum = new Dictionary<BuilderMendRule, (float t50, float t90, string healed, float trail, int sites)>();
        var all = new List<CutResult>();
        foreach (var mend in new[] { BuilderMendRule.None, BuilderMendRule.Gap, BuilderMendRule.Alarm, BuilderMendRule.Both })
        {
            var rows = seeds.Select(s => CutRun(s, mend, game: false)).ToList();
            all.AddRange(rows);
            sum[mend] = Summarise(rows);
            var m = sum[mend];
            Console.WriteLine($"    {mend,-6} t50 {m.t50,5:F1} s  t90 {m.t90,5:F1} s  healed(90%) {m.healed}  {m.sites} sites/cut  repair from trail {m.trail:P0}"
                              + $"  built {rows.Average(r => r.Built):F0}");
        }
        var defOn = seeds.Select(s => CutRun(s, BuilderMendRule.Both, false, tweak: p => p.DefendCaste = 0f)).ToList();
        var dS = Summarise(defOn);
        Console.WriteLine($"    both, every idle worker defends (caste off): t50 {dS.t50:F1} s  t90 {dS.t90:F1} s  healed {dS.healed}");
        float b50 = sum[BuilderMendRule.Both].t50, n50 = sum[BuilderMendRule.None].t50;
        Check(b50 <= 8f, $"alarm + gap knits a wound shut fast: t50 {b50:F1} s <= 8 (research 6.4 / 5.6 with the caste)");
        Check(n50 >= 15f, $"ordinary building alone is slow: t50 {n50:F1} s >= 15 (research 39.5)");
        Check(n50 >= 3f * b50, $"the mending rules are the difference: none/both = {n50 / b50:F1}x >= 3 (research 6.2x)");
        Check(sum[BuilderMendRule.Alarm].t50 <= 12f && sum[BuilderMendRule.Alarm].t50 < sum[BuilderMendRule.Gap].t50,
              $"ALARM is what makes the wound close: alarm-only t50 {sum[BuilderMendRule.Alarm].t50:F1} s <= 12 and faster than the gap rule alone (research 6.8 vs 15.8)");
        Check(sum[BuilderMendRule.Gap].t50 < n50, "the gap rule alone beats ordinary building (research 15.8 vs 39.5)");
        Check(dS.t50 > 1.5f * b50, $"defence competes with repair: caste off t50 {dS.t50:F1} s > 1.5 x caste 30% (research 17.8 vs 5.6)");
        Check(sum[BuilderMendRule.Both].trail >= 0.85f, $"the plug is the cutter's own trail: {sum[BuilderMendRule.Both].trail:P0} >= 85% (research 98%)");
        double worst = all.Max(r => Math.Abs(r.Audit) / r.Created);
        Check(worst < 1e-9, $"mass audit (created - live - eaten - destroyed) / created, worst of {all.Count} runs: {worst:E1} (research 0.000)");
        var both = all.Skip(3 * seeds.Length).ToList();
        Console.WriteLine($"    cost: {both.Average(r => r.MsPerStep):F3} ms/step mean, {both.Max(r => r.MsP95):F3} ms p95 (CoreCLR, 48 workers, 10 Hz);"
                          + $" carried-prism writes {both.Average(r => r.MovesPerS):F0}/s (research 148-241 in this test), QuerySphere {both.Average(r => r.QueriesPerS):F0}/s");
        Check(both.Average(r => r.MovesPerS) is > 100f and < 260f, "prism moves per second in the research's range (the expensive part in game)");
        Check(both.Average(r => r.MsPerStep) < 1.0f, "one colony step costs well under a millisecond");

        Console.WriteLine("\nB2. the GAME colony (stomachs, births, the reservation) still mends");
        var g = seeds.Select(s => CutRun(s, BuilderMendRule.Both, game: true)).ToList();
        var gs = Summarise(g);
        Console.WriteLine($"    both+game t50 {gs.t50:F1} s  t90 {gs.t90:F1}  healed {gs.healed}  eaten {g.Average(r => r.Eaten):F1}/run  births {g.Average(r => r.Births):F1}/run"
                          + $"  starved {g.Sum(r => r.Starved)}  rammed {g.Average(r => r.Kills):F1}/run");
        Check(gs.t50 <= 8f, $"game colony t50 {gs.t50:F1} s <= 8");
        Check(g.Sum(r => r.Starved) == 0, "no worker starves in a fed cell (death only to pilots, or an EMPTY stomach)");
        Check(g.Max(r => Math.Abs(r.Audit) / r.Created) < 1e-9, "game colony mass audit 0 (eating is the only exit besides the vessels' ram)");

        Console.WriteLine("\nB3. shielded mass is never a target (test_rules.py: 30% of mass shielded)");
        var sh = CutRun(7, BuilderMendRule.Both, game: true, shielded: 0.3f);
        Check(sh.ShieldedTaken == 0, $"0 shielded prisms changed hands, were carried or built ({sh.ShieldedTaken})");
        var liar = CutRun(7, BuilderMendRule.Both, game: true, shielded: 0.3f, lieAboutShields: true);
        Check(liar.ShieldedTaken > 0, $"negative control CAUGHT: a world that shows every shield as off -> {liar.ShieldedTaken} shielded prisms taken (research 33)");

        Console.WriteLine("\nB4. scar tissue: three cuts along ONE line (research: line mass 65 -> 89 -> 113 with scar, 65 -> 74 -> 86 without)");
        var withScar = seeds.Take(3).Select(s => CutRun(s, BuilderMendRule.Both, false, scar: 3f, sameLine: true)).ToList();
        var noScar = seeds.Take(3).Select(s => CutRun(s, BuilderMendRule.Both, false, scar: 0f, sameLine: true)).ToList();
        string Line(List<CutResult> r) => string.Join(" -> ", Enumerable.Range(0, 3).Select(i => r.Average(x => x.LineMass[i]).ToString("F0")));
        float Growth(List<CutResult> r) => (float)r.Average(x => x.LineMass[2] - x.LineMass[0]);
        Console.WriteLine($"    with scar {Line(withScar)}   without {Line(noScar)}");
        Check(Growth(withScar) > Growth(noScar), $"the fortress thickens where it is attacked: +{Growth(withScar):F0} vs +{Growth(noScar):F0} on the cut line");

        Console.WriteLine("\nB5. steal vs ram (round 2): a stealing vessel's bricks fall loose at the breach and are re-stolen");
        var st = seeds.Take(3).Select(s => CutRun(s, BuilderMendRule.Both, false, raid: "steal")).ToList();
        var stS = Summarise(st);
        Console.WriteLine($"    steal raid t50 {stS.t50:F1} s t90 {stS.t90:F1} (research t90 12.5 vs 70 for ramming)");
        Check(!float.IsNaN(stS.t50), "a wall breached by stealing (changed domain) is treated as breached and re-filled");

        Console.WriteLine("\nB6. starvation is an EMPTY stomach, not a clock (a cell with no mass at all)");
        {
            var ar = new Arena(5);
            var p = new BuilderColonyParams { Containment = ar.R * 0.95f };
            var col = new BuilderColonyCore(ar, p, new Vector3(400, 0, 0), 2, 1, 5);
            var vs = new BuilderVessel[1];
            float firstDeath = -1f, lastDeath = -1f;
            float expect = p.Stomach.Capacity * p.Stomach.FounderFill / p.Stomach.Metabolism;   // the emptiest founder
            float full = p.Stomach.Capacity / p.Stomach.Metabolism;                                // the fullest
            for (int s = 0; s < (int)(full * 1.05f / Dt); s++)
            {
                int before = col.Starved;
                col.Step(Dt, vs, 0); ar.Step(Dt);
                if (col.Starved > before) { if (firstDeath < 0) firstDeath = col.Time; lastDeath = col.Time; }
            }
            Check(firstDeath >= expect - 1f && firstDeath <= expect * 1.06f && lastDeath <= full + 1f && col.AliveCount == 0,
                  $"an unfed worker starves exactly when its stomach empties: deaths from {firstDeath:F0} s to {lastDeath:F0} s"
                  + $" (stomachs {p.Stomach.FounderFill:P0}-100% of {p.Stomach.Capacity} at {p.Stomach.Metabolism}/s = {expect:F0}-{full:F0} s), {col.Starved} starved");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════════ THIEVES

    sealed class ThiefResult
    {
        public int Steals, Recaptured, Hoard, Alive, Births, Starved, Kills, Raided;
        public float StealsPerMin, MaxClaimAge, MaxLaden, MsPerStep, FirstMinuteSteals;
        public double Audit, Created;
        public int ShieldTaken;
    }

    /// <summary>bestiary/run.py one(): a species x a pilot policy x a seed, trails on (15 u / 10 vol), 1.5 min.</summary>
    static ThiefResult ThiefRun(int seed, string policy, bool game, float minutes = 1.5f, bool cold = false, bool bold = false,
                                Func<float, bool> present = null, int founders = -1, float shielded = 0f)
    {
        var ar = new Arena(seed);
        ar.Scatter(3000, shieldedFrac: shielded);
        ar.TrailSpacing = 15f; ar.TrailSpacingVol = 10f; ar.TrailShieldedFrac = shielded;
        var p = new ThiefParams { Cold = cold, Bold = bold };
        if (!game)
        {
            p.Founders = 18; p.Stomach = null; p.Territory = 1e9f;   // the research: 18 at once, no stomach, no leash
        }
        if (founders > 0) p.Founders = founders;
        // the nest is a random environment prism (a plant)
        var nest = ar.Pos[ar.Rng.Range(ar.Count)];
        var core = new ThiefNestCore(ar, p, nest, 3, 2, seed);
        var pilot = ar.AddPilot(new Pilot
        {
            Policy = policy, Name = policy,
            Speed = policy == "hunter" ? 160f : 120f,
        });
        int steps = (int)(minutes * 60 / Dt);
        var vessels = new BuilderVessel[4];
        double ms = 0;
        var res = new ThiefResult();
        bool wasPresent = true;
        for (int s = 0; s < steps; s++)
        {
            float t = s * Dt;
            if (present != null)
            {
                bool now = present(t);
                if (now && !wasPresent) { pilot.Pos = ar.Ball(0.5f * ar.R, 0.8f * ar.R); }
                pilot.Present = now; wasPresent = now;
            }
            ar.Targets.Clear();
            for (int i = 0; i < core.Cap; i++) if (core.Alive[i]) ar.Targets.Add(core.Pos[i]);
            int nv = ar.Vessels(vessels);
            long t0 = Stopwatch.GetTimestamp();
            core.Step(Dt, vessels, nv);
            ms += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            ar.Step(Dt);
            if (s == (int)(60f / Dt) - 1) res.FirstMinuteSteals = core.Steals;
        }
        res.Steals = core.Steals; res.Recaptured = core.Recaptured; res.Hoard = core.HoardCount; res.Alive = core.AliveCount;
        res.Births = core.Births; res.Starved = core.Starved; res.Kills = core.Kills; res.Raided = core.Raided;
        res.StealsPerMin = core.Steals / minutes; res.MaxClaimAge = core.MaxClaimAge; res.MaxLaden = core.MaxLadenSpeed;
        res.MsPerStep = (float)(ms / steps);
        res.Audit = ar.Audit(); res.Created = ar.Scattered + ar.LaidVol;
        for (int i = 0; i < ar.Count; i++) if (ar.ShieldedL[i] && (ar.Dom[i] != 0 || ar.Built.ContainsKey(i))) res.ShieldTaken++;
        return res;
    }

    static void Thieves()
    {
        int[] seeds = { 7, 23, 41, 101, 202, 303 };
        var tp = new ThiefParams();
        Console.WriteLine("\nT1. thieves tail a wanderer and snatch its warm wake (bestiary: 18 thieves, 1.5 min, research 39 steals/min)");
        var w = seeds.Select(s => ThiefRun(s, "wander", game: false)).ToList();
        Console.WriteLine($"    steals/min {w.Average(r => r.StealsPerMin):F1} (per seed {string.Join(", ", w.Select(r => r.StealsPerMin.ToString("F0")))})"
                          + $"  hoard {w.Average(r => r.Hoard):F0}  max claim age {w.Max(r => r.MaxClaimAge):F2} s  max laden speed {w.Max(r => r.MaxLaden):F1} u/s"
                          + $"  cost {w.Average(r => r.MsPerStep):F3} ms/step");
        Check(w.Average(r => r.StealsPerMin) is >= 15f and <= 90f, $"steals per minute in the research's range ({w.Average(r => r.StealsPerMin):F1}; research 39)");
        Check(w.Max(r => r.MaxClaimAge) <= tp.Warm + 1e-3f, $"snatch window: every claimed prism was laid <= {tp.Warm} s ago (worst {w.Max(r => r.MaxClaimAge):F2} s)");
        Check(w.Max(r => r.MaxLaden) <= tp.LadenSpeed * 1.06f + 1f,
              $"a laden thief is slow: max laden speed {w.Max(r => r.MaxLaden):F1} u/s <= half the free speed {tp.FreeSpeed} (+ separation)");
        Check(w.Max(r => Math.Abs(r.Audit) / r.Created) < 1e-9, "nothing is destroyed: mass audit 0");

        Console.WriteLine("\nT2. counterplay: turn back - a hunter knocks laden thieves down and gets its prisms back");
        var h = seeds.Select(s => ThiefRun(s, "hunter", game: false)).ToList();
        Console.WriteLine($"    hunter: steals/min {h.Average(r => r.StealsPerMin):F1}  recaptured {h.Average(r => r.Recaptured):F1}/run (research 1.9)  thieves downed {h.Average(r => r.Kills):F1}/run");
        Check(h.Sum(r => r.Recaptured) > 0, "knocking a laden thief down returns its prism to the pilot it was stolen from");
        Check(h.Sum(r => r.Kills) > 0, "a vessel that turns on the thieves downs them (each a crystal)");
        // a unit check of the recapture: one laden thief, one vessel parked on it
        {
            var ar = new Arena(9); ar.TrailSpacing = 15f;
            var p = new ThiefParams { Founders = 1, MaxThieves = 1, Stomach = null };
            var core = new ThiefNestCore(ar, p, new Vector3(600, 0, 0), 3, 2, 9);
            int prism = ar.Lay(core.Pos[0] + new Vector3(1, 0, 0), 10f, 1, true);
            ar.Rebuild();
            var pilot = ar.AddPilot(new Pilot { Policy = "wander", Domain = 1 });
            pilot.Pos = ar.Pos[prism] + new Vector3(0, 0, 60); pilot.Vel = new Vector3(0, 0, 120);
            var vs = new BuilderVessel[2];
            for (int s = 0; s < 3 && core.Carry[0] < 0; s++) { int n = ar.Vessels(vs); core.Step(Dt, vs, n); ar.Step(Dt); }
            bool laden = core.Carry[0] == prism && ar.Dom[prism] == 3;
            double before = ar.LiveVolume();
            pilot.Pos = core.Pos[0];
            int nv = ar.Vessels(vs, everyoneRams: true);
            core.Step(Dt, vs, nv);
            Check(laden && !core.Alive[0] && ar.Dom[prism] == 1 && ar.AliveL[prism] && Math.Abs(ar.LiveVolume() - before) < 1e-9,
                  $"recapture: the thief snatched it (laden {laden}), the knock-down returned it to its pilot's domain ({ar.Dom[prism]}), volume unchanged");
        }

        Console.WriteLine("\nT3. ablations (research: cold = any trail, never tails -> 10.9 vs 39 steals/min)");
        var cold = seeds.Select(s => ThiefRun(s, "wander", game: false, cold: true)).ToList();
        Console.WriteLine($"    cold steals/min {cold.Average(r => r.StealsPerMin):F1} vs warm {w.Average(r => r.StealsPerMin):F1}");
        Check(cold.Average(r => r.StealsPerMin) < w.Average(r => r.StealsPerMin), "wanting only the WARM wake makes thieves more effective (and legible)");

        Console.WriteLine("\nT4. the opening transient (living cell: thieves seeded at full strength starved in the opening, 3/4 seeds extinct)");
        // (a) an EMPTY opening: no ship for 10 minutes - the founders roost in torpor and nobody starves
        var empty = seeds.Select(s => ThiefRun(s, "wander", game: true, minutes: 10f, present: t => false)).ToList();
        Console.WriteLine($"    no ship for 10 min: alive {empty.Average(r => r.Alive):F1} of {tp.Founders} founders, starved {empty.Sum(r => r.Starved)}");
        Check(empty.Sum(r => r.Starved) == 0 && empty.All(r => r.Alive >= tp.Founders), "an empty opening costs the nest nothing (torpor: no starvation; well-fed founders may still breed from their own stomachs)");
        // (b) no siege: the founders' first minute vs a full research colony's first minute against the same ship
        var full = seeds.Select(s => ThiefRun(s, "wander", game: false, minutes: 1f)).ToList();
        var founded = seeds.Select(s => ThiefRun(s, "wander", game: true, minutes: 8f)).ToList();
        float fullFirst = (float)full.Average(r => r.FirstMinuteSteals), foundedFirst = (float)founded.Average(r => r.FirstMinuteSteals);
        float later = (float)founded.Average(r => (r.Steals - r.FirstMinuteSteals) / 7f);
        Console.WriteLine($"    first-minute steals: founded nest {foundedFirst:F1} vs a full 18 seeded at once {fullFirst:F1};"
                          + $" founded nest later {later:F1}/min, births {founded.Average(r => r.Births):F1}, alive {founded.Average(r => r.Alive):F1}, hoard {founded.Average(r => r.Hoard):F0}");
        Check(foundedFirst <= 0.6f * fullFirst, $"no opening siege: the founded nest's first minute is {foundedFirst / Math.Max(1f, fullFirst):P0} of a full colony's");
        Check(founded.Average(r => r.Births) >= 1f && founded.Average(r => r.Alive) > tp.Founders, "the nest BLOOMS from its own takings (births paid from the stomach)");
        Check(later > foundedFirst, "the steal rate grows with the nest, it does not open at full strength");
        // (c) the long run: 45 minutes, a ship that visits 2 minutes in every 6, NO flora at all (the opening crash
        //     at its worst): thieves live off the larder - their food is the stolen trail
        var longRun = seeds.Take(4).Select(s => ThiefRun(s, "wander", game: true, minutes: 45f, present: t => (t % 360f) < 120f)).ToList();
        Console.WriteLine($"    45 min, a ship 2 min in 6, no flora: alive at the end {string.Join(", ", longRun.Select(r => r.Alive))}"
                          + $" (starved {longRun.Sum(r => r.Starved)}, births {longRun.Sum(r => r.Births)})");
        Check(longRun.All(r => r.Alive > 0), "no seed goes extinct over 45 min (research final: 3 of 4 extinct by ~35 min)");
        Check(longRun.Max(r => Math.Abs(r.Audit) / r.Created) < 1e-9, "eating from the larder is the only exit: mass audit 0");

        Console.WriteLine("\nT5. shielded trail is never snatched");
        var shl = seeds.Take(3).Select(s => ThiefRun(s, "wander", game: true, minutes: 3f, shielded: 0.3f)).ToList();
        Check(shl.Sum(r => r.ShieldTaken) == 0, "0 shielded prisms changed hands");
    }
}
