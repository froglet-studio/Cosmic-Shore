using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByProjectileEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselChangeSpeedByProjectileEffectSO")]
    public class VesselChangeSpeedByProjectileEffectSO : VesselProjectileEffectSO
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            impactor.Vessel.VesselStatus.VesselTransformer.ModifyThrottle(_amount, _duration);
        }
    }
}
