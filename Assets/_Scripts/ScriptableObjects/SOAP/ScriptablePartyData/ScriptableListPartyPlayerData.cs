using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "List_" + nameof(PartyPlayerData),
        menuName = "ScriptableObjects/Lists/" + nameof(PartyPlayerData))]
    public class ScriptableListPartyPlayerData : ScriptableList<PartyPlayerData>
    {
    }
}
