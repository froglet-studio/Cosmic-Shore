using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
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
        fileName = "ExplosionDebuffAppliedEvent",
        menuName = "ScriptableObjects/Events/Vessel/ExplosionDebuffAppliedEvent")]
    public class ScriptableEventExplosionDebuffApplied : ScriptableEvent<ExplosionDebuffPayload>
    {
    }
}