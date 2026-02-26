using CosmicShore.Game.ImpactEffects;
using UnityEngine;
using CosmicShore.Game.Environment;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "ProjectileExplosionByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Projectile - Crystal/ProjectileExplosionByCrystalEffectSO")]
    public class ProjectileExplosionByCrystalEffectSO : ProjectileCrystalEffectSO
    {
        [SerializeField] AOEExplosion[] aoePrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        
        

        public override void Execute(ProjectileImpactor impactor, CrystalImpactor crystalImpactee)
        {
            ExplosionHelper.CreateExplosion(
                aoePrefabs,
                impactor,
                minExplosionScale,
                maxExplosionScale);
        }
    }
}


/*
 // Remove later
 public void Execute(ProjectileImpactor impactor, ImpactorBase impactee)
        {

            var vesselStatus =  projectile.VesselStatus;

            foreach (var AOE in aoePrefabs)
            {
                var spawnedExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                spawnedExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = vesselStatus.Team,
                    Vessel = vesselStatus.Vessel,
                    MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, projectile.Charge),
                    OverrideMaterial = vesselStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition = projectile.transform.position,
                    SpawnRotation = projectile.transform.rotation
                });
                spawnedExplosion.Detonate();
        }
}*/