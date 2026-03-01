using Obvious.Soap;
using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.ScriptableObjects
{
    [System.Serializable]
    public struct ExplosionDebuffPayload
    {
        public IVessel Vessel;
        public float Duration;

        public ExplosionDebuffPayload(IVessel vessel, float duration)
        {
            Vessel   = vessel;
            Duration = duration;
        }
    }
    
    [CreateAssetMenu(
        fileName = "Event_ExplosionDebuffApplied",
        menuName = "ScriptableObjects/Events/ExplosionDebuffApplied")]
    public class ScriptableEventExplosionDebuffApplied : ScriptableEvent<ExplosionDebuffPayload>
    {
    }
}