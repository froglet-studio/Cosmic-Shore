using CosmicShore.Game.Managers;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptablePrismStats
{
    [CreateAssetMenu(fileName = "Event_" + nameof(PrismStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(PrismStats))]
    public class ScriptableEventPrismStats : ScriptableEvent<PrismStats>
    {
        
    }
}
