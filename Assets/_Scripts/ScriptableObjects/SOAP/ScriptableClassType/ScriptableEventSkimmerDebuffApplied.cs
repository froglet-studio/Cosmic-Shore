using Obvious.Soap;
using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.ScriptableObjects
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
        menuName = "ScriptableObjects/Events/SkimmerDebuffApplied")]
    public class ScriptableEventSkimmerDebuffApplied : ScriptableEvent<SkimmerDebuffPayload>
    {
    }
}
