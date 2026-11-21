// Round 11f (Docs/ECOLOGY_LOD.md): the hierarchical ecology's gates, run against the SHIPPED pure-C# cores
// (Assets/_Scripts/Controller/Environment/FloraAndFauna/Ecology/*.cs). Each research gate is ported with its planted-bug
// negative controls; each control must FAIL and must have been exercised (DISCOVERIES negative 10: a control that
// cannot bite is a fake pass).
//
//   groups: cons (conservation), cont (continuity), consist (consistency), cycles (long macro run), cost,
//           stomach (FaunaStomach: StomachHarness.cs). The swarm's IMacroPopulation gates need the plan assets and run in
//           Tools/Build/swarm_core_harness (mode lod: SwarmLodHarness.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;

public static class EcologyLodHarness
{
    static int s_fail;
    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) s_fail++;
    }

    public static int Main(string[] args)
    {
        var groups = args.Length > 0 ? new HashSet<string>(args[0].Split(',')) : new HashSet<string> { "cons", "cont", "consist", "cycles", "cost", "stomach" };
        var t0 = DateTime.Now;
        if (groups.Contains("stomach")) StomachHarness.Run(Check);
        if (groups.Contains("cons")) Conservation();
        if (groups.Contains("cont")) Continuity();
        if (groups.Contains("consist")) Consistency();
        if (groups.Contains("cycles")) Cycles();
        if (groups.Contains("cost")) Cost();
        Console.WriteLine($"ecology lod harness: {(s_fail == 0 ? "OK" : s_fail + " FAILURES")} ({(DateTime.Now - t0).TotalSeconds:F0} s)");
        return s_fail == 0 ? 0 : 1;
    }

    // ───────────────────────────────────────────────────────────── GATE 1: conservation to the unit

    struct ConsResult { public bool Passed, Exercised, Negative; public double Drift, LodMass; public long LodCount; public string Flows; }

    static ConsResult ConsRun(EcologyBug bug, double T = 300, ulong seed = 5, long nHerb = 30000, long nPred = 2500)
    {
        var P = new EcologyParams { Bug = bug };
        var sim = new EcologyLodSim(P, seed);
        sim.Populate(nHerb, nPred);
        sim.AddWanderer(220); sim.AddWanderer(260);
        double L0 = sim.Ledger(), worst = 0, worstLod = 0; long worstCnt = 0; bool neg = false;
        double m0 = 0, n0 = 0; long c0 = 0;
        sim.LodHook = before =>
        {
            double fm = sim.M.Mass() + sim.A.Mass(), nsum = 0;
            for (int r = 0; r < sim.W.NReg; r++) nsum += sim.W.N[r];
            long cnt = sim.Individuals();
            if (before) { m0 = fm; n0 = nsum; c0 = cnt; return; }
            worstLod = Math.Max(worstLod, Math.Max(Math.Abs((m0 - fm) - (nsum - n0)), Math.Abs(nsum - n0)));
            worstCnt = Math.Max(worstCnt, Math.Abs(c0 - cnt));
        };
        int steps = (int)(T / P.DtMicro);
        for (int i = 0; i < steps; i++)
        {
            sim.Step();
            if (sim.K % 10 != 0) continue;
            worst = Math.Max(worst, Math.Abs(sim.Ledger() - L0) / L0);
            for (int q = 0; q < sim.M.H.N.Length && !neg; q++) neg |= sim.M.H.N[q] < 0 || sim.M.Pr.N[q] < 0;
            for (int q = 0; q < sim.A.Count && !neg; q++) neg |= sim.A.E[q] < -1e-9;
        }
        var M = sim.M; var A = sim.A;
        bool ex = sim.Expands > 0 && sim.Absorbs > 0 && sim.Arrives > 0 && M.Kills > 0 && A.Kills > 0 && M.Births.Sum() > 0 && A.Births.Sum() > 0;
        bool ok = worst < 1e-11 && worstLod < 1e-6 && worstCnt == 0 && !neg;
        return new ConsResult
        {
            Passed = ok, Exercised = ex, Negative = neg, Drift = worst, LodMass = worstLod, LodCount = worstCnt,
            Flows = $"expand {sim.Expands} absorb {sim.Absorbs} arrive {sim.Arrives}; macro births {M.Births[0]}/{M.Births[1]} deaths {M.Deaths[0]}/{M.Deaths[1]} kills {M.Kills}; " +
                    $"micro births {A.Births[0]}/{A.Births[1]} deaths {A.Deaths[0]}/{A.Deaths[1]} kills {A.Kills}; settle {sim.Settle:E1}",
        };
    }

    static void Conservation()
    {
        Console.WriteLine("GATE conservation (1200-u cell, 30k grazers + 2.5k predators, 2 pilots at 220/260 u/s, 300 s)");
        var clean = ConsRun(EcologyBug.None);
        Console.WriteLine($"  clean: drift {clean.Drift:E2} rel, LOD mass {clean.LodMass:E2}, LOD count {clean.LodCount}; {clean.Flows}");
        Check(clean.Passed, "clean run conserves mass to 1e-11 rel and every LOD pass to 1e-6 volume / 0 individuals");
        Check(clean.Exercised, "every flow exercised (expand, absorb, arrive, births and kills in both levels)");
        foreach (var bug in new[] { EcologyBug.AbsorbDropStomach, EcologyBug.BirthFreeBody, EcologyBug.ExpandLosePool })
        {
            var r = ConsRun(bug);
            Console.WriteLine($"  {bug}: drift {r.Drift:E2}, LOD mass {r.LodMass:E2}, count {r.LodCount}, exercised {r.Exercised}; {r.Flows}");
            Check(!r.Passed && r.Exercised, $"negative control {bug} fails the gate (and was exercised)");
        }
    }

    // ───────────────────────────────────────────────────────────── GATE 2: no popping where a pilot can see

    struct ContResult { public long Seen, PopIn, PopOut, Teleport, Expand, Absorb, Arrive; public bool Passed => PopIn == 0 && PopOut == 0 && Teleport == 0; }

    static ContResult ContRun(EcologyBug bug, double T = 150, ulong seed = 9)
    {
        var P = new EcologyParams { Bug = bug };
        var sim = new EcologyLodSim(P, seed);
        sim.Populate(45000, 3000);
        sim.AddWanderer(140); sim.AddWanderer(200);
        // the test's own eyes are never patched by a planted bug
        Func<double, double, double, bool> observe = sim.Visible;
        if (bug == EcologyBug.AbsorbIgnoresVisibility) sim.VisibleForAbsorb = (x, y, z) => false;
        double sprintStep = Math.Max(P.Species[0].Sprint, P.Species[1].Sprint) * P.DtMicro;
        var prev = new Dictionary<long, (double, double, double)>();
        var drawnLast = new HashSet<long>();
        var res = new ContResult();
        int steps = (int)(T / P.DtMicro);
        for (int s = 0; s < steps; s++)
        {
            long k0 = sim.A.Kills, d0 = sim.A.Deaths[0] + sim.A.Deaths[1];
            sim.Step();
            var A = sim.A;
            var cur = new Dictionary<long, (double, double, double)>();
            var ids = new HashSet<long>();
            for (int i = 0; i < A.Count; i++)
            {
                ids.Add(A.Id[i]);
                double x, y, z;
                if (bug == EcologyBug.ExpandNoEmerge) { x = A.Px[i]; y = A.Py[i]; z = A.Pz[i]; }
                else A.Drawn(i, out x, out y, out z);
                if (!observe(x, y, z)) continue;
                long id = A.Id[i];
                cur[id] = (x, y, z);
                if (prev.TryGetValue(id, out var q))
                {
                    double dx = x - q.Item1, dy = y - q.Item2, dz = z - q.Item3;
                    if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > 3 * sprintStep + 1e-6 && A.Emerge[i] >= 1.0) res.Teleport++;
                    continue;
                }
                bool legal = A.Bloom[i] < 1.0 || (A.Emerge[i] < 1.0 && bug != EcologyBug.ExpandNoEmerge) || sim.ArrivedIds.Contains(id);
                if (!(legal || drawnLast.Contains(id))) res.PopIn++;
            }
            res.Seen += cur.Count;
            long gone = 0;
            foreach (var id in prev.Keys) if (!ids.Contains(id)) gone++;
            long explained = (A.Kills - k0) + (A.Deaths[0] + A.Deaths[1] - d0);
            res.PopOut += Math.Max(0, gone - explained);
            prev = cur;
            drawnLast = ids;
        }
        res.Expand = sim.Expands; res.Absorb = sim.Absorbs; res.Arrive = sim.Arrives;
        return res;
    }

    static void Continuity()
    {
        Console.WriteLine("GATE continuity (1200-u cell, 45k + 3k, 2 pilots at 140/200 u/s, 150 s; controls 60 s)");
        var c = ContRun(EcologyBug.None);
        Console.WriteLine($"  clean: {c.Seen} pilot-seen agent-ticks, pop-in {c.PopIn}, pop-out {c.PopOut}, teleport {c.Teleport}; expand {c.Expand} absorb {c.Absorb} arrive {c.Arrive}");
        Check(c.Passed && c.Seen > 10000 && c.Expand > 0 && c.Absorb > 0, "clean: zero pop-in / pop-out / teleport over a non-trivial seen sample");
        foreach (var bug in new[] { EcologyBug.AbsorbIgnoresVisibility, EcologyBug.ExpandNoEmerge })
        {
            var r = ContRun(bug, 60);
            Console.WriteLine($"  {bug}: pop-in {r.PopIn}, pop-out {r.PopOut}, teleport {r.Teleport}");
            Check(!r.Passed, $"negative control {bug} fails the gate");
        }
    }

    // ───────────────────────────────────────────────────────────── GATE 3: macro for T then expand ≈ micro for T

    sealed class Snap { public EcologySpeciesSummary H, Pd; public double Flora; public long Kills; }

    static Snap Arm(string kind, EcologyParams P, ulong runSeed, double T, double R = 600, double dens = 40, double predFrac = 0.06)
    {
        var sim = new EcologyLodSim(P, 1, R);
        int nreg = sim.W.NReg;
        sim.Populate((long)(nreg * dens), (long)(nreg * dens * predFrac));
        sim.Rng.Reseed(10_000 + runSeed);
        int steps = (int)(T / P.DtMicro);
        var all = Enumerable.Repeat(true, nreg).ToArray(); var none = new bool[nreg];
        for (int i = 0; i < steps; i++)
        {
            bool hot = kind == "micro" || (kind == "switch" && i >= steps / 2);
            sim.ForceHot = hot ? all : none;
            sim.Step();
        }
        sim.ForceHot = all;
        sim.LodNow();
        return new Snap { H = sim.Summary(0), Pd = sim.Summary(1), Flora = sim.W.FloraTotal(), Kills = sim.A.Kills + sim.M.Kills };
    }

    static readonly (string key, double tol)[] Tol =
    {
        ("herb_count", 0.10), ("pred_count", 0.10), ("herb_mean_e", 0.10), ("pred_mean_e", 0.10), ("herb_sd_e", 0.25),
        ("pred_sd_e", 0.25), ("herb_phase", 0.08), ("pred_phase", 0.08), ("flora", 0.05), ("kills", 0.30),
    };

    static Dictionary<string, double> Compare(List<Snap> refs, List<Snap> test)
    {
        double M(List<Snap> l, Func<Snap, double> f) => l.Average(f);
        var e = new Dictionary<string, double>();
        void Rel(string k, Func<Snap, double> f) { double a = M(refs, f), b = M(test, f); e[k] = Math.Abs(b - a) / Math.Max(Math.Abs(a), 1e-9); }
        Rel("herb_count", s => s.H.Count); Rel("pred_count", s => s.Pd.Count);
        Rel("herb_mean_e", s => s.H.MeanE); Rel("pred_mean_e", s => s.Pd.MeanE);
        Rel("herb_sd_e", s => s.H.SdE); Rel("pred_sd_e", s => s.Pd.SdE);
        double Tv(Func<Snap, EcologySpeciesSummary> g) =>
            0.5 * (Math.Abs(M(refs, s => g(s).Sated) - M(test, s => g(s).Sated)) + Math.Abs(M(refs, s => g(s).Forage) - M(test, s => g(s).Forage)) +
                   Math.Abs(M(refs, s => g(s).Hungry) - M(test, s => g(s).Hungry)));
        e["herb_phase"] = Tv(s => s.H); e["pred_phase"] = Tv(s => s.Pd);
        Rel("flora", s => s.Flora);
        { double a = M(refs, s => s.Kills), b = M(test, s => s.Kills); e["kills"] = Math.Abs(b - a) / Math.Max(a, 1.0); }
        return e;
    }

    static bool Within(Dictionary<string, double> e) => Tol.All(t => e[t.key] <= t.tol);

    static string Fmt(Dictionary<string, double> e) => string.Join(" ", Tol.Select(t => $"{t.key}={e[t.key]:F3}{(e[t.key] <= t.tol ? "" : "!")}"));

    static void Consistency()
    {
        int seeds = int.TryParse(Environment.GetEnvironmentVariable("ECO_SEEDS"), out int sv) ? sv : 4;
        double T = 300;
        Console.WriteLine($"GATE consistency (600-u world, 40 grazers/region, 6% predators, calibrated regime flora cap 120, T = {T} s, {seeds} seeds)");
        EcologyParams Pm(EcologyBug bug = EcologyBug.None) => new() { FloraCap = 120.0, Bug = bug };
        var micro = new List<Snap>(); var macro = new List<Snap>(); var sw = new List<Snap>();
        for (int s = 0; s < seeds; s++) { micro.Add(Arm("micro", Pm(), (ulong)s, T)); macro.Add(Arm("macro", Pm(), (ulong)s, T)); sw.Add(Arm("switch", Pm(), (ulong)s, T)); }
        Console.WriteLine($"  micro seed-mean: herb {micro.Average(x => x.H.Count):F0} (mean e {micro.Average(x => x.H.MeanE):F2}), pred {micro.Average(x => x.Pd.Count):F0}, kills {micro.Average(x => x.Kills):F0}, flora {micro.Average(x => x.Flora):F0}");
        var ec = Compare(micro, macro);
        Console.WriteLine($"  clean : {Fmt(ec)}");
        Check(Within(ec), "macro-then-expand matches micro within the research tolerances");
        var es = Compare(micro, sw);
        Console.WriteLine($"  switch: {Fmt(es)}");
        Check(Within(es), "switch arm (a pilot arrives half way) matches micro");
        foreach (var bug in new[] { EcologyBug.MacroGrazeX15, EcologyBug.ExpandMeanField, EcologyBug.MacroAttackX2 })
        {
            var arm = new List<Snap>();
            for (int s = 0; s < seeds; s++) arm.Add(Arm("macro", Pm(bug), (ulong)s, T));
            var eb = Compare(micro, arm);
            Console.WriteLine($"  {bug}: {Fmt(eb)}");
            Check(!Within(eb), $"negative control {bug} fails the gate");
        }
    }

    // ───────────────────────────────────────────────────────────── living dynamics: cycles persist (no extinction lock)

    static void Cycles()
    {
        double hours = double.TryParse(Environment.GetEnvironmentVariable("ECO_CYCLE_HOURS"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hv) ? hv : 8;
        Console.WriteLine($"LIVING DYNAMICS (macro only, full 1200-u cell, enriched regime flora cap 240, 45k + 3k, {hours} h)");
        var P = new EcologyParams { FloraCap = 240.0 };
        var sim = new EcologyLodSim(P, 7);
        sim.Populate(45000, 3000);
        var hot = new bool[sim.W.NReg];
        double L0 = sim.Ledger(), drift = 0;
        int steps = (int)(hours * 3600);
        var H = new List<double>(); var Pd = new List<double>();
        int localExt = 0;
        var hadHerb = new bool[sim.W.NReg]; var lostHerb = new bool[sim.W.NReg];
        double ms = 0;
        for (int k = 0; k < steps; k++)
        {
            sim.W.StepFlora(1.0);
            sim.M.Step(1.0, hot);
            ms += sim.M.LastStepMs;
            H.Add(sim.M.H.Total()); Pd.Add(sim.M.Pr.Total());
            if (k % 60 == 0)
            {
                drift = Math.Max(drift, Math.Abs(sim.Ledger() - L0) / L0);
                for (int r = 0; r < sim.W.NReg; r++)
                {
                    bool has = sim.M.H.RegionCount(r) > 0;
                    if (has && lostHerb[r]) { localExt++; lostHerb[r] = false; }
                    if (hadHerb[r] && !has) lostHerb[r] = true;
                    hadHerb[r] = has;
                }
            }
            if (k % 3600 == 0) Console.WriteLine($"    t={k / 3600.0:F0} h  grazers {H[^1]:F0}  predators {Pd[^1]:F0}  flora {sim.W.FloraTotal():F0}");
        }
        var dump = Environment.GetEnvironmentVariable("ECO_CYCLE_DUMP");
        if (!string.IsNullOrEmpty(dump))
            System.IO.File.WriteAllLines(dump, H.Select((v, i) => $"{i},{v},{Pd[i]}"));
        int burn = Math.Min(600, steps / 4);
        var h = H.Skip(burn).ToArray(); var p = Pd.Skip(burn).ToArray();
        double cvH = Sd(h) / h.Average(), cvP = Sd(p) / p.Average();
        // the cycle recurs: predator REVERSALS (a zigzag with 10 % hysteresis - a turn counts once the series has come
        // back 10 % from its extreme) after the first quarter of the run. Crossings of the run's mean undercount a cycle
        // that settles around a level the opening transient pulled the mean away from (the first 8 h run had 1).
        var late = Pd.Skip(steps / 4).ToArray();
        var turns = Reversals(late, 0.10, out var swings, out var at);
        // period: twice the mean spacing of successive reversals (seconds - one sample per macro tick)
        double period = at.Count >= 2 ? 2.0 * (at[^1] - at[0]) / (at.Count - 1) : 0;
        var hl = H.Skip(steps / 2).ToArray(); var pl = Pd.Skip(steps / 2).ToArray();
        double cvHl = Sd(hl) / hl.Average(), cvPl = Sd(pl) / pl.Average();
        string sw = string.Join(" ", swings.Select(x => $"{x:P0}"));
        Console.WriteLine($"  grazers {h.Min():F0}-{h.Max():F0} (cv {cvH:F2}), predators {p.Min():F0}-{p.Max():F0} (cv {cvP:F2}); " +
                          $"regional recolonisations {localExt}; ledger drift {drift:E1}; macro {ms / steps:F2} ms/step");
        Console.WriteLine($"  predator reversals after {hours / 4:F1} h: {turns} (swings {sw}; period ~{period / 3600:F2} h); last-half cv grazers {cvHl:F2} predators {cvPl:F2}");
        Check(h.Min() > 0 && p.Min() > 0, "no extinction: both species alive at every step");
        Check(cvH > 0.2 && cvP > 0.2, "it breathes: coefficient of variation > 0.2 for both species (research cv 0.69 / 0.43)");
        Check(turns >= (hours >= 6 ? 3 : 1) && cvHl > 0.1 && cvPl > 0.05,
              "the predator-prey cycle recurs: >= 3 predator reversals of >= 10 % after the opening quarter, and the last half still breathes");
        Check(drift < 1e-9, "the closed ledger holds over the whole run");
    }

    /// <summary>Turning points of a zigzag with relative hysteresis <paramref name="h"/>; swings are |extreme - previous
    /// extreme| / previous extreme.</summary>
    static int Reversals(double[] x, double h, out List<double> swings, out List<int> at)
    {
        swings = new List<double>(); at = new List<int>();
        if (x.Length < 2) return 0;
        int dir = 0, n = 0; double hi = x[0], lo = x[0], last = x[0];
        for (int i = 1; i < x.Length; i++)
        {
            double v = x[i];
            if (dir == 0)
            {
                hi = Math.Max(hi, v); lo = Math.Min(lo, v);
                if (v < hi * (1 - h)) { dir = -1; last = hi; lo = v; }
                else if (v > lo * (1 + h)) { dir = 1; last = lo; hi = v; }
            }
            else if (dir > 0)
            {
                if (v > hi) hi = v;
                else if (v < hi * (1 - h)) { n++; at.Add(i); swings.Add(Math.Abs(hi - last) / Math.Max(last, 1e-9)); last = hi; dir = -1; lo = v; }
            }
            else
            {
                if (v < lo) lo = v;
                else if (v > lo * (1 + h)) { n++; at.Add(i); swings.Add(Math.Abs(lo - last) / Math.Max(last, 1e-9)); last = lo; dir = 1; hi = v; }
            }
        }
        return n;
    }

    static double Sd(double[] x) { double m = x.Average(); return Math.Sqrt(x.Sum(v => (v - m) * (v - m)) / x.Length); }

    // ───────────────────────────────────────────────────────────── cost per macro tick

    static void Cost()
    {
        Console.WriteLine("COST (full 1200-u cell, ~50k individuals; one pilot expanding around it)");
        var P = new EcologyParams();
        var sim = new EcologyLodSim(P, 3);
        sim.Populate(45000, 4000);
        sim.AddWanderer(150);
        for (int i = 0; i < 600; i++) sim.Step();   // 60 s: warm the JIT and the LOD
        sim.MacroMs = sim.MicroMs = sim.FloraMs = sim.LodMs = 0; sim.MacroSteps = sim.Steps = 0;
        double agents = 0; int samples = 0;
        for (int i = 0; i < 1200; i++) { sim.Step(); if (i % 10 == 0) { agents += sim.A.Count; samples++; } }
        double macro = sim.MacroMs / sim.MacroSteps, micro = sim.MicroMs / sim.Steps, flora = sim.FloraMs / sim.MacroSteps;
        Console.WriteLine($"  regions {sim.W.NReg} x {P.Cohorts} cohorts x 2 species; expanded agents mean {agents / samples:F0}");
        Console.WriteLine($"  macro step (cohorts + inbox + reps) {macro:F2} ms per 1 Hz tick = {macro / 60:F3} ms per 60 fps frame; flora+soil {flora:F2} ms per tick");
        Console.WriteLine($"  micro step {micro:F2} ms per 10 Hz tick = {micro / 6:F3} ms per 60 fps frame (managed, one thread; research C kernel ~390-520 ns/agent)");
        Check(macro < 40.0, "macro tick under 40 ms managed single-thread (research: 2.1 ms C kernel, 31 ms Python); off the main thread in game");
        Check(macro / 60 < 1.0, "macro cost per 60 fps frame under 1 ms amortised");
    }
}
