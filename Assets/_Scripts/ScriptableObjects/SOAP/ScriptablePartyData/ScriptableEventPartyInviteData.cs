using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(PartyInviteData),
        menuName = "ScriptableObjects/Events/" + nameof(PartyInviteData))]
    public class ScriptableEventPartyInviteData : ScriptableEvent<PartyInviteData>
    {
    }
}
