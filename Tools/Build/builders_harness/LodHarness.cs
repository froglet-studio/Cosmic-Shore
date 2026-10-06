// Round 11f-2 (Docs/ECOLOGY_LOD.md §6.2): the builder colonies - fortress, thief nest, wearers - as IMacroPopulations.
// Group "lod". A collapsed colony ROOSTS: members hold still, every stomach burns at the species' torpor once a second, the
// structure and every carried / worn prism stay exactly where they are, nobody is born or dies; it expands before its
// emptiest stomach runs out, and steps on from where it was drawn. Planted-bug negative controls for each gate.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class Program
{
    /// <summary>The three cores behind one face (what BuilderColonyFauna switches over).</summary>
    sealed class Colony
    {
        public string Name;
        public Action<float, BuilderVessel[], int> Step;
        public Action<float> Roost;
        public Func<bool> CanRoost;
        public Func<float> SecondsLeft;
        public Func<double> Metabolised;
        public Func<int> Starved;
        public Action<RoostBug> SetBug;
        public bool[] Alive; public float[] Stomach; public Vector3[] Pos;
        public float Torpor;
        public Arena World;
        public Action Targets = () => { };
        public Action<bool> SetWindDown = _ => { };
    }

    static Colony MakeColony(string species, int seed)
    {
        if (species == "fortress")
        {
            var ar = new Arena(seed);
            ar.Scatter(600);
            var p = new BuilderColonyParams { Containment = ar.R * 0.95f };
            var c = new BuilderColonyCore(ar, p, ar.Ball(0.3f * ar.R, 0.5f * ar.R), 2, 1, seed);
            return new Colony
            {
                Name = species, World = ar, Step = c.Step, Roost = c.Roost, CanRoost = () => c.CanRoost,
                SecondsLeft = () => c.RoostSecondsLeft, Metabolised = () => c.Metabolised, Starved = () => c.Starved,
                SetBug = b => c.RoostBug = b, Alive = c.Alive, Stomach = c.Stomach, Pos = c.Pos, Torpor = p.Stomach.Torpor,
                SetWindDown = w => c.WindDown = w,
            };
        }
        if (species == "thieves")
        {
            var ar = new Arena(seed);
            ar.Scatter(1500);
            ar.TrailSpacing = 15f; ar.TrailSpacingVol = 10f;
            var p = new ThiefParams();
            var c = new ThiefNestCore(ar, p, ar.Pos[ar.Rng.Range(ar.Count)], 3, 2, seed);
            var col = new Colony
            {
                Name = species, World = ar, Step = c.Step, Roost = c.Roost, CanRoost = () => c.CanRoost,
                SecondsLeft = () => c.RoostSecondsLeft, Metabolised = () => c.Metabolised, Starved = () => c.Starved,
                SetBug = b => c.RoostBug = b, Alive = c.Alive, Stomach = c.Stomach, Pos = c.Pos, Torpor = p.Stomach.Torpor,
            };
            col.Targets = () => { ar.Targets.Clear(); for (int i = 0; i < c.Cap; i++) if (c.Alive[i]) ar.Targets.Add(c.Pos[i]); };
            ar.AddPilot(new Pilot { Policy = "wander", Name = "wander", Speed = 120f });
            return col;
        }
        {
            var ar = new WearArena(seed);
            ar.Scatter(1500);
            var p = new WearerParams { Containment = ar.R * 0.95f };
            var c = new WearerCore(ar, p, ar.Ball(0.3f * ar.R, 0.6f * ar.R), 4, 3, seed);
            var col = new Colony
            {
                Name = species, World = ar, Step = c.Step, Roost = c.Roost, CanRoost = () => c.CanRoost,
                SecondsLeft = () => c.RoostSecondsLeft, Metabolised = () => c.Metabolised, Starved = () => c.Starved,
                SetBug = b => c.RoostBug = b, Alive = c.Alive, Stomach = c.Stomach, Pos = c.Pos, Torpor = p.Stomach.Torpor,
            };
            col.Targets = () => { ar.Targets.Clear(); for (int k = 0; k < c.Cap; k++) if (c.IsLeader(k)) ar.Targets.Add(c.Pos[k]); };
            ar.AddPilot(new Pilot { Policy = "wander", Name = "wander", TrailEvery = 0.25f, TrailVol = 6f });
            return col;
        }
    }

    sealed class RoostResult
    {
        public bool Collapsed, Thawed;
        public int Roosts, AliveBefore, AliveAfter, StarvedDuring, PrismsMoved, MembersMoved;
        public double BurnErr, LedgerErr, Audit0, AuditMax, MinLeft = double.MaxValue;
        public float StepBound, FirstStep;
        public double CanRoostFrac;
    }

    /// <summary>Two minutes with a pilot, then the pilot leaves; once the colony may roost it collapses and roosts at 1 Hz
    /// (expanding below <paramref name="thaw"/> s of torpor unless <paramref name="ignoreThaw"/>), for up to
    /// <paramref name="roostS"/> s; then steps on.</summary>
    static RoostResult RunRoost(string species, int seed, RoostBug bug = RoostBug.None, float thaw = 20f, bool ignoreThaw = false,
                                float roostS = 900f, float stomachScale = 1f)
    {
        var c = MakeColony(species, seed);
        var ar = c.World;
        var vessels = new BuilderVessel[4];
        var res = new RoostResult();
        float maxStep = 0f;
        var last = (Vector3[])c.Pos.Clone();
        int canRoostTicks = 0, ticks = 0;
        void StepOnce()
        {
            c.Targets();
            int nv = ar.Vessels(vessels);
            Array.Copy(c.Pos, last, last.Length);
            var alive = (bool[])c.Alive.Clone();
            c.Step(Dt, vessels, nv);
            ar.Step(Dt);
            for (int k = 0; k < c.Pos.Length; k++)
                if (alive[k] && c.Alive[k]) maxStep = MathF.Max(maxStep, Vector3.Distance(last[k], c.Pos[k]));
        }
        for (int s = 0; s < 1200; s++) StepOnce();
        foreach (var p in ar.Pilots) p.Present = false;   // the pilot leaves the cell
        c.SetWindDown(true);   // the glue's wind-down: the director keeps asking to collapse while the cell is empty
        for (int s = 0; s < 1800; s++)
        {
            StepOnce();
            ticks++;
            if (c.CanRoost()) canRoostTicks++;
            if (s > 300 && c.CanRoost() && c.SecondsLeft() > 2f * thaw) break;
        }
        res.CanRoostFrac = (double)canRoostTicks / Math.Max(1, ticks);
        c.SetWindDown(false);
        if (!c.CanRoost()) return res;
        res.StepBound = maxStep * 1.05f + 1e-3f;
        if (stomachScale != 1f) for (int k = 0; k < c.Stomach.Length; k++) c.Stomach[k] *= stomachScale;

        // collapse: snapshot what must not change
        res.Collapsed = true;
        c.SetBug(bug);
        res.AliveBefore = c.Alive.Count(a => a);
        var pos0 = (Vector3[])c.Pos.Clone();
        var alive0 = (bool[])c.Alive.Clone();
        var wpos = ar.Pos.ToArray(); var walive = ar.AliveL.ToArray(); var wdom = ar.Dom.ToArray();
        res.Audit0 = ar.Audit();
        int starved0 = c.Starved();
        for (int t = 0; t < (int)roostS; t++)
        {
            var st = (float[])c.Stomach.Clone();
            double met = c.Metabolised(), sum0 = st.Where((v, k) => c.Alive[k]).Sum(v => (double)v);
            c.Roost(1f);
            res.Roosts++;
            double sum1 = 0;
            for (int k = 0; k < st.Length; k++)
            {
                if (!alive0[k]) continue;
                if (!c.Alive[k]) continue;
                sum1 += c.Stomach[k];
                double want = st[k] - Math.Min(st[k], c.Torpor * 1f);
                res.BurnErr = Math.Max(res.BurnErr, Math.Abs(c.Stomach[k] - want));
            }
            res.LedgerErr = Math.Max(res.LedgerErr, Math.Abs((sum0 - sum1) - (c.Metabolised() - met)));
            res.AuditMax = Math.Max(res.AuditMax, Math.Abs(ar.Audit() - res.Audit0));
            double left = c.SecondsLeft();
            res.MinLeft = Math.Min(res.MinLeft, left);
            if (!ignoreThaw && left < thaw) { res.Thawed = true; break; }
        }
        res.AliveAfter = c.Alive.Count(a => a);
        res.StarvedDuring = c.Starved() - starved0;
        for (int k = 0; k < c.Pos.Length; k++) if (alive0[k] && c.Pos[k] != pos0[k]) res.MembersMoved++;
        for (int i = 0; i < wpos.Length; i++)
            if (ar.Pos[i] != wpos[i] || ar.AliveL[i] != walive[i] || ar.Dom[i] != wdom[i]) res.PrismsMoved++;
        // expand: the next steps are ordinary steps from where the members were drawn
        c.SetBug(RoostBug.None);
        maxStep = 0f;
        for (int s = 0; s < 5; s++) StepOnce();
        res.FirstStep = maxStep;
        return res;
    }

    static void Lod()
    {
        Console.WriteLine("\nL. the ecology LOD: a colony far from every pilot ROOSTS (Docs/ECOLOGY_LOD.md §6.2)");
        foreach (var sp in new[] { "fortress", "thieves", "wearers" })
        {
            var r = RunRoost(sp, 31);
            Console.WriteLine($"    {sp}: may roost {r.CanRoostFrac:P0} of ticks once the pilot left; collapsed {r.Collapsed}; {r.Roosts} roost ticks; " +
                              $"alive {r.AliveBefore} -> {r.AliveAfter}; members moved {r.MembersMoved}; world prisms changed {r.PrismsMoved}; " +
                              $"burn err {r.BurnErr:E1}; ledger err {r.LedgerErr:E1}; audit drift {r.AuditMax:E1}; " +
                              $"min torpor left {r.MinLeft:F0} s; first steps {r.FirstStep:F2} u (the species' own max step {r.StepBound:F2})");
            Check(r.Collapsed && r.Roosts >= 60, $"{sp}: the colony comes to rest (nothing carried, claimed or hunting) and collapses once unwatched");
            Check(r.AliveAfter == r.AliveBefore && r.MembersMoved == 0 && r.PrismsMoved == 0 && r.AuditMax == 0,
                  $"{sp}: exact while roosting - every member alive and still, every structure / carried / worn prism untouched, the world audit unchanged");
            Check(r.BurnErr < 1e-5 && r.LedgerErr < 1e-3, $"{sp}: one rule set - each stomach burns exactly the torpor, booked as Metabolised");
            Check(r.FirstStep <= r.StepBound, $"{sp}: no pop on expansion - the first steps are no larger than the species' own steps");
            var hungry = RunRoost(sp, 31, stomachScale: 0.25f, roostS: 3600f);
            Console.WriteLine($"    {sp}, quarter-full stomachs: expanded after {hungry.Roosts} roost ticks (thawed {hungry.Thawed}, min torpor left {hungry.MinLeft:F0} s); " +
                              $"starved while roosting {hungry.StarvedDuring}");
            Check(hungry.Thawed && hungry.MinLeft > 0 && hungry.StarvedDuring == 0 && hungry.AliveAfter == hungry.AliveBefore,
                  $"{sp}: it expands before its emptiest stomach runs out - starvation is an individual's death, never the roost's");
        }
        var noBurn = RunRoost("fortress", 31, RoostBug.NoBurn);
        Console.WriteLine($"    NoBurn: burn err {noBurn.BurnErr:E1}");
        Check(noBurn.Collapsed && noBurn.BurnErr > 1e-3, "negative control: a roost that burns nothing fails the one-rule-set check");
        var killer = RunRoost("wearers", 31, RoostBug.KillOnEmpty, ignoreThaw: true, roostS: 3600f, stomachScale: 0.25f);
        Console.WriteLine($"    KillOnEmpty, no thaw rule: alive {killer.AliveBefore} -> {killer.AliveAfter} while roosting");
        Check(killer.Collapsed && killer.AliveAfter < killer.AliveBefore, "negative control: a roost that kills an emptied member fails the exact-count check");
    }
}
