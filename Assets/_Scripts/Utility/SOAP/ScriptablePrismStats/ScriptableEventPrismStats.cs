using CosmicShore.Core;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(PrismStats), menuName = "Soap/ScriptableEvents/"+ nameof(PrismStats))]
    public class ScriptableEventPrismStats : ScriptableEvent<PrismStats>
    {
        
    }
}
