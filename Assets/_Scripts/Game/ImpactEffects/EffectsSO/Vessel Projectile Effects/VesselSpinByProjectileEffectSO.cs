using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselSpinByProjectileEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselSpinByProjectileEffectSO")]
    public class VesselSpinByProjectileEffectSO : VesselProjectileEffectSO
    {
        [SerializeField]
        float spinSpeed;
        
        [SerializeField]
        VesselClassType[] vesselTypesToImpact;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            if (!IsAllowedToSpin(impactor.Vessel.VesselStatus.VesselType))
                return;
            
            Vector3 impactVector = (impactee.Transform.position - impactor.Transform.position).normalized;
            var shipStatus = impactor.Vessel.VesselStatus;
            shipStatus.VesselTransformer.SpinShip(impactVector * spinSpeed);
        }

        bool IsAllowedToSpin(VesselClassType vesselType) => 
            vesselTypesToImpact.Length == 0 || vesselTypesToImpact.Any(v => v == vesselType);
    }
}