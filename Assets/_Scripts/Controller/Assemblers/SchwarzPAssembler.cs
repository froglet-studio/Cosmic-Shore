using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class SchwarzPGrowthInfo : GrowthInfo
    {
        public SchwarzPSurfaceFrame Frame;
        public Vector3 ParamPosition;
        public Vector3 ParamHeading;
    }

    /// <summary>
    /// Shared lattice frame for one Schwarz P flora. Created by the seed assembler and
    /// passed by reference to every tile grown from it, so the whole organism agrees on
    /// a single mapping between the surface's parameter space (radians, where the
    /// implicit function lives) and world space, plus one occupancy registry that keeps
    /// two branches from growing into the same spot.
    ///
    /// Occupancy is intentionally weak: a bucket is only "taken" while its resident
    /// prism is alive. Mass is conserved — when fauna eat a tile, the bucket frees and
    /// a reseeded survivor can regrow the wound (see AssembledFlora.ReseedBranches).
    /// </summary>
    public class SchwarzPSurfaceFrame
    {
        public Vector3 WorldOrigin;
        public Quaternion WorldRotation;
        public Vector3 ParamAnchor;
        public float WorldScale;   // world units per parameter radian
        public float ParamStep;    // parameter-space distance between neighboring tiles

        struct Claim
        {
            public SchwarzPAssembler Occupant;
            public int FrameCount;
            public bool Resident;
        }

        readonly Dictionary<Vector3Int, Claim> occupied = new();

        public Vector3 ToWorld(Vector3 param) =>
            WorldOrigin + WorldRotation * ((param - ParamAnchor) * WorldScale);

        public Vector3 ToWorldDirection(Vector3 paramDirection) => WorldRotation * paramDirection;

        Vector3Int Quantize(Vector3 param)
        {
            float cell = ParamStep * 0.5f;
            return new Vector3Int(
                Mathf.RoundToInt(param.x / cell),
                Mathf.RoundToInt(param.y / cell),
                Mathf.RoundToInt(param.z / cell));
        }

        public bool IsOccupied(Vector3 param)
        {
            if (!occupied.TryGetValue(Quantize(param), out var claim)) return false;

            if (claim.Resident)
            {
                if (claim.Occupant) return true;
                occupied.Remove(Quantize(param));   // resident was consumed — bucket frees
                return false;
            }

            // Reservations only hold for the frame they were made in. AssembledFlora.Grow
            // may discard a CanGrow result (random skip), so an unconsumed reservation
            // must not block the spot forever.
            if (Time.frameCount == claim.FrameCount) return true;
            occupied.Remove(Quantize(param));
            return false;
        }

        /// <summary>Same-frame hold on a growth site, placed when GetGrowthInfo returns it.</summary>
        public void Reserve(Vector3 param, SchwarzPAssembler by) =>
            occupied[Quantize(param)] = new Claim { Occupant = by, FrameCount = Time.frameCount, Resident = false };

        /// <summary>Permanent (while alive) claim, placed once the tile's prism actually exists.</summary>
        public void Settle(Vector3 param, SchwarzPAssembler resident) =>
            occupied[Quantize(param)] = new Claim { Occupant = resident, Resident = true };
    }

    /// <summary>
    /// Grows the Schwarz P minimal surface — cos(x) + cos(y) + cos(z) = 0, the same
    /// triply periodic surface that SpawnableSchwarzPSurface stamps out all at once for
    /// Joust — incrementally, prism by prism, as a flora.
    ///
    /// Where the gyroid assembler walks a baked bond-mate table (its non-Euclidean tile
    /// data), Schwarz P has a closed-form implicit function with an analytic gradient,
    /// so each tile's four tangent-plane neighbors (ahead, behind, left, right of the
    /// growth heading) are computed on the fly: step along the surface, Newton-project
    /// back onto the zero level set, orient the new tile to the gradient. Growth fans
    /// out as a wavefront across the surface, opening the characteristic six-way tunnel
    /// network one plate at a time.
    /// </summary>
    public class SchwarzPAssembler : Assembler
    {
        [Header("Surface Lattice")]
        [Tooltip("World-space distance between neighboring surface tiles. Match the flora's " +
                 "leaf size so plates tile edge to edge.")]
        [SerializeField] float separationDistance = 6f;

        [Tooltip("World-space size of one full period of the surface — the tunnel-to-tunnel repeat.")]
        [SerializeField] float periodScale = 60f;

        const int NewtonIterations = 6;
        const float SurfaceTolerance = 1e-3f;
        const int SiteCount = 4;    // ahead, right, left, behind

        // Surface-frame state: programmed by the parent for grown tiles, built lazily for the seed.
        SchwarzPSurfaceFrame frame;
        Vector3 paramPosition;
        Vector3 paramHeading;

        int depth = -1;

        public override Prism Prism { get; set; }
        public override Spindle Spindle { get; set; }

        public override int Depth
        {
            get => depth;
            set => depth = value;
        }

        void Start()
        {
            if (!Prism) Prism = GetComponent<Prism>();
        }

        /// <summary>
        /// Copies the parent's surface frame and this tile's place on it. Called by
        /// AssembledFlora.AssemblerFactory right after the new prism is instantiated.
        /// </summary>
        public void Program(SchwarzPGrowthInfo info)
        {
            frame = info.Frame;
            paramPosition = info.ParamPosition;
            paramHeading = info.ParamHeading;
            depth = info.Depth;
            frame.Settle(paramPosition, this);
        }

        public override bool IsFullyBonded()
        {
            if (frame == null) return false;

            for (int site = 0; site < SiteCount; site++)
            {
                if (!TryStepAlongSurface(paramPosition, SiteDirection(site), out var neighborParam, out _))
                    continue;   // unreachable site is not an open site
                if (!frame.IsOccupied(neighborParam)) return false;
            }
            return true;
        }

        public override void StartBonding()
        {
            // Flora growth drives this assembler exclusively through GetGrowthInfo; there
            // is no free-block recruitment phase like the gyroid's mate search.
        }

        public override GrowthInfo GetGrowthInfo()
        {
            if (!Prism) Prism = GetComponent<Prism>();
            EnsureSeeded();

            for (int site = 0; site < SiteCount; site++)
            {
                Vector3 stepDirection = SiteDirection(site);

                if (!TryStepAlongSurface(paramPosition, stepDirection, out Vector3 childParam, out Vector3 childNormal))
                    continue;

                if (frame.IsOccupied(childParam))
                    continue;

                // Parallel-transport the heading so children keep marching outward and the
                // growth front stays coherent across the surface's curvature.
                Vector3 childHeading = Vector3.ProjectOnPlane(stepDirection, childNormal);
                childHeading = childHeading.sqrMagnitude > 1e-6f ? childHeading.normalized : AnyTangent(childNormal);

                Vector3 worldPosition = frame.ToWorld(childParam);
                Quaternion worldRotation = Quaternion.LookRotation(
                    frame.ToWorldDirection(childNormal),
                    frame.ToWorldDirection(childHeading));

                // World-space occupancy via PrismSpatialIndex.TryReserve — catches foreign
                // geometry the param-space lattice registry can't know about (vessel
                // trails, other floras, environment prisms). The frame registry above
                // stays as this flora's own lattice bookkeeping; the spatial index is
                // the cross-structure authority. Replaces Physics.CheckBox, which was
                // structurally blind to prisms inside their 0.6s disabled-collider spawn
                // window (Prism.waitTime) — see Docs/SPATIAL_INDEX.md. clearRadius is
                // 0.4× the lattice step: below half-spacing so neighbor sites are never
                // blocked, above any drift so a same-site duplicate always is.
                float clearRadius = Mathf.Max(2f, 0.4f * (worldPosition - transform.position).magnitude);
                var spatialIndex = PrismSpatialIndex.EnsureInstance();
                if (spatialIndex != null && spatialIndex.IsAvailable &&
                    !spatialIndex.TryReserve(worldPosition, clearRadius))
                    continue;

                frame.Reserve(childParam, this);    // upgraded to Settle by the child's Program

                return new SchwarzPGrowthInfo
                {
                    CanGrow = true,
                    Position = worldPosition,
                    Rotation = worldRotation,
                    Depth = depth - 1,
                    Frame = frame,
                    ParamPosition = childParam,
                    ParamHeading = childHeading
                };
            }

            // Nothing viable this cycle. Sites are re-probed live each call, so a branch
            // culled here can still heal a wound later when ReseedBranches re-activates it
            // after fauna consume a neighbor (mass is conserved — no decay, only grazing).
            return new GrowthInfo { CanGrow = false };
        }

        /// <summary>
        /// Anchors the surface lattice to the seed prism. The seed sits at (π/2, π/2, π/2) —
        /// on the zero level set with a clean non-degenerate gradient — and its transform
        /// orients the whole surface: prism forward becomes the surface normal there.
        /// </summary>
        void EnsureSeeded()
        {
            if (frame != null) return;

            Vector3 seedParam = new Vector3(Mathf.PI / 2f, Mathf.PI / 2f, Mathf.PI / 2f);
            Vector3 seedNormal = SurfaceNormal(seedParam);
            Vector3 seedTangent = AnyTangent(seedNormal);

            float worldScale = periodScale / (2f * Mathf.PI);
            frame = new SchwarzPSurfaceFrame
            {
                WorldOrigin = transform.position,
                WorldRotation = transform.rotation * Quaternion.Inverse(Quaternion.LookRotation(seedNormal, seedTangent)),
                ParamAnchor = seedParam,
                WorldScale = worldScale,
                ParamStep = separationDistance / worldScale
            };

            paramPosition = seedParam;
            paramHeading = seedTangent;
            frame.Settle(seedParam, this);
        }

        Vector3 SiteDirection(int site)
        {
            Vector3 normal = SurfaceNormal(paramPosition);
            Vector3 ahead = Vector3.ProjectOnPlane(paramHeading, normal);
            ahead = ahead.sqrMagnitude > 1e-6f ? ahead.normalized : AnyTangent(normal);
            Vector3 right = Vector3.Cross(normal, ahead).normalized;

            return site switch
            {
                0 => ahead,
                1 => right,
                2 => -right,
                _ => -ahead
            };
        }

        /// <summary>
        /// Marches one lattice step along the surface: midpoint probe to re-aim the tangent
        /// across curvature, full step, then Newton-project back onto the zero level set.
        /// </summary>
        bool TryStepAlongSurface(Vector3 fromParam, Vector3 tangentDirection, out Vector3 result, out Vector3 resultNormal)
        {
            Vector3 midpoint = fromParam + tangentDirection * (frame.ParamStep * 0.5f);
            if (ProjectToSurface(ref midpoint))
            {
                Vector3 midTangent = Vector3.ProjectOnPlane(tangentDirection, SurfaceNormal(midpoint));
                if (midTangent.sqrMagnitude > 1e-6f)
                    tangentDirection = midTangent.normalized;
            }

            result = fromParam + tangentDirection * frame.ParamStep;
            if (!ProjectToSurface(ref result))
            {
                resultNormal = Vector3.up;
                return false;
            }

            resultNormal = SurfaceNormal(result);
            return true;
        }

        // f(x,y,z) = cos x + cos y + cos z — zero level set is the Schwarz P surface.
        static float SurfaceValue(Vector3 p) => Mathf.Cos(p.x) + Mathf.Cos(p.y) + Mathf.Cos(p.z);

        static Vector3 SurfaceGradient(Vector3 p) => new(-Mathf.Sin(p.x), -Mathf.Sin(p.y), -Mathf.Sin(p.z));

        static Vector3 SurfaceNormal(Vector3 p)
        {
            Vector3 gradient = SurfaceGradient(p);
            return gradient.sqrMagnitude > 1e-8f ? gradient.normalized : Vector3.up;
        }

        static bool ProjectToSurface(ref Vector3 p)
        {
            for (int i = 0; i < NewtonIterations; i++)
            {
                float f = SurfaceValue(p);
                if (Mathf.Abs(f) < SurfaceTolerance) return true;

                Vector3 gradient = SurfaceGradient(p);
                float magnitudeSqr = gradient.sqrMagnitude;
                if (magnitudeSqr < 1e-8f) return false;     // critical point — nowhere to project

                p -= gradient * (f / magnitudeSqr);
            }
            return Mathf.Abs(SurfaceValue(p)) < SurfaceTolerance * 10f;
        }

        static Vector3 AnyTangent(Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.right);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.Cross(normal, Vector3.up);
            return tangent.normalized;
        }
    }
}
