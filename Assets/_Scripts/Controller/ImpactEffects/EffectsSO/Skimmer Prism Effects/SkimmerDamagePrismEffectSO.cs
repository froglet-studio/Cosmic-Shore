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

        [Header("Domain")]
        [Tooltip("OFF (the default, and every shipped asset): this skimmer damages whatever it " +
                 "touches — the Rhino's sword cuts its own team's mass too, which is its design. " +
                 "ON: only OPPOSING-domain prisms are damaged. The Butterfly's wings need this: " +
                 "their whole Space ability is erasing enemy territory by soaring over it, and a " +
                 "wing that dissolved its own team's wake would delete the surfaces this vessel " +
                 "exists to paint.\n\n" +
                 "It has to live HERE rather than on Skimmer.affectSelf, which is a domain " +
                 "compare evaluated AFTER the effect loop and gates only the skim bookkeeping — " +
                 "a vessel with affectSelf off still runs every skimmer prism effect on its own " +
                 "mass (the contract's three-self-guard-shapes note).")]
        [SerializeField] bool opposingDomainOnly;

        [Tooltip("None (the default, and every shipped asset): this effect always runs. Set an " +
                 "element and it is a NO-OP until that element's level-5 upgrade is live — which " +
                 "lets one effect sit in two skimmer containers with only the outer one gated, so " +
                 "an upgrade widens a swath rather than changing what a pass does (the Butterfly's " +
                 "retired SPACE-5 'Broadwing' was the shape; no shipped asset uses it today).\n\n" +
                 "Gated on the REPLICATED unlock bit, not a local level read: what it decides is " +
                 "how much conserved mass leaves the world.")]
        [SerializeField] Element requiresUpgradeElement = Element.None;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;

            if (requiresUpgradeElement != Element.None)
            {
                var abilities = status?.ElementalAbilityHandler;
                if (abilities == null || !abilities.IsUpgradeActive(requiresUpgradeElement)) return;
            }

            if (opposingDomainOnly)
            {
                var prism = prismImpactee != null ? prismImpactee.Prism : null;
                if (prism == null || status == null || prism.Domain == status.Domain) return;
            }

            var velocity = PrismEffectHelper.ContactVelocity(
                impactor, status, prismImpactee.Prism.transform.position, swingVelocityScale, maxImpactSpeed);

            if (proportionalDebris)
                PrismEffectHelper.DamageProportional(status, prismImpactee, velocity, restitution, debrisSpeedLimit);
            else
                PrismEffectHelper.Damage(status, prismImpactee, inertia, velocity);
        }
    }
}

