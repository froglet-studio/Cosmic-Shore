using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using UnityEngine;

/// <summary>
/// Hold-to-charge cannon. Holding the action builds energy; releasing fires a
/// projectile whose scale grows with the accumulated charge. Pressing while a
/// charged shot is in flight stops it; releasing while one is in flight detonates it.
/// </summary>
public sealed class ChargedFireGunActionExecutor : ShipActionExecutorBase
{
    /// <summary>Static event: a charged shot was fired. Param = player name.</summary>
    public static event Action<string> OnChargedShotFired;

    /// <summary>Charge amount changed (0-1 of the energy resource). For HUD binding.</summary>
    public event Action<float> OnChargeChanged;

    [Header("Scene Refs")]
    [SerializeField] Gun gun;
    [SerializeField] Transform projectileContainer;

    IVesselStatus _status;
    ResourceSystem _resources;

    CancellationTokenSource _chargeCts;
    CancellationTokenSource _watchCts;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus.ResourceSystem;

        if (gun != null)
            gun.Initialize(shipStatus);
    }

    void OnDisable()
    {
        CancelCharge();
        CancelWatch();
    }

    public void Begin(ChargedFireGunActionSO so, IVesselStatus status)
    {
        if (so == null || _resources == null || !gun)
            return;

        if (_status is { HasLiveProjectiles: true })
        {
            gun.StopProjectile();
            _status.HasLiveProjectiles = false;
            CancelWatch();
            return;
        }

        CancelCharge();
        _chargeCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        ChargeAsync(so, _chargeCts.Token).Forget();
    }

    public void Release(ChargedFireGunActionSO so, IVesselStatus status)
    {
        if (so == null || _resources == null || !gun)
            return;

        if (_status is { HasLiveProjectiles: true })
        {
            gun.DetonateProjectile();
            _status.HasLiveProjectiles = false;
            CancelWatch();
            return;
        }

        CancelCharge();

        var energy = _resources.Resources[so.EnergyResourceIndex];
        var ammo = _resources.Resources[so.AmmoResourceIndex];
        var charge = energy.CurrentAmount;

        if (charge > 0f && ammo.CurrentAmount > charge)
        {
            _resources.ChangeResourceAmount(so.AmmoResourceIndex, -charge);

            var inheritedDirection = _status is { IsAttached: false, IsTranslationRestricted: false }
                ? _status.Course
                : gun.transform.forward;

            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GunFire);
            OnChargedShotFired?.Invoke(_status?.PlayerName);

            gun.FireGun(
                projectileContainer ? projectileContainer : gun.transform,
                so.ProjectileSpeed,
                inheritedDirection * (_status?.Speed ?? 0f),
                so.ProjectileScale * charge,
                true,
                so.ProjectileTime,
                charge);

            StartWatchingProjectiles();
        }

        _resources.ResetResource(so.EnergyResourceIndex);
        OnChargeChanged?.Invoke(0f);
    }

    async UniTaskVoid ChargeAsync(ChargedFireGunActionSO so, CancellationToken token)
    {
        const float chargePeriod = 0.1f;
        var index = so.EnergyResourceIndex;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var resource = _resources.Resources[index];
                if (resource.CurrentAmount >= resource.MaxAmount)
                    break;

                await UniTask.Delay(
                    TimeSpan.FromSeconds(chargePeriod),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    token);

                _resources.ChangeResourceAmount(index, so.ChargePerSecond * chargePeriod);
                OnChargeChanged?.Invoke(
                    resource.MaxAmount > 0f ? resource.CurrentAmount / resource.MaxAmount : 0f);
            }
        }
        catch (OperationCanceledException)
        {
            // Released — charge value is consumed by Release()
        }
    }

    void StartWatchingProjectiles()
    {
        CancelWatch();
        _watchCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        WatchProjectilesAsync(_watchCts.Token).Forget();
    }

    async UniTaskVoid WatchProjectilesAsync(CancellationToken token)
    {
        if (_status == null || !projectileContainer)
            return;

        try
        {
            _status.HasLiveProjectiles = true;
            while (!token.IsCancellationRequested &&
                   projectileContainer.GetComponentsInChildren<Projectile>().Length > 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            // no-op
        }
        finally
        {
            if (_status != null)
                _status.HasLiveProjectiles = false;
        }
    }

    void CancelCharge() => Cancel(ref _chargeCts);
    void CancelWatch() => Cancel(ref _watchCts);

    static void Cancel(ref CancellationTokenSource cts)
    {
        if (cts == null) return;
        try
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        }
        catch
        {
            // no-op
        }
        cts.Dispose();
        cts = null;
    }
}
