using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(Quaternion),
        menuName = "Soap/ScriptableEvents/" + nameof(Quaternion))]
    public class ScriptableEventQuaternion : ScriptableEvent<Quaternion>
    {
    }
}