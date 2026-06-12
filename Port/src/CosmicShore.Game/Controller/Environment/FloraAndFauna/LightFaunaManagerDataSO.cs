using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "LightFaunaManagerDataSO",
        menuName = "ScriptableObjects/LifeForms/FaunaPrefab/Light FaunaPrefab Manager Data")]
    public class LightFaunaManagerDataSO : ScriptableObject
    {
        [Header("Spawn Settings")]
        [Tooltip("Baseline batch size when the cell is empty of prisms. Scaled up by extraFaunaPerHundredPrisms when the cell is loaded.")]
        [Min(0)] public int spawnCount = 10;
        [Min(0f)] public float spawnRadius = 10f;

        [Header("Population Response")]
        [Tooltip("Extra fauna added per 100 prisms in the host cell. Lets the population grow with food supply so consumption keeps pace with flora growth without bolting on a hard prism cap. Set to 0 to disable.")]
        [Min(0)] public int extraFaunaPerHundredPrisms = 4;
        [Tooltip("Optional ceiling on total fauna spawned per group, after the prism-load scaling. 0 disables the ceiling.")]
        [Min(0)] public int maxFaunaPerGroup = 0;

        [Header("Formation Settings")]
        [Min(0f)] public float formationSpread = 5f;
        public float phaseIncrease = 0.1f;
    }
}