using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselProjectileEffects
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