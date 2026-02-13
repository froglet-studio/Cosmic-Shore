using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(
        fileName = "LightFaunaManagerDataSO",
        menuName = "Cosmic Shore/LifeForms/FaunaPrefab/Light FaunaPrefab Manager Data")]
    public class LightFaunaManagerDataSO : ScriptableObject
    {
        [Header("Spawn Settings")]
        [Min(0)] public int spawnCount = 10;
        [Min(0f)] public float spawnRadius = 10f;
        
        [Header("Formation Settings")]
        [Min(0f)] public float formationSpread = 5f;
        public float phaseIncrease = 0.1f;
    }
}