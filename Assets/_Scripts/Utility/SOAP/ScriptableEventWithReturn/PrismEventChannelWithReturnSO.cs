using CosmicShore.Core;
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
        public string PoolName = "Explosion"; // Default to explosion pool
    }

    // Alternative: Create separate event channels for explosions and implosions
    [System.Serializable]
    public class ExplosionEventData
    {
        public Teams OwnTeam;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [System.Serializable]
    public class ImplosionEventData
    {
        public Teams OwnTeam;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 SinkPoint; // Specific to implosions
    }

    [CreateAssetMenu(fileName = "PrismEventChannelWithReturn", menuName = "ScriptableObjects/Event Channels/PrismEventChannelWithReturnSO")]
    public class PrismEventChannelWithReturnSO : GenericEventChannelWithReturnSO<PrismEventData, PrismReturnEventData>
    {
    }
}