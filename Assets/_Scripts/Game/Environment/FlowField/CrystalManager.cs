using System;
using System.Collections.Generic;
using CosmicShore.Soap;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Game
{
    [Serializable]
    public class CrystalPositionSet
    {
        public List<Vector3> positions;
    }

    public abstract class CrystalManager : NetworkBehaviour
    {
        // This is a squared distance threshold because we compare SqrMagnitude.
        // If you want 100 units minimum distance, set this to 100f * 100f.
        const float MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS = 25;

        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected CellDataSO cellData;

        [SerializeField] protected Crystal crystalPrefab;

        [SerializeField] bool scaleCrystalPositionWithIntensity;
        [SerializeField] IntVariable intensityLevelData;

        [SerializeField] List<CrystalPositionSet> listOfCrystalPositions;

        // Runtime state
        readonly Dictionary<int, Vector3> lastSpawnPosById = new();

        int _itemsAdded;
        int crystalPositionIndex;

        protected virtual void Awake()
        {
            cellData.CellItems = new List<CellItem>();
            if (cellData.Crystals == null)
                cellData.Crystals = new List<Crystal>();
        }

        public bool TryRemoveItem(CellItem item)
        {
            if (!cellData.CellItems.Contains(item))
                return false;

            cellData.CellItems.Remove(item);
            cellData.OnCellItemsUpdated.Raise();
            return true;
        }

        protected bool TryInitializeAndAdd(CellItem item, int forcedId = 0)
        {
            if (item.Id != 0)
            {
                Debug.LogError("To initialize a cell item, its default Id must be 0");
                return false;
            }

            int id = forcedId != 0 ? forcedId : (++_itemsAdded);
            _itemsAdded = Mathf.Max(_itemsAdded, id);

            item.Initialize(id);
            cellData.CellItems.Add(item);
            cellData.OnCellItemsUpdated.Raise();
            return true;
        }

        protected virtual Crystal Spawn(int crystalId, Vector3 spawnPos)
        {
            if (cellData.TryGetCrystalById(crystalId, out Crystal existing))
            {
                DebugExtensions.LogErrorColored(
                    $"Crystal with id {crystalId} already exists, skipping spawn.",
                    Color.magenta
                );
                return existing;
            }

            var crystal = Instantiate(crystalPrefab, spawnPos, Quaternion.identity, transform);
            crystal.InjectDependencies(this);

            cellData.Crystals.Add(crystal);
            TryInitializeAndAdd(crystal, crystalId);
            lastSpawnPosById[crystalId] = spawnPos;

            cellData.OnCrystalSpawned.Raise();
            return crystal;
        }

        // --------------------------------------------------------------------
        // ✅ Batch spawn using SAME anchor index for the whole batch
        // --------------------------------------------------------------------
        protected void SpawnBatchIfMissing()
        {
            int count = Mathf.Max(1, gameData.SelectedPlayerCount.Value);
            for (int id = 1; id <= count; id++)
            {
                if (!cellData.TryGetCrystalById(id, out _))
                {
                    Spawn(id, GetSpawnPointBasedOnCurrentAnchor());
                }
            }

            // Advance index ONCE after finishing the batch.
            AdvanceSpawnAnchorIndex();
        }

        // --------------------------------------------------------------------
        // Respawn for a specific crystalId (keeps it away from its last spawn)
        // --------------------------------------------------------------------
        protected Vector3 CalculateNewSpawnPos(int crystalId)
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] positions))
                return Random.insideUnitSphere * cellData.CrystalRadius + cellData.CellTransform.position;

            Vector3 last = lastSpawnPosById.TryGetValue(crystalId, out var l) ? l : Vector3.positiveInfinity;
            Vector3 spawnPos;

            int safety = 0;
            const int MAX_TRIES = 50;

            do
            {
                if (positions != null && positions.Length > 0)
                {
                    crystalPositionIndex %= positions.Length;

                    spawnPos = GetSpawnPointAroundAnchor(positions[crystalPositionIndex]);

                    // For respawns, it’s OK to advance (keeps distribution moving)
                    crystalPositionIndex = (crystalPositionIndex + 1) % positions.Length;
                }
                else
                {
                    spawnPos = Random.insideUnitSphere * cellData.CrystalRadius + cellData.CellTransform.position;
                }

                safety++;
                if (safety >= MAX_TRIES)
                    break;

            } while (Vector3.SqrMagnitude(last - spawnPos) <= MIN_SQR_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS);

            lastSpawnPosById[crystalId] = spawnPos;
            return spawnPos;
        }

        protected void UpdateCrystalPos(int crystalId, Vector3 newPos)
        {
            if (!cellData.TryGetCrystalById(crystalId, out var crystal))
                return;

            crystal.DeactivateModels();
            crystal.MoveToNewPos(newPos);

            cellData.OnCellItemsUpdated.Raise();
        }

        public abstract void RespawnCrystal(int crystalId);
        public abstract void ExplodeCrystal(int crystalId, Crystal.ExplodeParams explodeParams);

        // --------------------------------------------------------------------
        // ✅ Positions list getter (fixed)
        // --------------------------------------------------------------------
        bool TryGetCrystalPositionListByIntensity(out Vector3[] positions)
        {
            positions = null;

            if (listOfCrystalPositions == null || listOfCrystalPositions.Count == 0)
                return false;

            int intensity = Mathf.Clamp(intensityLevelData ? intensityLevelData.Value : 1, 1, listOfCrystalPositions.Count);
            var set = listOfCrystalPositions[intensity - 1];

            if (set == null || set.positions == null || set.positions.Count == 0)
            {
                positions = Array.Empty<Vector3>();
                return true; // we DID resolve the list; it's just empty
            }

            positions = set.positions.ToArray();
            return true;
        }

        // --------------------------------------------------------------------
        // ✅ Anchor helpers for batch spawning
        // --------------------------------------------------------------------
        protected Vector3 GetCurrentSpawnAnchor()
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] positions))
                return Vector3.forward * 30f;

            if (positions != null && positions.Length > 0)
            {
                crystalPositionIndex %= positions.Length;
                return positions[crystalPositionIndex];
            }

            return Vector3.forward * 30f;
        }

        protected void AdvanceSpawnAnchorIndex()
        {
            if (!TryGetCrystalPositionListByIntensity(out Vector3[] positions))
                return;

            if (positions != null && positions.Length > 0)
                crystalPositionIndex = (crystalPositionIndex + 1) % positions.Length;
        }
        
        protected Vector3 GetSpawnPointBasedOnCurrentAnchor()
        {
            Vector3 anchor = GetCurrentSpawnAnchor();
            Vector3 spawnPos = GetSpawnPointAroundAnchor(anchor);
            return spawnPos;
        }
        
        protected Vector3 GetSpawnPointAroundAnchor(Vector3 anchor) =>
        anchor + Random.onUnitSphere * 35f;
    }
}
