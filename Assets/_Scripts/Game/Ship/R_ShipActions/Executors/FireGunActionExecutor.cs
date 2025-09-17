using System;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using UnityEngine;

public  class FireGunActionExecutor : ShipActionExecutorBase
{
    public event Action OnGunFired;

    [Header("Scene Refs")]
    [SerializeField] Gun gun;
    [SerializeField] Transform projectileContainer;

    IVesselStatus _status;
    ResourceSystem _resources;
    FireGunActionSO _soRef;
    public float Ammo01
    {
        get
        {
            if (_resources == null || _resources.Resources == null) return 0f;

            var index = _soRef != null ? _soRef.AmmoIndex : 0;

            if (index < 0 || index >= _resources.Resources.Count) return 0f;

            var res = _resources.Resources[index];
            if (res == null || res.MaxAmount <= 0f) return 0f;

            return Mathf.Clamp01(res.CurrentAmount / res.MaxAmount);
        }
    }


    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus.ResourceSystem;

        if (gun != null)
            gun.Initialize(shipStatus);

        if (projectileContainer != null)
            projectileContainer.SetParent(null, true);
    }

    public void Fire(FireGunActionSO so, IVesselStatus status)
    {
        if (_resources.Resources[so.AmmoIndex].CurrentAmount < so.AmmoCost)
            return;

        _soRef = so;
        _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);
        var inheritedVelocity = /*status.Attached ?*/ gun.transform.forward  /*:status.Course*/;

        OnGunFired?.Invoke();
        Debug.Log($"Inherited Velocity {inheritedVelocity} status Attached {status.Attached}");
        gun.FireGun(projectileContainer, so.Speed, inheritedVelocity * status.Speed, so.ProjectileScale, true, so.ProjectileTime.Value, 0,
            FiringPatterns.Default, so.Energy);
    }
}