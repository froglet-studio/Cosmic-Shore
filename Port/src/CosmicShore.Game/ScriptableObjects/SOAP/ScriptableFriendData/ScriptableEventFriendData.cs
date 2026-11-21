using CosmicShore.Engine;
using CosmicShore.Engine.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(FriendData),
        menuName = "ScriptableObjects/Events/" + nameof(FriendData))]
    public class ScriptableEventFriendData : ScriptableEvent<FriendData>
    {
    }
}
