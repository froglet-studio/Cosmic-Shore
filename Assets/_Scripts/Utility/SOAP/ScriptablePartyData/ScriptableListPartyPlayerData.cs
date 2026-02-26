using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptablePartyData
{
    [CreateAssetMenu(
        fileName = "List_" + nameof(PartyPlayerData),
        menuName = "ScriptableObjects/SOAP/Lists/" + nameof(PartyPlayerData))]
    public class ScriptableListPartyPlayerData : ScriptableList<PartyPlayerData>
    {
    }
}
