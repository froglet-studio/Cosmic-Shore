using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fires the Urchin's spikes. Per-vessel state lives here, never on
    /// <see cref="UrchinSpikeActionSO"/> — one asset serves every Urchin in the match, and the
    /// CHARGE TIMER is exactly the kind of per-vessel state that would be last-writer-wins on a
    /// shared asset.
    ///
    /// One trigger, two shots. The press fires the aimed shotgun immediately (semi-automatic)
    /// and starts a charge; the release throws an omni burst whose spike count is how long the
    /// trigger was down. A tap therefore behaves exactly as the old aimed volley did on its
    /// first pull, which is what keeps the weapon readable after the merge.
    /// </summary>
    public class UrchinSpikeActionExecutor : ShipActionExecutorBase
    {
        [Tooltip("The vessel's gun muzzles. Both AIMED patterns fire from every one of them " +
                 "(the single shot and the concentric-ring shotgun). Only the omni barrage " +
                 "ignores them and fires from the hull, which is what makes that one read as " +
                 "the ship bristling rather than shooting.")]
        [SerializeField] Transform[] muzzles;

        [Tooltip("The gun that spawns spikes. Its ProjectileFactory must be wired to a factory " +
                 "whose pools hold the Urchin spike prefabs.")]
        [SerializeField] Gun gun;

        [Tooltip("Origin of the charged omni burst - the hull, not a muzzle, which is what " +
                 "makes it read as the ship bristling rather than shooting. Defaults to this " +
                 "executor's own transform.")]
        [SerializeField] Transform barrageOrigin;

        [Header("Audio")]
        [Tooltip("FMOD event when the trigger starts charging. Leave empty for silence - never " +
                 "point it at a borrowed event to hear something.")]
        [SerializeField] EventReference chargeStartEvent;

        [Tooltip("FMOD event when a charged burst is released. Leave empty for silence.")]
        [SerializeField] EventReference chargedReleaseEvent;

        IVesselStatus _status;
        ResourceSystem _resources;
        CancellationTokenSource _cts;
        UrchinSpikeActionSO _active;

        /// <summary>The ability whose trigger is currently held and charging, or null. Kept
        /// separate from <see cref="_active"/> so a teardown can drop the charge WITHOUT firing
        /// it — a vessel swap must never discharge in the previous pilot's name.</summary>
        UrchinSpikeActionSO _charging;
        float _chargeStartTime;

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

            // The press has already fired. Charging starts here rather than instead of the
            // shot, so holding is strictly additive: a tap is a shot, a hold is a shot plus a
            // burst, and the pilot never has to choose between them.
            if (so.ChargeEnabled)
            {
                _charging = so;
                _chargeStartTime = Time.time;
                PlayOneShot(chargeStartEvent);
            }
        }

        /// <param name="so">The ability releasing. Null stops whatever is running (teardown);
        /// otherwise a release only stops the ability that actually started, so letting go of
        /// one trigger cannot cancel the other's burst.</param>
        public void End(UrchinSpikeActionSO so)
        {
            if (so != null && _active != so) return;

            // A RELEASE discharges; a TEARDOWN (so == null) does not. Same distinction the
            // ability filter above draws, one step further in: letting go of the trigger is the
            // ability completing, while a vessel swap or a disable is the ability being taken
            // away mid-hold and must fire nothing.
            if (so != null && _charging == so) ReleaseCharge(so);

            Stop();
        }

        void Stop()
        {
            _active = null;
            _charging = null;
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        /// <summary>The trigger came up. Anything shorter than the authored minimum was a tap
        /// and has already been paid for by the press.</summary>
        void ReleaseCharge(UrchinSpikeActionSO so)
        {
            float held = Time.time - _chargeStartTime;
            _charging = null;
            if (held < so.MinChargeSeconds) return;

            if (FireChargedBurst(so, so.Charge01(held)))
                PlayOneShot(chargedReleaseEvent);
        }

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
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
                    // The gun going away is fatal to the hold; running out of ammo is not
                    // (see below). Checked here so a destroyed gun ends the loop instead of
                    // spinning it.
                    if (!gun || !gun.gameObject) { Stop(); return; }

                    // Drop debt beyond the cap rather than carrying it: after a hitch the gun
                    // resumes firing, it does not discharge the stall as a burst.
                    float maxOwed = interval * MaxVolleysPerTick;
                    if (owed > maxOwed) owed = maxOwed;

                    int volleys = (int)(owed / interval);
                    owed -= volleys * interval;

                    for (int v = 0; v < volleys && !token.IsCancellationRequested; v++)
                    {
                        // Running dry must NOT end the hold. Ammo refills while you fly (and
                        // fast while you ride a trail), so ending the loop here would force the
                        // pilot to release and re-press to resume - the gun would feel jammed
                        // rather than empty. Break the volley, keep the loop, resume when the
                        // meter recovers.
                        if (!FireOnce(so)) break;
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

            if (!CanPay(so.AmmoIndex, so.AmmoCost)) return false;

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

            // Spikes ALWAYS inherit the vessel's live velocity - free flight and rides alike.
            // (This briefly inherited only while attached, which made every free-flight
            // volley fire as if from a standing gun: at cruise speed the vessel outran its
            // own shotgun's lateral spikes and the blast read as dropping behind the ship.)
            Vector3 inherited = _status != null
                ? _status.Course * _status.Speed
                : Vector3.zero;

            float speed = so.ProjectileSpeed * rangeScale;

            if (so.FiringPattern == FiringPatterns.Spherical)
            {
                // The ship's own barrage fires at the authored density (a dense, gapless
                // sphere); chain children keep their budgeted energy-derived counts.
                gun.FireGun(barrageOrigin ? barrageOrigin : transform, speed, inherited,
                            so.ProjectileScale, true, so.ProjectileTime, 0,
                            FiringPatterns.Spherical, generations,
                            sphericalPoints: so.BarrageSpikeCount);
            }
            else if (so.FiringPattern == FiringPatterns.ConcentricRings)
            {
                // The shotgun: one blast per pull, from EVERY muzzle, so the pull reads as the
                // ship's guns firing rather than the hull venting. Each muzzle's fan is spun by
                // half a spoke relative to the last, so N guns interleave into one denser cone
                // instead of N copies of the same spokes. The per-ring count is authored for
                // ONE muzzle - a vessel that grows a third gun gets a denser blast, which is
                // the intended reading of mounting another gun.
                var barrels = ResolveMuzzles();
                int spokes = Mathf.Max(1, so.SpikesPerRing);
                for (int i = 0; i < barrels.Length; i++)
                {
                    if (!barrels[i]) continue;
                    float phase = 360f / spokes * i / Mathf.Max(1, barrels.Length);
                    gun.FireRingBlast(barrels[i], speed, inherited,
                                      so.ProjectileScale, so.ProjectileTime, 0, generations,
                                      so.RingCount, so.SpikesPerRing, so.ConeHalfAngleDegrees,
                                      so.CenterSpike, phase);
                }
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

            Pay(so.AmmoIndex, so.AmmoCost);
            return true;
        }

        /// <summary>
        /// The charged omni burst — the release half of the trigger. Same three things every
        /// spike does; only the PATTERN, the count and the price differ from the tap.
        /// </summary>
        bool FireChargedBurst(UrchinSpikeActionSO so, float charge01)
        {
            if (!gun || !gun.isActiveAndEnabled) return false;
            if (!CanPay(so.AmmoIndex, so.ChargedAmmoCost)) return false;

            // Live per burst, exactly like the tap: a crystal collected DURING the hold changes
            // the burst it is charging.
            float rangeScale = so.ResolveRangeScale(_status);
            int generations = so.ResolveGenerations(_status);

            gun.ChainRangeScale = rangeScale;
            gun.ChainRangeFalloff = so.ResolveRangeFalloff(_status);

            Vector3 inherited = _status != null
                ? _status.Course * _status.Speed
                : Vector3.zero;

            gun.FireGun(barrageOrigin ? barrageOrigin : transform,
                        so.ChargedProjectileSpeed * rangeScale, inherited,
                        so.ProjectileScale, true, so.ProjectileTime, 0,
                        FiringPatterns.Spherical, generations,
                        sphericalPoints: so.ChargedSpikeCount(charge01));

            Pay(so.AmmoIndex, so.ChargedAmmoCost);
            return true;
        }

        /// <summary>Can the vessel afford <paramref name="cost"/> from slot
        /// <paramref name="index"/>? A zero cost is always affordable and never touches the
        /// meter, which is what makes the charged burst free.</summary>
        bool CanPay(int index, float cost)
        {
            if (cost <= 0f) return true;

            if (_resources?.Resources == null || index < 0 || index >= _resources.Resources.Count)
            {
                CSDebug.LogError("[UrchinSpikeActionExecutor] Invalid ammo index or ResourceSystem.");
                return false;
            }

            return _resources.Resources[index].CurrentAmount >= cost;
        }

        void Pay(int index, float cost)
        {
            if (cost > 0f) _resources.ChangeResourceAmount(index, -cost);
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
