using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class SpawnableGyroid : SpawnableAbstractBase
    {
        [System.Serializable]
        public struct GyroidIntensityConfig
        {
            public GyroidBlockType seedBlockType;
            public int maxDepth;
            public float separationDistance;
            public float overlapCellSize;
            public bool expandTopLeft;
            public bool expandTopRight;
            public bool expandBottomLeft;
            public bool expandBottomRight;
            public Vector3 blockScale;
        }

        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = Vector3.one;

        [Header("Gyroid Structure")]
        [SerializeField] GyroidBlockType seedBlockType = GyroidBlockType.AB;
        [SerializeField] int maxDepth = 5;
        [SerializeField] float separationDistance = 3f;

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

        [Header("Intensity Level Configurations")]
        [Tooltip("One config per intensity level (1-4). Index 0 = intensity 1.")]
        [SerializeField] GyroidIntensityConfig[] intensityConfigs = new GyroidIntensityConfig[]
        {
            // Level 1 — "Helix Spine": single expansion direction produces a snaking
            // chain. Large blocks and wide gaps make it easy to read.
            new()
            {
                seedBlockType = GyroidBlockType.AB,
                maxDepth = 15,
                separationDistance = 5f,
                overlapCellSize = 2f,
                expandTopLeft = false,
                expandTopRight = true,
                expandBottomLeft = false,
                expandBottomRight = false,
                blockScale = new Vector3(1.5f, 1.5f, 1.5f),
            },
            // Level 2 — "Branching Coral": CD seed grows differently from AB, and
            // top-biased expansion creates an upward-reaching fan like coral branches.
            new()
            {
                seedBlockType = GyroidBlockType.CD,
                maxDepth = 25,
                separationDistance = 3.5f,
                overlapCellSize = 1.5f,
                expandTopLeft = true,
                expandTopRight = true,
                expandBottomLeft = false,
                expandBottomRight = false,
                blockScale = new Vector3(1.2f, 1.2f, 1.2f),
            },
            // Level 3 — "Classic Gyroid": the proven diagonal-cross pattern.
            new()
            {
                seedBlockType = GyroidBlockType.AB,
                maxDepth = 35,
                separationDistance = 3f,
                overlapCellSize = 1.5f,
                expandTopLeft = false,
                expandTopRight = true,
                expandBottomLeft = true,
                expandBottomRight = false,
                blockScale = Vector3.one,
            },
            // Level 4 — "Dense Thicket": EF seed with all four expansion directions
            // and tight spacing fills space aggressively. Small blocks pack dense.
            new()
            {
                seedBlockType = GyroidBlockType.EF,
                maxDepth = 45,
                separationDistance = 2f,
                overlapCellSize = 1f,
                expandTopLeft = true,
                expandTopRight = true,
                expandBottomLeft = true,
                expandBottomRight = true,
                blockScale = new Vector3(0.8f, 0.8f, 0.8f),
            },
        };

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
            if (intensityConfigs != null && intensityConfigs.Length > 0)
            {
                var config = intensityConfigs[Mathf.Clamp(intensityLevel - 1, 0, intensityConfigs.Length - 1)];
                seedBlockType = config.seedBlockType;
                maxDepth = config.maxDepth;
                separationDistance = config.separationDistance;
                overlapCellSize = config.overlapCellSize;
                expandTopLeft = config.expandTopLeft;
                expandTopRight = config.expandTopRight;
                expandBottomLeft = config.expandBottomLeft;
                expandBottomRight = config.expandBottomRight;
                blockScale = config.blockScale;
            }
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