using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(SilhouetteData), menuName = "Soap/ScriptableEvents/"+ nameof(SilhouetteData))]
    public class ScriptableEventSilhouetteData : ScriptableEvent<SilhouetteData>
    {
        
    }
}
