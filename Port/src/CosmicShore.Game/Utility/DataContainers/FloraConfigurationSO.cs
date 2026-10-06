using CosmicShore.Gameplay;
// Port note: `using Unity.Entities.UI;` (the original [MinMax] source) maps to
// CosmicShore.Engine, where the inert MinMaxAttribute shim lives.
using CosmicShore.Engine;

namespace CosmicShore.Utility
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
