using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    // DEPRECATED - Use Projectile.cs
    /*public class ExplodableProjectile : Projectile
    {
        [SerializeField] List<AOEExplosion> AOEPrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        public float Charge { get; private set; } = 0;

        public override void Initialize(PoolManager poolManager, Teams team, IVesselStatus vesselStatus, float charge)
        {
            base.Initialize(poolManager, team, vesselStatus, charge);
            Charge = charge;
        }

        protected override void PerformEndEffects()
        {
            foreach (TrailBlockImpactEffects effect in endEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.Stop:
                        Stop();
                        _poolManager.ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("EndExplode");
                        // Detonate();
                        break;
                }
            }
        }

        protected override void PerformTrailImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        Debug.Log("DeactivateTrailBlock from projectile");
                        trailBlockProperties.trailBlock.Damage(Velocity * Inertia, VesselStatus.Team, VesselStatus.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        // trailBlockProperties.trailBlock.Steal(Vessel.VesselStatus.PlayerName, Team);
                        trailBlockProperties.trailBlock.Steal(VesselStatus.PlayerName, OwnTeam);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                    case TrailBlockImpactEffects.Stop:

                        if (!trailBlockProperties.trailBlock.GetComponent<Boid>()) Stop();
                        else _poolManager.ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("TrailExplode");
                        // Detonate();
                        break;

                }
            }
        }

        public void Detonate()
        {
            foreach (var AOE in AOEPrefabs)
            {
                if (VesselStatus.Vessel == null)
                {
                    Debug.LogError("VesselStatus.Vessel is null in ExplodableProjectile.Detonate()");
                    return;
                }

                var AOEExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                AOEExplosion.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnTeam = OwnTeam,
                    Vessel = VesselStatus.Vessel,
                    MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, Charge),
                    OverrideMaterial = VesselStatus.Vessel.VesselStatus.AOEExplosionMaterial,
                    AnnonymousExplosion = false
                });
                AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                AOEExplosion.Detonate();
            }

            _poolManager.ReturnToPool(gameObject, gameObject.tag);
        }
    }*/
}