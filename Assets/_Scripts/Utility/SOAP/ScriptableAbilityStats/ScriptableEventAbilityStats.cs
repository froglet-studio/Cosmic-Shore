using CosmicShore.Core;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "Event_" + nameof(AbilityStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(AbilityStats))]
    public class ScriptableEventAbilityStats : ScriptableEvent<AbilityStats>
    {
        
    }
}
