using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(BoostChangedPayload),
        menuName = "ScriptableObjects/Events/" + nameof(BoostChangedPayload))]
    public sealed class ScriptableEventBoostChanged : ScriptableEvent<BoostChangedPayload> { }
}