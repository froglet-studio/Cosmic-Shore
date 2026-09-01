using CosmicShore.Data;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The one channel a granted reward is announced on. Raised by <c>RewardService</c> only;
    /// every reward display subscribes here rather than watching the wallet, so what the player
    /// SEES and what the wallet DID cannot drift apart.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_" + nameof(RewardGranted),
                     menuName = "ScriptableObjects/Events/" + nameof(RewardGranted))]
    public class ScriptableEventRewardGranted : ScriptableEvent<RewardGranted>
    {
    }
}
