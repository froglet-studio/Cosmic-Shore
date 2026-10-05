using CosmicShore.Data;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(BootStatusRequest),
        menuName = "ScriptableObjects/Events/" + nameof(BootStatusRequest))]
    public class ScriptableEventBootStatusRequest : ScriptableEvent<BootStatusRequest>
    {
    }
}
