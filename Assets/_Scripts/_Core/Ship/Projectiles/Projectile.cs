using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public Teams Team;
        public Ship Ship;
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (trailBlock.Team == Team)
                    return;

                PerformTrailImpactEffects(trailBlock.TrailBlockProperties);
            }
            if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
            {
                Debug.Log($"projectile hit ship {shipGeometry}");
                if (shipGeometry.Ship.Team == Team)
                    return;

                PerformShipImpactEffects(shipGeometry);
            }
        }

        protected virtual void PerformTrailImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Explode(Velocity, Team, Ship.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trailBlock.Steal(Ship.Player.PlayerName, Team);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                }
            }
        }

        protected virtual void PerformShipImpactEffects(ShipGeometry shipGeometry)
        {
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        HapticController.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipGeometry.Ship.transform.localRotation = Quaternion.LookRotation(Velocity);
                        break;
                    case ShipImpactEffects.Knockback:
                        //shipGeometry.Ship.transform.localPosition += Velocity/2f;
                        shipGeometry.Ship.ShipController.ModifyVelocity(Velocity * 100,2);
                        break;
                    case ShipImpactEffects.Stun:
                        shipGeometry.Ship.ShipController.ModifyThrottle(.1f, 10);
                        break;
                }
            }
        }

    }
}