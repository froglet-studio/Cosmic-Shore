using UnityEngine;


namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "ArcadeEventChannel", menuName = "ScriptableObjects/Event Channels/ArcadeEventChannelSO")]
    public class ArcadeEventChannelSO : GenericEventChannelSO<ArcadeData>
    { }

    public struct ArcadeData
    {
        public string SceneName;
        public int MaxPlayers;
    }
}