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
        fileName = "SkimmerDebuffAppliedEvent",
        menuName = "ScriptableObjects/Events/Vessel/SkimmerDebuffAppliedEvent")]
    public class ScriptableEventSkimmerDebuffApplied : ScriptableEvent<SkimmerDebuffPayload>
    {
    }
}
