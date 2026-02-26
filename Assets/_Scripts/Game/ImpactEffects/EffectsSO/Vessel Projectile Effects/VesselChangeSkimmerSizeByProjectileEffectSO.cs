using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselProjectileEffects
{
    [CreateAssetMenu(
        fileName = "VesselChangeSkimmerSizeByProjectileEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselChangeSkimmerSizeByProjectileEffectSO")]
    public class VesselChangeSkimmerSizeByProjectileEffectSO : VesselProjectileEffectSO
    {
        [Header("Debuff Settings")]
        [SerializeField] private float sizeMultiplier = 0.5f;   // how much to scale max size (e.g. 0.5 = half)
        [SerializeField] private float duration = 3f;           // seconds

        [Header("Scale Driver Config Reference")]
        [SerializeField] private ShieldSkimmerScaleConfigSO scaleConfig; // assign in Inspector

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            var vesselStatus = impactor?.Vessel?.VesselStatus;
            if (vesselStatus == null) return;

            if (!IsVesselAllowedToImpact(vesselStatus.VesselType, vesselTypesToImpact))
                return;

            if (!scaleConfig) return;

            _ = scaleConfig.ApplyMaxSizeDebuff(sizeMultiplier, duration);
        }
    }
}