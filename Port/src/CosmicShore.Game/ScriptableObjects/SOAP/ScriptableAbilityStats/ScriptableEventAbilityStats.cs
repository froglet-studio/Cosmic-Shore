using CosmicShore.Gameplay;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(AbilityStats), menuName = "ScriptableObjects/Events/"+ nameof(AbilityStats))]
    public class ScriptableEventAbilityStats : ScriptableEvent<AbilityStats>
    {
        
    }
}
