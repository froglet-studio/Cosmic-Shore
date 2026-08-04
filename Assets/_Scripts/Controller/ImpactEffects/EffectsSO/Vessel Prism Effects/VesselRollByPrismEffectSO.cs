using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Prism-hit punish that ROLLS the vessel instead of redirecting it: a barrel roll in a
    /// randomly chosen direction (left or right), reusing the vessel's existing
    /// <see cref="BarrelRollController"/> roll rather than a bespoke spin. The sideways
    /// displacement is opt-in and defaults to zero, so the hit costs the pilot their
    /// orientation, not their heading.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselRollByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselRollByPrismEffectSO")]
    public class VesselRollByPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Roll")]
        [SerializeField, Tooltip("If false, the roll direction follows which side of the " +
                                 "vessel the prism was struck on instead of a coin flip.")]
        private bool randomizeRollDirection = true;

        [SerializeField, Min(0f),
         Tooltip("Optional sideways displacement injected for the roll's duration (world " +
                 "units/second). 0 = pure roll, no redirect.")]
        private float nudgeSpeed;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = vesselImpactor?.Vessel?.VesselStatus;
            if (vesselStatus == null || vesselStatus.IsTranslationRestricted) return;

            var shipTransform = vesselStatus.ShipTransform;
            if (shipTransform == null) return;

            // BarrelRollController lives on the vessel root, beside VesselStatus — the same
            // transform IVesselStatus.ShipTransform resolves to.
            if (!shipTransform.TryGetComponent(out BarrelRollController roller))
                return;

            float rollSign;
            if (randomizeRollDirection)
            {
                rollSign = Random.value < 0.5f ? -1f : 1f;
            }
            else
            {
                // Struck on the right → roll right (clockwise from the pilot's seat).
                var prismTf = prismImpactee.Prism.prismProperties.prism.transform;
                float side = Vector3.Dot(prismTf.position - shipTransform.position,
                                         shipTransform.right);
                rollSign = side >= 0f ? 1f : -1f;
            }

            roller.TriggerRoll(rollSign, shipTransform.right * rollSign, nudgeSpeed);
        }
    }
}
