using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "CellRandomSpawnProfile",
        menuName = "Cosmic Shore/Cells/Cell Random Spawn Profile")]
    public class CellRandomSpawnProfileSO : ScriptableObject
    {
        [Header("Flora")]
        [Min(0)] public int FloraTypeCount = 2;
        public bool SpawnJade = true;
        [Min(0f)] public float FloraSpawnVolumeCeiling = 12000f;

        [Header("Fauna")]
        [Min(0)] public int FaunaTypeCount = 2;
        [Min(0f)] public float InitialFaunaSpawnWaitTime = 10f;
        [Min(0f)] public float FaunaSpawnVolumeThreshold = 1f;
        [Min(0f)] public float BaseFaunaSpawnTime = 60f;

        [Header("Misc")]
        public bool HasRandomFloraAndFauna = false;
    }
}