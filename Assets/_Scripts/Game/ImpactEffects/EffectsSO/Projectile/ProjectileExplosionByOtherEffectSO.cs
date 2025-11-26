using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileExplosionByOtherEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/ProjectileExplosionByOtherEffectSO")]
    public class ProjectileExplosionByOtherEffectSO : ImpactEffectSO<ProjectileImpactor, ImpactorBase>
    {
        [SerializeField] AOEExplosion[] aoePrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;

        protected override void ExecuteTyped(ProjectileImpactor impactor, ImpactorBase impactee)
        {
            var projectile = impactor.Projectile;
            var shipStatus = projectile.ShipStatus;

            foreach (var AOE in aoePrefabs)
            {
                var spawnedExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                spawnedExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = shipStatus.Team,
                    Ship = shipStatus.Ship,
                    MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, projectile.Charge),
                    OverrideMaterial = shipStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition = projectile.transform.position,
                    SpawnRotation = projectile.transform.rotation
                });
                spawnedExplosion.Detonate();
            }

            projectile.ReturnToPool();
        }
    }
}