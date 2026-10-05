using CosmicShore.Gameplay;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(PrismStats), menuName = "ScriptableObjects/Events/"+ nameof(PrismStats))]
    public class ScriptableEventPrismStats : ScriptableEvent<PrismStats>
    {
        
    }
}
