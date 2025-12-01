using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselChangeSkimmerSizeByProjectileEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselChangeSkimmerSizeByProjectileEffectSO")]
    public class VesselChangeSkimmerSizeByProjectileEffectSO : VesselProjectileEffectSO
    {
        [SerializeField] private float sizeMultiplier = 0.5f; // set in Inspector

        [SerializeField] private float duration = 3f;
        
        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            var vesselStatus = impactor.Vessel.VesselStatus;
            
            if (!IsVesselAllowedToImpact(vesselStatus.VesselType, vesselTypesToImpact))
                return;
            
            var vesselSkimmer = vesselStatus.NearFieldSkimmer;
            vesselSkimmer.ResizeForSeconds(sizeMultiplier, duration);
        }
    }
}