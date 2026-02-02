using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowChangerManager : MonoBehaviour
    {
        [SerializeField]
        CellDataSO cellData;
        
        SnowChanger snowChanger;
        
        private void OnEnable()
        {
            cellData.OnCrystalSpawned.OnRaised += OnCrystalSpawned;
            
        }

        private void OnDisable()
        {
            cellData.OnCrystalSpawned.OnRaised -= OnCrystalSpawned;
        }

        private void OnCrystalSpawned()
        {
            SpawnSnows();
            snowChanger.ChangeSnowOrientation(cellData.CellTransform.position);
        }

        private void SpawnSnows()
        {
            if (snowChanger)
                return;
            
            snowChanger = Instantiate(cellData.CellType.CytoplasmPrefab, cellData.CellTransform.position, Quaternion.identity);
            snowChanger.Initialize();
        }
    }
}