using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(BootStatusRequest),
        menuName = "ScriptableObjects/Events/" + nameof(BootStatusRequest))]
    public class ScriptableEventBootStatusRequest : ScriptableEvent<BootStatusRequest>
    {
    }
}
