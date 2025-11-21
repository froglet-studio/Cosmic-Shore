using System;
using Obvious.Soap;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using UnityEngine;

public class FireGunActionExecutor : ShipActionExecutorBase
{
    public event Action OnGunFired;

    [Header("Scene Refs")]
    [SerializeField] Gun gun;

    [SerializeField] Transform projectileContainer;

    [Header("Events")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

    IVesselStatus _status;
    ResourceSystem _resources;
    FireGunActionSO _soRef;
    readonly bool _detachFromContainer = true;

    Transform _worldMuzzleAnchor;

    void Awake()
    {
        var go = new GameObject("[MuzzleWorldAnchor]")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _worldMuzzleAnchor = go.transform;
        _worldMuzzleAnchor.SetParent(null, true);
    }

    void OnEnable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnDestroy()
    {
        if (_worldMuzzleAnchor)
            Destroy(_worldMuzzleAnchor.gameObject);
    }

    public float Ammo01
    {
        get
        {
            if (!_resources || _resources.Resources == null) return 0f;
            var index = _soRef ? _soRef.AmmoIndex : 0;
            if (index < 0 || index >= _resources.Resources.Count) return 0f;
            var res = _resources.Resources[index];
            if (res == null || res.MaxAmount <= 0f) return 0f;
            return Mathf.Clamp01(res.CurrentAmount / res.MaxAmount);
        }
    }

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status    = shipStatus;
        _resources = shipStatus.ResourceSystem;

        if (gun != null)
            gun.Initialize(shipStatus);
    }

    public void Fire(FireGunActionSO so, IVesselStatus status)
    {
        if (_resources.Resources[so.AmmoIndex].CurrentAmount < so.AmmoCost)
            return;

        _soRef = so;
        _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);

        var gunTf = gun ? gun.transform : transform;
        _worldMuzzleAnchor.SetPositionAndRotation(gunTf.position, gunTf.rotation);

        var inheritedVelocityWS = status.Course * status.Speed;

        OnGunFired?.Invoke();

        gun.FireGun(
            _worldMuzzleAnchor,        
            so.Speed,
            inheritedVelocityWS,
            so.ProjectileScale,
            true,                      
            so.ProjectileTime.Value,
            0,
            FiringPatterns.Default,
            so.Energy,
            detachAfterSpawn: _detachFromContainer
        );
    }

    void OnTurnEndOfMiniGame()
    {
        _soRef = null;
    }
}
