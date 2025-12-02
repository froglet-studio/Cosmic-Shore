using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeSkimmerSizeByProjectileEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselChangeSkimmerSizeByProjectileEffectSO")]
    public class VesselChangeSkimmerSizeByProjectileEffectSO : VesselProjectileEffectSO
    {
        [Header("Debuff Settings")]
        [SerializeField] private float sizeMultiplier = 0.5f;   // how much to scale max size (e.g. 0.5 = half)
        [SerializeField] private float duration = 3f;           // seconds

        [Header("Grow Skimmer Action Reference")]
        [SerializeField] private GrowSkimmerActionSO growSkimmerAction; // assign in Inspector

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            var vesselStatus = impactor.Vessel.VesselStatus;

            if (!IsVesselAllowedToImpact(vesselStatus.VesselType, vesselTypesToImpact))
                return;

            _ = growSkimmerAction.ApplyMaxSizeDebuff(sizeMultiplier, duration);
        }
    }
}