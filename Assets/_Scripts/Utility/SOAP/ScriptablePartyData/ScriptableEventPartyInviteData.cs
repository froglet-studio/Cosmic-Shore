using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "scriptable_event_" + nameof(PartyInviteData),
        menuName = "Soap/ScriptableEvents/" + nameof(PartyInviteData))]
    public class ScriptableEventPartyInviteData : ScriptableEvent<PartyInviteData>
    {
    }
}
