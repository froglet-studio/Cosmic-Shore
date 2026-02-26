using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP
{
    [CreateAssetMenu(
        fileName = "List_" + nameof(PartyPlayerData),
        menuName = "ScriptableObjects/SOAP/Lists/" + nameof(PartyPlayerData))]
    public class ScriptableListPartyPlayerData : ScriptableList<PartyPlayerData>
    {
    }
}
