using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Executor for the Grizzly's fly-by-wire charged cannon. Owns the per-vessel
    /// shot state machine:
    ///
    ///   Idle ─press→ Charging ─release→ InFlight ─press→ Frozen ─release→ (detonate) → Idle
    ///                                        │
    ///                                        └─ natural death (impact/expiry) → Idle
    ///
    /// A frozen shell must never leak: it is force-resolved on turn end, disable,
    /// and re-initialization. Stale-projectile races are guarded with
    /// Projectile.FlightGeneration snapshots (the same pattern ProjectileDetonatorSO uses).
    /// </summary>
    public sealed class GrizzlyChargedShotActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        /// <summary>Static event: a charged shot left the barrel. Param = player name.</summary>
        public static event Action<string> OnChargedShotFired;

        /// <summary>Normalized charge for the HUD meter (0..1).</summary>
        public event Action<float> OnChargeChanged;

        public enum ShotState { Idle, Charging, InFlight, Frozen }
        /// <summary>State for the HUD (weapon-mode indicator / reticle hinting).</summary>
        public event Action<ShotState> OnShotStateChanged;
        public ShotState State { get; private set; } = ShotState.Idle;

        [Header("Scene Refs")]
        [SerializeField] Gun gun;
        [SerializeField] Transform projectileContainer;

        [Header("Events")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        IVesselStatus _status;
        ResourceSystem _resources;

        Projectile _shot;
        int _shotGeneration;
        CancellationTokenSource _chargeCts;

        // The SO that fired the outstanding shell — a natural-impact detonation
        // (HandleFlightEnded) needs its AOE prefabs and scales, and the event
        // carries no SO. One action asset drives this executor, so last-wins is safe.
        GrizzlyChargedShotActionSO _lastSo;

        const float ChargeTickSeconds = 0.1f;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;

            // A disabled executor can no longer resolve its shell — clean up silently.
            ResolveOutstandingShot(detonate: false, so: null);
            CancelCharge();
            SetState(ShotState.Idle);
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _resources = shipStatus.ResourceSystem;

            if (gun != null)
                gun.Initialize(shipStatus);

            ResolveOutstandingShot(detonate: false, so: null);
            CancelCharge();
            SetState(ShotState.Idle);
        }

        // ── Input ─────────────────────────────────────────────────────────────

        public void OnPress(GrizzlyChargedShotActionSO so, IVesselStatus status)
        {
            switch (State)
            {
                case ShotState.Idle:
                    BeginCharge(so);
                    break;

                case ShotState.InFlight:
                    if (ShotIsLive())
                    {
                        _shot.Freeze();
                        SetState(ShotState.Frozen);
                    }
                    else
                    {
                        // The shell died between frames — treat this press as a new charge.
                        ForgetShot();
                        BeginCharge(so);
                    }
                    break;

                case ShotState.Charging:
                case ShotState.Frozen:
                    break; // presses are idempotent in these states
            }
        }

        public void OnRelease(GrizzlyChargedShotActionSO so, IVesselStatus status)
        {
            switch (State)
            {
                case ShotState.Charging:
                    CancelCharge();
                    Fire(so, status);
                    break;

                case ShotState.Frozen:
                    ResolveOutstandingShot(detonate: true, so: so);
                    SetState(ShotState.Idle);
                    break;

                case ShotState.Idle:
                case ShotState.InFlight:
                    break;
            }
        }

        // ── Charge ────────────────────────────────────────────────────────────

        void BeginCharge(GrizzlyChargedShotActionSO so)
        {
            CancelCharge();
            _chargeCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            SetState(ShotState.Charging);
            ChargeAsync(so, _chargeCts.Token).Forget();
        }

        async UniTaskVoid ChargeAsync(GrizzlyChargedShotActionSO so, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var res = _resources.Resources[so.EnergyIndex];
                    if (res.CurrentAmount >= res.MaxAmount)
                    {
                        OnChargeChanged?.Invoke(1f);
                        break; // fully charged — hold until release
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(ChargeTickSeconds),
                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);

                    _resources.ChangeResourceAmount(so.EnergyIndex, so.ChargePerSecond * ChargeTickSeconds);
                    OnChargeChanged?.Invoke(Charge01(so));
                }
            }
            catch (OperationCanceledException) { }
        }

        void CancelCharge()
        {
            if (_chargeCts == null) return;
            _chargeCts.Cancel();
            _chargeCts.Dispose();
            _chargeCts = null;
        }

        float Charge01(GrizzlyChargedShotActionSO so)
        {
            var res = _resources.Resources[so.EnergyIndex];
            return res.MaxAmount <= 0f ? 0f : Mathf.Clamp01(res.CurrentAmount / res.MaxAmount);
        }

        // ── Fire / detonate ───────────────────────────────────────────────────

        void Fire(GrizzlyChargedShotActionSO so, IVesselStatus status)
        {
            float charge = Charge01(so);
            if (charge < so.MinChargeToFire)
            {
                SetState(ShotState.Idle);
                return;
            }

            // Single-pool economy: the whole accumulated charge IS the shot's cost.
            _resources.ResetResource(so.EnergyIndex);
            OnChargeChanged?.Invoke(0f);

            // In turret stance (translation restricted) the shot leaves along the gun's
            // facing; in free flight it inherits the vessel's course.
            var gunTf = gun ? gun.transform : transform;
            var inheritedDirection = status.IsTranslationRestricted ? gunTf.forward : status.Course;

            audioSystem.PlayGameplaySFX(GameplaySFXCategory.GunFire);
            OnChargedShotFired?.Invoke(_status?.PlayerName);
            _lastSo = so;

            gun.FireGun(
                gunTf,
                so.ProjectileSpeed,
                inheritedDirection * status.Speed,
                so.ProjectileScale * Mathf.Max(charge, 0.2f),   // visible shell even at low charge
                true,                                            // ignoreCooldown
                so.ProjectileTime,
                charge,                                          // Projectile.Charge drives blast lerp
                FiringPatterns.Default,
                0,
                detachAfterSpawn: true,                          // frozen shells must not ride the ship
                // The shell is a BOMB: it stops on the first prism it hits and
                // HandleFlightEnded detonates it there. Without this it pierces
                // everything and the only blast is the manual freeze->release —
                // which the 400 u/s shell has carried out of self-launch range
                // long before a human can press it.
                stopOnFirstPrismImpact: true,
                spareOwnDomain: SpaceUpgradeActive(status));

            _shot = gun.LastProjectile;
            if (_shot == null)
            {
                SetState(ShotState.Idle);
                return;
            }

            _shotGeneration = _shot.FlightGeneration;
            _shot.FlightEnded += HandleFlightEnded;
            SetState(ShotState.InFlight);
        }

        void HandleFlightEnded(Projectile p, bool stoppedByImpact)
        {
            if (p != _shot) return;

            // Capture the shell's death pose BEFORE ForgetShot/pool-return can touch it.
            // ProjectileImpactor raises this event before ReturnToFactory, so the
            // transform still holds the impact position here.
            var pos = p.transform.position;
            var rot = p.transform.rotation;
            float charge = Mathf.Clamp01(p.Charge);
            var di = p.GetComponent<ProjectileImpactor>()?.DIContainer;

            ForgetShot();
            if (State == ShotState.InFlight || State == ShotState.Frozen)
                SetState(ShotState.Idle);

            // The bomb goes off where it lands. The projectile's own impactor/end
            // effects handle the pool return, so spawn the AOE directly instead of
            // routing through the detonator (whose delayed ReturnToFactory would
            // double-return the pooled shell).
            if (stoppedByImpact)
                SpawnBlast(pos, rot, charge, di);
        }

        /// <summary>
        /// Spawns the charged-shot AOE at a shell's death point — the natural-impact
        /// twin of the detonator's manual freeze→release path. Same charge-driven
        /// scale lerp, same Space scaling, and AffectSelfOverride is true for the
        /// same reason: the shooter riding its own blast is the class identity.
        /// </summary>
        void SpawnBlast(Vector3 pos, Quaternion rot, float charge01, Reflex.Core.Container di)
        {
            var so = _lastSo;
            if (so == null || so.AoePrefabs == null || _status == null) return;

            float spaceMul = ElementalScaling.Multiplier(
                _status, Element.Space, so.SpaceScaleAtFull, so.SpaceScaleMinMul);
            float targetScale = Mathf.Lerp(
                so.MinExplosionScale * spaceMul, so.MaxExplosionScale * spaceMul, charge01);

            foreach (var prefab in so.AoePrefabs)
            {
                if (!prefab) continue;
                var spawned = Instantiate(prefab, pos, rot);
                if (di != null)
                    GameObjectInjector.InjectRecursive(spawned.gameObject, di);
                spawned.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnDomain           = _status.Domain,
                    Vessel              = _status.Vessel,
                    MaxScale            = targetScale,
                    OverrideMaterial    = _status.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition       = pos,
                    SpawnRotation       = rot,
                    AffectSelfOverride  = true
                });
                spawned.Detonate();
            }
        }

        /// <summary>Detonates (or silently despawns) a live outstanding shell, if any.</summary>
        void ResolveOutstandingShot(bool detonate, GrizzlyChargedShotActionSO so)
        {
            if (!ShotIsLive())
            {
                ForgetShot();
                return;
            }

            var shot = _shot;
            ForgetShot();

            if (detonate && so != null && so.Detonator != null)
            {
                float spaceMul = ElementalScaling.Multiplier(
                    _status, Element.Space, so.SpaceScaleAtFull, so.SpaceScaleMinMul);

                so.Detonator.Detonate(new ProjectileDetonatorSO.Request
                {
                    Projectile          = shot,
                    Position            = shot.transform.position,
                    Rotation            = shot.transform.rotation,
                    FaceExitVelocity    = false,
                    MinScale            = so.MinExplosionScale * spaceMul,
                    MaxScale            = so.MaxExplosionScale * spaceMul,
                    ExplodeDelaySeconds = so.ExplodeDelaySeconds,
                    ReturnDelay         = so.ReturnDelaySeconds,
                    StopAtImpact        = true,
                    DisableColliderNow  = true,
                    Prefabs             = so.AoePrefabs,
                    Anonymous           = false,
                    OverrideMaterial    = _status?.AOEExplosionMaterial,
                    DIContainer         = shot.GetComponent<ProjectileImpactor>()?.DIContainer,
                    // The shooter must always ride its own blast; ally sparing at Space 5
                    // is handled inside VesselImpulseByExplosionEffectSO instead.
                    AffectSelfOverride  = true
                });
            }
            else
            {
                shot.ReturnToFactory();
            }
        }

        bool ShotIsLive() =>
            _shot != null && _shot.isActiveAndEnabled && _shot.FlightGeneration == _shotGeneration;

        void ForgetShot()
        {
            if (_shot != null)
                _shot.FlightEnded -= HandleFlightEnded;
            _shot = null;
        }

        bool SpaceUpgradeActive(IVesselStatus status) =>
            status?.ElementalAbilityHandler != null &&
            status.ElementalAbilityHandler.IsUpgradeActive(Element.Space);

        void SetState(ShotState next)
        {
            if (State == next) return;
            State = next;
            OnShotStateChanged?.Invoke(next);
        }

        void HandleTurnEnd()
        {
            // Never leave a live shell across turns; silent cleanup, no surprise blasts.
            ResolveOutstandingShot(detonate: false, so: null);
            CancelCharge();
            SetState(ShotState.Idle);
            OnChargeChanged?.Invoke(0f);
        }
    }
}
