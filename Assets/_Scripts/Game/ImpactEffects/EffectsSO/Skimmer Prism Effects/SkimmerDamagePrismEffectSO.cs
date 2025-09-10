using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerDamagePrismEffectSO")]
    public class SkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] float inertia = 70f;   // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;
    
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.ShipStatus;
            PrismEffectHelper.Damage(status, prismImpactee, inertia, overrideCourse, overrideSpeed);
        }
    }
}

