using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The gravity MOVER (Docs/BLACK_HOLE.md §3): every prism inside a live hole's influence
    /// sphere becomes a body, and every body is integrated under every hole each frame.
    ///
    /// <para><b>This is live gameplay data, not animation.</b> Where a prism is next frame
    /// depends on where the holes are and where every other step put it — the GPU could not have
    /// known it at any stamp — so it is exactly the class the clock-material law leaves alone
    /// (Docs/PRISM_ANIMATION.md §1 "Animation vs. live gameplay data", §3.6 movers): a
    /// per-frame transform write under the movers contract, the same contract fauna locomotion
    /// and the builders keep. Colliders, the spatial index, the render entity and every gameplay
    /// query see the prism where it actually is.</para>
    ///
    /// <para><b>The data is ECS, the work is three chained Burst jobs.</b> A body's state is the
    /// <see cref="GravityBody"/> component on the prism's companion entity (on every prism's
    /// prototype, enabled on admission), so "which prisms are under gravity" is a question the
    /// entity world can answer and a pure-entity prism of the future is a body with nothing to
    /// add. Per frame:
    /// <list type="number">
    /// <item><see cref="ReadPoseJob"/> — <see cref="IJobParallelForTransform"/> scheduled
    /// READ-ONLY, so it runs on every worker even when all the prisms share one parent (a write
    /// job is serialised per root hierarchy; a read-only one is not).</item>
    /// <item><see cref="IntegrateJob"/> — <see cref="IJobParallelFor"/> in batches of
    /// <see cref="IntegrateBatch"/>: every body stepped through <see cref="BlackHolePhysics"/>
    /// (substeps × wells — the expensive part), emitting the render matrix, the index point and
    /// the verdict. Pure <c>Unity.Mathematics</c>, no managed or UnityEngine call.</item>
    /// <item><see cref="WritePoseJob"/> — the new positions back onto the transforms, alongside
    /// the bulk render write
    /// (<see cref="PrismRenderService.SetTransformsBatch(NativeArray{PrismRenderHandle}, NativeArray{float4x4}, int, JobHandle)"/>),
    /// which depends on the integrate job only. Then one bulk index write
    /// (<see cref="PrismSpatialIndex.UpdatePositionsBatch"/>).</item>
    /// </list>
    /// The render and index writes are the two halves of <c>Prism.NotifyPositionChanged</c> done
    /// for N prisms at once, the way the swarm and the builders already do. Nothing per prism is
    /// managed except admission and the verdicts.</para>
    ///
    /// <para><b>Admission is a spatial query, bounded.</b> Every admission interval each hole asks
    /// <see cref="PrismSpatialIndex.QuerySphere"/> for the prisms in its influence sphere; new
    /// ones are ranked nearest-first and admitted up to the config's body budget. Super-shielded
    /// mass is never admitted (it is structure, and nothing can destroy it — pulling it would
    /// pile it unkillable at the singularity), nor is a prism still growing in, nor one whose
    /// entity was born on a non-Prism prototype (debris, husks: their flight is a clock stamp).</para>
    ///
    /// <para><b>Two verdicts leave the set.</b> CAPTURED (a body's centre crossed a horizon) is
    /// consumed into the singularity through <c>Prism.Consume</c> with the hole's transform as
    /// the sink — the implosion debris converges on the hole, which is what "it vanished into
    /// the black hole" looks like, and it is the one implosion whose moving target is already a
    /// documented exception. RELEASED (coasted clear of every influence sphere and slowed below
    /// the release speed) goes back to being static mass with its body disabled.</para>
    ///
    /// One stated limit: <see cref="WritePoseJob"/> writes transforms, and Unity serialises a
    /// transform WRITE job per root hierarchy, so a field whose prisms all share one parent (a
    /// test lattice, a cell environment) writes its poses on one worker. That job is one position
    /// store per body; the read and the integration — the work — run on every worker.
    /// </summary>
    public static class BlackHoleGravityField
    {
        const int InitialCapacity = 1024;

        /// <summary>Transforms per worker batch for the read-only pose read (a matrix copy each).</summary>
        const int ReadBatch = 64;

        /// <summary>
        /// Bodies per worker batch for the integration: a body is up to MaxSubsteps × wells
        /// accelerations, so batches stay small enough to spread a few thousand bodies over
        /// every worker and large enough that scheduling is not the cost.
        /// </summary>
        public const int IntegrateBatch = 32;
        const string ConsumerName = "Black Hole";

        // SoA ledger of admitted bodies. Every array is index-aligned with _prisms and every
        // removal is a swap-back on all of them at once.
        static readonly List<Prism> _prisms = new();
        static readonly HashSet<Prism> _admitted = new();
        static TransformAccessArray _transforms;
        static NativeList<PrismRenderHandle> _handles;
        static NativeList<Entity> _entities;
        static NativeList<int> _indexIds;
        static NativeArray<float4x4> _matrices;
        static NativeArray<float3> _points;
        static NativeArray<byte> _verdicts;
        static NativeArray<int> _capturedBy;
        static NativeArray<byte> _valid;
        static int _outputCapacity;

        static readonly List<Prism> _query = new();
        static readonly List<Candidate> _candidates = new();
        static readonly List<Prism> _evict = new();
        static float _nextAdmission;

        // The hole behind each well slot this frame. A well index is NOT a holes-list index: the
        // wells skip null holes, so a capture's well index is resolved through this table.
        static readonly BlackHole[] _wellHoles = new BlackHole[BlackHolePhysics.NativeWells.Capacity];

        struct Candidate
        {
            public Prism Prism;
            public float DistanceSq;
        }

        static readonly System.Comparison<Candidate> _byDistance = (a, b) => a.DistanceSq.CompareTo(b.DistanceSq);

        /// <summary>Prisms currently under gravity.</summary>
        public static int BodyCount => _prisms.Count;

        /// <summary>Prisms consumed by a horizon since the last reset (session total).</summary>
        public static int CapturedTotal { get; private set; }

        /// <summary>Captures since <see cref="ResetSecondCounters"/> (the verbose report's rate).</summary>
        public static int CapturedThisSecond { get; private set; }

        /// <summary>Prisms carried through a dipole's throat since the last reset (session total).</summary>
        public static int ThroatTransitsTotal { get; private set; }

        public static void ResetSecondCounters() => CapturedThisSecond = 0;

        /// <summary>True if the prism is currently a body (diagnostics, tests).</summary>
        public static bool IsBody(Prism prism) => prism != null && _admitted.Contains(prism);

        /// <summary>
        /// Run the field for this frame: admit, prune, integrate, apply, judge. Called once per
        /// frame by <see cref="BlackHoleRegistry"/>'s driver at order 29500.
        /// </summary>
        public static void Tick(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config, float dt)
        {
            EnsureAllocated();

            var wells = new BlackHolePhysics.NativeWells();
            System.Array.Clear(_wellHoles, 0, _wellHoles.Length);
            for (int i = 0; i < holes.Count && wells.Count < BlackHolePhysics.NativeWells.Capacity; i++)
            {
                var h = holes[i];
                if (h == null) continue;
                _wellHoles[wells.Count] = h;
                wells.Add(h.ToWell(config));
            }

            bool sweep = Time.unscaledTime >= _nextAdmission;
            if (sweep)
            {
                _nextAdmission = Time.unscaledTime + config.AdmissionInterval;
                if (wells.Count > 0) Admit(holes, config);
            }

            // The cheap checks every frame; the entity and hierarchy checks on the sweep cadence
            // — the job and the bulk writers are safe against a dead entity in between.
            Prune(deep: sweep);
            int n = _prisms.Count;
            if (n == 0 || dt <= 0f) return;

            EnsureOutputCapacity(n);
            if (!PrismRenderService.TryGetGravityBodyLookup(out var bodies))
            {
                // No entity world: there is nothing to integrate against. Release everything so a
                // prism never stays "admitted" with a body nobody can read.
                ReleaseAll();
                return;
            }

            // 1. Read every pose, in parallel (read-only transform access is not root-bound).
            var read = new ReadPoseJob
            {
                Poses = _matrices,
                Valid = _valid,
            }.ScheduleReadOnly(_transforms, ReadBatch);

            // 2. Integrate every body, in parallel, in Burst.
            var integrate = new IntegrateJob
            {
                Wells = wells,
                Params = new BlackHolePhysics.StepParams
                {
                    ReleaseDamping = config.ReleaseDamping,
                    ReleaseSpeed = config.ReleaseSpeed,
                    MaxSubsteps = config.MaxSubsteps,
                },
                DeltaTime = dt,
                Entities = _entities.AsArray(),
                Valid = _valid,
                Bodies = bodies,
                Matrices = _matrices,
                Points = _points,
                Verdicts = _verdicts,
                CapturedBy = _capturedBy,
            }.Schedule(n, IntegrateBatch, read);

            // 3. Write the poses back to the transforms while the render write runs beside it:
            // both depend on the integration only (the write-back reads points and verdicts, the
            // render write reads matrices). SetTransformsBatch completes its own write.
            var writeBack = new WritePoseJob
            {
                Points = _points,
                Verdicts = _verdicts,
                Valid = _valid,
            }.Schedule(_transforms, integrate);
            PrismRenderService.SetTransformsBatch(_handles.AsArray(), _matrices, n, integrate);
            writeBack.Complete();

            var index = PrismSpatialIndex.Instance;
            if (index != null && index.IsAvailable)
                index.UpdatePositionsBatch(_indexIds.AsArray(), _points, n);

            ApplyVerdicts(index);
        }

        // ---------------- Admission ----------------

        static void Admit(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;
            int budget = config.MaxBodies - _prisms.Count;
            if (budget <= 0) return;

            for (int h = 0; h < holes.Count && budget > 0; h++)
            {
                var hole = holes[h];
                if (hole == null) continue;
                var centre = hole.transform.position;
                float radius = hole.InfluenceRadius;
                if (!(radius > 0f)) continue;

                index.QuerySphere(centre, radius, _query);
                if (_query.Count == 0) continue;

                _candidates.Clear();
                for (int i = 0; i < _query.Count; i++)
                {
                    var p = _query[i];
                    if (!IsAdmissible(p)) continue;
                    _candidates.Add(new Candidate
                    {
                        Prism = p,
                        DistanceSq = (p.transform.position - centre).sqrMagnitude,
                    });
                }
                if (_candidates.Count == 0) continue;

                // Nearest first: QuerySphere is unordered, and the budget truncates, so the
                // ranking decides WHICH mass moves. A field that decays with distance spends its
                // budget on the mass it moves most (the cradle's selector, for the same reason).
                _candidates.Sort(_byDistance);
                int take = Mathf.Min(budget, _candidates.Count);
                for (int i = 0; i < take; i++)
                {
                    if (TryAdmit(_candidates[i].Prism)) budget--;
                }
            }
        }

        static bool IsAdmissible(Prism p)
        {
            if (p == null || p.destroyed) return false;
            if (_admitted.Contains(p)) return false;
            if (!p.IsCreationComplete) return false;            // still growing in: its transform is not final
            if (p.SpatialIndexId < 0) return false;
            if (p.prismProperties is { IsSuperShielded: true }) return false;
            // A creature's or a plant's body prism is posed by its rig every frame (the fauna
            // movers contract); pulling it on its own would tear the body off the creature while
            // the creature keeps swimming. A lifeform under gravity is a whole-body question for
            // the ecology (Docs/BLACK_HOLE.md §8), not a per-prism one — excluded here.
            if (p is HealthPrism) return false;
            if (!p.gameObject.activeInHierarchy) return false;
            return PrismRenderService.IsHandleUsable(in p.RenderHandle);
        }

        static bool TryAdmit(Prism p)
        {
            // A body starts at REST relative to the world: it orbits only with the angular momentum a
            // moving hole gives it, and the frame dragging winds its infall (Docs/BLACK_HOLE.md §2).
            // Seating the component is the one structural-free write admission makes.
            if (!PrismRenderService.SetGravityBody(in p.RenderHandle, new GravityBody(), enabled: true))
                return false;

            _prisms.Add(p);
            _admitted.Add(p);
            _transforms.Add(p.transform);
            _handles.Add(p.RenderHandle);
            _entities.Add(p.RenderHandle.Entity);
            _indexIds.Add(p.SpatialIndexId);
            return true;
        }

        /// <summary>Drop a body by ledger index — swap-back on every aligned array.</summary>
        static void RemoveAt(int i, bool clearComponent)
        {
            var p = _prisms[i];
            if (p != null)
            {
                _admitted.Remove(p);
                if (clearComponent) PrismRenderService.ClearGravityBody(in p.RenderHandle);
            }
            int last = _prisms.Count - 1;
            _prisms[i] = _prisms[last];
            _prisms.RemoveAt(last);
            _transforms.RemoveAtSwapBack(i);
            _handles.RemoveAtSwapBack(i);
            _entities.RemoveAtSwapBack(i);
            _indexIds.RemoveAtSwapBack(i);
        }

        /// <summary>
        /// Bodies that died, were pooled, or lost their entity since last frame leave the set.
        /// <paramref name="deep"/> adds the hierarchy and entity checks, which cost a native call
        /// per body and are only needed at the sweep cadence.
        /// </summary>
        static void Prune(bool deep)
        {
            for (int i = _prisms.Count - 1; i >= 0; i--)
            {
                var p = _prisms[i];
                bool gone = p == null || p.destroyed || p.SpatialIndexId < 0 ||
                            p.RenderHandle.Epoch != PrismRenderService.HandleEpoch;
                if (!gone && deep)
                    gone = !p.gameObject.activeInHierarchy || !PrismRenderService.IsHandleUsable(in p.RenderHandle);
                if (gone) RemoveAt(i, clearComponent: p != null && !p.destroyed);
                else if (_indexIds[i] != p.SpatialIndexId) _indexIds[i] = p.SpatialIndexId;
            }
        }

        // ---------------- Verdicts ----------------

        static void ApplyVerdicts(PrismSpatialIndex index)
        {
            for (int i = _prisms.Count - 1; i >= 0; i--)
            {
                var p = _prisms[i];
                if (p == null) { RemoveAt(i, clearComponent: false); continue; }

                // A shielded body carries a shell pose the batch index write does not refresh.
                if (index != null && p.prismProperties is { IsShielded: true })
                    index.UpdateShellTransform(p.SpatialIndexId);

                var verdict = (BlackHolePhysics.Verdict)_verdicts[i];
                switch (verdict)
                {
                    case BlackHolePhysics.Verdict.Captured:
                    {
                        int by = _capturedBy[i];
                        var hole = by >= 0 && by < _wellHoles.Length ? _wellHoles[by] : null;

                        // A DIPOLE's sink is a throat, not a singularity (Docs/BLACK_HOLE.md §12):
                        // the body is carried through to the same point relative to the source
                        // and stays a body — velocity and all — so it falls on inward, through
                        // the source's centre, and the source drives it back out. Nothing is
                        // consumed, so the dipole conserves the mass it moves.
                        var throat = hole != null ? hole.Throat : null;
                        if (throat != null && !throat.IsDespawning)
                        {
                            p.transform.position += throat.transform.position - hole.transform.position;
                            ThroatTransitsTotal++;
                            break;
                        }

                        Transform sink = hole != null ? hole.transform : p.transform;
                        RemoveAt(i, clearComponent: true);
                        // Devastate: a shield is not an answer to a singularity. The suction's
                        // sink is the hole's own transform, so the debris converges on it as it
                        // moves (the one moving-target implosion, already a documented exception).
                        p.Consume(sink, Domains.Blue, ConsumerName, devastate: true);
                        CapturedTotal++;
                        CapturedThisSecond++;
                        break;
                    }
                    case BlackHolePhysics.Verdict.Released:
                        RemoveAt(i, clearComponent: true);
                        break;
                }
            }
        }

        // ---------------- The jobs ----------------

        /// <summary>
        /// Every body's world pose, read-only (scheduled with <c>ScheduleReadOnly</c>, so it is not
        /// serialised per root hierarchy). A dead transform is flagged, not read.
        /// </summary>
        [BurstCompile]
        struct ReadPoseJob : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<float4x4> Poses;
            [WriteOnly] public NativeArray<byte> Valid;

            public void Execute(int i, TransformAccess transform)
            {
                if (!transform.isValid)
                {
                    Poses[i] = float4x4.identity;
                    Valid[i] = 0;
                    return;
                }
                Poses[i] = transform.localToWorldMatrix;
                Valid[i] = 1;
            }
        }

        /// <summary>
        /// One body per index: step it under every well, then emit the render matrix (the read
        /// pose with its translation replaced — exact for a translation-only change), the index
        /// point and the verdict. The verdict and the capturing well go to the main thread.
        /// </summary>
        [BurstCompile]
        struct IntegrateJob : IJobParallelFor
        {
            public BlackHolePhysics.NativeWells Wells;
            public BlackHolePhysics.StepParams Params;
            public float DeltaTime;

            [ReadOnly] public NativeArray<Entity> Entities;
            [ReadOnly] public NativeArray<byte> Valid;
            // Entities are unique per ledger slot, so parallel writes never alias.
            [NativeDisableParallelForRestriction] public ComponentLookup<GravityBody> Bodies;

            /// <summary>In: the pose <see cref="ReadPoseJob"/> read. Out: the render matrix.</summary>
            public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float3> Points;
            [WriteOnly] public NativeArray<byte> Verdicts;
            [WriteOnly] public NativeArray<int> CapturedBy;

            public void Execute(int i)
            {
                var e = Entities[i];
                if (Valid[i] == 0 || e == Entity.Null || !Bodies.HasComponent(e))
                {
                    // A dead transform or entity: nothing to move, nothing to say. The main
                    // thread's prune drops the slot; the bulk writers skip a null entity, and
                    // the write-back skips an invalid slot.
                    Matrices[i] = float4x4.identity;
                    Points[i] = float3.zero;
                    Verdicts[i] = (byte)BlackHolePhysics.Verdict.Free;
                    CapturedBy[i] = -1;
                    return;
                }

                var m = Matrices[i];
                float3 p = m.c3.xyz;

                var body = Bodies[e];
                float3 v = body.Velocity;
                var verdict = BlackHolePhysics.Step(ref p, ref v, in Wells, in Params, DeltaTime, out int by);

                body.Velocity = v;
                body.CapturedBy = by;
                body.Flags = verdict == BlackHolePhysics.Verdict.Captured ? GravityBody.FlagCaptured
                    : verdict == BlackHolePhysics.Verdict.Released ? GravityBody.FlagReleased
                    : 0u;
                Bodies[e] = body;

                m.c3 = new float4(p, 1f);
                Matrices[i] = m;
                Points[i] = p;
                Verdicts[i] = (byte)verdict;
                CapturedBy[i] = by;
            }
        }

        /// <summary>
        /// The integrated positions back onto the transforms. A captured body keeps its pose: it
        /// is consumed on the main thread, and its implosion starts where it crossed in.
        /// </summary>
        [BurstCompile]
        struct WritePoseJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float3> Points;
            [ReadOnly] public NativeArray<byte> Verdicts;
            [ReadOnly] public NativeArray<byte> Valid;

            public void Execute(int i, TransformAccess transform)
            {
                if (!transform.isValid || Valid[i] == 0) return;
                if (Verdicts[i] == (byte)BlackHolePhysics.Verdict.Captured) return;
                transform.position = Points[i];
            }
        }

        // ---------------- Memory ----------------

        static void EnsureAllocated()
        {
            if (_transforms.isCreated) return;
            _transforms = new TransformAccessArray(InitialCapacity);
            _handles = new NativeList<PrismRenderHandle>(InitialCapacity, Allocator.Persistent);
            _entities = new NativeList<Entity>(InitialCapacity, Allocator.Persistent);
            _indexIds = new NativeList<int>(InitialCapacity, Allocator.Persistent);
        }

        static void EnsureOutputCapacity(int n)
        {
            if (_outputCapacity >= n && _matrices.IsCreated) return;
            int cap = Mathf.Max(InitialCapacity, Mathf.NextPowerOfTwo(n));
            DisposeOutputs();
            _matrices = new NativeArray<float4x4>(cap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _points = new NativeArray<float3>(cap, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _verdicts = new NativeArray<byte>(cap, Allocator.Persistent);
            _capturedBy = new NativeArray<int>(cap, Allocator.Persistent);
            _valid = new NativeArray<byte>(cap, Allocator.Persistent);
            _outputCapacity = cap;
        }

        static void DisposeOutputs()
        {
            if (_matrices.IsCreated) _matrices.Dispose();
            if (_points.IsCreated) _points.Dispose();
            if (_verdicts.IsCreated) _verdicts.Dispose();
            if (_capturedBy.IsCreated) _capturedBy.Dispose();
            if (_valid.IsCreated) _valid.Dispose();
            _outputCapacity = 0;
        }

        static void DisposeLedger()
        {
            if (_transforms.isCreated) _transforms.Dispose();
            if (_handles.IsCreated) _handles.Dispose();
            if (_entities.IsCreated) _entities.Dispose();
            if (_indexIds.IsCreated) _indexIds.Dispose();
        }

        // ---------------- Lifecycle ----------------

        /// <summary>
        /// Hand every body back to static mass (teardown: the prisms are still alive) and free
        /// the native memory.
        /// </summary>
        internal static void ReleaseAll()
        {
            for (int i = _prisms.Count - 1; i >= 0; i--)
                RemoveAt(i, clearComponent: _prisms[i] != null && !_prisms[i].destroyed);
            _prisms.Clear();
            _admitted.Clear();
            _query.Clear();
            _candidates.Clear();
            _evict.Clear();
            DisposeLedger();
            DisposeOutputs();
        }

        /// <summary>
        /// Play-mode (re)entry: the prisms of the previous session are gone, so drop the
        /// bookkeeping without touching them, and free native memory the last session may have
        /// left (domain reload disabled is the case that bites).
        /// </summary>
        internal static void ResetOnLoad()
        {
            _prisms.Clear();
            _admitted.Clear();
            _query.Clear();
            _candidates.Clear();
            _evict.Clear();
            _nextAdmission = 0f;
            CapturedTotal = 0;
            CapturedThisSecond = 0;
            ThroatTransitsTotal = 0;
            DisposeLedger();
            DisposeOutputs();
        }
    }
}
