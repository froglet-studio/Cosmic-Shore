using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "scriptable_event_" + nameof(BoostChangedPayload),
        menuName = "Soap/ScriptableEvents/" + nameof(BoostChangedPayload))]
    public sealed class ScriptableEventBoostChanged : ScriptableEvent<BoostChangedPayload> { }
}