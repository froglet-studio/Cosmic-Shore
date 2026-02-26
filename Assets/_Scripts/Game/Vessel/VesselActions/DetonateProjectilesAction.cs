using CosmicShore.Game.ImpactEffects;
using CosmicShore.Models.Enums;
using UnityEngine;

namespace CosmicShore.Game.Ship
{
    public class DetonateProjectilesAction : ShipAction
    {
        // TODO: WIP gun firing needs to be reworked
        [SerializeField] Gun gun;

        public override void StartAction()
        {
            gun.DetonateProjectile();
        }

        public override void StopAction()
        {
        
        }
    }
}
