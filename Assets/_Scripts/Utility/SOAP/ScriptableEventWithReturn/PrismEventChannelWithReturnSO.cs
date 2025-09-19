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
        public Quaternion Rotation;
        public Vector3 Position;
        public Vector3 Scale;
        public Vector3 Velocity;
        public Vector3 SinkPoint;
        public float Volume;
        public PrismType PrismType;
    }

    [CreateAssetMenu(fileName = "PrismEventChannelWithReturn", menuName = "ScriptableObjects/Event Channels/PrismEventChannelWithReturnSO")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}