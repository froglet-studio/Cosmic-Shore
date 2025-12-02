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

    [Header("Events")]
    [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

    private IVesselStatus _status;
    private ResourceSystem _resources;

    private CancellationTokenSource _cts;
    private CancellationToken _lifetimeToken;

    #region Unity Lifecycle
    private void Awake()
    {
        // Token that is cancelled when this component is destroyed
        _lifetimeToken = this.GetCancellationTokenOnDestroy();
    }

    private void OnEnable()
    {
        if (OnMiniGameTurnEnd != null)
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    private void OnDisable()
    {
        End();

        if (OnMiniGameTurnEnd != null)
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }
    #endregion

    #region ShipActionExecutorBase
    public override void Initialize(IVesselStatus shipStatus)
    {
        _status    = shipStatus;
        _resources = shipStatus.ResourceSystem;

        if (!_resources)
        {
            Debug.LogError("[FullAutoActionExecutor] ResourceSystem is missing on vessel.");
        }

        gun?.Initialize(shipStatus);

        if ((muzzles == null || muzzles.Length == 0) && gun != null)
            muzzles = new[] { gun.transform };
    }
    #endregion

    #region Public API
    public void Begin(FullAutoActionSO so)
    {
        // Always stop any previous loop before starting a new one
        End();

        if (!isActiveAndEnabled)
            return;

        if (!gun)
        {
            Debug.LogError("[FullAutoActionExecutor] Gun reference not assigned.");
            return;
        }

        if (_resources == null)
        {
            Debug.LogError("[FullAutoActionExecutor] No ResourceSystem available.");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        var token = _cts.Token;

        FireLoopAsync(so, token).Forget();
    }

    public void End()
    {
        if (_cts == null)
            return;

        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch
        {
            // no-op
        }

        _cts.Dispose();
        _cts = null;
    }

    private void OnTurnEndOfMiniGame()
    {
        // Optional debug:
        // Debug.Log("[FullAutoActionExecutor] Turn end received. Stopping full-auto.");
        End();
    }
    #endregion

    #region Core Loop
    private async UniTaskVoid FireLoopAsync(FullAutoActionSO so, CancellationToken token)
    {
        if (muzzles == null || muzzles.Length == 0)
        {
            Debug.LogError("[FullAutoActionExecutor] No muzzles assigned.");
            return;
        }

        var interval = 1f / Mathf.Max(0.01f, so.FiringRate);

        var   ammoIndex       = so.AmmoIndex;
        var ammoCost        = so.AmmoCost;
        var  inherit         = so.Inherit;
        var projectileScale = so.ProjectileScale;
        var projectileTime  = so.ProjectileTime;
        var   firingPattern   = so.FiringPattern;
        var energy          = so.Energy;
        var speedValue      = so.SpeedValue.Value;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_resources == null || _resources.Resources == null || ammoIndex < 0 || ammoIndex >= _resources.Resources.Count)
                {
                    Debug.LogError("[FullAutoActionExecutor] Invalid resource index or ResourceSystem.");
                    return;
                }

                var res = _resources.Resources[ammoIndex];

                if (res.CurrentAmount >= ammoCost)
                {
                    var inheritVel = inherit && _status != null
                        ? _status.Course * _status.Speed
                        : Vector3.zero;

                    for (int i = 0, count = muzzles.Length; i < count; i++)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        if (!gun || !gun.gameObject)
                        {
                            Debug.LogError("[FullAutoActionExecutor] Gun destroyed or missing during loop.");
                            return;
                        }

                        if (!gun.isActiveAndEnabled)
                        {
                            // No gun to fire, but don't hard-crash the loop
                            continue;
                        }

                        var muzzle = muzzles[i];
                        if (!muzzle)
                            continue;

                        gun.FireGun(
                            muzzle,
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

                await UniTask.Delay(
                    TimeSpan.FromSeconds(interval),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.PreLateUpdate,
                    token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop path
        }
        catch (Exception e)
        {
            Debug.LogError($"[FullAutoActionExecutor] Loop error: {e}");
        }
    }
    #endregion
}
