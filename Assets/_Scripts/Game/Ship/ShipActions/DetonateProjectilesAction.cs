using CosmicShore.Game.Projectiles;
using CosmicShore.Core;
using UnityEngine;

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