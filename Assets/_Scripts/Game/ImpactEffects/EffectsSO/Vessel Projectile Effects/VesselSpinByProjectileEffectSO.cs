using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselSpinByProjectileEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselSpinByProjectileEffectSO")]
    public class VesselSpinByProjectileEffectSO : VesselProjectileEffectSO
    {
        [SerializeField]
        float spinSpeed;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            var vesselStatus = impactor.Vessel.VesselStatus;
            if (!IsVesselAllowedToImpact(vesselStatus.VesselType, vesselTypesToImpact))
                return;
            
            Vector3 impactVector = (impactee.Transform.position - impactor.Transform.position).normalized;
            vesselStatus.VesselTransformer.SpinShip(impactVector * spinSpeed);
        }
    }
}