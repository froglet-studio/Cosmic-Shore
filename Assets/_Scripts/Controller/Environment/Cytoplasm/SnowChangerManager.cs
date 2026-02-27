using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class SnowChangerManager : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        bool _spawned;

        private void OnEnable()
        {
            _spawned = false;
            cellData.OnCellItemsUpdated.OnRaised += OnCellItemsUpdated;

            // If cell is already initialized and has crystals (party mode: cell init
            // and crystal spawn may have happened before this component re-enabled),
            // spawn immediately instead of waiting for the next OnCellItemsUpdated.
            TrySpawnIfReady();
        }

        private void OnDisable()
        {
            cellData.OnCellItemsUpdated.OnRaised -= OnCellItemsUpdated;
        }

        void OnCellItemsUpdated()
        {
            TrySpawnIfReady();
        }

        void TrySpawnIfReady()
        {
            if (_spawned) return;

            if (!cellData.TryGetLocalCrystal(out _))
                return;

            if (cellData.Config == null || cellData.CellTransform == null)
                return;

            _spawned = true;
            cellData.OnCellItemsUpdated.OnRaised -= OnCellItemsUpdated;
            SpawnSnows();
        }

        private void SpawnSnows()
        {
            if (cellData.Config == null || cellData.Config.CytoplasmPrefab == null || cellData.CellTransform == null)
            {
                CSDebug.LogWarning($"[SnowChangerManager] Cannot spawn: Config={cellData.Config != null}, " +
                    $"CytoplasmPrefab={cellData.Config?.CytoplasmPrefab != null}, CellTransform={cellData.CellTransform != null}");
                return;
            }

            var snowChanger = Instantiate(cellData.Config.CytoplasmPrefab, cellData.CellTransform.position, Quaternion.identity);
            snowChanger.Initialize();
        }
    }
}
