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

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                points[i] = new SpawnPoint(node.Position, node.Rotation, blockScale);
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

                elems[i] = new PrismLay(
                    new SpawnPoint(node.Position, node.Rotation, blockScale), blockDomain);
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

            // Compute child rotation from parent's basis vectors + bond deltas
            // Mirrors GyroidAssembler.CalculateRotation logic
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

            Quaternion childRotation;
            if (childForward.sqrMagnitude < 0.0001f || childUp.sqrMagnitude < 0.0001f)
                childRotation = parent.Rotation;
            else
                childRotation = Quaternion.LookRotation(childForward.normalized, childUp.normalized);

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
