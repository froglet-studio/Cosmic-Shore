// Round 11f-2 (Docs/ECOLOGY_LOD.md §6.1): the substrate's populations as IMacroPopulations - the FREEZE route.
// Group "lod". A frozen population is skipped in every pass of the shared core except metabolism; its agents hold still
// with exactly their stock (so its index entries and the cell's LiveVolume do not move); it thaws before its hungriest
// agent's reserve runs out, and when one of its agents is hunted. Each gate has a planted-bug negative control.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class SubstrateHarness
{
    /// <summary>A cell's substrate driven through its tick job (inline), with the owner's half modelled: real food for the
    /// hungry, a starving agent killed as an individual, a caught prey killed and fed to its hunter.</summary>
    sealed class LodWorld
    {
        public readonly SubstrateCore Core;
        public readonly SubstrateTickJob Job;
        public readonly int Locust, Pack, Lurker;
        readonly List<Vector3> _foodPos = new();
        readonly List<float> _foodVol = new();
        readonly List<bool> _foodAlive = new();
        readonly Random _rng;
        /// <summary>Thaws because a frozen population's agent was hunted (clause 3: a death needs individuals).</summary>
        public int HuntThaws;
        public readonly List<SubstrateEvent> Log = new();

        public LodWorld(int seed)
        {
            _rng = new Random(seed);
            Core = new SubstrateCore(900, R, Dt, 40, seed);
            Locust = Core.AddPopulation(SubstrateResearch.GameLocust(), 2);
            Pack = Core.AddPopulation(SubstrateResearch.GamePack(), 3);
            Lurker = Core.AddPopulation(SubstrateResearch.GameLurker(), 1, 100f, 1100f);
            for (int k = 0; k < 900; k++)
            {
                _foodPos.Add(Ball(_rng, 0.3f * R, 0.9f * R)); _foodVol.Add(8f + 32f * (float)_rng.NextDouble()); _foodAlive.Add(true);
            }
            Core.Seed(Locust, 160, Ball(_rng, 400f, 700f), 120f);
            Core.Seed(Pack, 8, Ball(_rng, 400f, 700f), 50f);
            Core.SeedAt(Lurker, Enumerable.Range(0, 12).Select(_ => _foodPos[_rng.Next(_foodPos.Count)]).ToArray());
            Job = new SubstrateTickJob(Core, new SubstrateTickSettings { EngageRadius = 160f, MaxEngaged = 24 });
            Job.Prime();
        }

        /// <summary>Populations this tick queued a meal for (applied at the next kick - "pending" for the freeze rule).</summary>
        public readonly HashSet<int> FedPops = new();

        /// <summary>One tick through the job (inline) and the owner's half of it. Returns false on a worker error.</summary>
        public bool Tick()
        {
            if (Core.Tick % 10 == 0)
            {
                int n = 0;
                if (Job.Food.Length < _foodPos.Count) Job.Food = new SubstrateFood[_foodPos.Count];
                for (int k = 0; k < _foodPos.Count; k++)
                    if (_foodAlive[k]) Job.Food[n++] = new SubstrateFood { Pos = _foodPos[k], Volume = _foodVol[k] };
                Job.FoodCount = n;
            }
            FedPops.Clear();
            Job.Kick(true);
            Job.Collect();
            if (Job.Error != null) return false;
            Log.AddRange(Job.Events);
            foreach (int i in Job.EatRequests)
            {
                if (!Core.Alive[i]) continue;
                var P = Core.Pops[Core.PopOf[i]].P;
                int best = -1; float bd = P.EatR;
                for (int k = 0; k < _foodPos.Count; k++)
                {
                    if (!_foodAlive[k]) continue;
                    float d = Vector3.Distance(_foodPos[k], Core.Pos[i]);
                    if (d < bd) { bd = d; best = k; }
                }
                if (best < 0) continue;
                _foodAlive[best] = false;
                Job.QueueFeed(i, _foodVol[best]);
                FedPops.Add(Core.PopOf[i]);
            }
            foreach (var r in Job.PreyRequests)
            {
                if (!Core.Alive[r.Prey] || !Core.Alive[r.Predator]) continue;
                var prey = Core.Pops[Core.PopOf[r.Prey]];
                if (prey.Frozen) { prey.Frozen = false; HuntThaws++; }   // the owner's MaterialiseForHit thaws it
                Job.QueueFeed(r.Predator, Core.Stock[r.Prey]);
                FedPops.Add(Core.PopOf[r.Predator]);
                Job.QueueKill(r.Prey);
            }
            foreach (var e in Job.Events)
                if (e.Kind == SubstrateEventKind.Starving) Job.QueueKill(e.Index);   // dies as an individual
            return true;
        }
    }

    sealed class LodRun
    {
        public long FrozenTicks, Births, Starvings, MovedFrozen;
        public int AliveBefore, AliveAfter, HuntThaws, ReserveThaws, Freezes;
        public double VolBefore, VolMaxDev, StockMaxDev, HungerErr, CoreLedger, MinReserveFrozen = double.MaxValue;
        public float FirstStep, GlideErr, StepBound;
        public bool Exercised;
    }

    /// <summary>
    /// Warm 30 s, then freeze one population (the pack by default) (the thaw rule of the glue: freeze only with twice the margin of reserve, thaw
    /// below it) for up to 120 s while the pack and lurkers run, then thaw. <paramref name="deactivate"/> is the naive
    /// collapse (switch the block off) the gate must reject.
    /// </summary>
    static LodRun RunLod(int seed, SubstrateFreezeBug bug, bool deactivate, string which = "pack")
    {
        const float thaw = 5f;
        var w = new LodWorld(seed);
        var c = w.Core; var job = w.Job;
        c.FreezeBug = bug;
        var res = new LodRun();
        for (int t = 0; t < 300; t++) w.Tick();
        int q = which == "locust" ? w.Locust : which == "lurker" ? w.Lurker : w.Pack;
        var pop = c.Pops[q];

        // the freeze lands between ticks (the host's ApplyLod), when nothing is pending for the population
        int guard = 0;
        // a meal queued last tick is pending too (it lands at the next kick): round 11-10's food heading brings the pack to
        // its food, so a freeze on the tick after a bite was the common case and booked that bite as frozen-stock drift
        while ((c.ReserveSeconds(q) <= 2f * thaw || w.FedPops.Contains(q)) && guard++ < 600) w.Tick();
        if (deactivate) pop.Active = false; else pop.Frozen = true;
        res.Freezes++;
        res.AliveBefore = job.PopAlive[q];
        res.VolBefore = job.PopVolume[q];
        var stock0 = new float[pop.Cap]; var pos0 = new Vector3[pop.Cap]; var hunger0 = new float[pop.Cap];
        var alive0 = new bool[pop.Cap];
        for (int k = 0; k < pop.Cap; k++)
        {
            int i = pop.Start + k;
            stock0[k] = c.Stock[i]; pos0[k] = c.Pos[i]; hunger0[k] = c.Hunger[i]; alive0[k] = c.Alive[i];
        }
        int logFrom = w.Log.Count;
        var drift = new Vector3(0.02f, 0f, 0f);   // a rigid drift per tick (a frozen body moved as one, between ticks; the caller keeps it inside the membrane)
        for (int t = 0; t < 1200; t++)
        {
            if (!(deactivate ? !pop.Active : pop.Frozen)) break;
            if (!deactivate) { c.Translate(q, drift); for (int k = 0; k < pop.Cap; k++) pos0[k] += drift; }   // the same float adds
            if (!w.Tick()) break;
            res.FrozenTicks++;
            // LiveVolume while frozen: the population's published volume (what its index entries sum to)
            res.VolMaxDev = Math.Max(res.VolMaxDev, Math.Abs(job.PopVolume[q] - res.VolBefore));
            float glide = 0f;
            for (int k = 0; k < pop.Cap; k++)
            {
                int i = pop.Start + k;
                if (!alive0[k]) continue;
                if (!c.Alive[i] || !job.Instances[i].Alive) { res.StockMaxDev = Math.Max(res.StockMaxDev, stock0[k]); continue; }
                res.StockMaxDev = Math.Max(res.StockMaxDev, Math.Abs(c.Stock[i] - stock0[k]));
                // still, except the rigid drift: the frame glides by exactly the drift, and the agent sits where it froze
                var moved = c.Pos[i] - pos0[k];
                if (moved.Length() > 1e-2f) res.MovedFrozen++;
                if (res.FrozenTicks > 1) glide = MathF.Max(glide, ((job.Instances[i].CurPos - job.Instances[i].PrevPos) - drift).Length());
                // one rule set: hunger rose by exactly Metabolism * Dt per tick
                double want = hunger0[k] + pop.P.Metabolism * Dt * res.FrozenTicks;
                res.HungerErr = Math.Max(res.HungerErr, Math.Abs(c.Hunger[i] - want) / Math.Max(1.0, want));
            }
            res.GlideErr = MathF.Max(res.GlideErr, glide);
            double left = c.ReserveSeconds(q);
            if (!deactivate) res.MinReserveFrozen = Math.Min(res.MinReserveFrozen, left);
            if (!deactivate && left < thaw) { pop.Frozen = false; res.ReserveThaws++; }   // thaw before the reserve runs out
        }
        res.HuntThaws = w.HuntThaws;
        for (int g = logFrom; g < w.Log.Count; g++)
        {
            var e = w.Log[g];
            if (c.PopOf[e.Index] != q) continue;
            if (e.Kind == SubstrateEventKind.Born) res.Births++;
            if (e.Kind == SubstrateEventKind.Starving) res.Starvings++;
        }
        // thaw (if the run did not) and step on: the first steps are ordinary steps from where the agents were drawn
        if (deactivate) pop.Active = true; else pop.Frozen = false;
        res.AliveAfter = job.PopAlive[q];
        var drawn = new Vector3[pop.Cap];
        for (int k = 0; k < pop.Cap; k++) drawn[k] = job.Instances[pop.Start + k].CurPos;
        var P = pop.P;
        res.StepBound = (MathF.Max(P.Solitary.Speed, P.Gregarious.Speed) * 1.5f + 30f) * Dt;
        for (int t = 0; t < 3; t++)
        {
            w.Tick();
            for (int k = 0; k < pop.Cap; k++)
            {
                int i = pop.Start + k;
                if (!job.Instances[i].Alive || job.BornThisTick(i)) continue;
                res.FirstStep = MathF.Max(res.FirstStep, Vector3.Distance(job.Instances[i].PrevPos, t == 0 ? drawn[k] : job.Instances[i].PrevPos));
                res.FirstStep = MathF.Max(res.FirstStep, Vector3.Distance(job.Instances[i].CurPos, job.Instances[i].PrevPos));
            }
        }
        res.CoreLedger = Math.Abs(c.MassIn - c.MassOut - c.MassHeld()) / Math.Max(1.0, c.MassIn);
        res.Exercised = res.FrozenTicks >= 50;
        return res;
    }

    /// <summary>The glue's IMacroPopulation over one core population (SubstrateFauna's rules, Unity-free), for the director.</summary>
    sealed class SubstratePopModel : IMacroPopulation
    {
        readonly LodWorld _w; readonly int _q; readonly float _thaw;
        public bool Collapsed;
        public SubstratePopModel(LodWorld w, int q, float thaw) { _w = w; _q = q; _thaw = thaw; }
        SubstratePopulation Pop => _w.Core.Pops[_q];
        public Vector3 MacroCentre
        {
            get
            {
                var c = Vector3.Zero; int n = 0;
                for (int i = Pop.Start; i < Pop.Start + Pop.Cap; i++) if (_w.Job.Instances[i].Alive) { c += _w.Job.Instances[i].CurPos; n++; }
                return n > 0 ? c / n : Vector3.Zero;
            }
        }
        public float MacroExtent
        {
            get
            {
                var c = MacroCentre; float e = 0f;
                for (int i = Pop.Start; i < Pop.Start + Pop.Cap; i++) if (_w.Job.Instances[i].Alive) e = MathF.Max(e, Vector3.Distance(c, _w.Job.Instances[i].CurPos));
                return e + 40f;
            }
        }
        public bool IsCollapsed => Collapsed;
        public bool CanCollapse => !Collapsed && _w.Job.EngagedCount[_q] == 0 && _w.Core.ReserveSeconds(_q) > 2f * _thaw;
        public bool NeedsIndividuals => Collapsed && (_w.Core.ReserveSeconds(_q) < _thaw || !Pop.Frozen);
        public MacroPopulationTotals Totals => new MacroPopulationTotals
        { Individuals = _w.Job.PopAlive[_q], BodyVolume = _w.Job.PopVolume[_q], Stomach = _w.Core.ReserveVolume(_q) };
        public bool Collapse() { if (!CanCollapse) return false; Collapsed = true; Pop.Frozen = true; return true; }
        public void Expand() { Collapsed = false; Pop.Frozen = false; }
        public void MacroTick(float dt) { }
    }

    static void Lod()
    {
        Console.WriteLine("\nL. the ecology LOD: a population FROZEN in the shared core (Docs/ECOLOGY_LOD.md §6.1)");
        var r = RunLod(41, SubstrateFreezeBug.None, false);
        var lr = RunLod(41, SubstrateFreezeBug.None, false, "lurker");
        Console.WriteLine($"    lurkers: {lr.FrozenTicks} frozen ticks, alive {lr.AliveBefore} -> {lr.AliveAfter}, volume dev {lr.VolMaxDev:E1}, stock dev {lr.StockMaxDev:E1}, " +
                          $"hunger err {lr.HungerErr:E1}, first step {lr.FirstStep:F2} u (bound {lr.StepBound:F2})");
        Check(lr.Exercised && lr.AliveAfter == lr.AliveBefore && lr.VolMaxDev == 0 && lr.StockMaxDev == 0 && lr.MovedFrozen == 0 &&
              lr.HungerErr < 1e-5 && lr.FirstStep <= lr.StepBound, "the same holds for the lurkers (a second species, seeded in the flora)");
        var hunted = RunLod(41, SubstrateFreezeBug.None, false, "locust");
        Console.WriteLine($"    locusts (the pack's prey): thawed after {hunted.FrozenTicks} frozen ticks by {hunted.HuntThaws} hunt(s); births {hunted.Births}, starvings {hunted.Starvings} while frozen");
        Check(hunted.HuntThaws >= 1, "a frozen population whose agent is hunted thaws first (clause 3: the death is an individual's, crystal and all)");
        Console.WriteLine($"    pack: {r.FrozenTicks} frozen ticks; alive {r.AliveBefore} -> {r.AliveAfter}; volume dev {r.VolMaxDev:E1} (of {r.VolBefore:F0}); " +
                          $"stock dev {r.StockMaxDev:E1}; moved-while-frozen {r.MovedFrozen}; glide err {r.GlideErr:E1} u; hunger err {r.HungerErr:E1}; " +
                          $"min reserve while frozen {r.MinReserveFrozen:F1} s; thaws: reserve {r.ReserveThaws}, hunted {r.HuntThaws}; " +
                          $"births {r.Births}, starvings {r.Starvings} while frozen; first step after thaw {r.FirstStep:F2} u (bound {r.StepBound:F2}); ledger {r.CoreLedger:E1}");
        Check(r.Exercised && r.AliveAfter == r.AliveBefore && r.VolMaxDev == 0 && r.StockMaxDev == 0,
              "exact mass: every frozen agent alive with bit-identical stock, the population's published volume (its index entries, LiveVolume) unchanged");
        Check(r.MovedFrozen == 0 && r.GlideErr < 1e-3f, "frozen agents hold still except the rigid Translate, drawn as a glide of exactly the drift");
        Check(r.HungerErr < 1e-5, "one rule set: a frozen agent's hunger rises by exactly Metabolism x Dt per tick (freezing is not immortality)");
        Check(r.Births == 0 && r.Starvings == 0 && r.MinReserveFrozen > 0, "no birth and no death is decided frozen: it thaws before its hungriest agent's reserve runs out");
        Check(r.FirstStep <= r.StepBound, "no pop on thaw: the first steps are ordinary steps from where the agents were drawn");
        Check(r.CoreLedger < 1e-6, "the core's mass ledger closes across freeze and thaw");

        var dead = RunLod(41, SubstrateFreezeBug.None, true);
        Console.WriteLine($"    Deactivate (collapse by switching the block off): alive {dead.AliveBefore} -> published 0 while off; volume dev {dead.VolMaxDev:F0}");
        Check(dead.Exercised && dead.VolMaxDev > 0.5 * dead.VolBefore, "negative control: switching a population off instead of freezing it drops its mass from LiveVolume");
        var imm = RunLod(41, SubstrateFreezeBug.NoMetabolism, false);
        Console.WriteLine($"    NoMetabolism: hunger err {imm.HungerErr:E1}, {imm.FrozenTicks} frozen ticks, reserve thaws {imm.ReserveThaws}");
        Check(imm.Exercised && imm.HungerErr > 1e-3, "negative control: a frozen population that stops getting hungry fails the one-rule-set check");

        // the director (EcologyLodDirector, the rules every population shares) over the substrate's populations, with a pilot
        // flying in from outside the cell at 260 u/s: frozen while far, thawed before it can see them
        var w = new LodWorld(43);
        for (int t = 0; t < 200; t++) w.Tick();
        var director = new EcologyLodDirector();
        var models = new[] { w.Locust, w.Pack, w.Lurker }.Select(q => new SubstratePopModel(w, q, 5f)).ToArray();
        foreach (var m in models) director.Register(m);
        var target = models[0].MacroCentre;
        var dir = Vector3.Normalize(target);
        var pil = new EcologyPilot { X = target.X + dir.X * 2600, Y = target.Y + dir.Y * 2600, Z = target.Z + dir.Z * 2600, Speed = 260 };
        var pilots = new EcologyPilot[1];
        int seenFrozen = 0, frozenTicks = 0, ticks = 0;
        float sinceMacro = 0f;
        long collapses0 = 0;
        for (int t = 0; t < 1200; t++)
        {
            // the pilot: from 2600 u outside, straight at the locusts' centre, then on through the cell
            var to = new Vector3((float)(target.X - pil.X), (float)(target.Y - pil.Y), (float)(target.Z - pil.Z));
            if (t < 900 && to.Length() > 10f) { var v = Vector3.Normalize(to) * 260f; pil.Vx = v.X; pil.Vy = v.Y; pil.Vz = v.Z; }
            if (t >= 300) { pil.X += pil.Vx * Dt; pil.Y += pil.Vy * Dt; pil.Z += pil.Vz * Dt; }
            pilots[0] = pil;
            director.SetPilots(pilots);
            director.Guard();
            sinceMacro += Dt;
            if (sinceMacro >= 1f) { sinceMacro -= 1f; director.Tick(1f); }
            if (t == 299) collapses0 = director.Collapses;
            w.Tick();
            ticks++;
            foreach (var m in models)
            {
                if (!m.Collapsed) continue;
                frozenTicks++;
                if (director.SeenByAnyPilot(m)) seenFrozen++;
            }
        }
        Console.WriteLine($"    director: {director.Collapses} collapses ({collapses0} before the pilot set off), {director.Expands} expands, " +
                          $"{frozenTicks} population-ticks frozen; seen while frozen {seenFrozen}");
        Check(collapses0 >= 1 && director.Expands >= 1 && seenFrozen == 0,
              "the director freezes the substrate far from every pilot and thaws it before a 260 u/s pilot can see it");

        // cost: the shared tick with every population frozen vs none (the saving a far cell buys)
        double Ms(LodWorld lw, bool frozen, int n)
        {
            foreach (var p in lw.Core.Pops) p.Frozen = frozen;
            double sum = 0;
            for (int t = 0; t < n; t++) { lw.Tick(); sum += lw.Job.LastTickMs; }
            foreach (var p in lw.Core.Pops) p.Frozen = false;
            return sum / n;
        }
        var cw = new LodWorld(47);
        for (int t = 0; t < 100; t++) cw.Tick();
        double live = Ms(cw, false, 200), cold = Ms(cw, true, 200);
        Console.WriteLine($"    the substrate tick (worker side, one thread): {live:F3} ms running, {cold:F3} ms all frozen ({live / Math.Max(cold, 1e-6):F1}x)");
        Check(cold < live, "a frozen population costs less than a running one");
    }
}
