using Unity.Entities.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "Flora Configuration",
        menuName = "ScriptableObjects/DataContainers/" + nameof(FloraConfigurationSO))]
    public class FloraConfigurationSO : ScriptableObject
    {
        public Flora FloraPrefab;
        [MinMax(0f, 1f)]
        public float SpawnProbability;
        public int InitialSpawnCount;
        public bool OverrideDefaultPlantPeriod;
        public int NewPlantPeriod = int.MaxValue;
    }
}