using CosmicShore.Data;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(ApplicationState),
        menuName = "ScriptableObjects/Events/" + nameof(ApplicationState))]
    public class ScriptableEventApplicationState : ScriptableEvent<ApplicationState>
    {
    }
}
