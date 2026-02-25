using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "scriptable_list_" + nameof(PartyPlayerData),
        menuName = "Soap/ScriptableLists/" + nameof(PartyPlayerData))]
    public class ScriptableListPartyPlayerData : ScriptableList<PartyPlayerData>
    {
    }
}
