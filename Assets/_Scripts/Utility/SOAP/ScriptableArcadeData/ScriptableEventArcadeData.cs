using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(ArcadeData), menuName = "Soap/ScriptableEvents/"+ nameof(ArcadeData))]
    public class ScriptableEventArcadeData : ScriptableEvent<ArcadeData>
    {
        
    }
}
