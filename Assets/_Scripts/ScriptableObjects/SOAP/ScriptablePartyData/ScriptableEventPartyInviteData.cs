using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(PartyInviteData),
        menuName = "ScriptableObjects/SOAP/Events/" + nameof(PartyInviteData))]
    public class ScriptableEventPartyInviteData : ScriptableEvent<PartyInviteData>
    {
    }
}
