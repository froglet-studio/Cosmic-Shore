using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(PipData), menuName = "Soap/ScriptableEvents/" + nameof(PipData))]
    public class ScriptableEventPipData : ScriptableEvent<PipData>
    {

    }
}
