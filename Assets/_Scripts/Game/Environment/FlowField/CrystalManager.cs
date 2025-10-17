using System;
using System.Collections.Generic;
using CosmicShore.SOAP;
using CosmicShore.Utilities;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class CrystalManager : SingletonNetwork<CrystalManager>
    {
        [SerializeField] GameDataSO gameData;
        [SerializeField] Crystal crystalPrefab;
        [SerializeField] bool scaleCrystalPositionWithIntensity;
        [SerializeField] IntVariable intensityLevelData;
        [SerializeField] ScriptableEventNoParam OnCellItemsUpdated;
        
        public List<CellItem> CellItems { get; private set; }
        
        Crystal crystal;
        int _itemsAdded;
        
        void OnEnable()
        {
            gameData.OnGameStarted += Initialize;
        }

        void OnDisable()
        {
            gameData.OnGameStarted -= Initialize;
        }
        
        public void SetOrigin(Vector3 position) => crystal.SetOrigin(position);
        public Transform GetCrystalTransform() => crystal.transform;
        public float GetSphereRadius() => crystal.SphereRadius;
        public void UpdateItem() => OnCellItemsUpdated.Raise();

        public bool TryRemoveItem(CellItem item)
        {
            if (!CellItems.Contains(item))
                return false;
            
            CellItems.Remove(item);
            OnCellItemsUpdated.Raise();

            return true;
        }
        
        void Initialize()
        {
            CellItems = new List<CellItem>();
            Spawn();
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
            CellItems.Add(item);
            OnCellItemsUpdated.Raise();

            return true;
        }

        void Spawn()
        {
            crystal = Instantiate(crystalPrefab, Vector3.zero, Quaternion.identity, transform);
            crystal.transform.position = scaleCrystalPositionWithIntensity ? crystal.Origin * intensityLevelData : crystal.transform.position;
            TryInitializeAndAdd(crystal);
        }
    }
}