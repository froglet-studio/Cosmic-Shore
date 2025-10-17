using System;
using System.Collections.Generic;
using CosmicShore.SOAP;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;


namespace CosmicShore.Game
{
    public abstract class CrystalManager : NetworkBehaviour
    {
        const int MIN_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS = 100;
        
        [SerializeField]
        protected GameDataSO gameData;
        
        [SerializeField]
        protected CellDataSO cellData;
        
        [SerializeField] 
        protected Crystal crystalPrefab;
        [SerializeField] bool scaleCrystalPositionWithIntensity;
        [SerializeField] IntVariable intensityLevelData;
        
        int _itemsAdded;
        
        Vector3 lastSpawnPos;

        void Awake()
        {
            cellData.CellItems = new List<CellItem>();
        }
        
        public bool TryRemoveItem(CellItem item)
        {
            if (!cellData.CellItems.Contains(item))
                return false;
            
            cellData.CellItems.Remove(item);
            cellData.OnCellItemsUpdated.Raise();

            return true;
        }
        
        protected bool TryInitializeAndAdd(CellItem item)
        {
            if (item.Id != 0)
            {
                Debug.LogError("To initialize a cell item, it's default Id must be 0");
                return false;
            }
            
            // item.Initialize(++_itemsAdded, this);
            item.Initialize(++_itemsAdded);
            cellData.CellItems.Add(item);
            cellData.OnCellItemsUpdated.Raise();
            return true;
        }
        
        protected abstract void Spawn(Vector3 spawnPos);

        protected Vector3 CalculateSpawnPos() => 
            scaleCrystalPositionWithIntensity ? cellData.CellTransform.position * intensityLevelData : Vector3.forward * 30; // 30 unit forward if none

        protected Vector3 CalculateNewSpawnPos()
        {
            Vector3 spawnPos;
            do
            {
                spawnPos = Random.insideUnitSphere * cellData.CrystalRadius + cellData.CellTransform.position;
            } while (Vector3.SqrMagnitude(lastSpawnPos - spawnPos) <= MIN_SPACE_BTWN_CURRENT_AND_LAST_SPAWN_POS);
            
            lastSpawnPos = spawnPos;
            return spawnPos;
        }

        public abstract void RespawnCrystal();

        public abstract void ExplodeCrystal(Crystal.ExplodeParams explodeParams);

        protected void UpdateCrystalPos(Vector3 newPos)
        {
            var crystal = cellData.Crystal;
            crystal.DeactivateModels();
            crystal.MoveToNewPos(newPos);
            cellData.OnCellItemsUpdated.Raise();
        }
    }
}
