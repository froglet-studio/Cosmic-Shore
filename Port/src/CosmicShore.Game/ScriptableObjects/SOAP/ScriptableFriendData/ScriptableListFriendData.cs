using CosmicShore.Engine;
using CosmicShore.Engine.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "List_" + nameof(FriendData),
        menuName = "ScriptableObjects/Lists/" + nameof(FriendData))]
    public class ScriptableListFriendData : ScriptableList<FriendData>
    {
    }
}
