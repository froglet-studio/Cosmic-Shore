using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.SOAP
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(InputEvents),
        menuName = "Soap/ScriptableEvents/" + nameof(InputEvents))]
    public class ScriptableEventInputEvents : ScriptableEvent<InputEvents>
    {
    }
}