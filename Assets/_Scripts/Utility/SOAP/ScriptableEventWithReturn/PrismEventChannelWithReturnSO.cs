using UnityEngine;
using CosmicShore.Game;
using UnityEngine.Serialization;


namespace CosmicShore.Utilities
{
    public struct PrismReturnEventData
    {
        public GameObject SpawnedObject;
    }

    [System.Serializable]
    public class PrismEventData
    {
        [FormerlySerializedAs("OwnTeam")] public Domains ownDomain;
        public Quaternion Rotation;
        public Vector3 SpawnPosition;
        public Vector3 Scale;
        public Vector3 Velocity;
        public float Volume;
        public PrismType PrismType;
        public Transform TargetTransform;
    }

    [CreateAssetMenu(fileName = "PrismEventChannelWithReturn", menuName = "ScriptableObjects/Event Channels/PrismEventChannelWithReturnSO")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}