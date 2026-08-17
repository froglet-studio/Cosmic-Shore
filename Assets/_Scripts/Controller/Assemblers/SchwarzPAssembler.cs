using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class SchwarzPGrowthInfo : GrowthInfo
    {
        public SchwarzPSurfaceFrame Frame;
        public Vector3Int Tile;
        public int Site;
    }

    /// <summary>
    /// A prism's address on the surface: which tile, and which site of it. Both integers,
    /// so occupancy is exact - there is no quantized float key to drift.
    /// </summary>
    public readonly struct SchwarzPSiteKey : IEquatable<SchwarzPSiteKey>
    {
        public readonly Vector3Int Tile;
        public readonly int Site;

        public SchwarzPSiteKey(Vector3Int tile, int site)
        {
            Tile = tile;
            Site = site;
        }

        public bool Equals(SchwarzPSiteKey other) => Tile == other.Tile && Site == other.Site;
        public override bool Equals(object obj) => obj is SchwarzPSiteKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tile, Site);
        public override string ToString() => $"{Tile}#{Site}";
    }

    /// <summary>
    /// Shared lattice frame for one Schwarz P flora. Created by the seed assembler and
    /// passed by reference to every prism grown from it, so the whole organism agrees on
    /// a single mapping between the surface's parameter space (radians, where the implicit
    /// function lives) and world space, on one subdivision level, and on one occupancy
    /// registry that keeps two branches from growing into the same site.
    ///
    /// Occupancy is intentionally weak: a site is only "taken" while its resident prism is
    /// alive. Mass is conserved - when fauna eat a prism, the site frees and a reseeded
    /// survivor can regrow the wound (see AssembledFlora.ReseedBranches).
    /// </summary>
    public class SchwarzPSurfaceFrame
    {
        public Vector3 WorldOrigin;
        public Quaternion WorldRotation;
        public Vector3 ParamAnchor;
        public float WorldScale;      // world units per parameter radian
        public float WorldSpacing;    // measured distance between neighbouring prisms
        public int Level;             // which subdivision of the tile this flora grows

        struct Claim
        {
            public SchwarzPAssembler Occupant;
            public int FrameCount;
            public bool Resident;
        }

        readonly Dictionary<SchwarzPSiteKey, Claim> occupied = new();

        public Vector3 ToWorld(Vector3 param) =>
            WorldOrigin + WorldRotation * ((param - ParamAnchor) * WorldScale);

        public Vector3 ToWorldDirection(Vector3 paramDirection) => WorldRotation * paramDirection;

        public bool IsOccupied(SchwarzPSiteKey key)
        {
            if (!occupied.TryGetValue(key, out var claim)) return false;

            if (claim.Resident)
            {
                if (claim.Occupant) return true;
                occupied.Remove(key);   // resident was consumed - the site frees
                return false;
            }

            // Reservations only hold for the frame they were made in. AssembledFlora.Grow
            // may discard a CanGrow result (random skip), so an unconsumed reservation
            // must not block the site forever.
            if (Time.frameCount == claim.FrameCount) return true;
            occupied.Remove(key);
            return false;
        }

        /// <summary>Same-frame hold on a growth site, placed when GetGrowthInfo returns it.</summary>
        public void Reserve(SchwarzPSiteKey key, SchwarzPAssembler by) =>
            occupied[key] = new Claim { Occupant = by, FrameCount = Time.frameCount, Resident = false };

        /// <summary>Permanent (while alive) claim, placed once the prism actually exists.</summary>
        public void Settle(SchwarzPSiteKey key, SchwarzPAssembler resident) =>
            occupied[key] = new Claim { Occupant = resident, Resident = true };
    }

    /// <summary>
    /// Grows the Schwarz P minimal surface - cos(x) + cos(y) + cos(z) = 0, the same triply
    /// periodic surface that SpawnableSchwarzPSurface stamps out all at once for Joust -
    /// incrementally, prism by prism, as a flora.
    ///
    /// <para>It grows on the surface's own <b>non-Euclidean tile</b>: the hyperbolic {6,4}
    /// hexagon, which on this surface is exactly the patch inside one half-period cube (see
    /// <see cref="SchwarzPTileData"/> for the full derivation and
    /// <c>Tools/Build/measure_schwarz_p_tile.py</c> for the measurement). A prism's address
    /// is therefore an integer cube index plus a site index within the tile, and its
    /// position is arithmetic on a measured offset.</para>
    ///
    /// <para><b>What this replaced, and why.</b> The first version marched a quasi-square
    /// array: from each prism, step a tangent direction by a fixed distance, Newton-project
    /// back onto the zero level set, orient to the gradient, and parallel-transport the
    /// heading onward. It works, but the surface is hyperbolic - it admits no Euclidean
    /// lattice at all - so a square-ish array can only ever be an approximation of one:
    /// the four sites it offers are not the surface's own neighbours, the walk accumulates
    /// drift because every position is computed from the previous one, growth fronts
    /// arriving at the same place from different directions do not agree, and occupancy had
    /// to be a QUANTIZED FLOAT KEY to paper over the mismatch. None of that survives here.
    /// Sites are exact, sites are shared, and two fronts meeting from opposite sides of the
    /// surface land on the same integer key.</para>
    /// </summary>
    public class SchwarzPAssembler : Assembler
    {
        [Header("Surface Lattice")]
        [Tooltip("Target world-space distance between neighbouring prisms. The tile itself " +
                 "is fixed by the surface; this picks how finely it is filled (the nearest " +
                 "measured subdivision wins - see SchwarzPTileData.ResolveLevel).")]
        [SerializeField] float separationDistance = 6f;

        [Tooltip("World-space size of one full period of the surface - the tunnel-to-tunnel " +
                 "repeat. One tile spans half of it.")]
        [SerializeField] float periodScale = 60f;

        [Tooltip("Cross-structure clearance probe, as a fraction of the measured MEAN prism " +
                 "spacing. TryReserve rejects anything within this radius, so it must stay " +
                 "under the MINIMUM spacing - 0.89 of the mean on the measured layouts. The " +
                 "authored 0.45 keeps a 2x margin while still catching any duplicate, which " +
                 "lands at zero distance.")]
        [SerializeField] float overlapProbeScale = 0.45f;

        // Surface-frame state: programmed by the parent for grown prisms, built lazily for the seed.
        SchwarzPSurfaceFrame frame;
        Vector3Int tile;
        int site;

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
        /// Copies the parent's surface frame and this prism's address on it. Called by
        /// AssembledFlora.AssemblerFactory right after the new prism is instantiated.
        /// </summary>
        public void Program(SchwarzPGrowthInfo info)
        {
            frame = info.Frame;
            tile = info.Tile;
            site = info.Site;
            depth = info.Depth;
            frame.Settle(new SchwarzPSiteKey(tile, site), this);
        }

        public override bool IsFullyBonded()
        {
            if (frame == null) return false;

            var bonds = SchwarzPTileData.GetBonds(frame.Level, site);
            for (int i = 0; i < bonds.Length; i++)
            {
                if (!frame.IsOccupied(new SchwarzPSiteKey(
                        SchwarzPTileData.NeighbourTile(tile, bonds[i].TileDelta), bonds[i].Site)))
                    return false;
            }
            return true;
        }

        public override void StartBonding()
        {
            // Flora growth drives this assembler exclusively through GetGrowthInfo; there
            // is no free-prism recruitment phase like the gyroid's mate search.
        }

        public override GrowthInfo GetGrowthInfo()
        {
            if (!Prism) Prism = GetComponent<Prism>();
            EnsureSeeded();

            var bonds = SchwarzPTileData.GetBonds(frame.Level, site);
            if (bonds.Length == 0) return new GrowthInfo { CanGrow = false };

            // Start the sweep at a per-prism offset so the whole plant does not prefer the
            // same bond index and grow a directional tendril. Deterministic (no RNG), so a
            // prism re-probed after a graze offers its sites in the same order.
            int start = BondStartIndex(bonds.Length);

            for (int n = 0; n < bonds.Length; n++)
            {
                var bond = bonds[(start + n) % bonds.Length];
                Vector3Int neighbourTile = SchwarzPTileData.NeighbourTile(tile, bond.TileDelta);
                var key = new SchwarzPSiteKey(neighbourTile, bond.Site);

                if (frame.IsOccupied(key)) continue;

                Vector3 param = SchwarzPTileData.SiteParam(neighbourTile, frame.Level, bond.Site);
                Vector3 worldPosition = frame.ToWorld(param);

                Vector3 paramNormal = SchwarzPTileData.SurfaceNormalParam(param);
                Vector3 paramTangent = SchwarzPTileData.SiteTangentParam(neighbourTile, frame.Level, bond.Site);
                Quaternion worldRotation = Quaternion.LookRotation(
                    frame.ToWorldDirection(paramNormal),
                    frame.ToWorldDirection(Orthogonalize(paramTangent, paramNormal)));

                // World-space occupancy via PrismSpatialIndex.TryReserve - catches foreign
                // geometry the tile registry above can't know about (vessel trails, other
                // floras, environment prisms). The tile registry stays as this flora's own
                // lattice bookkeeping; the spatial index is the cross-structure authority.
                // Not Physics.CheckBox, which is structurally blind to prisms inside their
                // 0.6s disabled-collider spawn window (Prism.waitTime) - see
                // Docs/SPATIAL_INDEX.md.
                // Purely proportional to the measured spacing, so it stays correct at any
                // periodScale. An absolute floor would not: at the finest level the sites
                // are 0.31 rad apart, which is under 2 world units once periodScale drops
                // below ~40 - and a floor above the spacing rejects the plant's own next
                // site, which reads as growth mysteriously stalling.
                float clearRadius = overlapProbeScale * frame.WorldSpacing;
                var spatialIndex = PrismSpatialIndex.EnsureInstance();
                if (spatialIndex != null && spatialIndex.IsAvailable &&
                    !spatialIndex.TryReserve(worldPosition, clearRadius))
                    continue;

                frame.Reserve(key, this);    // upgraded to Settle by the child's Program

                return new SchwarzPGrowthInfo
                {
                    CanGrow = true,
                    Position = worldPosition,
                    Rotation = worldRotation,
                    Depth = depth - 1,
                    Frame = frame,
                    Tile = neighbourTile,
                    Site = bond.Site
                };
            }

            // Nothing viable this cycle. Sites are re-probed live each call, so a branch
            // culled here can still heal a wound later when ReseedBranches re-activates it
            // after fauna consume a neighbour (mass is conserved - no decay, only grazing).
            return new GrowthInfo { CanGrow = false };
        }

        int BondStartIndex(int count)
        {
            if (count <= 1) return 0;
            unchecked
            {
                int hash = tile.x * 73856093 ^ tile.y * 19349663 ^ tile.z * 83492791 ^ site * 15485863;
                return (hash & 0x7fffffff) % count;
            }
        }

        /// <summary>
        /// Anchors the tile lattice to the seed prism. The seed takes tile (0,0,0) site 0 -
        /// the flat point at the tile's centre, where the surface's own 6-fold symmetry
        /// sits - and its transform orients the whole surface: prism forward becomes the
        /// surface normal there.
        /// </summary>
        void EnsureSeeded()
        {
            if (frame != null) return;

            int level = SchwarzPTileData.ResolveLevel(separationDistance, periodScale);
            tile = Vector3Int.zero;
            site = 0;

            Vector3 anchor = SchwarzPTileData.SiteParam(tile, level, site);
            Vector3 anchorNormal = SchwarzPTileData.SurfaceNormalParam(anchor);
            Vector3 anchorTangent = Orthogonalize(
                SchwarzPTileData.SiteTangentParam(tile, level, site), anchorNormal);

            frame = new SchwarzPSurfaceFrame
            {
                WorldOrigin = transform.position,
                WorldRotation = transform.rotation *
                                Quaternion.Inverse(Quaternion.LookRotation(anchorNormal, anchorTangent)),
                ParamAnchor = anchor,
                WorldScale = periodScale / SchwarzPTileData.ParamPeriod,
                WorldSpacing = SchwarzPTileData.WorldSpacing(level, periodScale),
                Level = level
            };

            frame.Settle(new SchwarzPSiteKey(tile, site), this);
        }

        /// <summary>
        /// A pure PREVIEW of the surface this assembler grows - the tunnel network as an
        /// icon, in the flora's own local space, spawning nothing.
        ///
        /// It is the real thing, not an impression: the same subdivision level, the same
        /// measured tile sites, the same integer tile arithmetic and the same param-to-local
        /// mapping the live <see cref="SchwarzPSurfaceFrame"/> uses. What it does NOT do is
        /// claim anything - the live occupancy registry and the cross-structure
        /// <c>PrismSpatialIndex</c> reservation are replaced by a local visited set, because
        /// a preview must not touch shared state.
        ///
        /// Growth order is breadth-first, so a small budget still reads as a patch of
        /// surface spreading out from the seed rather than one long tendril.
        /// </summary>
        /// <param name="budget">Maximum prisms to report.</param>
        /// <param name="tileScale">Prism scale to report for each site.</param>
        /// <param name="into">Receives local-space poses.</param>
        public bool TryPreviewLattice(int budget, Vector3 tileScale, List<SpawnPoint> into)
        {
            if (into == null || budget <= 0) return false;

            float worldScale = periodScale / SchwarzPTileData.ParamPeriod;
            if (worldScale <= 1e-4f) return false;

            int level = SchwarzPTileData.ResolveLevel(separationDistance, periodScale);
            Vector3 anchor = SchwarzPTileData.SiteParam(Vector3Int.zero, level, 0);
            Vector3 anchorNormal = SchwarzPTileData.SurfaceNormalParam(anchor);
            Vector3 anchorTangent = Orthogonalize(
                SchwarzPTileData.SiteTangentParam(Vector3Int.zero, level, 0), anchorNormal);

            // The frame's mapping with the flora at identity: local = rotation * (param - anchor) * scale.
            Quaternion frameRotation = Quaternion.Inverse(Quaternion.LookRotation(anchorNormal, anchorTangent));

            var visited = new HashSet<SchwarzPSiteKey> { new(Vector3Int.zero, 0) };
            var frontier = new Queue<SchwarzPSiteKey>();
            frontier.Enqueue(new SchwarzPSiteKey(Vector3Int.zero, 0));
            into.Add(new SpawnPoint(Vector3.zero,
                Quaternion.LookRotation(frameRotation * anchorNormal, frameRotation * anchorTangent),
                tileScale));

            while (frontier.Count > 0 && into.Count < budget)
            {
                var current = frontier.Dequeue();
                var bonds = SchwarzPTileData.GetBonds(level, current.Site);

                for (int i = 0; i < bonds.Length && into.Count < budget; i++)
                {
                    var key = new SchwarzPSiteKey(
                        SchwarzPTileData.NeighbourTile(current.Tile, bonds[i].TileDelta), bonds[i].Site);
                    if (!visited.Add(key)) continue;

                    Vector3 param = SchwarzPTileData.SiteParam(key.Tile, level, key.Site);
                    Vector3 normal = SchwarzPTileData.SurfaceNormalParam(param);
                    Vector3 tangent = Orthogonalize(
                        SchwarzPTileData.SiteTangentParam(key.Tile, level, key.Site), normal);

                    into.Add(new SpawnPoint(
                        frameRotation * ((param - anchor) * worldScale),
                        Quaternion.LookRotation(frameRotation * normal, frameRotation * tangent),
                        tileScale));
                    frontier.Enqueue(key);
                }
            }

            return into.Count > 1;
        }

        /// <summary>Re-seats a measured tangent against the analytic normal at its own site.
        /// The tangent is measured on the canonical tile and carried by a sign flip per odd
        /// axis, so it is already in the tangent plane to float precision - this only takes
        /// out the drift, and covers the flat point, where "radial" has no direction.</summary>
        static Vector3 Orthogonalize(Vector3 tangent, Vector3 normal)
        {
            Vector3 flat = Vector3.ProjectOnPlane(tangent, normal);
            if (flat.sqrMagnitude > 1e-8f) return flat.normalized;

            flat = Vector3.Cross(normal, Vector3.right);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.Cross(normal, Vector3.up);
            return flat.normalized;
        }
    }
}
