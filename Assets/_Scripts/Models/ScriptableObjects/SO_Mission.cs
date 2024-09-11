using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Mission", menuName = "CosmicShore/Game/Mission", order = 2)]
    [System.Serializable]
    public class SO_Mission : SO_Game
    {
        [Min(1)] public int MinIntensity = 1;
        [Range(1, 9)] public int MaxIntensity = 9;
    }
}