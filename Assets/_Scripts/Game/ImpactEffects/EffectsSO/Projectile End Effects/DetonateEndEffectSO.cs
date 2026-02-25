using Cysharp.Threading.Tasks;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselSkimmerEffects;
using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileEndEffects
{
    [CreateAssetMenu(
        fileName = "DetonateEndEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/End Effects/Detonate")]
    public class DetonateEndEffectSO : ProjectileEndEffectSO
    {
        [Header("Service")]
        [SerializeField] private ProjectileDetonatorSO detonator;

        [Header("Explosion")]
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 0.75f;
        [SerializeField] private float maxExplosionScale = 2.5f;
        [SerializeField] private bool faceExitVelocity = true;
        [SerializeField] private float returnDelay = 0.25f;

        public override void Execute(ProjectileImpactor impactor, ImpactorBase _)
        {
            if (!impactor || !impactor.Projectile || !detonator) return;

            detonator.Detonate(new ProjectileDetonatorSO.Request
            {
                Projectile       = impactor.Projectile,
                Position         = impactor.transform.position,
                Rotation         = impactor.transform.rotation,
                FaceExitVelocity = faceExitVelocity,
                MinScale         = minExplosionScale,
                MaxScale         = maxExplosionScale,
                ReturnDelay      = returnDelay,
                Prefabs          = aoePrefabs,
                Anonymous        = false,
                OverrideMaterial = impactor.Projectile.VesselStatus.AOEExplosionMaterial
            });
        }
    }
}