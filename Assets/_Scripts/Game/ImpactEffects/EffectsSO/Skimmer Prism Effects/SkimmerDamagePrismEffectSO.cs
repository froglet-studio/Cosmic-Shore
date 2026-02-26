using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "SkimmerDamagePrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerDamagePrismEffectSO")]
    public class SkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] float inertia = 70f;
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;
        
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            PrismEffectHelper.Damage(status, prismImpactee, inertia, status.Course, status.Speed);
        }
    }
}

