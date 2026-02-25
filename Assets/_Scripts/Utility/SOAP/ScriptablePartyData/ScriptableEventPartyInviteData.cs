using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptablePartyData
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(PartyInviteData),
        menuName = "ScriptableObjects/SOAP/Events/" + nameof(PartyInviteData))]
    public class ScriptableEventPartyInviteData : ScriptableEvent<PartyInviteData>
    {
    }
}
