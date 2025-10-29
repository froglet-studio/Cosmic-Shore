using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using Obvious.Soap;

public sealed class FullAutoActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] private Gun gun;
    [SerializeField] private Transform[] muzzles;
    [SerializeField]
    public ScriptableEventNoParam OnMiniGameTurnEnd;

    private IVesselStatus _status;
    private ResourceSystem _resources;

    private CancellationTokenSource _cts;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        End();
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
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

        _cts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        
        FireLoopAsync(so, _cts.Token).Forget(); // Fire and forget with cancellation
    }

    public void End()
    {
        if (_cts == null) return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }
    
    void OnTurnEndOfMiniGame()
    {
        End();
    }

    private async UniTaskVoid FireLoopAsync(FullAutoActionSO so, CancellationToken token)
    {
        float interval = 1f / Mathf.Max(0.01f, so.FiringRate);

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
                // Check resource before firing
                var res = _resources.Resources[ammoIndex];
                if (res.CurrentAmount >= ammoCost)
                {
                    var inheritVel = inherit ? _status.Course * _status.Speed : Vector3.zero;

                    for (int i = 0, count = muzzles.Length; i < count; i++)
                    {
                        if (!gun || !gun.gameObject)
                        {
                            Debug.LogError("No active gun found!");
                            return;
                        }

                        if (!gun.isActiveAndEnabled)
                        {
                            Debug.LogError("No active gun found!");
                            continue;
                        }
                        
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
                }

                // wait exactly for interval duration
                await UniTask.Delay(TimeSpan.FromSeconds(interval),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.PreLateUpdate,
                    token);
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
