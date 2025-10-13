using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;

public sealed class FullAutoActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] private Gun gun;
    [SerializeField] private Transform[] muzzles;

    private IVesselStatus _status;
    private ResourceSystem _resources;

    private CancellationTokenSource _cts;

    void OnDisable()
    {
        End();
    }
    
    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus.ResourceSystem;
        gun?.Initialize(shipStatus);

        if ((muzzles == null || muzzles.Length == 0) && gun != null)
            muzzles = new[] { gun.transform };
    }

    public void Begin(FullAutoActionSO so)
    {
        if (_cts != null || !gun) return;

        _cts = new CancellationTokenSource();
        FireLoopAsync(so, _cts.Token).Forget(); // Fire and forget with cancellation
    }

    public void End()
    {
        if (_cts == null) return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private async UniTaskVoid FireLoopAsync(FullAutoActionSO so, CancellationToken token)
    {
        float interval = 1f / Mathf.Max(0.01f, so.FiringRate);
        float elapsed = 0f;

        var ammoIndex = so.AmmoIndex;
        var ammoCost = so.AmmoCost;
        var inherit = so.Inherit;
        var projectileScale = so.ProjectileScale;
        var projectileTime = so.ProjectileTime;
        var firingPattern = so.FiringPattern;
        var energy = so.Energy;
        var speedValue = so.SpeedValue.Value;

        try
        {
            while (!token.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;

                if (elapsed < interval)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                elapsed -= interval;

                var res = _resources.Resources[ammoIndex];
                if (res.CurrentAmount < ammoCost)
                {
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                    continue;
                }

                var inheritVel = inherit ? _status.Course * _status.Speed : Vector3.zero;

                for (int i = 0; i < muzzles.Length; i++)
                {
                    gun.FireGun(
                        muzzles[i],
                        speedValue,
                        inheritVel,
                        projectileScale,
                        true,
                        projectileTime,
                        0,
                        firingPattern,
                        energy
                    );
                }

                _resources.ChangeResourceAmount(ammoIndex, -ammoCost);

                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
            }
        }
        catch (OperationCanceledException)
        {
            // normal stop
        }
        catch (Exception e)
        {
            Debug.LogError($"[FullAutoActionExecutor] Loop error: {e}");
        }
    }

}
