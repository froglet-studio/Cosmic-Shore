using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkimmerDamagePrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerDamagePrismEffectSO")]
    public class SkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] float inertia = 70f;
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;

        [Header("Swing (skimmers that move relative to the vessel - the Rhino's sword)")]
        [Tooltip("How much of the skimmer's own velocity at the contact point reaches the prism. 1 = the physical model: a tip strike mid-swipe drives debris many times harder than a hilt graze, and along the swing tangent. 0 = vessel velocity only (pre-model behaviour). Rigidly-mounted skimmers have no relative motion, so this never changes them.")]
        [SerializeField] float swingVelocityScale = 1f;

        [Tooltip("Ceiling on the impact speed handed to the prism. 0 = unclamped (the explosion VFX applies its own clamp downstream).")]
        [SerializeField] float maxImpactSpeed;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            var velocity = PrismEffectHelper.ContactVelocity(
                impactor, status, prismImpactee.Prism.transform.position, swingVelocityScale, maxImpactSpeed);
            PrismEffectHelper.Damage(status, prismImpactee, inertia, velocity);
        }
    }
}

