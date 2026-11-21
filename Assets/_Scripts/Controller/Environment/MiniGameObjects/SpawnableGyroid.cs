using CosmicShore.Gameplay;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class SpawnableGyroid : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = Vector3.one;

        /// <summary>Laying slice per frame before the load gate takes over (it raises the slice
        /// while the connecting screen is covered — see PrismTrailBuilder.EffectiveLayBudget).</summary>
        const float LayBudgetMsPerFrame = 100f;

        [Header("Gyroid Structure")]
        [SerializeField] GyroidBlockType seedBlockType = GyroidBlockType.AB;
        [SerializeField] int maxDepth = 35;
        [SerializeField] float separationDistance = 3.5f;

        [Tooltip("Spatial grid cell size for overlap detection. Blocks quantized to the same cell are considered overlapping.")]
        [SerializeField] float overlapCellSize = 1.5f;

        [Header("Expansion Sites")]
        [Tooltip("Which corner sites to grow from. Matches the gyroid flora default of TopRight + BottomLeft.")]
        [SerializeField] bool expandTopLeft = false;
        [SerializeField] bool expandTopRight = true;
        [SerializeField] bool expandBottomLeft = true;
        [SerializeField] bool expandBottomRight = false;

        [Header("Visual")]
        [Tooltip("Use a different domain for 'dangerous' block types (DE, EG, GEs, EsD) to match gyroid flora visuals.")]
        [SerializeField] bool colorDangerousBlocks = true;
        [SerializeField] Domains dangerousDomain = Domains.Ruby;

        struct GyroidNode
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public GyroidBlockType BlockType;
            public int Depth;
        }

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var nodes = ComputeGyroidPositions();
            var points = new SpawnPoint[nodes.Count];
            var lengthFactors = FlushLengthFactors();

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var scale = blockScale;
                scale.x *= lengthFactors[(int)node.BlockType];
                points[i] = new SpawnPoint(node.Position, node.Rotation, scale);
            }

            // Store node data for SpawnLeafObjects to use for per-block domain coloring
            _cachedNodes = nodes;

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        // Cache nodes for per-block domain assignment in SpawnLeafObjects
        private List<GyroidNode> _cachedNodes;

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedNodes == null) return;

            // A gyroid is a 2D prismscape - a SHELL, not a ribbon. The Trail here is the
            // general lay container, and the layer declares the shape it laid so topology
            // consumers (the Urchin's ride routing) roll ACROSS it rather than sliding along
            // the lay order.
            var trail = new Trail { Dimension = PrismscapeDimension.Surface };
            var nodes = _cachedNodes;

            // Per-block domain (dangerous blocks recolour), so this routes through the PrismLay
            // overload of the shared builder. Building the list is cheap; the cost is the
            // cloning, which the builder batches across worker threads.
            var elems = new PrismLay[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                bool isDangerous = node.BlockType is GyroidBlockType.GEs or GyroidBlockType.DE
                    or GyroidBlockType.EG or GyroidBlockType.EsD;

                Domains blockDomain = (colorDangerousBlocks && isDangerous) ? dangerousDomain : domain;

                // The pose GenerateTrailData sized flush (Docs/ECOSYSTEM.md §49.10), not blockScale.
                elems[i] = new PrismLay(trailData[0].Points[i], blockDomain);
            }

            // Streamed + batched at play time (this assembly is the intensity-4 structure in both
            // Joust and Crystal Capture; laying it in one frame froze the load). The arena-ready
            // gate holds the connecting screen until every prism is revealed and grown, so
            // streaming can never leak into play. Edit-mode spawns stay synchronous.
            if (Application.isPlaying)
                PrismTrailBuilder.LayBudgetedAsync(prism, elems, container.transform, trail,
                    $"{container.name}::BLOCK", LayBudgetMsPerFrame).Forget();
            else
                PrismTrailBuilder.LaySync(prism, elems, container.transform, trail, $"{container.name}::BLOCK");

            trails.Add(trail);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seedBlockType, maxDepth, separationDistance, overlapCellSize,
                System.HashCode.Combine(expandTopLeft, expandTopRight, expandBottomLeft, expandBottomRight, blockScale, seed));
        }

        /// <summary>Lengths are sized to this fraction of their exact flush length, so float noise
        /// never turns two faces that touch into a clip.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>
        /// Per block type, the fraction of blockScale.x (the long axis) at which every bond that
        /// block type makes is FLUSH (Docs/ECOSYSTEM.md §49.10), indexed by (int)GyroidBlockType.
        /// Read off the bond table itself: each bond's mate pose relative to its substrate is fixed
        /// (TryExpand composes it onto the parent's frame), so whether the two blocks touch or clip
        /// does not depend on where in the assembly the bond sits. 44 of the 48 bonds are flush at
        /// full length; the four short ones (EG/EF and EsC/EsD at their bottom corners, 7.66 apart
        /// where the rest sit 7.7-8.7 apart at the shipped separation) ran their tips into each
        /// other - about 5,250 of the shipped prefab's 6,487 clipping pairs - and those four block
        /// types shorten by about 6%. Every other type keeps its full length. Computed once per
        /// generation from the 48 bonds, not per prism, so it costs the load nothing.
        ///
        /// NOT flush yet: 1,233 pairs remain, all from the walk's own drift. The bond table's small
        /// inconsistencies leave a site reached round a loop of bonds up to a few units off where
        /// the other way round put it; the occupancy grid only knows cells, so where the two
        /// arrivals straddle a cell face the site is laid twice (2 coincident, ~580 near-copies a
        /// fraction of a unit apart), and elsewhere two mates sit closer than a bond. Parting those
        /// needs each block checked against its neighbourhood as it is laid - measured at about
        /// +50 ms a generation (the full 26-cell settle, 1 pair left) or +15 ms (closure lookups
        /// only, 713 left) - which is load-time work this generator does not do. Making it flush
        /// for free means fixing the placement itself (a drift-free bond table or a site-exact
        /// occupancy key), which changes which blocks the walk lays and is the owner's call.
        /// </summary>
        float[] FlushLengthFactors()
        {
            var factors = new float[16];
            for (int i = 0; i < factors.Length; i++) factors[i] = 1f;
            Vector3 half = 0.5f * new Vector3(Mathf.Abs(blockScale.x), Mathf.Abs(blockScale.y), Mathf.Abs(blockScale.z));
            foreach (var bond in GyroidBondMateDataContainer.BondMateDataMap)
            {
                var parentType = bond.Key.Item1;
                var root = new GyroidNode { Position = Vector3.zero, Rotation = Quaternion.identity, BlockType = parentType };
                var (offset, rotation) = MatePose(root, bond.Value);
                float f = FlushLengthFactor(offset, new Frame(Quaternion.identity), new Frame(rotation), half, half);
                factors[(int)parentType] = Mathf.Min(factors[(int)parentType], f);
                factors[(int)bond.Value.BlockType] = Mathf.Min(factors[(int)bond.Value.BlockType], f);
            }
            return factors;
        }

        /// <summary>A block's three axes, rotated once when it is placed.</summary>
        readonly struct Frame
        {
            public readonly Vector3 X, Y, Z;
            public Frame(Quaternion r) { X = r * Vector3.right; Y = r * Vector3.up; Z = r * Vector3.forward; }
        }

        /// <summary>
        /// The largest common factor k in (0, 1] on both boxes' x half-length (the long axis) at
        /// which two boxes are flush - closed form, no search. On every one of the 15 separating
        /// axes L the clearance is linear in k, |d.L| - c_L - k b_L, so axis L separates for k up
        /// to (|d.L| - c_L) / b_L, and the pair is flush for the best of the 15. Returns 1 when the
        /// pair already clears at full length, and 1 as well when no k separates them (coincident
        /// centres - a placement fault no length fixes).
        /// </summary>
        static float FlushLengthFactor(Vector3 d, in Frame a, in Frame b, Vector3 halfA, Vector3 halfB)
        {
            float best = float.NegativeInfinity;
            for (int i = 0; i < 15; i++)
            {
                Vector3 l = Axis(i, a, b);
                if (l.sqrMagnitude < 1e-10f) continue;
                float gap = Mathf.Abs(Vector3.Dot(d, l))
                          - halfA.y * Mathf.Abs(Vector3.Dot(a.Y, l)) - halfB.y * Mathf.Abs(Vector3.Dot(b.Y, l))
                          - halfA.z * Mathf.Abs(Vector3.Dot(a.Z, l)) - halfB.z * Mathf.Abs(Vector3.Dot(b.Z, l));
                if (gap < 0f) continue;
                float lengthTerm = halfA.x * Mathf.Abs(Vector3.Dot(a.X, l)) + halfB.x * Mathf.Abs(Vector3.Dot(b.X, l));
                if (lengthTerm <= gap) return 1f;
                best = Mathf.Max(best, gap / lengthTerm);
            }
            return best > 0f ? Mathf.Min(1f, best * FlushMargin) : 1f;
        }

        /// <summary>The 15 separating-axis candidates of two boxes: their six face normals, then the
        /// nine cross products of an edge of each. The short (y) face normals come first: mates sit
        /// off each other's corners, so that is where a bonded pair nearly always parts.</summary>
        static Vector3 Axis(int i, in Frame a, in Frame b) => i switch
        {
            0 => a.Y, 1 => b.Y, 2 => a.X, 3 => b.X, 4 => a.Z, 5 => b.Z,
            _ => Vector3.Cross((i - 6) / 3 == 0 ? a.X : (i - 6) / 3 == 1 ? a.Y : a.Z,
                               (i - 6) % 3 == 0 ? b.X : (i - 6) % 3 == 1 ? b.Y : b.Z),
        };

        List<GyroidNode> ComputeGyroidPositions()
        {
            var result = new List<GyroidNode>();
            var occupiedCells = new HashSet<Vector3Int>();
            var queue = new Queue<GyroidNode>();

            var seedNode = new GyroidNode
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                BlockType = seedBlockType,
                Depth = maxDepth
            };

            queue.Enqueue(seedNode);
            MarkOccupied(seedNode.Position, occupiedCells);
            result.Add(seedNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Depth <= 0) continue;

                if (expandTopRight)
                    TryExpand(current, CornerSiteType.TopRight, queue, result, occupiedCells);
                if (expandTopLeft)
                    TryExpand(current, CornerSiteType.TopLeft, queue, result, occupiedCells);
                if (expandBottomLeft)
                    TryExpand(current, CornerSiteType.BottomLeft, queue, result, occupiedCells);
                if (expandBottomRight)
                    TryExpand(current, CornerSiteType.BottomRight, queue, result, occupiedCells);
            }

            return result;
        }

        void TryExpand(GyroidNode parent, CornerSiteType site,
            Queue<GyroidNode> queue, List<GyroidNode> result, HashSet<Vector3Int> occupiedCells)
        {
            if (!GyroidBondMateDataContainer.BondMateDataMap.TryGetValue((parent.BlockType, site), out var data))
                return;

            // Compute child position: local offset transformed by parent's frame
            Vector3 localOffset = data.DeltaPosition * separationDistance;
            Vector3 childPosition = parent.Position + parent.Rotation * localOffset;

            // Spatial overlap check
            if (IsOccupied(childPosition, occupiedCells))
                return;

            var (_, childRotation) = MatePose(parent, data);

            var child = new GyroidNode
            {
                Position = childPosition,
                Rotation = childRotation,
                BlockType = data.BlockType,
                Depth = parent.Depth - 1
            };

            MarkOccupied(childPosition, occupiedCells);
            result.Add(child);
            queue.Enqueue(child);
        }

        /// <summary>The mate's offset from its substrate (in world axes) and its rotation, from the
        /// substrate's frame plus the bond's deltas. Mirrors GyroidAssembler.CalculateRotation logic.</summary>
        (Vector3 offset, Quaternion rotation) MatePose(in GyroidNode parent, in GyroidBondMateData data)
        {
            Vector3 offset = parent.Rotation * (data.DeltaPosition * separationDistance);

            Vector3 parentRight = parent.Rotation * Vector3.right;
            Vector3 parentUp = parent.Rotation * Vector3.up;
            Vector3 parentForward = parent.Rotation * Vector3.forward;

            Vector3 childForward = data.DeltaForward.x * parentRight
                                 + data.DeltaForward.y * parentUp
                                 + data.DeltaForward.z * parentForward
                                 + parentForward;

            Vector3 childUp = data.DeltaUp.x * parentRight
                            + data.DeltaUp.y * parentUp
                            + data.DeltaUp.z * parentForward
                            + parentUp;

            if (childForward.sqrMagnitude < 0.0001f || childUp.sqrMagnitude < 0.0001f)
                return (offset, parent.Rotation);
            return (offset, Quaternion.LookRotation(childForward.normalized, childUp.normalized));
        }

        Vector3Int Quantize(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x / overlapCellSize),
                Mathf.RoundToInt(position.y / overlapCellSize),
                Mathf.RoundToInt(position.z / overlapCellSize)
            );
        }

        void MarkOccupied(Vector3 position, HashSet<Vector3Int> occupied)
        {
            occupied.Add(Quantize(position));
        }

        bool IsOccupied(Vector3 position, HashSet<Vector3Int> occupied)
        {
            return occupied.Contains(Quantize(position));
        }
    }
}
