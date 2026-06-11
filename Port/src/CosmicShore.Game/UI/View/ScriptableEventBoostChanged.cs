using CosmicShore.Engine.Soap;
using CosmicShore.Engine;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(BoostChangedPayload),
        menuName = "ScriptableObjects/Events/" + nameof(BoostChangedPayload))]
    public sealed class ScriptableEventBoostChanged : ScriptableEvent<BoostChangedPayload> { }
}