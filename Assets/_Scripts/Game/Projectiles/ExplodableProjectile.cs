using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] List<AOEExplosion> AOEPrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        public float Charge { get; private set; } = 0;

        public override void Initialize(Teams team, IShipStatus shipStatus, float charge)
        {
            base.Initialize(team, shipStatus, charge);
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
                        poolManager.ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("EndExplode");
                        Detonate();
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
                        trailBlockProperties.trailBlock.Damage(Velocity * Inertia, ShipStatus.Team, ShipStatus.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        // trailBlockProperties.trailBlock.Steal(Ship.ShipStatus.PlayerName, Team);
                        trailBlockProperties.trailBlock.Steal(ShipStatus.PlayerName, Team);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                    case TrailBlockImpactEffects.Stop:

                        if (!trailBlockProperties.trailBlock.GetComponent<Boid>()) Stop();
                        else poolManager.ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("TrailExplode");
                        Detonate();
                        break;

                }
            }
        }

        public void Detonate()
        {
            // poolManager.ReturnToPool(gameObject, gameObject.tag);
            foreach (var AOE in AOEPrefabs)
            {
                if (ShipStatus.Ship == null)
                {
                    Debug.LogError("ShipStatus.Ship is null in ExplodableProjectile.Detonate()");
                    return;
                }

                var AOEExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                AOEExplosion.Detonate(ShipStatus.Ship);
                AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                AOEExplosion.MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, Charge);
            }
        }
    }
}