using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.Gameplay;
using System;
using System.Linq;


namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bit flags for prism status, packed into a single byte.
    /// The Burst job only checks bits 0-1 (IsActive + Destroyed).
    /// Bits 2-3 (shields) are only read on the main thread for the small hit set.
    /// </summary>
    public static class PrismFlags
    {
        public const byte IsActive       = 1 << 0; // bit 0
        public const byte Destroyed      = 1 << 1; // bit 1
        public const byte IsShielded     = 1 << 2; // bit 2
        public const byte IsSuperShielded = 1 << 3; // bit 3

        // Collider-LOD state: this slot was within the LOD radius of a focus at the
        // last classification pass (LodClassifyJob). Owned exclusively by
        // RunLodClassification - not part of JobSkipMask semantics, and reset for
        // free when Register writes fresh flags into a reused slot.
        public const byte LodNear        = 1 << 4; // bit 4

        // Mask for the Burst job's early-exit check:
        // Active (bit 0 set) AND not destroyed (bit 1 clear) → value == 0x01
        public const byte JobSkipMask    = IsActive | Destroyed;
        public const byte JobPassValue   = IsActive; // exactly active, not destroyed

        // Either shield bit — the shell-contact tier's candidate filter.
        public const byte AnyShieldMask  = IsShielded | IsSuperShielded;
    }

    /// <summary>
    /// HOT data: read by every Execute() call in the Burst spatial query job.
    /// 16 bytes - exactly 4 prisms per 64-byte cache line, zero waste.
    ///
    /// Layout:
    ///   offset 0:  Position.x  (4B)
    ///   offset 4:  Position.y  (4B)
    ///   offset 8:  Position.z  (4B)
    ///   offset 12: Flags       (1B)  bit-packed status
    ///   offset 13: _pad        (3B)  alignment to 16B
    ///
    /// For 3000 prisms: 48 KB - fits comfortably in L2,
    /// and on devices with 64KB+ L1D (Snapdragon 8 Gen 2, Apple M-series), in L1.
    /// </summary>
    public struct PrismSpatialData
    {
        public float3 Position; // 12B
        public byte Flags;      // 1B (see PrismFlags)
        public byte _pad0;      // 1B
        public byte _pad1;      // 1B
        public byte _pad2;      // 1B
        // Total: 16B - exactly 4 per 64B cache line
    }

    /// <summary>
    /// COLD data: only read on the main thread for prisms that pass the spatial filter.
    /// Typically a few dozen per frame as the AOE sphere grows - not a cache concern.
    ///
    /// Layout:
    ///   offset 0: Volume  (4B)
    ///   offset 4: Domain  (4B)
    ///   Total: 8B
    /// </summary>
    public struct PrismDamageData
    {
        public float Volume; // 4B
        public int Domain;   // 4B
        // Total: 8B
    }

    /// <summary>
    /// Cell-volume summation view data - one entry per slot, packed for the Burst
    /// <see cref="CellVolumeSumJob"/> that replaces Cell's managed per-prism volume
    /// recompute (the old 8000-prisms-per-frame slice was a ~10 ms reader-attributed
    /// frame spike at high prism counts; see Docs/PERFORMANCE_OPTIMIZATION.md).
    ///
    /// Distinct from <see cref="PrismDamageData"/> on purpose: the damage view's
    /// Volume/Domain are registration-time snapshots whose staleness is part of the
    /// tested AOE behavior ("Known gaps" in Docs/SPATIAL_INDEX.md), while this view
    /// must be LIVE - Volume mirrors Prism.CachedVolume (pushed by RefreshVolumeCache,
    /// O(growing)/frame), DomainSlot follows steals via ForwardDomainChangeToCell,
    /// and CellId/EnvMass mirror Cell.AddBlock/RemoveBlock membership exactly
    /// (Cell is the single writer of the binding, so the two cannot diverge).
    ///
    /// Layout:
    ///   offset 0: Volume     (4B)  live CachedVolume mirror
    ///   offset 4: CellId     (2B)  volume-membership cell id, -1 = unbound
    ///   offset 6: DomainSlot (1B)  live domain slot (0 Jade / 1 Ruby / 2 Gold / 3 Blue)
    ///   offset 7: EnvMass    (1B)  1 = environment mass (cell trackedBlocks mirror)
    ///   Total: 8B - 8 entries per 64B cache line
    /// </summary>
    public struct PrismCellData
    {
        public float Volume;    // 4B
        public short CellId;    // 2B
        public byte DomainSlot; // 1B
        public byte EnvMass;    // 1B
        // Total: 8B
    }

    /// <summary>Which analytic shield shell a slot currently presents to probes.</summary>
    public static class ShellKind
    {
        public const byte None = 0;
        public const byte Octahedron = 1; // SHIELDED: L1 ball circumscribing the authored box
        public const byte Stella = 2;     // SUPER-SHIELDED: union of two tetrahedra (non-convex)
    }

    /// <summary>
    /// Shell view (cold): the world pose of a shielded prism's analytic shell, read
    /// by <see cref="ShellContactQueryJob"/> only for slots whose flags carry a
    /// shield bit. Refreshed at shield engage/disengage (UpdateShieldState /
    /// Register), on growth steps (RefreshVolumeCache → UpdateShellTransform), and
    /// for movers (NotifyPositionChanged → UpdateShellTransform). Kind is cleared on
    /// Unregister so slot reuse can never inherit a stale shell.
    ///
    /// SemiAxes are WORLD semi-axes: shieldScale · authoredHalfExtents ⊙ lossyScale.
    /// Valid because prism transforms are rigid rotation × axis-aligned scale (no
    /// shear in any spawn path).
    /// </summary>
    public struct PrismShellData
    {
        public quaternion Rotation; // 16B
        public float3 Center;       // 12B  world shell center (TransformPoint(boxCollider.center))
        public float3 SemiAxes;     // 12B  world semi-axes
        public float BoundRadius;   // 4B   conservative bounding-sphere radius about Center
        public byte Kind;           // 1B   ShellKind
        // 3B pad — 48B total
    }

    /// <summary>Probe shape classification for the shell-contact query.</summary>
    public static class ShellProbeKind
    {
        public const byte Sphere = 0;
        public const byte Capsule = 1;
        public const byte Box = 2;
    }

    /// <summary>
    /// One collision probe (a vessel hull collider or skimmer sphere/capsule) in
    /// world space, rebuilt each frame by <see cref="PrismShellContactManager"/>
    /// from the live collider transforms.
    /// </summary>
    public struct ShellProbe
    {
        public float3 A;           // sphere center / capsule endpoint 0 / box center
        public float3 B;           // capsule endpoint 1 (unused otherwise)
        public float3 E1, E2, E3;  // box half-edge world vectors (unused otherwise)
        public float Radius;       // sphere/capsule world radius
        public float3 BoundCenter; // conservative bounding sphere for the coarse reject
        public float BoundRadius;
        public int OwnerSlot;      // index into the manager's registered-owner list
        public byte Kind;          // ShellProbeKind
    }

    /// <summary>One probe-vs-shell overlap found by the query job.</summary>
    public struct ShellContactHit
    {
        public int ProbeIndex;
        public int PrismIndex;
    }

    /// <summary>
    /// Burst query for the shielded-prism analytic-collision tier: scans the hot
    /// spatial array exactly like <see cref="AOESpatialQueryJob"/>, but only slots
    /// carrying a shield flag proceed to the exact shell narrowphase
    /// (<see cref="ShieldShellMath"/> — octahedron, or the NON-CONVEX two-tet
    /// stella union: a probe touching a spike tip overlaps; a probe threaded
    /// between spikes inside the bounding box does not).
    /// </summary>
    [BurstCompile]
    public struct ShellContactQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Prisms;
        [ReadOnly] public NativeArray<PrismShellData> Shells;
        [ReadOnly] public NativeArray<ShellProbe> Probes;
        [ReadOnly] public int ProbeCount;

        public NativeList<ShellContactHit>.ParallelWriter Hits;

        public void Execute(int index)
        {
            var p = Prisms[index];
            if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) return;
            if ((p.Flags & PrismFlags.AnyShieldMask) == 0) return;

            var shell = Shells[index];
            if (shell.Kind == ShellKind.None) return;

            bool frameBuilt = false;
            ShieldShellMath.ShellFrame frame = default;

            for (int i = 0; i < ProbeCount; i++)
            {
                var probe = Probes[i];
                float reach = probe.BoundRadius + shell.BoundRadius;
                if (math.distancesq(probe.BoundCenter, shell.Center) > reach * reach)
                    continue;

                if (!frameBuilt)
                {
                    frame = ShieldShellMath.CreateFrame(shell.Center, shell.Rotation, shell.SemiAxes);
                    frameBuilt = true;
                }

                bool octa = shell.Kind == ShellKind.Octahedron;
                bool hit;
                switch (probe.Kind)
                {
                    case ShellProbeKind.Sphere:
                        hit = octa
                            ? ShieldShellMath.SphereOverlapsOcta(in frame, probe.A, probe.Radius)
                            : ShieldShellMath.SphereOverlapsStella(in frame, probe.A, probe.Radius);
                        break;
                    case ShellProbeKind.Capsule:
                        hit = octa
                            ? ShieldShellMath.CapsuleOverlapsOcta(in frame, probe.A, probe.B, probe.Radius)
                            : ShieldShellMath.CapsuleOverlapsStella(in frame, probe.A, probe.B, probe.Radius);
                        break;
                    default:
                        hit = octa
                            ? ShieldShellMath.BoxOverlapsOcta(in frame, probe.A, probe.E1, probe.E2, probe.E3)
                            : ShieldShellMath.BoxOverlapsStella(in frame, probe.A, probe.E1, probe.E2, probe.E3);
                        break;
                }

                if (hit)
                    Hits.AddNoResize(new ShellContactHit { ProbeIndex = i, PrismIndex = index });
            }
        }
    }

    /// <summary>
    /// One prism hit by an AOE query frame: slot index plus the unit blast
    /// direction (blast origin → prism), computed in-job so the main thread
    /// never pays a managed sqrt per hit.
    /// </summary>
    public struct AOEHit
    {
        public int Index;
        public float3 ImpactDir;
    }

    /// <summary>
    /// Burst-compiled spatial query over cache-line-packed PrismSpatialData.
    /// Each Execute() reads exactly 16B (one PrismSpatialData entry).
    /// With 4 entries per cache line, a sequential scan of 3000 prisms
    /// touches only 750 cache lines (48KB).
    ///
    /// The query sphere (Center/RadiusSq) belongs to the SPHERICAL explosion: a
    /// stationary Center with a growing radius, so each frame's volume strictly
    /// contains the previous frame's. BlastOrigin is the emission point every hit's
    /// impact direction radiates from, so struck prisms fly outward with the blast.
    ///
    /// The conic explosion does NOT use this job - its volume translates rather than
    /// grows, so it queries an exact cone slab via <see cref="AOEConicSweepQueryJob"/>.
    /// </summary>
    [BurstCompile]
    public struct AOESpatialQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Prisms;
        [ReadOnly] public float3 Center;
        [ReadOnly] public float RadiusSq;
        [ReadOnly] public float3 BlastOrigin;

        public NativeList<AOEHit>.ParallelWriter Hits;

        public void Execute(int index)
        {
            var p = Prisms[index];

            // Single byte check: must be active (bit 0) and not destroyed (bit 1)
            if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) return;

            float distSq = math.lengthsq(p.Position - Center);
            if (distSq > RadiusSq) return;

            // Unit vector without a scalar sqrt: Burst lowers math.rsqrt to the
            // hardware reciprocal-sqrt on the squared length it already has,
            // vectorized across the scan. The max() guards a prism sitting
            // exactly on the origin (degenerates to a ~zero vector, no NaN).
            float3 diff = p.Position - BlastOrigin;
            float3 dir = diff * math.rsqrt(math.max(math.lengthsq(diff), 1e-12f));

            Hits.AddNoResize(new AOEHit { Index = index, ImpactDir = dir });
        }
    }

    /// <summary>
    /// One explosion hit deferred by the per-frame budget, waiting in the
    /// explosion's backlog. Carries the slot's occupancy GENERATION as an identity
    /// guard: registry slots are recycled through the free list and a deferred hit
    /// may wait many frames, so the raw index alone can silently alias onto a
    /// different prism — or onto the same pooled instance living a new life — by the
    /// time it drains. A generation stamp catches both; an object reference catches
    /// only the first (and a Unity-destroyed reference compares fake-null, which
    /// would disable the check in exactly the case it exists for).
    /// </summary>
    public struct PendingExplosionHit
    {
        public int Index;
        public int Generation;   // _slotGeneration[Index] captured at defer time
        public float3 ImpactDir;
    }

    /// <summary>
    /// Burst-compiled spatial query for the CONIC explosion: an exact test against
    /// the swept blast, sliced into the axial slab this frame newly covers.
    ///
    /// Why not a sphere. The conic explosion used to derive one ball per frame
    /// riding the cone's leading base plane. That family of balls is *tangent* to
    /// the rendered cone - its envelope half-angle asin(k) beats the cone's atan(k)
    /// by only 0.37% at the Dolphin's min charge (k = 1/12) - so it has almost no
    /// coverage margin: any discretisation leaves a scalloped shell along the mantle
    /// plus a solid never-sampled plug at the muzzle, and the ball simultaneously
    /// over-reaches a full hemisphere PAST the visible tip (which is what let a
    /// super-shielded prism outside the cone abort the blast).
    ///
    /// The slab test has none of that. Slice [SliceMin, SliceMax] is the axial
    /// interval between the previous frame's height and this frame's, so the union
    /// over the explosion's frames is EXACTLY the swept solid - no gaps at any
    /// frame rate and no over-reach.
    ///
    /// The cross-section is a CAPSULE (a 2D stadium), not a disc: a circle of the
    /// CORE radius swept along <see cref="GapeAxis"/>, the axis the emitting vessel's
    /// jaws open across. Both are self-similar in the axial depth s, so the two
    /// tangents below are invariant for the whole blast:
    ///
    ///     core half-width  = CoreTanHalfAngle * s      (never grows with charge)
    ///     gape half-length = TanGapePerUnit   * s      (all of what charge buys)
    ///
    /// Their sum is the rendered cone's base radius, so the capsule is inscribed in
    /// the visible cone and touches it exactly along the gape axis. TanGapePerUnit
    /// == 0 collapses this to the original circular cone test, term for term.
    ///
    /// Apex is both the blast origin and the sweep origin, so the apex-relative
    /// vector the containment test already computed doubles as the impact direction.
    /// </summary>
    [BurstCompile]
    public struct AOEConicSweepQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Prisms;
        [ReadOnly] public float3 Apex;
        [ReadOnly] public float3 Axis;             // unit vector, blast opening direction
        [ReadOnly] public float3 GapeAxis;         // unit vector perpendicular to Axis - the capsule's long axis
        [ReadOnly] public float SliceMin;          // axial distance already swept (previous frame's height)
        [ReadOnly] public float SliceMax;          // this frame's height
        [ReadOnly] public float CoreTanHalfAngle;  // coreRadius / height - the capsule's RADIUS per unit depth
        [ReadOnly] public float TanGapePerUnit;    // (baseRadius - coreRadius) / height - its HALF-LENGTH per unit depth

        public NativeList<AOEHit>.ParallelWriter Hits;

        public void Execute(int index)
        {
            var p = Prisms[index];

            // Same single-byte liveness gate as the spherical query.
            if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) return;

            float3 rel = p.Position - Apex;

            // Axial band: only the slab this frame newly covers.
            float s = math.dot(rel, Axis);
            if (s < SliceMin || s > SliceMax) return;

            // Capsule band: distance from the cross-section's SEGMENT, not from the
            // axis. Clamping onto the segment first is what makes the ends round -
            // the same point-to-segment distance a CapsuleCollider uses, so the Burst
            // volume and the trigger volume are the same shape by construction.
            float3 radial = rel - Axis * s;
            float halfLength = TanGapePerUnit * s;
            float along = math.dot(radial, GapeAxis);
            float3 offAxis = radial - GapeAxis * math.clamp(along, -halfLength, halfLength);

            float coreRadius = CoreTanHalfAngle * s;
            if (math.lengthsq(offAxis) > coreRadius * coreRadius) return;

            // Impact direction radiates from the apex - reuse rel, no extra work.
            float3 dir = rel * math.rsqrt(math.max(math.lengthsq(rel), 1e-12f));

            Hits.AddNoResize(new AOEHit { Index = index, ImpactDir = dir });
        }
    }

    /// <summary>
    /// Burst-compiled spatial query for the CYLINDRICAL explosion — the Scarab's cavitation
    /// punch. The volume is a flat circular PLATE of constant radius that starts centred on the
    /// hull with its face normal along the dash and sweeps that normal; there is no apex and no
    /// half-angle, so the cone job cannot express it (its cross-section is proportional to depth,
    /// which is the one thing this shape refuses to do).
    ///
    /// Coverage follows the cone job's contract exactly: slice [SliceMin, SliceMax] is the axial
    /// interval between the previous frame's sweep depth and this frame's, so the union over the
    /// blast's frames is EXACTLY the swept cylinder — no gaps at any frame rate and no reach past
    /// the visible end cap.
    ///
    /// THE IMPACT DIRECTION IS THE SWEEP AXIS, not a radial from an origin. A plate does not
    /// radiate; it shoves. Every prism it claims leaves along <see cref="Axis"/> at the blast's
    /// own speed, so the debris field travels with the punch instead of blooming out of it — the
    /// direction is a constant, which is also why this job does no per-hit normalize at all.
    /// </summary>
    [BurstCompile]
    public struct AOECylinderSweepQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Prisms;
        [ReadOnly] public float3 Origin;    // the plate's starting centre (the hull)
        [ReadOnly] public float3 Axis;      // unit vector, the plate's face normal = sweep direction
        [ReadOnly] public float SliceMin;   // axial depth already swept (previous frame)
        [ReadOnly] public float SliceMax;   // this frame's sweep depth
        [ReadOnly] public float RadiusSq;   // the plate's radius, squared — CONSTANT along the sweep

        public NativeList<AOEHit>.ParallelWriter Hits;

        public void Execute(int index)
        {
            var p = Prisms[index];

            // Same single-byte liveness gate as the spherical and conic queries.
            if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) return;

            float3 rel = p.Position - Origin;

            // Axial band: only the slab this frame newly covers.
            float s = math.dot(rel, Axis);
            if (s < SliceMin || s > SliceMax) return;

            // Radial band: constant radius about the axis — a true cylinder, flat end caps.
            float3 radial = rel - Axis * s;
            if (math.lengthsq(radial) > RadiusSq) return;

            Hits.AddNoResize(new AOEHit { Index = index, ImpactDir = Axis });
        }
    }

    /// <summary>
    /// Burst-compiled collider-LOD classification over the packed hot array.
    /// Maintains the per-slot <see cref="PrismFlags.LodNear"/> bit and emits only
    /// TRANSITIONS (slots whose near/far state changed since the last pass), so the
    /// managed apply that follows is O(changed) instead of O(population). With
    /// Reconcile set (first sweep / LOD re-enable) every live slot is emitted and
    /// the bits are rewritten from scratch. Single-threaded IJob: 25k entries is
    /// ~0.1-0.3 ms in Burst, and ordered appends keep the output deterministic.
    /// </summary>
    [BurstCompile]
    public struct LodClassifyJob : IJob
    {
        public NativeArray<PrismSpatialData> Prisms; // read-write: maintains the LodNear bit
        [ReadOnly] public NativeArray<float3> Centers;
        public int EntryCount;
        public int CenterCount;
        public float NearRadiusSq; // enter threshold: a FAR prism becomes near inside this
        public float FarRadiusSq;  // exit threshold (≥ NearRadiusSq): a NEAR prism becomes far outside this
        public bool Reconcile;
        public NativeList<int> BecameNear;
        public NativeList<int> BecameFar;

        public void Execute()
        {
            for (int i = 0; i < EntryCount; i++)
            {
                var p = Prisms[i];
                if ((p.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;

                bool wasNear = (p.Flags & PrismFlags.LodNear) != 0;

                // Hysteresis: entering the bubble uses the tight radius, leaving it
                // the wide one - prisms in the annulus keep their prior state, so
                // the boundary of a MOVING focus stops emitting near/far flip
                // transitions (and collider re-toggles) every tick. A reconcile has
                // no trusted prior state: classify by the wide radius (collider-on
                // is the safe direction).
                float thresholdSq = (Reconcile || wasNear) ? FarRadiusSq : NearRadiusSq;

                bool near = false;
                for (int cI = 0; cI < CenterCount; cI++)
                {
                    if (math.distancesq(p.Position, Centers[cI]) <= thresholdSq)
                    {
                        near = true;
                        break; // near at least one focus - no need to test the rest
                    }
                }

                if (near == wasNear && !Reconcile) continue;

                if (near)
                {
                    p.Flags |= PrismFlags.LodNear;
                    BecameNear.Add(i);
                }
                else
                {
                    p.Flags = (byte)(p.Flags & ~PrismFlags.LodNear);
                    BecameFar.Add(i);
                }
                Prisms[i] = p;
            }
        }
    }

    /// <summary>
    /// Burst-compiled per-cell volume summation over the packed arrays - the
    /// compute half of Cell.EnsureVolumeFresh ("volume is the spine"). One linear
    /// pass filters slots bound to the target cell (live, not destroyed) and
    /// accumulates the exact sums the old managed per-prism pass produced:
    /// all-source volume by domain, environment volume by domain, environment
    /// volume inside the nucleus by domain, plus the three totals. ~0.1-0.3 ms
    /// at 25k entries vs ~10 ms/frame for the managed 8000-prism slice it
    /// replaces (same collapse as LodClassifyJob). Runs synchronously (.Run()),
    /// like every other query in this index - no job/mutation races.
    /// </summary>
    [BurstCompile]
    public struct CellVolumeSumJob : IJob
    {
        [ReadOnly] public NativeArray<PrismSpatialData> Spatial;
        [ReadOnly] public NativeArray<PrismCellData> CellData;
        public int EntryCount;
        public short CellId;
        public float3 Centre;
        public float NucleusRadiusSqr;
        public NativeArray<float> Results; // layout: PrismSpatialIndex.CellVolume* constants

        public void Execute()
        {
            for (int i = 0; i < PrismSpatialIndex.CellVolumeResultCount; i++)
                Results[i] = 0f;

            for (int i = 0; i < EntryCount; i++)
            {
                var cd = CellData[i];
                if (cd.CellId != CellId) continue;
                var s = Spatial[i];
                if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                float v = cd.Volume;
                if (v <= 0f) continue; // destroyed / not yet grown

                int slot = cd.DomainSlot;
                Results[PrismSpatialIndex.CellVolumeBySlot + slot] += v;
                Results[PrismSpatialIndex.CellVolumeTotal] += v;

                if (cd.EnvMass == 0) continue; // fauna bodies: volume-only mass

                Results[PrismSpatialIndex.CellEnvVolumeBySlot + slot] += v;
                Results[PrismSpatialIndex.CellEnvVolumeTotal] += v;

                // Node control vs feeding ground: environment mass inside the
                // nucleus claims control; everything outside is edible prey.
                // Without a nucleus zone the whole cell is the feeding ground
                // (exterior == environment total, OpposingVolume's else-branch).
                if (NucleusRadiusSqr > 0f &&
                    math.distancesq(s.Position, Centre) <= NucleusRadiusSqr)
                    Results[PrismSpatialIndex.CellNucleusEnvVolumeBySlot + slot] += v;
                else
                    Results[PrismSpatialIndex.CellExteriorEnvVolumeTotal] += v;
            }
        }
    }

    /// <summary>
    /// THE canonical spatial index of all live prism mass. One registration
    /// lifecycle, multiple query views - see Docs/SPATIAL_INDEX.md before adding
    /// any new spatial query against prisms (Physics.OverlapSphere / CheckBox
    /// against prisms is an anti-pattern; query this index instead).
    ///
    /// Views served:
    ///   1. AOE damage    - Burst brute-force scan over the hot array, in two
    ///                      shapes: a sphere for the spherical explosion
    ///                      (ExplosionImpactor.ProcessBatchFrame) and an exact
    ///                      cone slab for the conic one (ProcessBatchConeFrame).
    ///                      Work an explosion cannot afford within its per-frame
    ///                      budget is deferred to a backlog and resolved by
    ///                      DrainPendingExplosionDamage - see "The AOE damage
    ///                      budget" in Docs/SPATIAL_INDEX.md.
    ///   2. Occupancy     - bucket hash grid + reservation set. Growth systems
    ///                      (GyroidAssembler / WallAssembler / SchwarzPAssembler)
    ///                      call TryReserve at the grow DECISION, before
    ///                      Instantiate - this closes the race that
    ///                      Physics.CheckBox could never close (prism colliders
    ///                      are disabled for the first Prism.waitTime seconds
    ///                      after spawn).
    ///   3. Neighborhood  - QuerySphere (gather live prisms in range) and
    ///                      IsAnyPrismWithin (boolean probe) serve fauna senses
    ///                      (LightFauna / Boid), assembler mate-finding
    ///                      (GyroidAssembler / WallAssembler) and trail passives
    ///                      (ScoutTrailPrismScaler) - no physics broadphase, no
    ///                      per-collider GetComponent, no scratch-array
    ///                      truncation. See Docs/SPATIAL_INDEX.md.
    ///   4. Cell density  - Register/MarkRestored file each prism into its
    ///                      containing cell's per-domain density grids
    ///                      (Cell.AddBlock); MarkDestroyed/Unregister remove it.
    ///                      The coarse view rides the same lifecycle stream as
    ///                      the fine views, so they cannot diverge (Phase 3).
    ///
    /// Data layout (hot/cold split):
    ///   _spatial[i]  - PrismSpatialData (16B) - read by Burst job for ALL prisms
    ///   _damage[i]   - PrismDamageData  (8B)  - read on main thread for HIT prisms only
    ///   _cellData[i] - PrismCellData    (8B)  - cell-volume summation view (CellVolumeSumJob)
    ///   _prisms[i]   - Prism reference         - managed array for applying damage
    ///   _buckets     - int3 bucket key → index - incremental, prisms are mostly static
    ///
    /// The Burst job scans only _spatial, keeping the working set tight.
    /// Domain/shield/volume data in _damage is never loaded into cache during the scan -
    /// it's only touched for the small set of prisms that actually got hit.
    ///
    /// Registration lifecycle (all main-thread):
    ///   Assembler.GetGrowthInfo()  → TryReserve(pos) BEFORE Instantiate (growth only)
    ///   Prism.CreateBlockCoroutine → Register(prism) → stores index on Prism,
    ///                                consumes the matching reservation, binds the
    ///                                containing cell's density grids
    ///   Prism.SetupDestruction     → MarkDestroyed(index) → AOE skips, bucket
    ///                                freed, cell grids release the prism
    ///   Prism.Restore              → MarkRestored(index) → re-enters AOE +
    ///                                bucket + cell grids
    ///   Prism.OnDisable/OnDestroy  → Unregister(index) → frees slot, releases
    ///                                the cell binding
    ///   PrismTeamManager (steal)   → ForwardDomainChangeToCell(index) re-files
    ///                                the prism in its cell's per-domain grids
    ///   PrismStateManager          → UpdateShieldState(index, ...) on state change
    ///   Assembler movers / fauna   → UpdatePosition(index, pos) - anything that
    ///                                moves a registered prism (gyroid/wall bond
    ///                                steering, fauna body prisms swimming) must
    ///                                keep the stored position honest
    /// </summary>
    public class PrismSpatialIndex : Singleton<PrismSpatialIndex>
    {
        private const int INITIAL_CAPACITY = 4096;
        private const int JOB_BATCH_SIZE = 256;

        /// <summary>
        /// Edge length of the occupancy hash-grid buckets. Sized to the gyroid
        /// bond spacing (~8m: |DeltaPosition| ≈ 2.7 × separationDistance 3) so an
        /// occupancy probe of radius ≤ half-spacing touches at most 8 buckets.
        /// </summary>
        public const float BucketSizeMeters = 8f;

        /// <summary>Quantization step for reservation keys (half bond spacing).</summary>
        private const float ReservationQuantum = 4f;

        // --- Cell-volume summation view: result layout (CellVolumeSumJob) ---
        // Domain slots are 0 Jade / 1 Ruby / 2 Gold / 3 Blue, matching
        // Cell's published dictionary order.
        public const int CellDomainSlotCount = 4;
        public const int CellVolumeBySlot = 0;            // [0..3]  all-source volume by domain slot
        public const int CellEnvVolumeBySlot = 4;         // [4..7]  environment volume by domain slot
        public const int CellNucleusEnvVolumeBySlot = 8;  // [8..11] environment volume inside the nucleus by slot
        public const int CellVolumeTotal = 12;
        public const int CellEnvVolumeTotal = 13;
        public const int CellExteriorEnvVolumeTotal = 14;
        public const int CellVolumeResultCount = 15;

        /// <summary>
        /// Safety net for reservations that are claimed but never confirmed by a
        /// Register (spawn skipped, prism AOE-killed inside Prism.waitTime, caller
        /// abandoned the GrowthInfo). Spawn-to-register is waitTime (0.6s) plus one
        /// flora grow cadence, so 5s is comfortably past any legitimate confirm.
        /// </summary>
        public const float ReservationTtlSeconds = 5f;

        /// <summary>
        /// Maximum prisms an explosion may DAMAGE per frame.
        /// Spreading damage across frames prevents catastrophic frame spikes
        /// (e.g. 2000+ prisms destroyed in one frame → 426ms).
        ///
        /// The budget bounds COST, not coverage. It is spent only on an actual
        /// <see cref="Prism.Damage"/> call - a dead slot, a super-shield block, or a
        /// same-domain shield activation resolves for free, so friendly mass sharing
        /// the blast can no longer starve enemy mass out of the budget.
        ///
        /// Over-budget hits are NOT dropped: they are claimed into the explosion's
        /// alreadyHit set and pushed onto its pending backlog, which is drained FIFO
        /// on later frames (and past the end of the visual, see
        /// <see cref="DrainPendingExplosionDamage"/>). The previous contract - skip
        /// without claiming and trust "the Burst job will re-find these prisms next
        /// frame" - is only sound while the query volume is NESTED frame to frame.
        /// That holds for the spherical explosion (fixed centre, growing radius) but
        /// is false for the conic explosion, whose volume TRANSLATES: its slab
        /// advances past skipped prisms and never returns, so every deferred prism
        /// was permanently undamaged. That was the "prisms inside the cone survive
        /// the blast" bug.
        /// </summary>
        private const int MAX_NEW_HITS_PER_FRAME = 48;

        /// <summary>
        /// Benchmark/diagnostic override of the per-frame damage budget (0 = the
        /// authored default above). The budget was sized for the CPU-per-effect era;
        /// the stress rig lifts it to measure what the clock-material system can
        /// take UNWEAKENED — the wavefront then destroys prisms the frame it reaches
        /// them instead of trickling at 48/frame. Gameplay never sets this.
        /// </summary>
        public static int DamageBudgetPerFrameOverride = 0;

        // Benchmark-harness override; a play exit mid-run must not leave gameplay unthrottled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBudgetOverride() => DamageBudgetPerFrameOverride = 0;

        static int EffectiveDamageBudget =>
            DamageBudgetPerFrameOverride > 0 ? DamageBudgetPerFrameOverride : MAX_NEW_HITS_PER_FRAME;

        /// <summary>
        /// Upper bound on backlog entries a single frame may dequeue — 8× the damage
        /// budget, tracking any override. Entries whose prism died (or whose slot was
        /// recycled) resolve for free, so without this a queue full of dead entries
        /// would be walked in one frame.
        /// </summary>
        static int EffectiveDrainExamined
        {
            get
            {
                // long: an int.MaxValue override must not wrap the *8.
                long scaled = (long)EffectiveDamageBudget * 8;
                return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
            }
        }

        /// <summary>
        /// Sentinel for "no generation check" - used by same-frame hits, which have no
        /// aliasing window. Never produced by Register (it pre-increments from 0).
        /// </summary>
        private const int AnyGeneration = 0;

        // Hot: scanned by Burst job every frame during AOE
        private NativeArray<PrismSpatialData> _spatial;

        // Cold: read only for hit prisms on main thread
        private NativeArray<PrismDamageData> _damage;

        // Cell-volume summation view: live volume + cell binding + live domain per
        // slot, scanned by CellVolumeSumJob on each cell's 0.25s recompute.
        private NativeArray<PrismCellData> _cellData;
        private NativeArray<float> _cellVolumeScratch;

        // Shell view (cold): world pose of each shielded slot's analytic shell,
        // read by ShellContactQueryJob only for shield-flagged slots.
        private NativeArray<PrismShellData> _shell;

        // Async summation snapshot: the live arrays are mutated freely on the main
        // thread (Register / UpdateCellVolume per grower / steals), so a
        // worker-thread sum job reads a point-in-time COPY instead. One snapshot
        // per frame is shared by every cell that schedules that frame; taking a
        // new one first completes all prior readers (they finished long ago -
        // requests are 0.25s apart). Main-thread cost = one memcpy, constant
        // whether or not Burst is active in the editor.
        private NativeArray<PrismSpatialData> _sumSnapSpatial;
        private NativeArray<PrismCellData> _sumSnapCellData;
        private int _sumSnapCount;
        private int _sumSnapFrame = -1;
        private JobHandle _sumSnapReaders;
        private static readonly ProfilerMarker s_volumeSnapshotMarker = new("Cell.VolumeSum.Snapshot");

        // Managed: Prism references for applying damage callbacks
        private Prism[] _prisms;

        // Per-slot occupancy stamp, incremented on every Register. A slot index is
        // only a valid handle while its generation is unchanged, so anything that
        // holds an index across frames (the explosion backlog) can detect BOTH a
        // free-list recycle to a different prism AND a pooled prism re-entering the
        // same slot for a new life. Object identity alone catches only the first.
        private int[] _slotGeneration;

        // Managed: the cell whose per-domain density grids each prism is filed in
        // (the coarse view of this same lifecycle), or null - open space, fauna
        // bodies, slot free. Bound on Register/MarkRestored, released on
        // MarkDestroyed/Unregister.
        private Cell[] _cells;

        private int _highWaterMark;
        private readonly Stack<int> _freeList = new(256);
        private NativeList<AOEHit> _aoeHits;

        // Occupancy view: bucket key → registry index, one entry per LIVE
        // (active, not destroyed) prism. Maintained incrementally by
        // Register / MarkDestroyed / MarkRestored / Unregister / UpdatePosition.
        private NativeParallelMultiHashMap<int3, int> _buckets;
        private int _bucketEntryCount;

        // Reservation view: quantized position → claim. Managed dictionary is fine
        // here - reservations are few (bounded by spawn rate × TTL) and main-thread.
        private struct Reservation
        {
            public Vector3 Position;
            public float Expires;
        }
        private readonly Dictionary<Vector3Int, Reservation> _reservations = new();
        private readonly List<Vector3Int> _scratchKeys = new(16);
        private float _nextReservationPrune;

        // --- ProfilerMarkers ---
        // Note: the source branch (PrismAOERegistry on development) had two more
        // markers, AOE.BurstJob.ScheduleECS and AOE.ResolveDamage.ECS - bleeding-edge
        // has no ECS companion-entity path, so they have no code to attach to.
        private static readonly ProfilerMarker s_processExplosion = new("AOE.ProcessExplosion");
        private static readonly ProfilerMarker s_burstJobSchedule = new("AOE.BurstJob.Schedule");
        private static readonly ProfilerMarker s_resolveDamage = new("AOE.ResolveDamage");
        private static readonly ProfilerMarker s_shellQuery = new("ShellContact.Query");

        public bool IsAvailable => _spatial.IsCreated;
        public int HighWaterMark => _highWaterMark;

        /// <summary>
        /// Number of live (active, not destroyed) entries - the O(1) counterpart
        /// of <see cref="CopyLivePrisms"/>'s count, maintained by
        /// Register/MarkDestroyed/MarkRestored/Unregister. Telemetry + LOD sizing;
        /// population-scale consumers must not need an O(N) walk just to count.
        /// </summary>
        public int LiveCount { get; private set; }

        public static PrismSpatialIndex EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[PrismSpatialIndex]");
            go.AddComponent<PrismSpatialIndex>();
            return Instance;
        }

        public override void Awake()
        {
            base.Awake();
            _spatial = new NativeArray<PrismSpatialData>(INITIAL_CAPACITY, Allocator.Persistent);
            _damage = new NativeArray<PrismDamageData>(INITIAL_CAPACITY, Allocator.Persistent);
            _cellData = new NativeArray<PrismCellData>(INITIAL_CAPACITY, Allocator.Persistent);
            _cellVolumeScratch = new NativeArray<float>(CellVolumeResultCount, Allocator.Persistent);
            _shell = new NativeArray<PrismShellData>(INITIAL_CAPACITY, Allocator.Persistent);
            _prisms = new Prism[INITIAL_CAPACITY];
            _slotGeneration = new int[INITIAL_CAPACITY];
            _cells = new Cell[INITIAL_CAPACITY];
            _aoeHits = new NativeList<AOEHit>(512, Allocator.Persistent);
            _buckets = new NativeParallelMultiHashMap<int3, int>(INITIAL_CAPACITY, Allocator.Persistent);
            _lodCenters = new NativeArray<float3>(16, Allocator.Persistent);
            _lodBecameNear = new NativeList<int>(512, Allocator.Persistent);
            _lodBecameFar = new NativeList<int>(512, Allocator.Persistent);
        }

        #region Bucket grid

        private static int3 BucketKey(float3 position) =>
            (int3)math.floor(position / BucketSizeMeters);

        private void AddToBucket(int index, float3 position)
        {
            if (_bucketEntryCount >= _buckets.Capacity)
                _buckets.Capacity = _buckets.Capacity * 2;
            _buckets.Add(BucketKey(position), index);
            _bucketEntryCount++;
        }

        private void RemoveFromBucket(int index, float3 position)
        {
            var key = BucketKey(position);
            if (!_buckets.TryGetFirstValue(key, out int value, out var it)) return;
            do
            {
                if (value != index) continue;
                _buckets.Remove(it);
                _bucketEntryCount--;
                return;
            } while (_buckets.TryGetNextValue(out value, ref it));
        }

        /// <summary>
        /// True when probing every bucket in the AABB would touch more entries than a
        /// straight scan of the slot array - wide queries (Scout open-space probes reach
        /// 100m → 26³ ≈ 17k bucket lookups) are cheaper as one pass over the hot array.
        /// </summary>
        private bool BucketWalkCostsMoreThanLinearScan(int3 min, int3 max)
        {
            long bucketVolume = (long)(max.x - min.x + 1) * (max.y - min.y + 1) * (max.z - min.z + 1);
            return bucketVolume > _highWaterMark;
        }

        /// <summary>
        /// True if any LIVE prism (active, not destroyed - reservations excluded)
        /// sits within <paramref name="radius"/> of <paramref name="position"/>.
        /// Bucket-accelerated for tight radii, linear hot-array scan for wide ones.
        /// No physics, no allocation.
        /// </summary>
        public bool IsAnyPrismWithin(Vector3 position, float radius)
        {
            if (!_buckets.IsCreated) return false;
            float3 center = position;
            float radiusSq = radius * radius;
            int3 min = (int3)math.floor((center - radius) / BucketSizeMeters);
            int3 max = (int3)math.floor((center + radius) / BucketSizeMeters);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue &&
                        math.distancesq(s.Position, center) <= radiusSq)
                        return true;
                }
                return false;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetFirstValue(new int3(x, y, z), out int idx, out var it))
                    continue;
                do
                {
                    var s = _spatial[idx];
                    if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue &&
                        math.distancesq(s.Position, center) <= radiusSq)
                        return true;
                } while (_buckets.TryGetNextValue(out idx, ref it));
            }
            return false;
        }

        /// <summary>
        /// Gathers every LIVE prism (active, not destroyed) within
        /// <paramref name="radius"/> of <paramref name="center"/> into
        /// <paramref name="results"/> (cleared first) and returns the count.
        /// The replacement for Physics.OverlapSphere against prisms: same
        /// population as the AOE view, returns Prism references directly (no
        /// per-collider GetComponent), unbounded (no NonAlloc truncation), and
        /// allocation-free given a reused caller list.
        ///
        /// Results are an unordered snapshot - entries can be destroyed by the
        /// caller's own side effects mid-iteration (consume, steal, convert), so
        /// iterate with a null/destroyed guard, exactly as collider snapshots
        /// required. Main-thread only.
        /// </summary>
        public int QuerySphere(Vector3 center, float radius, List<Prism> results)
        {
            results.Clear();
            if (!_buckets.IsCreated || _highWaterMark == 0) return 0;
            float3 c = center;
            float radiusSq = radius * radius;
            int3 min = (int3)math.floor((c - radius) / BucketSizeMeters);
            int3 max = (int3)math.floor((c + radius) / BucketSizeMeters);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if (math.distancesq(s.Position, c) > radiusSq) continue;
                    var prism = _prisms[i];
                    if (prism) results.Add(prism);
                }
                return results.Count;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetFirstValue(new int3(x, y, z), out int idx, out var it))
                    continue;
                do
                {
                    var s = _spatial[idx];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if (math.distancesq(s.Position, c) > radiusSq) continue;
                    var prism = _prisms[idx];
                    if (prism) results.Add(prism);
                } while (_buckets.TryGetNextValue(out idx, ref it));
            }
            return results.Count;
        }

        /// <summary>
        /// The SWEPT counterpart of <see cref="QuerySphere"/>: gathers every LIVE prism whose
        /// centre lies within <paramref name="radius"/> of the SEGMENT
        /// <paramref name="a"/>→<paramref name="b"/>, i.e. inside a capsule.
        ///
        /// This exists because a fast projectile is a **teleport, not a sweep**:
        /// <c>Projectile.MoveProjectileAsync</c> advances the transform by
        /// <c>Velocity·Δt</c> each frame, and PhysX samples that discrete trigger once per
        /// physics step. A Sparrow round at its base 375 u/s covers 6.25 u per frame behind a
        /// 1.65-diameter hit sphere, so **~74% of its path is never tested** — and at high
        /// SPACE (3375 u/s) that becomes ~97%. Prisms in the gaps are silently passed
        /// through, which reads in play as a gun that cannot clear a small area no matter how
        /// much you shoot. Querying the segment restores full path coverage without inflating
        /// the projectile.
        ///
        /// Same conventions as <see cref="QuerySphere"/>: results are cleared first, the test
        /// is against the prism's CENTRE (callers wanting contact against a prism's extent
        /// must add their own allowance and refine), the snapshot is unordered and entries can
        /// be destroyed by the caller's own side effects mid-iteration, and it is main-thread
        /// only with no allocation given a reused list.
        ///
        /// A degenerate segment (a == b) reduces to exactly <see cref="QuerySphere"/>.
        /// </summary>
        public int QuerySegment(Vector3 a, Vector3 b, float radius, List<Prism> results)
        {
            results.Clear();
            if (!_buckets.IsCreated || _highWaterMark == 0) return 0;

            float3 p0 = a;
            float3 ab = (float3)b - p0;
            float abLenSq = math.lengthsq(ab);
            float radiusSq = radius * radius;

            // The capsule's AABB — thin in the two axes across the flight, so the bucket walk
            // stays cheap even on a long step.
            float3 lo = math.min(p0, (float3)b) - radius;
            float3 hi = math.max(p0, (float3)b) + radius;
            int3 min = (int3)math.floor(lo / BucketSizeMeters);
            int3 max = (int3)math.floor(hi / BucketSizeMeters);

            if (BucketWalkCostsMoreThanLinearScan(min, max))
            {
                for (int i = 0; i < _highWaterMark; i++)
                {
                    var s = _spatial[i];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if (DistanceToSegmentSq(s.Position, p0, ab, abLenSq) > radiusSq) continue;
                    var prism = _prisms[i];
                    if (prism) results.Add(prism);
                }
                return results.Count;
            }

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_buckets.TryGetFirstValue(new int3(x, y, z), out int idx, out var it))
                    continue;
                do
                {
                    var s = _spatial[idx];
                    if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                    if (DistanceToSegmentSq(s.Position, p0, ab, abLenSq) > radiusSq) continue;
                    var prism = _prisms[idx];
                    if (prism) results.Add(prism);
                } while (_buckets.TryGetNextValue(out idx, ref it));
            }
            return results.Count;
        }

        /// <summary>
        /// Squared distance from <paramref name="p"/> to the segment starting at
        /// <paramref name="a"/> with direction/length <paramref name="ab"/> — the same
        /// point-to-segment metric a CapsuleCollider uses, clamped to the endpoints.
        /// </summary>
        public static float DistanceToSegmentSq(float3 p, float3 a, float3 ab, float abLenSq)
        {
            float t = abLenSq > 1e-8f ? math.saturate(math.dot(p - a, ab) / abLenSq) : 0f;
            return math.distancesq(p, a + ab * t);
        }

        #endregion

        #region Reservations

        private static Vector3Int ReservationKey(Vector3 position) => new(
            Mathf.FloorToInt(position.x / ReservationQuantum),
            Mathf.FloorToInt(position.y / ReservationQuantum),
            Mathf.FloorToInt(position.z / ReservationQuantum));

        /// <summary>
        /// Atomically checks that nothing occupies <paramref name="position"/>
        /// (no live prism, no unexpired reservation within
        /// <paramref name="clearRadius"/>) and claims it. Call at the grow
        /// DECISION, before Instantiate - the claim is what closes the
        /// spawn-vs-spawn race that collider-based checks can't see. The claim is
        /// consumed when the spawned prism registers at (or near) the reserved
        /// position, or lapses after <see cref="ReservationTtlSeconds"/>.
        /// </summary>
        public bool TryReserve(Vector3 position, float clearRadius)
        {
            PruneExpiredReservations();
            if (IsAnyPrismWithin(position, clearRadius)) return false;
            if (HasActiveReservationWithin(position, clearRadius)) return false;
            _reservations[ReservationKey(position)] = new Reservation
            {
                Position = position,
                Expires = Time.time + ReservationTtlSeconds
            };
            return true;
        }

        /// <summary>Read-only occupancy probe: live prism or active reservation in range.</summary>
        public bool IsPositionOccupied(Vector3 position, float clearRadius) =>
            IsAnyPrismWithin(position, clearRadius) ||
            HasActiveReservationWithin(position, clearRadius);

        /// <summary>Explicitly cancels a claim made by <see cref="TryReserve"/>.</summary>
        public void ReleaseReservation(Vector3 position) =>
            _reservations.Remove(ReservationKey(position));

        private bool HasActiveReservationWithin(Vector3 position, float clearRadius)
        {
            if (_reservations.Count == 0) return false;
            float radiusSq = clearRadius * clearRadius;
            float now = Time.time;
            Vector3Int min = ReservationKey(position - Vector3.one * clearRadius);
            Vector3Int max = ReservationKey(position + Vector3.one * clearRadius);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_reservations.TryGetValue(new Vector3Int(x, y, z), out var r)) continue;
                if (r.Expires <= now) continue; // lapsed - prune pass will collect it
                if ((r.Position - position).sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }

        /// <summary>
        /// Called by <see cref="Register"/>: the spawned prism has materialized, so
        /// the claim that protected its site is fulfilled. Matched by proximity, not
        /// exact key - re-parenting under a spindle round-trips the position through
        /// parent matrices, so the registered position can drift a few millimetres
        /// (and across a quantization boundary) from the reserved one.
        /// </summary>
        private void ConsumeReservationNear(Vector3 position)
        {
            if (_reservations.Count == 0) return;
            const float confirmRadiusSq = 2f * 2f;
            _scratchKeys.Clear();
            foreach (var kvp in _reservations)
            {
                if ((kvp.Value.Position - position).sqrMagnitude <= confirmRadiusSq)
                    _scratchKeys.Add(kvp.Key);
            }
            foreach (var key in _scratchKeys)
                _reservations.Remove(key);
        }

        /// <summary>Lazy TTL sweep, amortized to at most one pass per second.</summary>
        private void PruneExpiredReservations()
        {
            if (_reservations.Count == 0 || Time.time < _nextReservationPrune) return;
            _nextReservationPrune = Time.time + 1f;
            float now = Time.time;
            _scratchKeys.Clear();
            foreach (var kvp in _reservations)
            {
                if (kvp.Value.Expires <= now)
                    _scratchKeys.Add(kvp.Key);
            }
            foreach (var key in _scratchKeys)
                _reservations.Remove(key);
        }

        #endregion

        #region Cell density view

        /// <summary>
        /// Files the prism into the per-domain density grids of the cell that
        /// spatially contains it - the COARSE view of this same registration
        /// lifecycle (fauna anti-domain targeting reads the grids; the cell phase
        /// system reads LiveBlockCount). Before Phase 3 these call sites lived in
        /// Prism beside every index call; folding them in here means the fine
        /// (occupancy/AOE) and coarse (density) views are fed by one stream and
        /// cannot diverge.
        ///
        /// Fauna bodies (LightFauna / Boid HealthPrisms) bind as VOLUME-ONLY mass:
        /// "volume is the spine" says ALL prisms feed the cell's volume accounting
        /// (Cell.LiveVolume - phase, dominant domain, HUD), so they enter the
        /// volume membership - but they must NOT enter the targeting grids or
        /// prism counts, otherwise a forager swarm reads as its own "mass
        /// concentration" and seeks itself instead of the trail/flora buildup
        /// (and herbivores would be seeded against inedible "prey"). Only
        /// HealthPrisms can be fauna bodies, so the GetComponentInParent walk is
        /// gated to that subtype to keep ordinary trail-prism registrations cheap.
        /// (They stay in the AOE and occupancy views - they are damageable,
        /// space-occupying mass.)
        ///
        /// Coexists with the flora ownership stream: HealthBlockTracker also
        /// AddBlocks flora health prisms into the LifeForm's host cell.
        /// Cell.AddBlock no-ops on already-tracked prisms and RemoveBlock
        /// tolerates double removal, so the two contributors stay consistent.
        /// </summary>
        private void BindCell(int index, Prism prism, Vector3 position)
        {
            bool environmentMass = ComputeEnvironmentMass(prism);
            var cell = Cell.FindCellContaining(position);
            _cells[index] = cell;
            // Pass the slot index explicitly: during Register the caller hasn't
            // stored the returned id on prism.SpatialIndexId yet, so Cell.AddBlock
            // could not resolve it from the prism.
            if (cell) cell.AddBlock(prism, environmentMass, index);
        }

        /// <summary>
        /// Environment-mass classification for the cell density view. Two prism kinds bind
        /// VOLUME-ONLY - they feed the cell's volume accounting ("volume is the spine": ALL
        /// prisms count) but stay out of the targeting grids, per-domain counts, control and
        /// prey signals:
        ///   - FAUNA BODIES (see the BindCell remarks): a forager swarm must not read as its
        ///     own mass concentration, nor seed herbivores against inedible "prey".
        ///   - SUPER-SHIELDED structure (e.g. the Astro League edge lining): fully invulnerable
        ///     mass no force can consume. The same "never lead fauna to mass they cannot eat"
        ///     rule applies, and permanent neutral structure must not sway DominantDomain or
        ///     the prey-volume signal.
        /// Super-shield state is applied AFTER spawn (post-bloom), so UpdateShieldState re-files
        /// the classification on every engage/disengage - the Register-time read alone would be
        /// stale.
        /// </summary>
        static bool ComputeEnvironmentMass(Prism prism)
        {
            if (prism is HealthPrism bodyPrism && bodyPrism.ResolveOwnerFauna() != null) return false;
            if (prism && prism.prismProperties != null && prism.prismProperties.IsSuperShielded) return false;
            return true;
        }

        /// <summary>
        /// Re-file a prism with its bound cell after a state change that alters its
        /// environment-mass classification (super-shield engage/disengage). RemoveBlock +
        /// AddBlock are idempotent/tolerant by design, so this is safe for any state.
        /// </summary>
        private void RefileCellClassification(int index)
        {
            var cell = _cells[index];
            var prism = _prisms[index];
            if (!cell || prism == null) return;
            cell.RemoveBlock(prism, index);
            cell.AddBlock(prism, ComputeEnvironmentMass(prism), index);
        }

        /// <summary>
        /// Removes the prism from its bound cell's density grids. Idempotent -
        /// the destroyed→unregistered path calls this twice. The prism ref is
        /// passed (not read from _prisms) so Unregister can unbind before it
        /// frees the slot; Cell.RemoveBlock handles destroyed-but-non-null refs
        /// by design, so no Unity-aliveness gate here.
        /// </summary>
        private void UnbindCell(int index, Prism prism)
        {
            var cell = _cells[index];
            _cells[index] = null;
            if (cell) cell.RemoveBlock(prism, index);
        }

        // Collider-LOD classification scratch (persistent, reused per sweep).
        NativeArray<float3> _lodCenters;
        NativeList<int> _lodBecameNear;
        NativeList<int> _lodBecameFar;

        /// <summary>
        /// Collider-LOD classification view: one Burst pass over the packed hot
        /// array that maintains a per-slot near-any-focus bit
        /// (<see cref="PrismFlags.LodNear"/>) and emits only the prisms whose
        /// near/far state CHANGED since the last pass - or the full classification
        /// when <paramref name="reconcile"/> is true (first sweep / LOD re-enable).
        /// <paramref name="enterRadius"/>/<paramref name="exitRadius"/> form the
        /// hysteresis band: far→near inside enter, near→far outside exit, prior
        /// state preserved in the annulus (kills boundary flapping around moving
        /// foci - the transition count is what the managed apply pays for).
        /// Replaces the managed 8000-entries-per-frame sliced scan, whose per-entry
        /// interop cost made every sweep O(population) on the main thread
        /// (5.5 ms slice frames at 25k prisms); the Burst scan is ~0.1-0.3 ms for
        /// the same population and the managed cost becomes O(transitions).
        /// Runs synchronously (Run - Bursted on the main thread), same as every
        /// other query in this index, so there are no job/mutation races.
        /// The LodNear bit lives in the slot flags and is reset naturally when a
        /// slot is re-registered (Register writes fresh flags), so slot reuse can
        /// at worst cost one idempotent extra transition on the next sweep.
        /// </summary>
        public void RunLodClassification(List<Vector3> centers, float enterRadius, float exitRadius, bool reconcile,
            List<Prism> becameNear, List<Prism> becameFar)
        {
            becameNear.Clear();
            becameFar.Clear();
            if (!_spatial.IsCreated || _highWaterMark == 0 || centers == null || centers.Count == 0) return;

            if (!_lodCenters.IsCreated || _lodCenters.Length < centers.Count)
            {
                if (_lodCenters.IsCreated) _lodCenters.Dispose();
                _lodCenters = new NativeArray<float3>(Mathf.NextPowerOfTwo(centers.Count), Allocator.Persistent);
            }
            for (int cI = 0; cI < centers.Count; cI++)
                _lodCenters[cI] = (float3)centers[cI];

            _lodBecameNear.Clear();
            _lodBecameFar.Clear();
            if (_lodBecameNear.Capacity < _highWaterMark) _lodBecameNear.Capacity = _highWaterMark;
            if (_lodBecameFar.Capacity < _highWaterMark) _lodBecameFar.Capacity = _highWaterMark;

            float farRadius = Mathf.Max(enterRadius, exitRadius);
            new LodClassifyJob
            {
                Prisms = _spatial,
                Centers = _lodCenters,
                EntryCount = _highWaterMark,
                CenterCount = centers.Count,
                NearRadiusSq = enterRadius * enterRadius,
                FarRadiusSq = farRadius * farRadius,
                Reconcile = reconcile,
                BecameNear = _lodBecameNear,
                BecameFar = _lodBecameFar,
            }.Run();

            // Resolve indices to managed refs - O(transitions), the only managed cost.
            for (int i = 0; i < _lodBecameNear.Length; i++)
            {
                var prism = _prisms[_lodBecameNear[i]];
                if (prism) becameNear.Add(prism);
            }
            for (int i = 0; i < _lodBecameFar.Length; i++)
            {
                var prism = _prisms[_lodBecameFar[i]];
                if (prism) becameFar.Add(prism);
            }
        }

        /// <summary>
        /// Copies every LIVE prism (active, not destroyed) into
        /// <paramref name="results"/> (cleared first); returns the count. One linear
        /// pass over the managed refs - the iteration view for whole-population
        /// passes (proximity collider-LOD, telemetry). Allocation-free with a
        /// pre-sized caller list; main-thread only.
        /// </summary>
        public int CopyLivePrisms(List<Prism> results)
        {
            results.Clear();
            if (!_spatial.IsCreated) return 0;
            for (int i = 0; i < _highWaterMark; i++)
            {
                if ((_spatial[i].Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;
                var prism = _prisms[i];
                if (prism) results.Add(prism);
            }
            return results.Count;
        }

        /// <summary>
        /// Re-files a tracked prism whose domain changed (steal / ChangeTeam) in
        /// its bound cell's per-domain grids. Caller:
        /// Prism.HandleTeamChangedForCell only. Does NOT itself touch the AOE
        /// cold data - the caller pairs this with UpdateDomain so the damage
        /// view's friend/foe domain stays live on steals (the Charge-5 "spare
        /// own domain" unlock depends on it; see Docs/SPATIAL_INDEX.md).
        /// </summary>
        public void ForwardDomainChangeToCell(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            var prism = _prisms[index];

            // Keep the summation view's LIVE domain fresh for every registered
            // prism - volume-only mass (fauna bodies) and unbound mass never
            // re-file through NotifyBlockDomainChanged below, but their volume
            // must still re-attribute on a team change ("steals re-attribute
            // next pass", same as the old live prism.Domain read).
            if (prism && _cellData.IsCreated)
            {
                var cd = _cellData[index];
                cd.DomainSlot = DomainToSlot(prism.Domain);
                _cellData[index] = cd;
            }

            var cell = _cells[index];
            if (cell && prism) cell.NotifyBlockDomainChanged(prism);
        }

        /// <summary>
        /// Re-files a tracked prism whose SHIELD state changed in its bound cell's
        /// targeting grids - shielded mass is not food (Docs/ECOSYSTEM.md §16.2) and so
        /// must not be a fauna steering target either (see Cell.AddBlock). Caller:
        /// PrismStateManager.SyncAOERegistryShieldState only, which is the single funnel
        /// every shield transition already passes through - it pairs this with
        /// UpdateShieldState so the analytic shell view and the cell grids move together.
        /// </summary>
        public void ForwardShieldChangeToCell(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;

            var prism = _prisms[index];
            var cell = _cells[index];
            if (cell && prism) cell.NotifyBlockShieldStateChanged(prism);
        }

        // ------------------------------------------------------------------
        //  Cell-volume summation view (CellVolumeSumJob)
        //  Binding is written ONLY by Cell.AddBlock/RemoveBlock (both membership
        //  streams - Register→BindCell and the flora HealthBlockTracker - funnel
        //  through them), so the packed view mirrors the cell's membership
        //  bookkeeping by construction. Volume is pushed by
        //  Prism.RefreshVolumeCache (O(growing)/frame); domain by
        //  ForwardDomainChangeToCell above.
        // ------------------------------------------------------------------

        static byte DomainToSlot(Domains domain) => domain switch
        {
            Domains.Jade => 0,
            Domains.Ruby => 1,
            Domains.Gold => 2,
            _ => 3, // Blue - the "no team" sentinel bucket
        };

        /// <summary>
        /// Binds slot <paramref name="index"/> to <paramref name="cellId"/> in the
        /// summation view. Caller: Cell.AddBlock only (single writer).
        /// <paramref name="environmentMass"/> mirrors the cell's trackedBlocks
        /// membership; <paramref name="domain"/> is the prism's live domain.
        /// </summary>
        public void SetCellBinding(int index, short cellId, bool environmentMass, Domains domain)
        {
            if (!_cellData.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var cd = _cellData[index];
            cd.CellId = cellId;
            cd.EnvMass = environmentMass ? (byte)1 : (byte)0;
            cd.DomainSlot = DomainToSlot(domain);
            _cellData[index] = cd;
        }

        /// <summary>
        /// Releases slot <paramref name="index"/> from <paramref name="cellId"/>'s
        /// summation view. No-op when the slot is bound to a different cell - with
        /// dual membership (flora host-cell stream vs spatial containment) the last
        /// binder owns the slot, and the other cell's RemoveBlock must not evict it.
        /// Caller: Cell.RemoveBlock only (single writer). Idempotent.
        /// </summary>
        public void ClearCellBinding(int index, short cellId)
        {
            if (!_cellData.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var cd = _cellData[index];
            if (cd.CellId != cellId) return;
            cd.CellId = -1;
            cd.EnvMass = 0;
            _cellData[index] = cd;
        }

        /// <summary>
        /// Drops every summation-view binding held by <paramref name="cellId"/> -
        /// the packed counterpart of Cell's bulk membership clears
        /// (Initialize / ResetCell), which reset the cell's bookkeeping without a
        /// per-prism RemoveBlock. O(highWaterMark), rare (scene init / replay reset).
        /// </summary>
        public void ClearAllCellBindings(short cellId)
        {
            if (!_cellData.IsCreated) return;
            for (int i = 0; i < _highWaterMark; i++)
            {
                var cd = _cellData[i];
                if (cd.CellId != cellId) continue;
                cd.CellId = -1;
                cd.EnvMass = 0;
                _cellData[i] = cd;
            }
        }

        /// <summary>
        /// Mirrors a prism's live cached volume into the summation view. Caller:
        /// Prism.RefreshVolumeCache - the same O(growing)/frame cadence that keeps
        /// CachedVolume itself fresh, so settled prisms cost nothing.
        /// </summary>
        public void UpdateCellVolume(int index, float volume)
        {
            if (!_cellData.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var cd = _cellData[index];
            cd.Volume = volume;
            _cellData[index] = cd;
        }

        /// <summary>
        /// One Burst pass summing every live prism bound to <paramref name="cellId"/>
        /// into <paramref name="results"/> (layout: the CellVolume* constants).
        /// Replaces Cell's managed per-prism recompute slice - see
        /// Docs/PERFORMANCE_OPTIMIZATION.md. Returns false (results untouched) when
        /// the index isn't allocated or the buffer is undersized; the caller keeps
        /// its previously published sums.
        /// </summary>
        public bool SumCellVolumes(short cellId, Vector3 centre, float nucleusRadiusSqr, float[] results)
        {
            if (!_spatial.IsCreated || !_cellData.IsCreated || !_cellVolumeScratch.IsCreated) return false;
            if (results == null || results.Length < CellVolumeResultCount) return false;

            new CellVolumeSumJob
            {
                Spatial = _spatial,
                CellData = _cellData,
                EntryCount = _highWaterMark,
                CellId = cellId,
                Centre = centre,
                NucleusRadiusSqr = nucleusRadiusSqr,
                Results = _cellVolumeScratch,
            }.Run();

            for (int i = 0; i < CellVolumeResultCount; i++)
                results[i] = _cellVolumeScratch[i];
            return true;
        }

        /// <summary>
        /// Async counterpart of <see cref="SumCellVolumes"/>: schedules the sum on
        /// a worker thread against a point-in-time snapshot of the packed arrays
        /// and returns immediately. Caller (Cell.EnsureVolumeFresh) completes the
        /// handle lazily on a later read and publishes then - readers keep the
        /// previously published sums meanwhile, the same tolerance the old sliced
        /// pass declared. Main-thread cost is the snapshot memcpy (shared by every
        /// cell that schedules in the same frame), so the pass stays cheap even
        /// when the job executes managed (editor with Burst disabled).
        /// <paramref name="results"/> must be a caller-owned persistent
        /// NativeArray the caller does not read until the handle completes.
        /// </summary>
        public bool TryScheduleCellVolumeSum(short cellId, Vector3 centre, float nucleusRadiusSqr,
            NativeArray<float> results, out JobHandle handle)
        {
            handle = default;
            if (!_spatial.IsCreated || !_cellData.IsCreated) return false;
            if (!results.IsCreated || results.Length < CellVolumeResultCount) return false;

            if (_sumSnapFrame != Time.frameCount)
            {
                using (s_volumeSnapshotMarker.Auto())
                {
                    // Prior readers reference the buffers being overwritten; they
                    // were scheduled ≥ one 0.25s window ago, so this is a no-op wait.
                    _sumSnapReaders.Complete();
                    _sumSnapReaders = default;

                    if (!_sumSnapSpatial.IsCreated || _sumSnapSpatial.Length < _highWaterMark)
                    {
                        if (_sumSnapSpatial.IsCreated) _sumSnapSpatial.Dispose();
                        if (_sumSnapCellData.IsCreated) _sumSnapCellData.Dispose();
                        int size = Mathf.NextPowerOfTwo(Mathf.Max(INITIAL_CAPACITY, _highWaterMark));
                        _sumSnapSpatial = new NativeArray<PrismSpatialData>(size, Allocator.Persistent);
                        _sumSnapCellData = new NativeArray<PrismCellData>(size, Allocator.Persistent);
                    }

                    if (_highWaterMark > 0)
                    {
                        NativeArray<PrismSpatialData>.Copy(_spatial, _sumSnapSpatial, _highWaterMark);
                        NativeArray<PrismCellData>.Copy(_cellData, _sumSnapCellData, _highWaterMark);
                    }
                    _sumSnapCount = _highWaterMark;
                    _sumSnapFrame = Time.frameCount;
                }
            }

            handle = new CellVolumeSumJob
            {
                Spatial = _sumSnapSpatial,
                CellData = _sumSnapCellData,
                EntryCount = _sumSnapCount,
                CellId = cellId,
                Centre = centre,
                NucleusRadiusSqr = nucleusRadiusSqr,
                Results = results,
            }.Schedule();
            // Read-read concurrency across cells is fine; the combined handle only
            // gates the NEXT snapshot overwrite (and teardown).
            _sumSnapReaders = JobHandle.CombineDependencies(_sumSnapReaders, handle);
            return true;
        }

        #endregion

        #region Registration

        /// <summary>
        /// Registers a prism for batch AOE processing, growth occupancy, and the
        /// containing cell's density grids. Returns the registry index which
        /// should be stored on the Prism for O(1) updates and unregistration.
        /// </summary>
        public int Register(Prism prism)
        {
            if (!_spatial.IsCreated) return -1;
            int index;
            if (_freeList.Count > 0)
            {
                index = _freeList.Pop();
            }
            else
            {
                index = _highWaterMark++;
                EnsureCapacity(index);
            }

            _prisms[index] = prism;
            unchecked { _slotGeneration[index]++; }
            // Never let a live slot carry the "no check" sentinel (only reachable
            // after a full 2^32 wrap on one slot, but the guard is one comparison).
            if (_slotGeneration[index] == AnyGeneration) _slotGeneration[index] = 1;

            // Build flags byte
            byte flags = PrismFlags.IsActive;
            if (prism.prismProperties is { IsShielded: true }) flags |= PrismFlags.IsShielded;
            if (prism.prismProperties is { IsSuperShielded: true }) flags |= PrismFlags.IsSuperShielded;

            float3 position = (float3)(Vector3)prism.transform.position;
            _spatial[index] = new PrismSpatialData
            {
                Position = position,
                Flags = flags
            };

            _damage[index] = new PrismDamageData
            {
                Volume = Mathf.Max(prism.prismProperties?.volume ?? 1f, 1f),
                Domain = (int)prism.Domain
            };

            // Summation view: seed with the live volume cache (CreateBlock refreshes
            // it just before registering; grows are pushed via UpdateCellVolume).
            // CellId stays unbound until BindCell → Cell.AddBlock claims the slot.
            _cellData[index] = new PrismCellData
            {
                Volume = prism.CachedVolume,
                CellId = -1,
                DomainSlot = DomainToSlot(prism.Domain),
                EnvMass = 0,
            };

            AddToBucket(index, position);
            LiveCount++;
            // The prism this reservation protected has materialized - fulfil it.
            ConsumeReservationNear(prism.transform.position);
            // Coarse view: file into the containing cell's density grids.
            BindCell(index, prism, (Vector3)position);

            // Shell view: prisms whose shield engaged before registration (authored
            // IsShielded, SegmentSpawner track super-shielding, spawn-window engages)
            // publish their shell here — the flags above are the source of truth.
            RefreshShellData(index);

            return index;
        }

        public void Unregister(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            // Already freed (e.g. OnDisable then ResetState both fire) - don't
            // double-push the slot onto the free list.
            if (s.Flags == 0 && _prisms[index] == null) return;
            // Live entries hold a bucket slot; destroyed ones were already removed.
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
            {
                RemoveFromBucket(index, s.Position);
                LiveCount--;
            }
            // Coarse view: leave the cell grids (no-op if MarkDestroyed already did).
            UnbindCell(index, _prisms[index]);
            // Summation view hygiene: a freed slot must not keep contributing to a
            // cell's sums through the free-list window (Register re-seeds on reuse).
            if (_cellData.IsCreated)
            {
                var cd = _cellData[index];
                cd.CellId = -1;
                cd.EnvMass = 0;
                _cellData[index] = cd;
            }
            s.Flags = 0; // clear all flags including IsActive
            _spatial[index] = s;
            _prisms[index] = null;
            // Shell view hygiene: a freed slot must not present a shell through the
            // free-list window (slot reuse would alias a stale shell onto a new prism).
            if (_shell.IsCreated) _shell[index] = default;
            _freeList.Push(index);
        }

        public void MarkDestroyed(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            if ((s.Flags & PrismFlags.Destroyed) != 0) return; // already destroyed
            // Destroyed mass no longer occupies space - growth may fill the site.
            if ((s.Flags & PrismFlags.IsActive) != 0)
            {
                RemoveFromBucket(index, s.Position);
                LiveCount--;
            }
            s.Flags |= PrismFlags.Destroyed;
            _spatial[index] = s;
            // Coarse view: destroyed mass must stop attracting fauna, and the
            // cell's LiveBlockCount must fall so the phase system can descend
            // (the consumption half of the oscillation).
            UnbindCell(index, _prisms[index]);
        }

        /// <summary>
        /// Re-activates a destroyed entry (trail restore mechanics). Refreshes the
        /// stored position and re-enters the occupancy bucket - restored mass
        /// blocks growth and takes AOE damage again. (Before the spatial-index
        /// unification, Restore never told the registry anything, so restored
        /// prisms stayed permanently invisible to batch AOE.)
        /// </summary>
        public void MarkRestored(int index)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            if ((s.Flags & PrismFlags.Destroyed) == 0) return; // not destroyed
            var prism = _prisms[index];
            if (prism != null)
                s.Position = (float3)(Vector3)prism.transform.position;
            s.Flags &= unchecked((byte)~PrismFlags.Destroyed);
            _spatial[index] = s;
            if ((s.Flags & PrismFlags.IsActive) != 0)
            {
                AddToBucket(index, s.Position);
                LiveCount++;
            }
            // Coarse view: restored mass re-enters the cell's density grids
            // (re-resolved at the restored position, like the old
            // Prism.RegisterWithCell call this replaces).
            if (prism) BindCell(index, prism, (Vector3)s.Position);
            // Shell view: a restored prism that is still shielded re-captures its
            // shell at the restored pose (stale data from before destruction).
            RefreshShellData(index);
        }

        /// <summary>
        /// Keeps the index honest for the few prisms that move after registration
        /// (gyroid bonding steers existing blocks into bond sites). Cheap when the
        /// bucket key is unchanged; rebuckets otherwise.
        /// </summary>
        public void UpdatePosition(int index, Vector3 position)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            float3 newPosition = position;
            if ((s.Flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
            {
                int3 oldKey = BucketKey(s.Position);
                int3 newKey = BucketKey(newPosition);
                if (!oldKey.Equals(newKey))
                {
                    RemoveFromBucket(index, s.Position);
                    AddToBucket(index, newPosition);
                }
            }
            s.Position = newPosition;
            _spatial[index] = s;
        }

        public void UpdateShieldState(int index, bool shielded, bool superShielded)
        {
            if (!_spatial.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var s = _spatial[index];
            bool wasSuperShielded = (s.Flags & PrismFlags.IsSuperShielded) != 0;
            // Clear shield bits, then set
            s.Flags = (byte)(s.Flags & ~(PrismFlags.IsShielded | PrismFlags.IsSuperShielded));
            if (shielded) s.Flags |= PrismFlags.IsShielded;
            if (superShielded) s.Flags |= PrismFlags.IsSuperShielded;
            _spatial[index] = s;

            // Shell view: engage publishes the world shell pose; disengage clears it.
            RefreshShellData(index);

            // A super-shield transition flips the prism between environment mass and volume-only
            // structure (see ComputeEnvironmentMass) - re-file it with its bound cell so the
            // targeting grids, per-domain counts and control reads stay truthful.
            if (wasSuperShielded != superShielded)
                RefileCellClassification(index);
        }

        /// <summary>
        /// Re-files a prism whose OWNERSHIP changed. The one caller today is
        /// <see cref="HealthPrism.LeaveAsSkeleton"/>: a fauna body prism left behind as a
        /// dead creature's skeleton stops being body tissue, so it must graduate from
        /// volume-only mass to full environment mass (targeting grids, per-domain counts,
        /// prey) - otherwise the food web can neither see nor eat what the creature left.
        /// Same shape and same tolerance as the super-shield re-file in
        /// <see cref="UpdateShieldState"/>.
        /// </summary>
        public void NotifyOwnershipChanged(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            RefileCellClassification(index);
        }

        /// <summary>
        /// Re-binds a prism whose POSITION has carried it into a different cell (or out of every
        /// cell). <see cref="UpdatePosition"/> deliberately re-buckets only the fine spatial view -
        /// a prism's CELL binding (volume books, targeting grids, per-domain counts) is filed once
        /// at Register time, because nothing that moved ever crossed a cell before the Ark. A
        /// travelling structure calls this on a coarse cadence so the cell it is actually IN is
        /// the cell whose food web can see it: unbind from the bound cell, re-resolve by the
        /// index's CURRENT stored position (kept fresh by the mover's UpdatePosition calls), and
        /// re-file. Between cells the prism binds to nothing - it still occupies space and takes
        /// AOE damage, it is just not any cell's mass. Cell.AddBlock/RemoveBlock are
        /// idempotent/tolerant, so calling this when nothing changed re-files in place (which
        /// also refreshes the prism's stale density-grid bucket - the same reason a slow mover
        /// wants a cadence rather than a crossing test).
        /// </summary>
        public void NotifyCellChanged(int index)
        {
            if (index < 0 || index >= _highWaterMark) return;
            if (!_spatial.IsCreated) return;
            var prism = _prisms[index];
            if (prism == null) return;
            UnbindCell(index, prism);
            BindCell(index, prism, _spatial[index].Position);
        }

        public void UpdateDomain(int index, int domain)
        {
            if (!_damage.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Domain = domain;
            _damage[index] = d;
        }

        /// <summary>
        /// Updates the cached volume after a prism finishes growing.
        /// </summary>
        public void UpdateVolume(int index, float volume)
        {
            if (!_damage.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            var d = _damage[index];
            d.Volume = volume;
            _damage[index] = d;
        }

        /// <summary>
        /// Re-captures a shielded slot's world shell pose from its transform —
        /// called on growth steps (RefreshVolumeCache) and mover updates
        /// (NotifyPositionChanged). O(1) no-op for the unshielded majority: one
        /// cold-array byte read decides.
        /// </summary>
        public void UpdateShellTransform(int index)
        {
            if (!_shell.IsCreated) return;
            if (index < 0 || index >= _highWaterMark) return;
            if (_shell[index].Kind == ShellKind.None) return;
            RefreshShellData(index);
        }

        /// <summary>
        /// (Re)derives the shell view entry for a slot from its shield flags and its
        /// prism's live transform. Unshielded / dead / geometry-less slots clear to
        /// Kind = None, which the query job skips.
        /// </summary>
        private void RefreshShellData(int index)
        {
            if (!_shell.IsCreated) return;

            var s = _spatial[index];
            byte kind = ShellKind.None;
            if ((s.Flags & PrismFlags.IsSuperShielded) != 0) kind = ShellKind.Stella;
            else if ((s.Flags & PrismFlags.IsShielded) != 0) kind = ShellKind.Octahedron;

            var prism = _prisms[index];
            if (kind == ShellKind.None || prism == null
                || !prism.TryGetShellGeometry(out Vector3 centerLocal, out Vector3 semiAxesLocal))
            {
                _shell[index] = default;
                return;
            }

            Transform t = prism.transform;
            Vector3 lossy = t.lossyScale;
            float3 semi = new float3(
                Mathf.Abs(semiAxesLocal.x * lossy.x),
                Mathf.Abs(semiAxesLocal.y * lossy.y),
                Mathf.Abs(semiAxesLocal.z * lossy.z));
            // Octahedron vertices sit at ±semi along each axis; stella spike tips at
            // the scaled cube corners (±sx, ±sy, ±sz).
            float bound = kind == ShellKind.Stella ? math.length(semi) : math.cmax(semi);

            _shell[index] = new PrismShellData
            {
                Rotation = t.rotation,
                Center = t.TransformPoint(centerLocal),
                SemiAxes = semi,
                BoundRadius = bound,
                Kind = kind,
            };
        }

        /// <summary>
        /// Managed back-reference for a query-result slot (the same parallel-array
        /// resolve <see cref="ResolveExplosionHits"/> uses). Callers must treat the
        /// reference as same-frame only — never cache a registry index across frames,
        /// because the free list recycles slots and the index will silently alias
        /// onto a different prism.
        ///
        /// The one sanctioned exception is the explosion backlog
        /// (<see cref="PendingExplosionHit"/>), which holds indices across frames
        /// ONLY because it also captures the <see cref="Prism"/> and drops any entry
        /// whose slot no longer holds it. Anything else that needs to outlive the
        /// frame must carry the same identity guard.
        /// </summary>
        internal Prism GetRegisteredPrism(int index)
        {
            if (index < 0 || index >= _highWaterMark) return null;
            return _prisms[index];
        }

        /// <summary>
        /// Shell-contact query: one synchronous Burst pass over the hot array that
        /// tests every shield-flagged slot's analytic shell against the probe set
        /// and appends overlaps to <paramref name="hits"/>. Same
        /// Schedule-then-Complete discipline as ProcessExplosionFrame — the caller
        /// dispatches from the results afterwards, never during the scan.
        /// </summary>
        public void CollectShellContacts(NativeArray<ShellProbe> probes, int probeCount, NativeList<ShellContactHit> hits)
        {
            hits.Clear();
            if (!_spatial.IsCreated || !_shell.IsCreated || _highWaterMark == 0 || probeCount <= 0)
                return;

            // AddNoResize throws on overflow; size for a dense worst case (a large
            // skimmer riding a fully super-shielded track lining).
            int capacity = math.min(65536, math.max(1024, probeCount * 512));
            if (hits.Capacity < capacity)
                hits.Capacity = capacity;

            using (s_shellQuery.Auto())
            {
                var job = new ShellContactQueryJob
                {
                    Prisms = _spatial,
                    Shells = _shell,
                    Probes = probes,
                    ProbeCount = probeCount,
                    Hits = hits.AsParallelWriter()
                };
                job.Schedule(_highWaterMark, JOB_BATCH_SIZE).Complete();
            }
        }

        #endregion

        #region Benchmark Support

        /// <summary>
        /// Registers synthetic prism data for benchmarking without requiring a Prism
        /// MonoBehaviour. The managed _prisms[index] slot is null - ProcessExplosionFrame
        /// skips it after the spatial query, so this isolates Burst job cost from damage
        /// application cost. Maintains the live-entry-implies-bucket invariant so the
        /// occupancy view stays consistent with Unregister/MarkDestroyed. Deliberately
        /// NOT filed into the cell density view - synthetic mass must not perturb
        /// Cell.LiveVolume / phase / fauna-targeting accounting.
        /// </summary>
        internal int RegisterSynthetic(float3 position, byte flags, float volume, int domain)
        {
            if (!_spatial.IsCreated) return -1;
            int index;
            if (_freeList.Count > 0)
                index = _freeList.Pop();
            else
            {
                index = _highWaterMark++;
                EnsureCapacity(index);
            }

            _prisms[index] = null;
            _cells[index] = null;
            _spatial[index] = new PrismSpatialData { Position = position, Flags = flags };
            _damage[index] = new PrismDamageData { Volume = volume, Domain = domain };
            // Synthetic slots have no Prism to derive a shell from - stay Kind None.
            if (_shell.IsCreated) _shell[index] = default;
            // Synthetic mass stays out of the summation view (CellId -1) - it must
            // not perturb Cell.LiveVolume / phase accounting (see remarks above).
            _cellData[index] = new PrismCellData
            {
                Volume = volume,
                CellId = -1,
                DomainSlot = DomainToSlot((Domains)domain),
                EnvMass = 0,
            };
            if ((flags & PrismFlags.JobSkipMask) == PrismFlags.JobPassValue)
                AddToBucket(index, position);
            return index;
        }

        /// <summary>
        /// Clears all registered prisms, buckets, reservations, and cell bindings.
        /// Used by the AOE benchmark to reset between runs - never call during
        /// gameplay.
        /// </summary>
        internal void ClearAll()
        {
            if (!_spatial.IsCreated) return;
            for (int i = 0; i < _highWaterMark; i++)
            {
                // Real prisms registered before the benchmark ran are filed in
                // their cell's density grids - release them so the coarse view
                // doesn't keep counting mass the index dropped.
                UnbindCell(i, _prisms[i]);
                _prisms[i] = null;
                var s = _spatial[i];
                s.Flags = 0;
                _spatial[i] = s;
                var cd = _cellData[i];
                cd.CellId = -1;
                cd.EnvMass = 0;
                _cellData[i] = cd;
            }
            _freeList.Clear();
            _highWaterMark = 0;
            if (_buckets.IsCreated) _buckets.Clear();
            _bucketEntryCount = 0;
            _reservations.Clear();
        }

        #endregion

        #region AOE Processing

        /// <summary>
        /// Processes one frame of AOE explosion damage.
        ///
        /// Phase 1 (Burst job): Scans _spatial array (16B/prism, 4 per cache line).
        ///   - Checks Flags byte + distance² against all registered prisms.
        ///   - Outputs {index, unit blast direction} per prism within radius to
        ///     _aoeHits - the direction radiates from blastOrigin, normalized
        ///     in-job via rsqrt so the main thread never pays a per-hit sqrt.
        ///
        /// Phase 2 (main thread): For each hit (typically dozens, not thousands):
        ///   - Reads _damage[idx] for domain/shield info (cold data, not in Burst working set).
        ///   - Applies domain logic, shield activation/deactivation, or damage.
        ///   - Syncs results back to registry.
        ///
        /// The query sphere (center/radius) has a STATIONARY centre and a growing
        /// radius, so each frame's volume strictly contains the previous frame's -
        /// the nesting the deferred-hit backlog and the once-per-pair alreadyHit set
        /// both rely on. blastOrigin is the emission point all impact vectors radiate
        /// from; <see cref="ExplosionImpulse"/> carries the magnitude they leave at and
        /// the debris ceiling that magnitude is measured against.
        /// The conic explosion does NOT use this entry point: its volume translates,
        /// so it queries an exact cone slab via <see cref="ProcessExplosionConeFrame"/>.
        ///
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism - mirrors original Destroy(gameObject) behavior).
        /// </summary>
        public bool ProcessExplosionFrame(
            Vector3 center,
            float radius,
            Vector3 blastOrigin,
            in ExplosionImpulse impulse,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel,
            HashSet<int> alreadyHit,
            Queue<PendingExplosionHit> pending = null)
        {
            using var processScope = s_processExplosion.Auto();

            // --- Phase 1: Burst job over hot spatial data ---
            _aoeHits.Clear();

            // A degenerate query must not stall the backlog - resolve the debt anyway.
            if (_highWaterMark == 0 || !_spatial.IsCreated)
                return ResolveExplosionHits(
                    impulse, explosionDomain, affectSelf, destructive, devastating,
                    shielding, anonymous, vessel, alreadyHit, pending);

            // Ensure NativeList capacity can hold all prisms - AddNoResize in
            // ParallelWriter will throw if capacity < count, killing the async loop
            // and leaving the explosion stuck at max scale.
            if (_aoeHits.Capacity < _highWaterMark)
                _aoeHits.Capacity = _highWaterMark;

            using (s_burstJobSchedule.Auto())
            {
                var job = new AOESpatialQueryJob
                {
                    Prisms = _spatial,
                    Center = (float3)center,
                    RadiusSq = radius * radius,
                    BlastOrigin = (float3)blastOrigin,
                    Hits = _aoeHits.AsParallelWriter()
                };

                job.Schedule(_highWaterMark, JOB_BATCH_SIZE).Complete();
            }

            return ResolveExplosionHits(
                impulse, explosionDomain, affectSelf, destructive, devastating,
                shielding, anonymous, vessel, alreadyHit, pending);
        }

        /// <summary>
        /// Batch AOE damage for the CONIC explosion. Phase 1 runs
        /// <see cref="AOEConicSweepQueryJob"/> over the axial slab
        /// [<paramref name="sliceMin"/>, <paramref name="sliceMax"/>] the blast newly
        /// covers this frame; phase 2 is the shared resolve pass. The interval is
        /// CLOSED at both ends on purpose - consecutive slabs share an endpoint, so
        /// no prism can fall between them; the alreadyHit claim dedupes the overlap.
        ///
        /// The cross-section is a capsule: <paramref name="tanCoreHalfAngle"/> is its
        /// radius per unit depth and <paramref name="tanGapePerUnit"/> its half-length
        /// per unit depth along <paramref name="gapeAxis"/> (0 = a plain circular cone).
        ///
        /// Unlike the spherical path there is no separate blast origin - the sweep's
        /// apex is the emission point, and the slabs tile the swept solid exactly, so
        /// coverage is frame-rate independent and never reaches past the visible tip.
        /// </summary>
        public bool ProcessExplosionConeFrame(
            Vector3 apex,
            Vector3 axis,
            Vector3 gapeAxis,
            float sliceMin,
            float sliceMax,
            float tanCoreHalfAngle,
            float tanGapePerUnit,
            in ExplosionImpulse impulse,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel,
            HashSet<int> alreadyHit,
            Queue<PendingExplosionHit> pending = null)
        {
            using var processScope = s_processExplosion.Auto();

            // A degenerate query must not stall the backlog: already-claimed hits are
            // this explosion's debt and need no query at all to resolve. Fall through
            // to the shared resolve pass with an empty hit list instead of returning.
            bool queryable = _highWaterMark > 0 && _spatial.IsCreated
                             && sliceMax > 0f && tanCoreHalfAngle > 0f;

            _aoeHits.Clear();
            if (!queryable)
                return ResolveExplosionHits(
                    impulse, explosionDomain, affectSelf, destructive, devastating,
                    shielding, anonymous, vessel, alreadyHit, pending);

            if (_aoeHits.Capacity < _highWaterMark)
                _aoeHits.Capacity = _highWaterMark;

            using (s_burstJobSchedule.Auto())
            {
                float3 sweepAxis = math.normalizesafe((float3)axis, new float3(0f, 0f, 1f));

                // Re-orthogonalise the gape axis against the sweep axis here rather than
                // trusting the caller: any on-axis component would tilt the capsule out of
                // the cross-section plane and the slabs would stop tiling the swept solid.
                float3 gape = (float3)gapeAxis;
                gape -= sweepAxis * math.dot(gape, sweepAxis);
                gape = math.normalizesafe(gape, math.normalizesafe(
                    math.cross(sweepAxis, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f)));

                var job = new AOEConicSweepQueryJob
                {
                    Prisms = _spatial,
                    Apex = (float3)apex,
                    Axis = sweepAxis,
                    GapeAxis = gape,
                    SliceMin = math.max(sliceMin, 0f),
                    SliceMax = sliceMax,
                    CoreTanHalfAngle = tanCoreHalfAngle,
                    TanGapePerUnit = math.max(tanGapePerUnit, 0f),
                    Hits = _aoeHits.AsParallelWriter()
                };

                job.Schedule(_highWaterMark, JOB_BATCH_SIZE).Complete();
            }

            return ResolveExplosionHits(
                impulse, explosionDomain, affectSelf, destructive, devastating,
                shielding, anonymous, vessel, alreadyHit, pending);
        }

        /// <summary>
        /// Batch AOE damage for the CYLINDRICAL explosion (the Scarab's cavitation plate). Phase 1
        /// runs <see cref="AOECylinderSweepQueryJob"/> over the axial slab
        /// [<paramref name="sliceMin"/>, <paramref name="sliceMax"/>] the plate newly covers this
        /// frame; phase 2 is the shared resolve pass. The interval is CLOSED at both ends for the
        /// same reason as the cone's — consecutive slabs share an endpoint so no prism can fall
        /// between them, and the alreadyHit claim dedupes the overlap.
        ///
        /// <paramref name="radius"/> is CONSTANT along the sweep (that is what makes this a
        /// cylinder rather than a cone), and every hit's impact direction is
        /// <paramref name="axis"/> itself: the plate shoves what it claims along the sweep at the
        /// blast's own speed rather than radiating it from a point.
        /// </summary>
        public bool ProcessExplosionCylinderFrame(
            Vector3 origin,
            Vector3 axis,
            float sliceMin,
            float sliceMax,
            float radius,
            in ExplosionImpulse impulse,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel,
            HashSet<int> alreadyHit,
            Queue<PendingExplosionHit> pending = null)
        {
            using var processScope = s_processExplosion.Auto();

            // A degenerate query must not stall the backlog: already-claimed hits are this
            // explosion's debt and need no query at all to resolve.
            bool queryable = _highWaterMark > 0 && _spatial.IsCreated
                             && sliceMax > 0f && radius > 0f;

            _aoeHits.Clear();
            if (!queryable)
                return ResolveExplosionHits(
                    impulse, explosionDomain, affectSelf, destructive, devastating,
                    shielding, anonymous, vessel, alreadyHit, pending);

            if (_aoeHits.Capacity < _highWaterMark)
                _aoeHits.Capacity = _highWaterMark;

            using (s_burstJobSchedule.Auto())
            {
                var job = new AOECylinderSweepQueryJob
                {
                    Prisms = _spatial,
                    Origin = (float3)origin,
                    Axis = math.normalizesafe((float3)axis, new float3(0f, 0f, 1f)),
                    SliceMin = math.max(sliceMin, 0f),
                    SliceMax = sliceMax,
                    RadiusSq = radius * radius,
                    Hits = _aoeHits.AsParallelWriter()
                };

                job.Schedule(_highWaterMark, JOB_BATCH_SIZE).Complete();
            }

            return ResolveExplosionHits(
                impulse, explosionDomain, affectSelf, destructive, devastating,
                shielding, anonymous, vessel, alreadyHit, pending);
        }

        /// <summary>
        /// Phase 2, shared by the spherical, conic and cylindrical queries: main-thread damage
        /// logic over cold data + managed refs for the slots phase 1 returned in
        /// <c>_aoeHits</c>.
        /// </summary>
        private bool ResolveExplosionHits(
            in ExplosionImpulse impulse,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel,
            HashSet<int> alreadyHit,
            Queue<PendingExplosionHit> pending)
        {
            using var resolveScope = s_resolveDamage.Auto();
            bool shouldContinue = true;
            int expDomain = (int)explosionDomain;

            // Cache vessel info to avoid repeated interface property access
            Domains vesselDomain = Domains.Blue;
            string vesselPlayerName = null;
            if (!anonymous && vessel != null)
            {
                var status = vessel.VesselStatus;
                vesselDomain = status.Domain;
                vesselPlayerName = status.Player.Name;
            }

            int budgetSpent = 0;

            // --- Backlog first: hits deferred by an earlier frame's budget ---
            // FIFO, so prisms resolve roughly in the order the blast reached them
            // (apex outward) rather than the near ones lingering while far ones die.
            // These were already claimed in alreadyHit, so the query can never
            // re-emit them; draining here is their ONLY resolution path.
            budgetSpent += DrainBacklog(
                pending, budgetSpent, impulse, expDomain, affectSelf, destructive,
                devastating, shielding, anonymous, vesselDomain, vesselPlayerName,
                ref shouldContinue);

            for (int i = 0; i < _aoeHits.Length; i++)
            {
                int idx = _aoeHits[i].Index;

                // Skip if already hit by this explosion (mirrors OnTriggerEnter once-per-pair behavior)
                if (alreadyHit.Contains(idx)) continue;

                if (budgetSpent >= EffectiveDamageBudget)
                {
                    // Over budget. Defer with this frame's impact direction so the hit
                    // resolves identically later even once the blast has moved on, and
                    // claim it so the query cannot double-queue it. The claim is what
                    // makes the deferral lossless for a TRANSLATING query volume.
                    // Without a backlog to defer into, fall back to the legacy contract
                    // - leave it unclaimed for a NESTED (spherical) query to re-find.
                    if (pending == null) continue;

                    var live = _prisms[idx];
                    if (live == null || live.destroyed) { alreadyHit.Add(idx); continue; }

                    alreadyHit.Add(idx);
                    pending.Enqueue(new PendingExplosionHit
                    {
                        Index = idx,
                        Generation = _slotGeneration[idx],
                        ImpactDir = _aoeHits[i].ImpactDir
                    });
                    continue;
                }

                alreadyHit.Add(idx);

                if (ResolveExplosionHit(idx, AnyGeneration, _aoeHits[i].ImpactDir,
                        impulse, expDomain, affectSelf, destructive, devastating,
                        shielding, anonymous, vesselDomain, vesselPlayerName, ref shouldContinue))
                    budgetSpent++;
            }

            return shouldContinue;
        }

        /// <summary>
        /// THE per-hit decision, shared by the fresh-query loop and the backlog drain
        /// so a deferred hit resolves under the prism's state at DRAIN time, not at
        /// query time (a prism that gained a super-shield or changed domain while
        /// queued must be re-judged, not blindly damaged).
        ///
        /// <paramref name="expectedGeneration"/> is the identity guard: registry
        /// slots are recycled through <c>_freeList</c>, so a hit that sat in the
        /// backlog for several frames may find a different prism in its slot — or the
        /// same pooled instance living a new life. Pass the generation captured at
        /// defer time; a mismatch drops the hit. Pass <see cref="AnyGeneration"/> for
        /// a same-frame hit, where no aliasing window exists.
        ///
        /// Returns true if the frame's budget should be charged - i.e. real work was
        /// done. Both outcomes that do work are charged: <see cref="Prism.Damage"/>
        /// (destruction + VFX) and <c>ActivateShield</c> (shield-geometry engage +
        /// material swap + a per-prism SFX). A dead slot or a super-shield block is
        /// free.
        /// </summary>
        private bool ResolveExplosionHit(
            int idx,
            int expectedGeneration,
            float3 impactDir,
            in ExplosionImpulse impulse,
            int expDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            Domains vesselDomain,
            string vesselPlayerName,
            ref bool shouldContinue)
        {
            // Slot-recycling guard - see the summary. Checked BEFORE the prism is
            // touched: a stale entry must not resolve against whatever now owns the slot.
            if (expectedGeneration != AnyGeneration && _slotGeneration[idx] != expectedGeneration)
                return false;

            var prism = _prisms[idx];
            if (prism == null || prism.destroyed) return false;

            // Read cold data - only for hit prisms, never pollutes the Burst job's cache
            var flags = _spatial[idx].Flags;
            int prismDomain = _damage[idx].Domain;

            // Super-shielded prisms are fully invulnerable. AOE explosions
            // are physically blocked by the shield (shouldContinue = false
            // stops the explosion expanding past this layer) but cause no
            // damage and no state change. Ways to break super-shields will
            // be added later as targeted opt-in mechanics.
            //
            // The blast is not silent, though: the shared gate stamps the
            // deflection wobble (Prism.AbsorbSuperShieldHit), so the shield
            // visibly rocks instead of the explosion stopping dead against
            // nothing. Magnitude is Speed x Inertia — the impact vector's
            // length, without building the vector or taking its root.
            // Photons only; every gameplay consequence below is still skipped.
            if ((flags & PrismFlags.IsSuperShielded) != 0)
            {
                prism.AbsorbSuperShieldHit(impulse.Speed * impulse.Inertia);
                shouldContinue = false;
                return false;
            }

            // Same team (and not affectSelf) or non-destructive: shield the prism
            if ((prismDomain == expDomain && !affectSelf) || !destructive)
            {
                // The blast is ACCEPTED, not ignored: the prism armours up instead of the
                // explosion visibly passing through it. The blow's magnitude (Speed x
                // Inertia - no vector built, no root taken) and its ceiling ride along so
                // the timed pop sheds at half of it (PrismStateManager.
                // ExecuteTimerDeactivation). Mirrors ExecuteCommonPrismCommands.
                float impactSpeed = impulse.Speed * impulse.Inertia;
                if (shielding && prismDomain == expDomain)
                    prism.ActivateShieldFromImpact(impactSpeed, impulse.DebrisSpeedLimit);
                else
                    prism.ActivateShield(2f, impactSpeed, impulse.DebrisSpeedLimit);
                UpdateShieldState(idx, true, false);
                return true;
            }

            // Impact vector: the in-job unit direction (blastOrigin → prism)
            // at the blast-wave speed - no managed normalize per hit. The impulse's own
            // ceiling rides along: without it the explosion prefab's authored clamp
            // applies, and every AOE magnitude sits far enough above that clamp to
            // saturate, flattening blasts of every strength to one debris speed.
            Vector3 impactVector = impulse.Along(impactDir);

            if (anonymous)
                prism.Damage(impactVector, Domains.Blue, "🔥GuyFawkes🔥", devastating,
                             debrisSpeedLimit: impulse.DebrisSpeedLimit);
            else
                prism.Damage(impactVector, vesselDomain, vesselPlayerName, devastating,
                             debrisSpeedLimit: impulse.DebrisSpeedLimit);

            // Sync registry with the result of Damage()
            if (prism.destroyed)
                MarkDestroyed(idx);
            else
                UpdateShieldState(idx,
                    prism.prismProperties.IsShielded,
                    prism.prismProperties.IsSuperShielded);

            return true;
        }

        /// <summary>
        /// Spends what is left of a frame's budget on the deferred backlog.
        /// Returns how much budget it consumed. The examined cap keeps a queue full
        /// of dead/recycled slots from being walked in one frame.
        /// </summary>
        private int DrainBacklog(
            Queue<PendingExplosionHit> pending,
            int alreadySpent,
            in ExplosionImpulse impulse,
            int expDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            Domains vesselDomain,
            string vesselPlayerName,
            ref bool shouldContinue)
        {
            if (pending == null || pending.Count == 0) return 0;

            int spent = 0;
            int examined = 0;
            int cap = EffectiveDamageBudget - alreadySpent;

            while (pending.Count > 0 && spent < cap && examined < EffectiveDrainExamined)
            {
                examined++;
                var deferred = pending.Dequeue();
                if (ResolveExplosionHit(deferred.Index, deferred.Generation, deferred.ImpactDir,
                        impulse, expDomain, affectSelf, destructive, devastating,
                        shielding, anonymous, vesselDomain, vesselPlayerName, ref shouldContinue))
                    spent++;
            }

            return spent;
        }

        /// <summary>
        /// Drains an explosion's deferred backlog without running a new spatial
        /// query, honouring the same per-frame budget. Called by the explosion after
        /// its visual has finished so that a blast dense enough to exceed the budget
        /// still resolves everything it enclosed - a prism's fate is decided by
        /// whether the blast CONTAINED it, never by how long the VFX ran.
        /// Returns true while work remains.
        /// </summary>
        public bool DrainPendingExplosionDamage(
            Queue<PendingExplosionHit> pending,
            in ExplosionImpulse impulse,
            Domains explosionDomain,
            bool affectSelf,
            bool destructive,
            bool devastating,
            bool shielding,
            bool anonymous,
            IVessel vessel)
        {
            if (pending == null || pending.Count == 0) return false;

            using var resolveScope = s_resolveDamage.Auto();

            Domains vesselDomain = Domains.Blue;
            string vesselPlayerName = null;
            if (!anonymous && vessel != null)
            {
                var status = vessel.VesselStatus;
                var player = status?.Player;
                vesselDomain = status?.Domain ?? Domains.Blue;
                vesselPlayerName = player?.Name;
            }

            bool ignored = true;
            DrainBacklog(pending, 0, impulse, (int)explosionDomain, affectSelf,
                destructive, devastating, shielding, anonymous, vesselDomain,
                vesselPlayerName, ref ignored);

            return pending.Count > 0;
        }

        #endregion

        #region Capacity

        private void EnsureCapacity(int requiredIndex)
        {
            if (requiredIndex < _spatial.Length) return;

            int newSize = Mathf.NextPowerOfTwo(requiredIndex + 1);

            // Grow hot array
            var newSpatial = new NativeArray<PrismSpatialData>(newSize, Allocator.Persistent);
            NativeArray<PrismSpatialData>.Copy(_spatial, newSpatial, _spatial.Length);
            _spatial.Dispose();
            _spatial = newSpatial;

            // Grow cold array
            var newDamage = new NativeArray<PrismDamageData>(newSize, Allocator.Persistent);
            NativeArray<PrismDamageData>.Copy(_damage, newDamage, _damage.Length);
            _damage.Dispose();
            _damage = newDamage;

            // Grow cell-volume summation view
            var newCellData = new NativeArray<PrismCellData>(newSize, Allocator.Persistent);
            NativeArray<PrismCellData>.Copy(_cellData, newCellData, _cellData.Length);
            _cellData.Dispose();
            _cellData = newCellData;

            // Grow shell view
            var newShell = new NativeArray<PrismShellData>(newSize, Allocator.Persistent);
            NativeArray<PrismShellData>.Copy(_shell, newShell, _shell.Length);
            _shell.Dispose();
            _shell = newShell;

            // Grow managed arrays
            var newPrisms = new Prism[newSize];
            System.Array.Copy(_prisms, newPrisms, _prisms.Length);
            _prisms = newPrisms;

            var newGenerations = new int[newSize];
            System.Array.Copy(_slotGeneration, newGenerations, _slotGeneration.Length);
            _slotGeneration = newGenerations;

            var newCells = new Cell[newSize];
            System.Array.Copy(_cells, newCells, _cells.Length);
            _cells = newCells;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // In-flight async volume sums read the snapshot buffers - finish them
            // before the memory goes away.
            _sumSnapReaders.Complete();
            if (_sumSnapSpatial.IsCreated) _sumSnapSpatial.Dispose();
            if (_sumSnapCellData.IsCreated) _sumSnapCellData.Dispose();
            if (_spatial.IsCreated) _spatial.Dispose();
            if (_damage.IsCreated) _damage.Dispose();
            if (_cellData.IsCreated) _cellData.Dispose();
            if (_shell.IsCreated) _shell.Dispose();
            if (_cellVolumeScratch.IsCreated) _cellVolumeScratch.Dispose();
            if (_aoeHits.IsCreated) _aoeHits.Dispose();
            if (_buckets.IsCreated) _buckets.Dispose();
            if (_lodCenters.IsCreated) _lodCenters.Dispose();
            if (_lodBecameNear.IsCreated) _lodBecameNear.Dispose();
            if (_lodBecameFar.IsCreated) _lodBecameFar.Dispose();
            LiveCount = 0;
        }

        #endregion
    }
}
