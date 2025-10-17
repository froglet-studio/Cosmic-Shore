using System.Collections.Generic;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.SOAP
{
    [CreateAssetMenu(
        fileName = "scriptable_variable_" + nameof(CellDataSO),
        menuName = "ScriptableObjects/DataContainers/" + nameof(CellDataSO))]
    public class CellDataSO : ScriptableObject
    {
        [SerializeField]
        public ScriptableEventNoParam OnCrystalSpawned;
        
        [SerializeField] 
        public ScriptableEventNoParam OnCellItemsUpdated;
        
        public SO_CellType CellType;
        public Cell Cell;
        public Transform CellTransform => Cell.transform;
        public List<CellItem> CellItems;
        public Crystal Crystal;
        public Transform CrystalTransform => Crystal.transform;
        public float CrystalRadius => Crystal.SphereRadius;

        public void ResetRuntimeData()
        {
            CellType = null;
            Cell = null;
            CellItems.Clear();
            Crystal = null;
        }
    }
}