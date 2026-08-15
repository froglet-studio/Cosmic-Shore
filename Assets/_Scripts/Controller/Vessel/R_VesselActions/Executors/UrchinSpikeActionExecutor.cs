using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fires the Urchin's spikes. Per-vessel state lives here, never on
    /// <see cref="UrchinSpikeActionSO"/> — one asset serves every Urchin in the match.
    ///
    /// It drives BOTH spike abilities (the aimed volley and the omni barrage) and keeps them
    /// mutually exclusive: a second ability starting cancels the first, so a pilot cannot hold
    /// the volley and stack a barrage into the same frame budget.
    /// </summary>
    public class UrchinSpikeActionExecutor : ShipActionExecutorBase
    {
        [Tooltip("Muzzles for the AIMED volley. The omni barrage ignores these and fires from " +
                 "the hull, which is what makes it read as the ship bristling rather than shooting.")]
        [SerializeField] Transform[] muzzles;

        [Tooltip("The gun that spawns spikes. Its ProjectileFactory must be wired to a factory " +
                 "whose pools hold the Urchin spike prefabs.")]
        [SerializeField] Gun gun;

        [Tooltip("Fallback origin for the omni barrage when it should not come from a muzzle. " +
                 "Defaults to this executor's own transform.")]
        [SerializeField] Transform barrageOrigin;

        IVesselStatus _status;
        ResourceSystem _resources;
        CancellationTokenSource _cts;
        UrchinSpikeActionSO _active;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _resources = shipStatus?.ResourceSystem;

            if (gun) gun.Initialize(shipStatus);
            if (!barrageOrigin) barrageOrigin = transform;

            // Re-initialization happens on a live component (a vessel swap, a Cellular Duel
            // ownership change), so a run in flight for the PREVIOUS pilot must be stopped
            // here — unconditionally, above any pilot gate — or it keeps firing in their name.
            End(null);
        }

        public void Begin(UrchinSpikeActionSO so)
        {
            if (!so) return;
            if (!gun)
            {
                CSDebug.LogError($"{name}: UrchinSpikeActionExecutor has no Gun assigned - the Urchin cannot fire spikes.");
                return;
            }

            // One spike ability at a time.
            Stop();

            _active = so;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            if (so.RepeatWhileHeld) FireLoopAsync(so, _cts.Token).Forget();
            else FireOnce(so);
        }

        /// <param name="so">The ability releasing. Null stops whatever is running (teardown);
        /// otherwise a release only stops the ability that actually started, so letting go of
        /// one trigger cannot cancel the other's burst.</param>
        public void End(UrchinSpikeActionSO so)
        {
            if (so != null && _active != so) return;
            Stop();
        }

        void Stop()
        {
            _active = null;
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        void OnDisable() => Stop();

        async UniTaskVoid FireLoopAsync(UrchinSpikeActionSO so, CancellationToken token)
        {
            float interval = 1f / Mathf.Max(0.01f, so.FiringRate);

            // Fire in SECONDS owed, paid off in whole volleys — never a Delay(interval) loop,
            // which quantizes to whole frames and silently makes the authored rate
            // min(rate, framerate), so a 30 fps client fires at half the rate of a 60 fps one.
            float owed = interval;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Drop debt beyond the cap rather than carrying it: after a hitch the gun
                    // resumes firing, it does not discharge the stall as a burst.
                    float maxOwed = interval * MaxVolleysPerTick;
                    if (owed > maxOwed) owed = maxOwed;

                    int volleys = (int)(owed / interval);
                    owed -= volleys * interval;

                    for (int v = 0; v < volleys && !token.IsCancellationRequested; v++)
                    {
                        if (!FireOnce(so)) { Stop(); return; }
                    }

                    owed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[UrchinSpikeActionExecutor] Fire loop error: {ex}");
            }
        }

        const int MaxVolleysPerTick = 4;

        /// <summary>Fires one volley. False means the vessel could not pay for it.</summary>
        bool FireOnce(UrchinSpikeActionSO so)
        {
            if (!gun || !gun.isActiveAndEnabled) return false;

            if (so.AmmoCost > 0f)
            {
                if (_resources?.Resources == null ||
                    so.AmmoIndex < 0 || so.AmmoIndex >= _resources.Resources.Count)
                {
                    CSDebug.LogError("[UrchinSpikeActionExecutor] Invalid ammo index or ResourceSystem.");
                    return false;
                }

                if (_resources.Resources[so.AmmoIndex].CurrentAmount < so.AmmoCost) return false;
            }

            // Every element read is LIVE, per volley: a crystal collected mid-hold changes the
            // very next spike.
            float rangeScale = so.ResolveRangeScale(_status);
            int generations = so.ResolveGenerations(_status);

            // The reach and its per-generation decay ride the GUN, which stamps them onto each
            // projectile; each spike then hands them to its own LoadedGun. That is how the
            // pilot's SPACE level reaches the last generation of a cascade that may outlive
            // the pilot who started it.
            gun.ChainRangeScale = rangeScale;
            gun.ChainRangeFalloff = so.ResolveRangeFalloff(_status);

            // While riding a trail the vessel's course is the ribbon's, so spikes inherit the
            // ride's forward rather than being left behind by it.
            Vector3 inherited = _status != null && _status.IsAttached
                ? _status.Course * _status.Speed
                : Vector3.zero;

            float speed = so.ProjectileSpeed * rangeScale;

            if (so.FiringPattern == FiringPatterns.Spherical)
            {
                gun.FireGun(barrageOrigin ? barrageOrigin : transform, speed, inherited,
                            so.ProjectileScale, true, so.ProjectileTime, 0,
                            FiringPatterns.Spherical, generations);
            }
            else
            {
                var origin = ResolveMuzzles();
                for (int i = 0; i < origin.Length; i++)
                {
                    if (!origin[i]) continue;
                    gun.FireGun(origin[i], speed, inherited, so.ProjectileScale, true,
                                so.ProjectileTime, 0, FiringPatterns.Default, generations,
                                detachAfterSpawn: true);
                }
            }

            if (so.AmmoCost > 0f) _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);
            return true;
        }

        Transform[] ResolveMuzzles()
        {
            if (muzzles is { Length: > 0 }) return muzzles;
            _singleMuzzle[0] = barrageOrigin ? barrageOrigin : transform;
            return _singleMuzzle;
        }

        readonly Transform[] _singleMuzzle = new Transform[1];
    }
}
