using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowChangerManager : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        private void OnEnable()
        {
            cellData.OnCellItemsUpdated.OnRaised += OnCellItemsUpdated;
        }

        private void OnDisable()
        {
            cellData.OnCellItemsUpdated.OnRaised -= OnCellItemsUpdated;
        }

        void OnCellItemsUpdated()
        {
            if (!cellData.TryGetLocalCrystal(out _))
                return;
            
            cellData.OnCellItemsUpdated.OnRaised -= OnCellItemsUpdated;
            SpawnSnows();
        }
        
        private void SpawnSnows()
        {
            var snowChanger = Instantiate(cellData.Config.CytoplasmPrefab, cellData.CellTransform.position, Quaternion.identity);
            snowChanger.Initialize();
        }
    }
}