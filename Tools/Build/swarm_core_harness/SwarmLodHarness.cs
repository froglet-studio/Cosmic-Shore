// Round 11f (Docs/ECOLOGY_LOD.md §5): the swarm as an IMacroPopulation. A grown sort swarm collapses to its macro state,
// drifts for minutes at the 1 Hz macro tick, and re-expands - asserted against its index ledger (the cell's LiveVolume),
// its counts per element x lineage, its stomach, and the drawn path - each with a planted-bug negative control. Then the
// director drives it past an approaching pilot and must never move it where the pilot can see.
//   SWARM_DENSITY=5 bash Tools/Build/swarm_core_harness/run.sh "Assets/_SO_Assets/Swarm Fauna/Plans" lod
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using CosmicShore.Gameplay;

public static class SwarmLodHarness
{
    static int s_fail;
    static void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}"); if (!ok) s_fail++; }

    static SwarmTickSettings Settings() => new()
    {
        Centre = new Vector3(10, -20, 30), UnitScale = 2f, PrismScale = 1f, HeartPrismGap = 0.6f,
        DefaultHalf = new[] { new Vector3(1f, 0.6f, 0.6f), new Vector3(0.9f, 0.8f, 0.7f), new Vector3(2f, 0.3f, 0.3f), new Vector3(0.8f, 0.4f, 0.4f) },
        EngageRadius = 160f, MaxEngaged = 160, MultiDomain = true,
    };

    static SwarmTickJob Make(SwarmPlanData[] plans, int planElement, int seed)
    {
        var p = SortHarness.Game(plans);
        p.LayMax = Math.Max(p.LayMax, 5 * Math.Max(1, plans[1].N / 192));
        var core = SortHarness.GameSwarm(plans, planElement, seed, true, p);
        var job = new SwarmTickJob(core, Settings(), 10f) { SwimTarget = core.SwimTarget };
        job.Prime();
        return job;
    }

    static void Tick(SwarmTickJob j)
    {
        j.Kick(true);
        var spin = new SpinWait();
        while (j.State != SwarmJobState.Done) spin.SpinOnce();
        j.Collect();
        if (j.Error != null) throw j.Error;
    }

    /// <summary>The index as the ledger sees it: entry -> volume, and every call counted (the cell's LiveVolume model).</summary>
    sealed class Sink : ISwarmEntrySink
    {
        public readonly Dictionary<int, float> Vol = new();
        public readonly HashSet<int> Susp = new();
        int _next;
        public long Registers, Releases, Shapes;
        public double LiveVolume => Vol.Where(kv => !Susp.Contains(kv.Key)).Sum(kv => (double)kv.Value);
        public int Register(int slot, Vector3 point, int domainSlot, float volume, bool shielded, float radius) { Registers++; Vol[_next] = volume; return _next++; }
        public void Release(int id) { Releases++; Vol.Remove(id); Susp.Remove(id); }
        public void SetSuspended(int id, bool s) { if (s) Susp.Add(id); else Susp.Remove(id); }
        public void SetShape(int id, float volume, float radius) { Shapes++; Vol[id] = volume; }
        public void SetShielded(int id, bool shielded) { }
        public void SetDomainSlot(int id, int domainSlot) { }
    }

    /// <summary>The swarm's IMacroPopulation as the game glue builds it (SwarmFauna), minus Unity.</summary>
    sealed class Pop : IMacroPopulation
    {
        public readonly SwarmMacroBody Body; public Vector3 Target; public float Speed, Extent;
        public long DriftsWhileSeen; public Func<bool> Seen;
        public Pop(SwarmTickJob j) { Body = new SwarmMacroBody(j); }
        public Vector3 MacroCentre => Body.Job.Anchor;
        public float MacroExtent => Extent;
        public bool IsCollapsed => Body.Collapsed;
        public bool CanCollapse => Body.Job.State == SwarmJobState.Idle;
        public bool NeedsIndividuals => false;
        public MacroPopulationTotals Totals => default;
        public bool Collapse() => Body.TryCollapse();
        public void Expand() => Body.Expand();
        public void MacroTick(float dt)
        {
            var d = Body.MacroTick(dt, Target, Speed, Body.Job.S.UnitScale);
            if (d.LengthSquared() > 0 && Seen != null && Seen()) DriftsWhileSeen++;
        }
    }

    static float MaxStep(SwarmTickJob j)
    {
        float m = 0;
        foreach (var x in j.Instances) if (x.Alive) m = Math.Max(m, Vector3.Distance(x.PrevPos, x.CurPos));
        return m;
    }

    struct Outcome { public bool Same; public string Why; public double V0, V1, VCollapsedMax; public long ChurnWhileCollapsed; public float FormationErr, JumpAtExpand, StepBound; public double StomachErr; public float Drift; }

    static Outcome CollapseDriftExpand(SwarmPlanData[] plans, SwarmMacroBug bug, int seconds = 120)
    {
        var job = Make(plans, 1, 7);
        var led = new SwarmEntryLedger(job.Core.Cap);
        var sink = new Sink(); var realBody = new bool[job.Core.Cap];
        for (int t = 0; t < 900; t++) { Tick(job); led.Sync(job.Instances, realBody, job.IndexPoint, sink); }   // grow to the plan
        float stepBound = 0;
        for (int t = 0; t < 20; t++) { Tick(job); led.Sync(job.Instances, realBody, job.IndexPoint, sink); stepBound = Math.Max(stepBound, MaxStep(job)); }
        var before = SwarmMacroState.Read(job);
        var pos0 = job.Instances.Select(x => x.CurPos).ToArray();
        double v0 = sink.LiveVolume;
        var body = new Pop(job).Body; body.Bug = bug;
        if (!body.TryCollapse()) throw new Exception("collapse refused on an idle job");
        long churn0 = sink.Registers + sink.Releases + sink.Shapes;
        double vMax = 0;
        float speed = 0.35f * 10f * job.S.UnitScale;   // the game's cruise: 0.35 sim units/tick x 10 Hz x UnitScale = 7 u/s
        // a goal elsewhere on the swarm's own shell (the game's Goal is clamped into the band): 40 degrees round the cell
        var rel = job.Anchor - job.S.Centre;
        var target = job.S.Centre + Vector3.Transform(rel, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f));
        float banked = 0;
        for (int s = 0; s < seconds; s++)
        {
            body.MacroTick(1f, target, speed, job.S.UnitScale);
            if (s % 10 == 0) { body.Bank(1, 5f); banked += 5f; }
            led.Sync(job.Instances, realBody, job.IndexPoint, sink);   // the glue's 1 Hz SyncIndex
            vMax = Math.Max(vMax, Math.Abs(sink.LiveVolume - v0));
        }
        long churn = sink.Registers + sink.Releases + sink.Shapes - churn0;
        var drift = job.Anchor - before.Centroid;
        float formErr = 0;
        for (int i = 0; i < pos0.Length; i++)
            if (job.Instances[i].Alive) formErr = Math.Max(formErr, Vector3.Distance(job.Instances[i].CurPos - pos0[i], drift));
        var lastDrawn = job.Instances.Select(x => x.CurPos).ToArray();
        float stomach0 = job.Stomach.Sum();
        body.Expand();
        Tick(job);
        led.Sync(job.Instances, realBody, job.IndexPoint, sink);
        var after = SwarmMacroState.Read(job);
        bool same = before.SameBody(after, out string why);
        float jump = 0;
        for (int i = 0; i < lastDrawn.Length; i++)
            if (job.Instances[i].Alive) jump = Math.Max(jump, Vector3.Distance(job.Instances[i].PrevPos, lastDrawn[i]));
        jump = Math.Max(jump, MaxStep(job));
        // the plan is full, so nothing is laid: the stomach after the first tick is exactly what was banked plus held
        double stErr = Math.Abs(job.Stomach.Sum() - (stomach0 + banked));
        return new Outcome
        {
            Same = same, Why = why, V0 = v0, V1 = sink.LiveVolume, VCollapsedMax = vMax, ChurnWhileCollapsed = churn,
            FormationErr = formErr, JumpAtExpand = jump, StepBound = stepBound * 1.5f + 0.5f, StomachErr = stErr, Drift = drift.Length(),
        };
    }

    public static int Run(SwarmPlanData[] plans)
    {
        Console.WriteLine("ROUND 11f: the swarm as an IMacroPopulation (whale plan, sort core)");
        var c = CollapseDriftExpand(plans, SwarmMacroBug.None);
        Console.WriteLine($"  clean: drifted {c.Drift:F0} u in 120 macro ticks; LiveVolume {c.V0:F1} -> {c.V1:F1} (max deviation while collapsed {c.VCollapsedMax:E1}); " +
                          $"index churn while collapsed {c.ChurnWhileCollapsed}; formation error {c.FormationErr:E1} u; first-tick step {c.JumpAtExpand:F2} u (bound {c.StepBound:F2}); stomach error {c.StomachErr:E1}");
        Check(c.Same, "counts per element x lineage and Σbody are identical after collapse -> 2 min macro -> expand" + (c.Same ? "" : $" ({c.Why})"));
        Check(Math.Abs(c.V1 - c.V0) < 1e-3 && c.VCollapsedMax < 1e-3, "the cell's LiveVolume (its index entries, Cell.BindVirtualMass) reads the same collapsed and expanded");
        Check(c.ChurnWhileCollapsed == 0, "no entry is registered, released or re-shaped while collapsed (the rigid drift only moves points)");
        Check(c.FormationErr < 1e-2f && c.Drift > 200, "the formation drifts rigidly (every member moved by exactly the anchor's drift)");
        Check(c.JumpAtExpand <= c.StepBound, "no pop on expansion: the first micro step after expanding is an ordinary step from where it was drawn");
        Check(c.StomachErr < 1e-2, "food banked while collapsed is in the stomach after expansion, exactly");
        var b1 = CollapseDriftExpand(plans, SwarmMacroBug.TranslateCoreOnly);
        Console.WriteLine($"  TranslateCoreOnly: first-tick step {b1.JumpAtExpand:F0} u (bound {b1.StepBound:F2})");
        Check(b1.JumpAtExpand > b1.StepBound, "negative control: moving the core but not the drawn frame teleports the body on expansion");
        var b2 = CollapseDriftExpand(plans, SwarmMacroBug.ReseedOnExpand);
        Console.WriteLine($"  ReseedOnExpand (re-grow from the counts alone): same counts and Σbody {b2.Same}{(b2.Same ? "" : $" ({b2.Why})")}; LiveVolume {b2.V0:F1} -> {b2.V1:F1}; " +
                          $"members displaced up to {b2.JumpAtExpand:F0} u from where they were drawn");
        Check(b2.JumpAtExpand > b2.StepBound, "negative control: re-growing from the counts throws the drawn body away (members leave where they were drawn)");
        Console.WriteLine($"  (finding: re-growing from counts alone keeps counts and Σbody {(b2.Same && Math.Abs(b2.V1 - b2.V0) < 1e-3 ? "EXACT" : "inexact")} - a peer without the formation can rebuild it from the macro state, but it re-grows from a knot)");

        Director(plans);
        Console.WriteLine($"swarm lod: {(s_fail == 0 ? "OK" : s_fail + " FAILURES")}");
        return s_fail;
    }

    /// <summary>The director over the swarm: a pilot flies in at 260 u/s, past, and away. The swarm must collapse while
    /// far, expand before it is seen, and the macro tick must never move it while a pilot sees it.</summary>
    static void Director(SwarmPlanData[] plans)
    {
        foreach (bool guard in new[] { true, false })
        {
            var job = Make(plans, 1, 11);
            for (int t = 0; t < 300; t++) Tick(job);
            var P = new EcologyParams();
            var dir = new EcologyLodDirector(P);
            var pop = new Pop(job) { Speed = 40f, Extent = plans[1].Radius * 2f * 1.2f + 20f };
            pop.Target = job.Anchor + new Vector3(0, 0, 3000);
            dir.Register(pop);
            var pilot = new EcologyPilot { X = job.Anchor.X - 2600, Y = job.Anchor.Y + 40, Z = job.Anchor.Z, Speed = 260 };
            pilot.Vx = 260;
            pop.Seen = () => dir.SeenByAnyPilot(pop);
            var arr = new EcologyPilot[1];
            int frames = 0, collapsedFrames = 0, seenCollapsedFrames = 0;
            double acc = 0, microAcc = 0;
            for (int f = 0; f < 60 * 25; f++)   // 25 s at 60 fps
            {
                const float dt = 1f / 60f;
                pilot.X += pilot.Vx * dt;
                arr[0] = pilot; dir.SetPilots(arr);
                if (guard) dir.Guard();
                acc += dt; microAcc += dt;
                if (acc >= 1.0) { acc -= 1.0; dir.Tick(1f); }
                if (!pop.IsCollapsed && microAcc >= 0.1) { microAcc = 0; Tick(job); }
                frames++;
                if (pop.IsCollapsed) { collapsedFrames++; if (dir.SeenByAnyPilot(pop)) seenCollapsedFrames++; }
            }
            Console.WriteLine($"  director{(guard ? "" : " WITHOUT the per-frame guard")}: {dir.Collapses} collapses, {dir.Expands} expands, {dir.MacroTicks} macro ticks; " +
                              $"collapsed {collapsedFrames}/{frames} frames; seen while collapsed {seenCollapsedFrames} frames; drifts while seen {pop.DriftsWhileSeen}");
            if (guard)
            {
                Check(dir.Collapses >= 1 && dir.Expands >= 1 && dir.MacroTicks > 3, "the swarm collapses far from the pilot and expands as it approaches");
                Check(pop.DriftsWhileSeen == 0 && seenCollapsedFrames == 0, "never seen while collapsed, never drifted while seen");
            }
            else Check(seenCollapsedFrames > 0, "negative control: without the per-frame guard a 260 u/s pilot sees a collapsed swarm");
        }
    }
}
