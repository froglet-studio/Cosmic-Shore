// Round 11f (Docs/ECOLOGY_LOD.md §4-§6): the GENERIC population LOD contract, the director that applies the
// research's LOD rules to any population implementing it, the macro fauna ledger the cell's volume spine reads, and
// the swarm's implementation (SwarmMacroBody). Pure C#: compiled and RUN headless by Tools/Build/ecology_lod_harness.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>What a collapsed population holds - the numbers the conservation gate compares across a collapse and
    /// an expand. Mass is prism volume.</summary>
    public struct MacroPopulationTotals
    {
        public long Individuals;
        /// <summary>Σ body volume - what the cell's LiveVolume counts for this population.</summary>
        public double BodyVolume;
        /// <summary>Σ stomach (the population's banked food).</summary>
        public double Stomach;
    }

    /// <summary>
    /// A population that can run as a MACRO state far from every pilot and as individuals near one
    /// (Docs/ECOLOGY_LOD.md §4 - the contract the substrate and builder populations adopt).
    ///
    /// The contract, each clause a gate the harness asserts for the swarm:
    ///  1. <b>Collapse and expand are atomic and exact</b>: individuals per element/lineage, Σbody and Σstomach are the
    ///     same before and after (<see cref="Totals"/>), and so is the cell's LiveVolume - either the population keeps
    ///     its index entries (bound with Cell.BindVirtualMass) while collapsed, or it releases them and books the same
    ///     volume into <see cref="MacroFaunaLedger"/> in the same call.
    ///  2. <b>Nothing pops</b>: the director only collapses a population no pilot can see (simulated and drawn
    ///     positions, widened by <see cref="MacroExtent"/>), and expands it on the prefetch radius, before it can be
    ///     seen. A population whose expansion draws new bodies must grow them in (bloom) or emerge them from what was
    ///     drawn.
    ///  3. <b>No imposed death in the macro</b>: a death needs a body to wither and a crystal to drop, so a macro tick
    ///     that would kill (starvation) reports <see cref="NeedsIndividuals"/> instead and is expanded to die properly.
    ///  4. <b>One rule set</b>: the macro tick is the same biology at a coarse cadence - feeding through the same
    ///     edibility rules, metabolism to the same soil, births from the same stomach tail.
    /// </summary>
    public interface IMacroPopulation
    {
        /// <summary>World position the LOD rules measure from (the body's centre).</summary>
        Vector3 MacroCentre { get; }
        /// <summary>World radius of the body (0 for a point population).</summary>
        float MacroExtent { get; }
        bool IsCollapsed { get; }
        /// <summary>The population could collapse NOW (no proxies, no tick in flight, nothing it must resolve with bodies).</summary>
        bool CanCollapse { get; }
        /// <summary>Something only individuals can resolve is pending (a starvation shed, a hit): expand now.</summary>
        bool NeedsIndividuals { get; }
        MacroPopulationTotals Totals { get; }
        /// <summary>Atomic, exact (clause 1). Returns false (nothing changed) when it cannot right now.</summary>
        bool Collapse();
        /// <summary>Atomic, exact, no pop-in (clauses 1-2).</summary>
        void Expand();
        /// <summary>One coarse tick while collapsed (the director's cadence, 1 Hz).</summary>
        void MacroTick(float dt);
    }

    /// <summary>
    /// Applies the research's LOD rules (EcologyLodRules: expand within 280 u / 450 u ahead in the forward cone,
    /// collapse beyond 400 u / 530 u ahead, only when unseen) to every registered population, at the macro cadence.
    /// One per cell. Pure C#: the Unity glue only feeds it pilots and calls <see cref="Tick"/>.
    /// </summary>
    public sealed class EcologyLodDirector
    {
        public readonly EcologyParams P;
        readonly List<IMacroPopulation> _pops = new();
        EcologyPilot[] _pilots = new EcologyPilot[8];
        int _npil;
        public long Collapses, Expands, MacroTicks, RefusedVisible;

        public EcologyLodDirector(EcologyParams p = null) { P = p ?? new EcologyParams(); }

        public IReadOnlyList<IMacroPopulation> Populations => _pops;
        public void Register(IMacroPopulation p) { if (p != null && !_pops.Contains(p)) _pops.Add(p); }
        public void Unregister(IMacroPopulation p) => _pops.Remove(p);

        public void SetPilots(ReadOnlySpan<EcologyPilot> pilots)
        {
            if (_pilots.Length < pilots.Length) _pilots = new EcologyPilot[pilots.Length];
            pilots.CopyTo(_pilots);
            _npil = pilots.Length;
        }

        ReadOnlySpan<EcologyPilot> Pilots => new(_pilots, 0, _npil);

        public bool SeenByAnyPilot(IMacroPopulation p)
        {
            var c = p.MacroCentre;
            return EcologyLodRules.Visible(Pilots, P, c.X, c.Y, c.Z, p.MacroExtent);
        }

        public bool WantsIndividuals(IMacroPopulation p)
        {
            var c = p.MacroCentre;
            return EcologyLodRules.WantHot(Pilots, P, c.X, c.Y, c.Z, !p.IsCollapsed, p.MacroExtent);
        }

        /// <summary>
        /// Every frame (cheap: a distance test per collapsed population): expand at once anything collapsed that a pilot
        /// wants or can see. The macro tick only moves a collapsed body when nobody sees it at that instant; this guard
        /// is what keeps a pilot faster than the 1 Hz cadence from ever watching one (a 260 u/s pilot covers the
        /// prefetch margin in under a second).
        /// </summary>
        public void Guard()
        {
            for (int i = 0; i < _pops.Count; i++)
            {
                var p = _pops[i];
                if (!p.IsCollapsed) continue;
                if (p.NeedsIndividuals || WantsIndividuals(p) || SeenByAnyPilot(p)) { p.Expand(); Expands++; }
            }
        }

        /// <summary>One macro tick: expand what a pilot is approaching (or what must resolve something with bodies),
        /// collapse what is far AND unseen, and tick everything collapsed.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < _pops.Count; i++)
            {
                var p = _pops[i];
                bool want = WantsIndividuals(p);
                if (p.IsCollapsed)
                {
                    // a pilot can SEE it (should not happen with the prefetch radii, but a teleporting pilot can): expand
                    if (want || p.NeedsIndividuals || SeenByAnyPilot(p)) { p.Expand(); Expands++; continue; }
                    p.MacroTick(dt);
                    MacroTicks++;
                    // the tick MOVED it (a collapsed swarm drifts toward its goal): if that brought it into a pilot's view or
                    // prefetch, it expands in this same tick, before a frame can draw it. Before round 11-10 it stayed
                    // collapsed until the next frame's Guard - the whole-cell run caught one such frame in 30 min.
                    if (WantsIndividuals(p) || SeenByAnyPilot(p)) { p.Expand(); Expands++; }
                    continue;
                }
                if (want || p.NeedsIndividuals || !p.CanCollapse) continue;
                if (SeenByAnyPilot(p)) { RefusedVisible++; continue; }   // never collapse what is seen
                if (p.Collapse()) Collapses++;
            }
        }
    }

    /// <summary>
    /// Fauna body volume held by MACRO populations that released their index entries, per domain - what
    /// <c>Cell.LiveVolume</c> adds to the index sum so the phase ladder reads the same number collapsed or expanded
    /// (research recommendation item 2). The cell's index sum is ASYNC (scheduled, published later), so the cell
    /// <see cref="Latch"/>es this ledger when it schedules a sum and publishes the latched copy with it: a population
    /// that moves volume from the index to the ledger between the two is then counted once, never twice or zero times.
    /// </summary>
    public sealed class MacroFaunaLedger
    {
        public const int Slots = 8;
        readonly double[] _live = new double[Slots], _latched = new double[Slots];
        double _liveTotal, _latchedTotal;

        public void Book(int domainSlot, double volume)
        {
            if (domainSlot < 0 || domainSlot >= Slots) return;
            _live[domainSlot] += volume; _liveTotal += volume;
        }

        public double Live(int domainSlot) => domainSlot >= 0 && domainSlot < Slots ? _live[domainSlot] : 0;
        public double LiveTotal => _liveTotal;

        /// <summary>Snapshot taken in the same frame the cell schedules its index volume sum.</summary>
        public void Latch() { Array.Copy(_live, _latched, Slots); _latchedTotal = _liveTotal; }

        public double Latched(int domainSlot) => domainSlot >= 0 && domainSlot < Slots ? _latched[domainSlot] : 0;
        public double LatchedTotal => _latchedTotal;

        public void Clear() { Array.Clear(_live, 0, Slots); Array.Clear(_latched, 0, Slots); _liveTotal = _latchedTotal = 0; }
    }

    /// <summary>The swarm's macro state: what a collapsed swarm IS (Docs/ECOLOGY_LOD.md §5).</summary>
    public sealed class SwarmMacroState
    {
        /// <summary>Members per (element, domain slot = lineage): [element * 3 + slot].</summary>
        public readonly long[] Counts = new long[12];
        public long Alive;
        /// <summary>The swarm's pooled stomach per research element (it is ONE pool per element, so Σstomach is exact and
        /// a per-member Σstomach² does not exist - the swarm is one creature-population, not a cohort).</summary>
        public readonly double[] Stomach = new double[4];
        /// <summary>Σ member body volume (|x·y·z| of each published body) - what its index entries sum to.</summary>
        public double BodyVolume;
        /// <summary>Anchor (world) and the body frame (heading) at collapse; the anchor drifts with the macro tick.</summary>
        public Vector3 Centroid, BX, BY, BZ;
        public int PlanIx;
        public long Tick;
        public double MacroSeconds, Drift;

        public long Count(int element, int slot) => Counts[element * 3 + slot];
        public double StomachTotal => Stomach[0] + Stomach[1] + Stomach[2] + Stomach[3];
        public MacroPopulationTotals Totals => new() { Individuals = Alive, BodyVolume = BodyVolume, Stomach = StomachTotal };

        /// <summary>Reads the state off a published frame (main thread, job Idle).</summary>
        public static SwarmMacroState Read(SwarmTickJob job)
        {
            var s = new SwarmMacroState();
            s.Capture(job);
            return s;
        }

        public void Capture(SwarmTickJob job)
        {
            Array.Clear(Counts, 0, Counts.Length);
            Alive = 0; BodyVolume = 0;
            var inst = job.Instances;
            for (int i = 0; i < inst.Length; i++)
            {
                if (!inst[i].Alive) continue;
                int e = job.Core.EffectiveElement(i), d = Math.Clamp(inst[i].DomainSlot, 0, 2);
                Counts[Math.Clamp(e, 0, 3) * 3 + d]++;
                Alive++;
                BodyVolume += Math.Abs((double)inst[i].Scale.X * inst[i].Scale.Y * inst[i].Scale.Z);
            }
            for (int e = 0; e < 4; e++) Stomach[e] = job.Stomach[e];
            Centroid = job.Anchor; BX = job.BX; BY = job.BY; BZ = job.BZ;
            PlanIx = job.PlanIx; Tick = job.Tick;
        }

        /// <summary>Exact equality of what must be conserved (counts per element x lineage, Σbody to 1e-6 relative).</summary>
        public bool SameBody(SwarmMacroState o, out string why)
        {
            for (int k = 0; k < 12; k++)
                if (Counts[k] != o.Counts[k]) { why = $"count[{k / 3},{k % 3}] {Counts[k]} != {o.Counts[k]}"; return false; }
            if (Math.Abs(BodyVolume - o.BodyVolume) > 1e-6 * Math.Max(1, BodyVolume)) { why = $"body {BodyVolume} != {o.BodyVolume}"; return false; }
            why = ""; return true;
        }
    }

    /// <summary>
    /// The swarm's side of <see cref="IMacroPopulation"/>, Unity-free: SwarmFauna wraps it (and the harness drives it
    /// directly). A collapsed swarm keeps its formation - the body plan it re-expands into - frozen in the core, keeps
    /// its index entries (moved rigidly once per macro tick, so LiveVolume, weapons and predators see the same prisms),
    /// stops its 10 Hz worker tick, and drifts toward its goal at cruise speed. Counts never change while collapsed:
    /// births are funded on expansion from the banked stomach (the core's own laying), and a starvation shed expands
    /// it first (a death needs a body to wither and a crystal to drop).
    /// </summary>
    public sealed class SwarmMacroBody
    {
        public readonly SwarmTickJob Job;
        public bool Collapsed { get; private set; }
        /// <summary>The state captured at collapse, kept current by every macro tick (stomach, centroid).</summary>
        public SwarmMacroState State { get; private set; }
        /// <summary>Test seam: a planted bug that must fail the gates (never set in game).</summary>
        public SwarmMacroBug Bug;

        public SwarmMacroBody(SwarmTickJob job) { Job = job; }

        /// <summary>What the swarm holds: the frozen state while collapsed, the published frame otherwise (job Idle or
        /// Done - a tick in flight owns the frame).</summary>
        public MacroPopulationTotals Totals => (Collapsed ? State : SwarmMacroState.Read(Job)).Totals;

        /// <summary>Collapses when no tick is in flight (call after a Collect). Returns false when it cannot.</summary>
        public bool TryCollapse()
        {
            if (Collapsed || Job.State != SwarmJobState.Idle) return false;
            State = SwarmMacroState.Read(Job);
            Collapsed = true;
            return true;
        }

        /// <summary>
        /// One macro tick: the anchor drifts toward <paramref name="swimTargetWorld"/> at <paramref name="speedWorld"/>
        /// (the swarm's cruise), rigidly. Returns the world displacement. Food the glue landed this tick is banked by
        /// <see cref="Bank"/>.
        /// </summary>
        public Vector3 MacroTick(float dt, Vector3 swimTargetWorld, float speedWorld, float unitScale)
        {
            if (!Collapsed) return Vector3.Zero;
            var to = swimTargetWorld - Job.Anchor;
            float dist = to.Length(), step = Math.Min(dist, Math.Max(0f, speedWorld) * dt);
            var d = dist > 1e-4f ? to / dist * step : Vector3.Zero;
            if (Bug == SwarmMacroBug.TranslateCoreOnly) { Job.Core.Translate(d / unitScale); State.Centroid += d; }
            else if (!Job.Translate(d / unitScale)) d = Vector3.Zero;
            else State.Centroid = Job.Anchor;
            State.MacroSeconds += dt; State.Drift += d.Length();
            return d;
        }

        /// <summary>Food landed while collapsed (the glue consumed a real prism): banked into the stomach the next
        /// tick pays from - the same QueueDeposit path the micro uses.</summary>
        public void Bank(int element, float volume)
        {
            if (element < 0 || element > 3 || !(volume > 0f)) return;
            Job.QueueDeposit(element, volume);
            if (State != null) State.Stomach[element] += volume;
        }

        /// <summary>Re-expands. With <see cref="SwarmMacroBug.ReseedOnExpand"/> the formation is thrown away and the body
        /// re-grown from the counts (the negative control: shapes re-roll, so Σbody - LiveVolume - jumps).</summary>
        public void Expand()
        {
            if (!Collapsed) return;
            Collapsed = false;
            if (Bug == SwarmMacroBug.ReseedOnExpand) Regrow(Job, State);
        }

        /// <summary>
        /// Re-grow a body from a macro state alone (a peer that never saw the formation; the reseed negative control):
        /// the exact counts per element and per lineage, a knot at the centroid that grows out into the body plan
        /// (the cores' SeedWith; every member is a newborn that blooms in). The element x lineage pairing is not
        /// preserved (SeedWith pairs them at random) and the members' shapes re-roll.
        /// </summary>
        public static void Regrow(SwarmTickJob job, SwarmMacroState s)
        {
            var el = new int[4]; var dm = new int[3];
            for (int e = 0; e < 4; e++) for (int d = 0; d < 3; d++) { el[e] += (int)s.Counts[e * 3 + d]; dm[d] += (int)s.Counts[e * 3 + d]; }
            var c = job.Core;
            var centre = (s.Centroid - job.S.Centre) / job.S.UnitScale;
            for (int i = 0; i < c.Cap; i++) if (c.Alive[i]) c.Kill(i);
            job.Prime();   // publish the empty frame first, so every re-grown member is a NEWBORN (it blooms in)
            switch (c)
            {
                case SwarmSortCore so: so.SeedWith(el, dm, centre, 2f); break;
                case SwarmGridCore g: g.SeedWith(el, dm, centre, 2f); break;
                case SwarmEvoFateCore ev: ev.SeedWith(el, dm, centre, 2f); break;
                default: c.Seed(s.PlanIx, (int)s.Alive, centre, s.BX); break;
            }
            job.Prime();
        }
    }

    /// <summary>Planted bugs for the swarm LOD gates.</summary>
    public enum SwarmMacroBug
    {
        None = 0,
        /// <summary>Move the core but not the published frame: the drawn body jumps on expansion.</summary>
        TranslateCoreOnly = 1,
        /// <summary>Throw the formation away and re-grow from the counts: Σbody (LiveVolume) re-rolls.</summary>
        ReseedOnExpand = 2,
    }
}
