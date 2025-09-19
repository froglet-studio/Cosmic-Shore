using UnityEngine;
using CosmicShore.Game;


namespace CosmicShore.Utilities
{
    public struct PrismReturnEventData
    {
        public GameObject SpawnedObject;
    }

    [System.Serializable]
    public class PrismEventData
    {
        public Teams OwnTeam;
        public Vector3 Position;
        public Quaternion Rotation;
        public PrismType PrismType;
    }

    [CreateAssetMenu(fileName = "PrismEventChannelWithReturn", menuName = "ScriptableObjects/Event Channels/PrismEventChannelWithReturnSO")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}