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
        public System.Action OnGrowCompleted;
    }

    [CreateAssetMenu(fileName = "EventChannel_Prism", menuName = "ScriptableObjects/SOAP/Event Channels/PrismEventChannel")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}