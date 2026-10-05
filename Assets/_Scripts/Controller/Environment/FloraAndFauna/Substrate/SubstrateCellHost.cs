using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11b (Docs/SUBSTRATE_FAUNA.md §5): ONE substrate per cell. Every population of every species in the cell -
    /// each a <see cref="SubstrateFauna"/> anchor the cell's ordinary spawner made - is a block of slots in one
    /// <see cref="SubstrateCore"/>, so they share the stigmergy fields: a pack's threat wake is a locust's danger
    /// signal, a locust's scent is what the pack hunts by, with nothing wired between them.
    ///
    /// The host owns the core and its <see cref="SubstrateTickJob"/> and runs the round-7 cadence (Docs/SWARM_FAUNA.md
    /// §14): the tick runs on a worker thread; the main thread, once per tick, collects the published frame, hands it to
    /// every population, senses the vessels and the food, applies joins, and kicks the next tick. A plain object, driven
    /// by whichever of its populations updates first each frame - it has no GameObject and nothing to pop.
    ///
    /// Round 11b-2 (§7): the tick is SPLIT. The worker runs the fields, the moments and the gather, then parks; the host
    /// schedules the agent pass as Burst jobs (<see cref="SubstrateAgentPass"/>, jobs are scheduled from the main thread
    /// only), completes them on its next Advance, and the worker finishes the tick (the world pass and the frame).
    /// </summary>
    public sealed class SubstrateCellHost
    {
        static readonly Dictionary<Cell, SubstrateCellHost> s_hosts = new();
        static readonly Collider[] s_overlap = new Collider[4096];
        static readonly List<IVesselStatus> s_seen = new(SubstrateCore.MaxPilots);
        static readonly ProfilerMarker s_mCollect = new("SubstrateCellHost.Collect");
        static readonly ProfilerMarker s_mPublish = new("SubstrateCellHost.Publish");
        static readonly ProfilerMarker s_mSense = new("SubstrateCellHost.Sense");
        static readonly ProfilerMarker s_mAgents = new("SubstrateCellHost.AgentPass");

        public readonly Cell Cell;
        public readonly SubstrateCore Core;
        public readonly SubstrateTickJob Job;
        readonly SubstrateAgentPass _agents;
        public readonly Vector3 Centre;
        public readonly float Dt;
        readonly bool _inline;
        readonly List<SubstrateFauna> _members = new();
        readonly List<SubstrateFauna> _joining = new();
        readonly List<int> _leaving = new();
        // round 11f-2: freezes / thaws and rigid moves the populations asked for, applied between ticks
        readonly List<(int pop, bool frozen)> _freezes = new();
        readonly List<(int pop, SVector3 d)> _moves = new();
        readonly SubstrateFauna[] _byPop = new SubstrateFauna[16];
        float _acc;
        int _frame = -1;
        bool _warnedError, _warnedSense;

        /// <summary>This frame's display alpha between the published pair (0..1).</summary>
        public float Alpha { get; private set; }

        SubstrateCellHost(Cell cell, SubstrateSpeciesSO first)
        {
            Cell = cell;
            Centre = cell.transform.position;
            float r = cell.MembraneRadius > 1f ? cell.MembraneRadius : 1200f;
            Dt = 0.1f;   // the research substrate's dt (its parameters are per this step)
            Core = new SubstrateCore(first.CellCapacity, r, Dt, 40, Random.Range(1, int.MaxValue));
            Job = new SubstrateTickJob(Core, new SubstrateTickSettings
            {
                Centre = new SVector3(Centre.x, Centre.y, Centre.z),
                HeartPrismGap = first.HeartPrismGap,
                Thin = first.BodyThin,
                EngageRadius = first.EngageRadius,
                MaxEngaged = first.MaxProxies,
            });
            var hs = first.HeartWorldScaleByElement;
            Job.S.HeartWorldScale = new[] { hs.x, hs.y, hs.z, hs.w };
            Job.Prime();
            _agents = new SubstrateAgentPass(Core);
            Job.ExternalAgentPass = true;   // one step per tick: the agent pass runs as Burst jobs between its halves
            _inline = !first.SimulateOffMainThread || Application.platform == RuntimePlatform.WebGLPlayer;
            _acc = Random.value * Dt;   // stagger cells across frames from the first tick
        }

        // Enter Play Mode without a domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var h in s_hosts.Values) h._agents.Dispose();
            s_hosts.Clear();
        }

        /// <summary>The cell's substrate, made on the first population's arrival.</summary>
        public static SubstrateCellHost For(Cell cell, SubstrateSpeciesSO species)
        {
            if (!cell || !species) return null;
            if (s_hosts.TryGetValue(cell, out var h)) return h;
            h = new SubstrateCellHost(cell, species);
            s_hosts[cell] = h;
            return h;
        }

        /// <summary>A population asks for its block. Applied between ticks (the worker owns the core while it runs).</summary>
        public void Join(SubstrateFauna population)
        {
            if (population && !_joining.Contains(population) && !_members.Contains(population)) _joining.Add(population);
        }

        /// <summary>A population leaves (its anchor is going): its block is retired between ticks.</summary>
        public void Leave(SubstrateFauna population)
        {
            _joining.Remove(population);
            int q = _members.IndexOf(population);
            if (q < 0) return;
            _members.RemoveAt(q);
            int pop = population.Pop;
            if (pop >= 0 && pop < _byPop.Length && _byPop[pop] == population) { _byPop[pop] = null; _leaving.Add(pop); }
            if (_members.Count == 0 && _joining.Count == 0)
            {
                s_hosts.Remove(Cell);
                _agents.Dispose();   // completes a scheduled pass first; a parked worker is simply never resumed
            }
        }

        /// <summary>The population that owns core population <paramref name="pop"/> (a predator finds its prey's owner).</summary>
        public SubstrateFauna PopulationOf(int pop) => pop >= 0 && pop < _byPop.Length ? _byPop[pop] : null;

        /// <summary>The population that owns core slot <paramref name="i"/>.</summary>
        public SubstrateFauna OwnerOfSlot(int i) => i >= 0 && i < Core.Capacity ? PopulationOf(Core.PopOf[i]) : null;

        /// <summary>Once per frame, by any member (the first call in a frame does the work).</summary>
        public void Advance()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            if (Job.Error != null)
            {
                if (!_warnedError)
                {
                    _warnedError = true;
                    CSDebug.LogError($"[Substrate] the cell's substrate tick threw on its worker and has stopped: {Job.Error}");
                }
                return;
            }
            PumpAgentPass();
            _acc += Time.deltaTime;
            if (_acc >= Dt) Step();
            Alpha = Mathf.Clamp01(_acc / Dt);
        }

        void Step()
        {
            var state = Job.State;
            if (state == SwarmJobState.Running) { _acc = Dt; return; }   // a slow worker slows the substrate, never the frame
            if (state == SwarmJobState.Done)
            {
                using (s_mCollect.Auto()) Job.Collect();
                using (s_mPublish.Auto())
                    for (int k = _members.Count - 1; k >= 0; k--)
                        if (k < _members.Count && _members[k]) _members[k].OnTickPublished(this);
                _acc -= Dt;
                if (_acc > Dt) _acc = Dt;   // never bank more than one tick
            }
            ApplyMembership();
            ApplyLod();
            using (s_mSense.Auto()) { SenseVessels(); SenseFood(); }
            Job.Kick(_inline);
            if (_inline && Job.AwaitingAgentPass)
                using (s_mAgents.Auto())
                {
                    _agents.Schedule();
                    _agents.Complete();
                    Job.ResumeAfterAgentPass(true);
                }
        }

        /// <summary>Every frame: a pass scheduled last frame is completed and the tick job resumed; a worker that has
        /// parked since is given its agent pass. So a tick spans a few frames and the main thread pays only the copies.</summary>
        void PumpAgentPass()
        {
            using (s_mAgents.Auto())
            {
                if (_agents.Scheduled)
                {
                    _agents.Complete();
                    Job.ResumeAfterAgentPass(_inline);
                }
                else if (Job.AwaitingAgentPass) _agents.Schedule();
            }
        }

        /// <summary>Round 11f-2 (Docs/ECOLOGY_LOD.md §6.1): freeze (collapse) or thaw (expand) a population. Applied between
        /// ticks - the worker owns the core while it runs.</summary>
        public void Freeze(int pop, bool frozen)
        {
            if (pop >= 0) _freezes.Add((pop, frozen));
        }

        /// <summary>Move a population rigidly by <paramref name="d"/> (world units), between ticks.</summary>
        public void Translate(int pop, SVector3 d)
        {
            if (pop >= 0) _moves.Add((pop, d));
        }

        void ApplyLod()
        {
            for (int k = 0; k < _freezes.Count; k++)
            {
                var (pop, frozen) = _freezes[k];
                if (pop < Core.Pops.Count && Core.Pops[pop].Active) Core.Pops[pop].Frozen = frozen;
            }
            _freezes.Clear();
            for (int k = 0; k < _moves.Count; k++)
            {
                var (pop, d) = _moves[k];
                if (pop < Core.Pops.Count && Core.Pops[pop].Active) Core.Translate(pop, d);
            }
            _moves.Clear();
        }

        /// <summary>Joins and leaves land while the job is Idle (between ticks), so the worker never sees a half-made block.</summary>
        void ApplyMembership()
        {
            for (int k = 0; k < _leaving.Count; k++) Core.RemovePopulation(_leaving[k]);
            _leaving.Clear();
            for (int k = 0; k < _joining.Count; k++)
            {
                var p = _joining[k];
                if (!p) continue;
                int pop = p.ClaimBlock(this);
                if (pop < 0 || pop >= _byPop.Length)
                {
                    if (pop >= 0) Core.RemovePopulation(pop);
                    p.OnRefused();
                    continue;
                }
                _byPop[pop] = p;
                _members.Add(p);
            }
            _joining.Clear();
        }

        /// <summary>Every vessel in the cell (up to <see cref="SubstrateCore.MaxPilots"/>), in sim space.</summary>
        void SenseVessels()
        {
            int n = 0;
            s_seen.Clear();
            int mask = SubstrateFauna.VesselOverlapMask;
            int hits = Physics.OverlapSphereNonAlloc(Centre, Core.R, s_overlap, mask);
            if (hits >= s_overlap.Length && !_warnedSense)
            {
                _warnedSense = true;
                CSDebug.LogWarning($"[Substrate] the cell-wide vessel sense filled its {s_overlap.Length}-collider buffer; " +
                                   "a vessel may be missed this tick.");
            }
            float radius = _members.Count > 0 && _members[0] ? _members[0].VesselRadius : 6f;
            for (int h = 0; h < hits && n < Job.Pilots.Length; h++)
            {
                var col = s_overlap[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || s_seen.Contains(status) || status is not Component c || !c) continue;
                s_seen.Add(status);
                var p = c.transform.position - Centre;
                var v = status.Course * status.Speed;
                Job.Pilots[n++] = new SubstratePilot
                {
                    Pos = new SVector3(p.x, p.y, p.z), Vel = new SVector3(v.x, v.y, v.z), Radius = radius,
                    Id = c.GetInstanceID(),
                };
            }
            Job.PilotCount = n;
        }

        /// <summary>The cell's living FLORA, one unit of food per plant heart - the food field is a READ of mass. A Charge
        /// plant is no food at all (its leaves are armoured: shielded mass is never food).</summary>
        void SenseFood()
        {
            var live = FloraHeartRegistry.Live;
            int n = 0;
            float r2 = Core.R * Core.R;
            for (int k = 0; k < live.Count; k++)
            {
                var f = live[k];
                if (!f || f.IsDying || f.Element == Element.Charge) continue;
                var ht = f.HeartTransform;
                if (!ht) continue;
                var p = ht.position - Centre;
                if (p.sqrMagnitude > r2) continue;
                if (n >= Job.Food.Length) System.Array.Resize(ref Job.Food, Job.Food.Length * 2);
                Job.Food[n++] = new SubstrateFood { Pos = new SVector3(p.x, p.y, p.z), Volume = 1f };
            }
            Job.FoodCount = n;
        }
    }
}
