using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "List_" + nameof(FriendData),
        menuName = "ScriptableObjects/SOAP/Lists/" + nameof(FriendData))]
    public class ScriptableListFriendData : ScriptableList<FriendData>
    {
    }
}
