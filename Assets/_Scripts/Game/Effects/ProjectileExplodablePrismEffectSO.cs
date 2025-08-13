using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileExplodablePrismEffect", menuName = "ScriptableObjects/Impact Effects/ProjectileExplodablePrismEffectSO")]
    public class ProjectileExplodablePrismEffectSO : ImpactEffectSO<R_ProjectileImpactor, R_PrismImpactor>
    {
        [SerializeField] AOEExplosion[] aoePrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        
        protected override void ExecuteTyped(R_ProjectileImpactor impactor, R_PrismImpactor impactee)
        {
            var projectile =  impactor.Projectile;
            var shipStatus =  projectile.ShipStatus;
            
            foreach (var AOE in aoePrefabs)
            {
                var spawnedExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                spawnedExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = shipStatus.Team,
                    Ship = shipStatus.Ship,
                    MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, projectile.Charge),
                    OverrideMaterial = shipStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false
                });
                
                spawnedExplosion.SetPositionAndRotation(projectile.transform.position, projectile.transform.rotation);
                spawnedExplosion.Detonate();
            }

            projectile.ReturnToPool();
        }
    }
}