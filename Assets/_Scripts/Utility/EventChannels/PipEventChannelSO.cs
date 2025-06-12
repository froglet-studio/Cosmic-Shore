using UnityEngine;


namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "PipEventChannel", menuName = "ScriptableObjects/Event Channels/PipEventChannelSO")]
    public class  PipEventChannelSO : GenericEventChannelSO<PipEventData>
    {
        
    }

    public struct PipEventData
    {
        public bool IsActive;
        public bool IsMirrored;
    }
}