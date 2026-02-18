using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class SpawnableGyroid : SpawnableAbstractBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = Vector3.one;

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

        static int ObjectsSpawned = 0;

        struct GyroidNode
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public GyroidBlockType BlockType;
            public int Depth;
        }

        public override GameObject Spawn()
        {
            GameObject container = new GameObject();
            container.name = "Gyroid" + ObjectsSpawned++;

            var trail = new Trail();
            var nodes = ComputeGyroidPositions();

            foreach (var node in nodes)
            {
                bool isDangerous = node.BlockType is GyroidBlockType.GEs or GyroidBlockType.DE
                    or GyroidBlockType.EG or GyroidBlockType.EsD;

                Domains blockDomain = (colorDangerousBlocks && isDangerous) ? dangerousDomain : domain;

                var block = Instantiate(prism);
                block.ChangeTeam(blockDomain);
                block.ownerID = "public";
                block.transform.SetPositionAndRotation(node.Position, node.Rotation);
                block.transform.SetParent(container.transform, false);
                block.ownerID = $"{container.name}::BLOCK::{trail.TrailList.Count}";
                block.TargetScale = blockScale;
                block.Trail = trail;
                block.Initialize();
                trail.Add(block);
            }

            trails.Add(trail);
            return container;
        }

        public override GameObject Spawn(int intensityLevel)
        {
            return Spawn();
        }

        List<GyroidNode> ComputeGyroidPositions()
        {
            var result = new List<GyroidNode>();
            var occupiedCells = new HashSet<Vector3Int>();
            var queue = new Queue<GyroidNode>();

            var seed = new GyroidNode
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                BlockType = seedBlockType,
                Depth = maxDepth
            };

            queue.Enqueue(seed);
            MarkOccupied(seed.Position, occupiedCells);
            result.Add(seed);

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
