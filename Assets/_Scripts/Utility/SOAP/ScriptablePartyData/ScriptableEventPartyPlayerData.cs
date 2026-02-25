using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(PartyPlayerData),
        menuName = "ScriptableObjects/SOAP/Events/" + nameof(PartyPlayerData))]
    public class ScriptableEventPartyPlayerData : ScriptableEvent<PartyPlayerData>
    {
    }
}
