using CosmicShore.Game.Managers;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptableAbilityStats
{
    [CreateAssetMenu(fileName = "Event_" + nameof(AbilityStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(AbilityStats))]
    public class ScriptableEventAbilityStats : ScriptableEvent<AbilityStats>
    {
        
    }
}
