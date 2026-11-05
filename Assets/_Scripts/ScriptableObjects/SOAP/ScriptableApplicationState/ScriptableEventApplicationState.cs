using CosmicShore.Data;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(ApplicationState),
        menuName = "ScriptableObjects/Events/" + nameof(ApplicationState))]
    public class ScriptableEventApplicationState : ScriptableEvent<ApplicationState>
    {
    }
}
