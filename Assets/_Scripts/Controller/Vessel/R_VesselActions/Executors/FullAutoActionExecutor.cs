using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using Obvious.Soap;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
public sealed class FullAutoActionExecutor : ShipActionExecutorBase
{
    /// <summary>Static event: each time a full-auto volley fires. Param = player name.</summary>
    public static event Action<string> OnVolleyFired;

    [Header("Scene Refs")]
    [SerializeField] private Gun gun;
    [SerializeField] private Transform[] muzzles;

    [Header("Events")]
    [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

    /// <summary>
    /// How many volleys one frame may fire at most. It bounds the catch-up after a hitch so a
    /// stall can never discharge a second of accumulated fire in one frame, and at 4 it still
    /// sustains the full authored rate down to a quarter of the rate in FPS (60 volleys/s holds
    /// exactly down to 15 fps).
    /// </summary>
    private const int MaxVolleysPerTick = 4;

    private IVesselStatus _status;
    private ResourceSystem _resources;
    private GunSprayAccuracy _spray;

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
            CSDebug.LogError("[FullAutoActionExecutor] ResourceSystem is missing on vessel.");
        }

        gun?.Initialize(shipStatus);

        if ((muzzles == null || muzzles.Length == 0) && gun != null)
            muzzles = new[] { gun.transform };
    }
    #endregion

    #region Public API
    public void Begin(FullAutoActionSO so, GunSprayAccuracy spray = null)
    {
        // Always stop any previous loop before starting a new one
        End();

        if (spray) _spray = spray;

        if (!isActiveAndEnabled)
            return;

        if (!gun)
        {
            CSDebug.LogError("[FullAutoActionExecutor] Gun reference not assigned.");
            return;
        }

        if (_resources == null)
        {
            CSDebug.LogError("[FullAutoActionExecutor] No ResourceSystem available.");
            return;
        }

        // The trigger is down. Idempotent: if the Turret Stance handed the hold over mid-press
        // this only refreshes the profile, so the accumulated cone survives the mode switch.
        if (_spray) _spray.BeginHold(so.Spread);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        var token = _cts.Token;

        FireLoopAsync(so, token).Forget();
    }

    public void End()
    {
        // Arms the accuracy reset; GunSprayAccuracy applies it in LateUpdate unless a
        // BeginHold lands first in the same frame (which is what a stance flip does).
        if (_spray) _spray.ReleaseHold();

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
        // CSDebug.Log("[FullAutoActionExecutor] Turn end received. Stopping full-auto.");
        End();
    }
    #endregion

    #region Core Loop
    private async UniTaskVoid FireLoopAsync(FullAutoActionSO so, CancellationToken token)
    {
        if (muzzles == null || muzzles.Length == 0)
        {
            CSDebug.LogError("[FullAutoActionExecutor] No muzzles assigned.");
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

        // The cadence is a TIME accumulator, not a per-shot delay. A `Delay(interval)` loop
        // quantizes to whole frames — it can never exceed one volley per frame, so the authored
        // rate silently became "min(rate, framerate)" and a 30 fps device fired at half the rate
        // a 60 fps device did. Owing fire in seconds and paying it off in whole volleys makes the
        // rate frame-rate independent and lets it run past the frame rate, which is the whole
        // point of a spray: at 60 volleys/s a 60 fps client fires 1 per frame and a 30 fps client
        // fires 2, and both put the same number of rounds downrange.
        float owed = interval;   // one volley is owed the instant the trigger goes down

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_resources == null || _resources.Resources == null || ammoIndex < 0 || ammoIndex >= _resources.Resources.Count)
                {
                    CSDebug.LogError("[FullAutoActionExecutor] Invalid resource index or ResourceSystem.");
                    return;
                }

                // Drop debt beyond the per-tick cap rather than carrying it: after a hitch the
                // gun resumes firing, it does not discharge the stall in a burst.
                float maxOwed = interval * MaxVolleysPerTick;
                if (owed > maxOwed) owed = maxOwed;

                int volleys = (int)(owed / interval);
                owed -= volleys * interval;

                if (volleys > 0)
                {
                    var inheritVel = inherit && _status != null
                        ? _status.Course * _status.Speed
                        : Vector3.zero;

                    // SPACE → gun range (range = speed × lifetime): read the LIVE element level
                    // per tick through the vessel's ElementalAbilityMapSO. Never cache across
                    // the hold and never bind ElementalFloats on the shared SO asset — per-vessel
                    // state lives in the handler (multiplayer: last-initializer-wins otherwise).
                    // The resolve lives on the SO so the Turret Stance can adopt the SAME number
                    // instead of authoring a second copy of it.
                    var abilities  = _status?.ElementalAbilityHandler;
                    var speedValue = so.ResolveSpeed(_status);

                    // SPACE level-5 'Piercing Bullets': below the threshold, bullets are
                    // destroyed on their first prism impact; at 5+ they pierce through.
                    var piercing = abilities != null && abilities.IsUpgradeActive(Element.Space);

                    // MASS → in-flight growth. Rounds leave the muzzle small and swell as they
                    // travel; the swept hit radius grows with them, so the size you see is the
                    // size that hits. Resolved per tick off the LIVE Mass level.
                    var growth = so.ResolveGrowthFactor(_status);

                    for (int v = 0; v < volleys && !token.IsCancellationRequested; v++)
                    {
                        // Re-read per volley — several can be paid off in one tick and each one
                        // spends ammo, so a hoisted copy would let the last ones fire for free.
                        if (_resources.Resources[ammoIndex].CurrentAmount < ammoCost)
                            break;

                        for (int i = 0, count = muzzles.Length; i < count; i++)
                        {
                            if (token.IsCancellationRequested)
                                break;

                            if (!gun || !gun.gameObject)
                            {
                                CSDebug.LogError("[FullAutoActionExecutor] Gun destroyed or missing during loop.");
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

                            // Accuracy decay: every round is deflected somewhere inside the cone
                            // the held trigger has opened. One roll PER ROUND — two muzzles in
                            // the same frame scatter independently, which is what turns the
                            // stream into a widening danger zone instead of two widening lines.
                            var aim = _spray ? _spray.PerturbAim(muzzle.forward) : muzzle.forward;

                            // detachAfterSpawn: bullets fly in world space instead of staying
                            // parented to the moving muzzle (which made them swerve with the
                            // shooter's maneuvers and die with the ship hierarchy mid-flight).
                            gun.FireGun(
                                muzzle,
                                speedValue,
                                inheritVel,
                                projectileScale,
                                true,
                                projectileTime,
                                0,
                                firingPattern,
                                energy,
                                detachAfterSpawn: true,
                                stopOnFirstPrismImpact: !piercing,
                                aimDirection: aim,
                                flightGrowthFactor: growth
                            );
                        }

                        _resources.ChangeResourceAmount(ammoIndex, -ammoCost);
                        OnVolleyFired?.Invoke(_status?.PlayerName);
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                owed += Time.deltaTime;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop path
        }
        catch (Exception e)
        {
            CSDebug.LogError($"[FullAutoActionExecutor] Loop error: {e}");
        }
    }
    #endregion

    }
}
