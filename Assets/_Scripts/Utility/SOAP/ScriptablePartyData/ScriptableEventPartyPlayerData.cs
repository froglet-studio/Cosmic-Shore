using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "scriptable_event_" + nameof(PartyPlayerData),
        menuName = "Soap/ScriptableEvents/" + nameof(PartyPlayerData))]
    public class ScriptableEventPartyPlayerData : ScriptableEvent<PartyPlayerData>
    {
    }
}
