using CosmicShore.Core;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(CrystalStats), menuName = "Soap/ScriptableEvents/"+ nameof(CrystalStats))]
    public class ScriptableEventCrystalStats : ScriptableEvent<CrystalStats>
    {
        
    }
}
