using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
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

            if (!TrySpawnSnows())
                return;

            cellData.OnCellItemsUpdated.OnRaised -= OnCellItemsUpdated;
        }

        private bool TrySpawnSnows()
        {
            if (cellData.Config == null || cellData.Config.CytoplasmPrefab == null || cellData.CellTransform == null)
                return false;

            var snowChanger = Instantiate(cellData.Config.CytoplasmPrefab, cellData.CellTransform.position, Quaternion.identity);
            snowChanger.Initialize();
            return true;
        }
    }
}