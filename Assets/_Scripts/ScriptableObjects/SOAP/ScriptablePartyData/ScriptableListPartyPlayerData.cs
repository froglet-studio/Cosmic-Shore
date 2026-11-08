using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "List_" + nameof(PartyPlayerData),
        menuName = "ScriptableObjects/Lists/" + nameof(PartyPlayerData))]
    public class ScriptableListPartyPlayerData : ScriptableList<PartyPlayerData>
    {
    }
}
