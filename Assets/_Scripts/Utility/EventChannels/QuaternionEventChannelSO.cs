using UnityEngine;


namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "QuaternionEventChannel", menuName = "ScriptableObjects/Event Channels/QuaternionEventChannelSO")]
    public class QuaternionEventChannelSO : GenericEventChannelSO<Quaternion>
    { }
}