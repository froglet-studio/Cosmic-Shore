using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Prism-hit punish that ROLLS the vessel instead of redirecting it. This is the same
    /// roll the pilot flies with — a rotation about the vessel's forward axis folded into
    /// VesselTransformer.accumulatedRotation, exactly as VesselTransformer.Roll() integrates
    /// InputStatus.YDiff — so the transformer's own slerp carries the ship into the new bank
    /// and it keeps flying from there. No animation, no displacement: the hit costs the pilot
    /// their orientation, not their heading.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselRollByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselRollByPrismEffectSO")]
    public class VesselRollByPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Roll")]
        [SerializeField, Range(0f, 180f),
         Tooltip("How far to roll about the vessel's forward axis on each hit. The " +
                 "transformer slerps the ship into it, so this is a bank the pilot has to " +
                 "fly out of, not a snap.")]
        private float rollDegrees = 60f;

        [SerializeField, Tooltip("If false, the roll direction follows which side of the " +
                                 "vessel the prism was struck on instead of a coin flip.")]
        private bool randomizeRollDirection = true;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = vesselImpactor?.Vessel?.VesselStatus;
            if (vesselStatus == null || vesselStatus.IsTranslationRestricted) return;

            var shipTransform = vesselStatus.ShipTransform;
            var transformer = vesselStatus.VesselTransformer;
            if (shipTransform == null || transformer == null) return;

            float rollSign;
            if (randomizeRollDirection)
            {
                rollSign = Random.value < 0.5f ? -1f : 1f;
            }
            else
            {
                // Struck on the right → roll right (positive about +forward is clockwise
                // from the pilot's seat in Unity's left-handed space).
                var prismTf = prismImpactee.Prism.prismProperties.prism.transform;
                float side = Vector3.Dot(prismTf.position - shipTransform.position,
                                         shipTransform.right);
                rollSign = side >= 0f ? 1f : -1f;
            }

            transformer.ApplyRotation(rollSign * rollDegrees, shipTransform.forward);
        }
    }
}
