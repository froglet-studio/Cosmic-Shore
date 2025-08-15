using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(TrailBlockEventData), menuName = "Soap/ScriptableEvents/"+ nameof(TrailBlockEventData))]
    public class ScriptableEventTrailBlockEventData : ScriptableEvent<TrailBlockEventData>
    {
        
    }
}
