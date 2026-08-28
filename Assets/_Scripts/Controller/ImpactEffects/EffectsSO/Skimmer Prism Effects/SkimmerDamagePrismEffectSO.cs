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

        [Tooltip("Ceiling on the impact speed handed to the prism. 0 = unclamped.")]
        [SerializeField] float maxImpactSpeed;

        [Header("Debris response")]
        [Tooltip("ON: debris leaves at the actual impact speed, identically for every prism size - a tip strike visibly throws mass harder than a hilt graze. OFF (legacy): debris speed is impact * inertia / prismVolume, a gain spanning ~100x across prism sizes that the explosion clamp then flattens, so the magnitude reads the same no matter what hit it.")]
        [SerializeField] bool proportionalDebris;

        [Tooltip("Debris speed as a multiple of impact speed. 1 = the physical read (the prism leaves at the speed of the thing that hit it); shipped at 1/3 because full speed reads too hot. Also drives the shatter RATE, so gentle hits crumble slowly and hard hits burst instantly - scale it and both scale together.")]
        [SerializeField] float restitution = 1f / 3f;

        [Tooltip("Ceiling on debris speed, in real speed units, replacing the explosion prefab's clamp. Keep it above restitution x the impacts you want to read apart. Scale it with restitution - the two must move together or the retune just clips instead of toning down.")]
        [SerializeField] float debrisSpeedLimit = 200f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            var velocity = PrismEffectHelper.ContactVelocity(
                impactor, status, prismImpactee.Prism.transform.position, swingVelocityScale, maxImpactSpeed);

            if (proportionalDebris)
                PrismEffectHelper.DamageProportional(status, prismImpactee, velocity, restitution, debrisSpeedLimit);
            else
                PrismEffectHelper.Damage(status, prismImpactee, inertia, velocity);
        }
    }
}

