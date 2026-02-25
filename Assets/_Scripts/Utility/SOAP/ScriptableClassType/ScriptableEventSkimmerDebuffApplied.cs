using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [System.Serializable]
    public struct SkimmerDebuffPayload
    {
        public IVesselStatus Attacker;
        public IVesselStatus Victim;
        public float         Duration;

        public SkimmerDebuffPayload(IVesselStatus attacker, IVesselStatus victim, float duration)
        {
            Attacker = attacker;
            Victim   = victim;
            Duration = duration;
        }
    }
    
    [CreateAssetMenu(
        fileName = "Event_SkimmerDebuffApplied",
        menuName = "ScriptableObjects/SOAP/Events/SkimmerDebuffApplied")]
    public class ScriptableEventSkimmerDebuffApplied : ScriptableEvent<SkimmerDebuffPayload>
    {
    }
}
