using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileExplosionByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Projectile/ProjectileExplosionByCrystalEffectSO")]
    public abstract class ProjectileExplosionByCrystalEffectSO : ProjectileCrystalEffectSO
    {
        [SerializeField] AOEExplosion[] aoePrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        
        public void Execute(ProjectileImpactor impactor, ImpactorBase impactee)
        {
            /*
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
                    AnnonymousExplosion = false,
                    SpawnPosition = projectile.transform.position,
                    SpawnRotation = projectile.transform.rotation
                });
                spawnedExplosion.Detonate();
            }*/
            
            ExplosionHelper.CreateExplosion(
                aoePrefabs,
                impactor,
                minExplosionScale,
                maxExplosionScale);
            var projectile =  impactor.Projectile;
            projectile.ReturnToPool();
        }
    }
}