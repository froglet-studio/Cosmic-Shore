using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(BoostChangedPayload),
        menuName = "ScriptableObjects/SOAP/Events/" + nameof(BoostChangedPayload))]
    public sealed class ScriptableEventBoostChanged : ScriptableEvent<BoostChangedPayload> { }
}