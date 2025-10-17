using System;
using System.Collections.Generic;
using CosmicShore.SOAP;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;


namespace CosmicShore.Game
{
    public class CrystalManager : NetworkBehaviour
    {
        [SerializeField]
        GameDataSO gameData;
        
        [SerializeField]
        CellDataSO cellData;
        
        [SerializeField] Crystal crystalPrefab;
        [SerializeField] bool scaleCrystalPositionWithIntensity;
        [SerializeField] IntVariable intensityLevelData;
        
        Crystal crystal;
        int _itemsAdded;

        private void OnEnable()
        {
            gameData.OnGameStarted += OnGameStarted;
        }

        private void OnDisable()
        {
            gameData.OnGameStarted -= OnGameStarted;
        }

        private void OnGameStarted()
        {
            cellData.CellItems = new List<CellItem>();
            Spawn();
        }
        
        public bool TryRemoveItem(CellItem item)
        {
            if (!cellData.CellItems.Contains(item))
                return false;
            
            cellData.CellItems.Remove(item);
            cellData.OnCellItemsUpdated.Raise();

            return true;
        }
        
        bool TryInitializeAndAdd(CellItem item)
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

        void Spawn()
        {
            var spawnPos = scaleCrystalPositionWithIntensity ? cellData.CellTransform.position * intensityLevelData : Vector3.one * 10; // 10 unit forward if none
            crystal = Instantiate(crystalPrefab, spawnPos, Quaternion.identity, transform);
            crystal.InjectDependencies(this);
            cellData.Crystal = crystal;
            TryInitializeAndAdd(crystal);
            cellData.OnCrystalSpawned.Raise();
        }
    }
}
