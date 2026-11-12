using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(PartyPlayerData),
        menuName = "ScriptableObjects/Events/" + nameof(PartyPlayerData))]
    public class ScriptableEventPartyPlayerData : ScriptableEvent<PartyPlayerData>
    {
    }
}
