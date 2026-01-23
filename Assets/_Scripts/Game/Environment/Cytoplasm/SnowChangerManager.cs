using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowChangerManager : MonoBehaviour
    {
        [SerializeField]
        CellDataSO cellData;
        
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
            cellData.OnCrystalSpawned.OnRaised -= OnCrystalSpawned;
            
            var snowChanger = Instantiate(cellData.CellType.CytoplasmPrefab, cellData.CellTransform.position, Quaternion.identity);
            snowChanger.SetOrigin(cellData.CellTransform.position);
            snowChanger.Initialize();
        }
    }
}