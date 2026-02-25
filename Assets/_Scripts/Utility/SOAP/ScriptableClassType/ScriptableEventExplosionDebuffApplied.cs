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
        fileName = "Event_ExplosionDebuffApplied",
        menuName = "ScriptableObjects/SOAP/Events/ExplosionDebuffApplied")]
    public class ScriptableEventExplosionDebuffApplied : ScriptableEvent<ExplosionDebuffPayload>
    {
    }
}