using CosmicShore.Core;
using UnityEngine;


namespace CosmicShore.Utilities
{
    public struct PrismReturnEventData
    {
        public GameObject SpawnedObject;
    }

    public struct PrismEventData
    {
        public Teams OwnTeam;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [CreateAssetMenu(fileName = "PrismEventChannelWithReturn",
        menuName = "ScriptableObjects/Event Channels/PrismEventChannelWithReturnSO")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}