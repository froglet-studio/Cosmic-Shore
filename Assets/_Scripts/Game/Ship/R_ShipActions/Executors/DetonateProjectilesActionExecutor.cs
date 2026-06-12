using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using UnityEngine;

/// <summary>
/// Remotely detonates the vessel's live projectiles at their current position.
/// </summary>
public sealed class DetonateProjectilesActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] Gun gun;

    IVesselStatus _status;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;

        if (gun != null)
            gun.Initialize(shipStatus);
    }

    public void Detonate(IVesselStatus status)
    {
        if (!gun) return;

        gun.DetonateProjectile();

        var vesselStatus = status ?? _status;
        if (vesselStatus != null)
            vesselStatus.HasLiveProjectiles = false;
    }
}
