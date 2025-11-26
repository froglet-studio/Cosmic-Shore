using CosmicShore.Core;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(AbilityStats),
        menuName = "Soap/ScriptableEvents/" + nameof(AbilityStats))]
    public class ScriptableEventAbilityStats : ScriptableEvent<AbilityStats>
    {
    }
}