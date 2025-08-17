using UnityEngine;


namespace CosmicShore.Utilities
{
    public struct TrailBlockReturnEventData
    {
        public GameObject SpawnedObject;
    }

    [CreateAssetMenu(fileName = "TrailBlockEventChannelWithReturn", menuName = "ScriptableObjects/Event Channels/TrailBlockEventChannelWithReturnSO")]
    public class TrailBlockEventChannelWithReturnSO : GenericEventChannelWithReturnSO<TrailBlockEventData, TrailBlockReturnEventData>
    {
    }
}