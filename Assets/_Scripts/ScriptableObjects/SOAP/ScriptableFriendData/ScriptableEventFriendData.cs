using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(FriendData),
        menuName = "ScriptableObjects/Events/" + nameof(FriendData))]
    public class ScriptableEventFriendData : ScriptableEvent<FriendData>
    {
    }
}
