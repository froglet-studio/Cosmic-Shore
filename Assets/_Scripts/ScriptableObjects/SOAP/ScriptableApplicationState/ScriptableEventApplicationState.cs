using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(ApplicationState),
        menuName = "ScriptableObjects/SOAP/Events/" + nameof(ApplicationState))]
    public class ScriptableEventApplicationState : ScriptableEvent<ApplicationState>
    {
    }
}
