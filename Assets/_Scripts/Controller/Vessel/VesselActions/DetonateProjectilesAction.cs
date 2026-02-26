using CosmicShore.Gameplay;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
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
