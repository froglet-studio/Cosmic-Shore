using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Utilities
{
    public struct  TrailBlockEventData
    {
        public Teams Team;
        public string PlayerName;
        public Vector3 Position;
        public Quaternion Rotation;
        public TrailBlockProperties TrailBlockProperties;
    }

    [CreateAssetMenu(fileName = "TrailBlockEventChannel", menuName = "ScriptableObjects/Event Channels/TrailBlockEventChannelSO")]
    public class TrailBlockEventChannelSO : GenericEventChannelSO<TrailBlockEventData>
    {}
}
