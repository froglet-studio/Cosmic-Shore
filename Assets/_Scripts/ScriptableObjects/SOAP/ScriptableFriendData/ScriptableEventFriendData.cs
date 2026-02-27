using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(FriendData),
        menuName = "ScriptableObjects/SOAP/Events/" + nameof(FriendData))]
    public class ScriptableEventFriendData : ScriptableEvent<FriendData>
    {
    }
}
