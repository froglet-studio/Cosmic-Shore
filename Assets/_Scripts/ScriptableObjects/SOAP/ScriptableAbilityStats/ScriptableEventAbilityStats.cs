using CosmicShore.Gameplay;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(AbilityStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(AbilityStats))]
    public class ScriptableEventAbilityStats : ScriptableEvent<AbilityStats>
    {
        
    }
}
