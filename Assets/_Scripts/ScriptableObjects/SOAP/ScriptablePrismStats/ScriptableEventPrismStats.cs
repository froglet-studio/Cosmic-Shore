using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(PrismStats), menuName = "ScriptableObjects/Events/"+ nameof(PrismStats))]
    public class ScriptableEventPrismStats : ScriptableEvent<PrismStats>
    {
        
    }
}
