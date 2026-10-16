using System;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly sniper. On the triggers (design ask, 2026-10-16): LT HOLDS the scope
    /// (<see cref="BeginScope"/> / <see cref="EndScope"/> - the HUD listens to OnScopeChanged) and
    /// RT FIRES (<see cref="Fire"/>), gated by the asset's cooldown and energy; pressure is
    /// irrelevant. A round fired while scoped takes the view with it for as long as LT stays
    /// held (GrizzlyHUDView). The single-button path (<see cref="OnPress"/> /
    /// <see cref="OnRelease"/>: hold to scope, release to fire) is kept for any binding that
    /// still uses it. The scope closes when the active weapon changes away from the sniper. The round's super-shield crack is a
    /// ProjectilePrismEffectSO on the sniper projectile's impact container
    /// (SniperCrackSuperShieldPrismEffectSO) — impact effects, not end effects,
    /// because end effects fire without an impactee.
    /// </summary>
    public sealed class GrizzlySniperShotActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        [Header("Scene Refs")]
        [SerializeField, Tooltip("Dedicated sniper gun (own ProjectileFactory wired to the sniper round).")]
        Gun sniperGun;

        /// <summary>HUD: scope (PiP) opened/closed.</summary>
        public event Action<bool> OnScopeChanged;

        /// <summary>HUD: a round is away - the PiP rides it until it lands.</summary>
        public event Action<Transform> OnRoundInFlight;

        /// <summary>HUD: the round landed or expired - hand the PiP back to the ship.</summary>
        public event Action OnRoundEnded;
        /// <summary>HUD: cooldown started, param = seconds.</summary>
        public event Action<float> OnCooldownStarted;

        IVesselStatus _status;
        GrizzlyWeaponModeExecutor _modes;
        float _lastShotTime = float.NegativeInfinity;
        bool _scoped;
        Projectile _round;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            if (sniperGun != null)
                sniperGun.Initialize(shipStatus);
            SetScoped(false);

            if (_modes != null) _modes.OnModeChanged -= HandleModeChanged;
            _modes = TryGetComponent<ActionExecutorRegistry>(out var registry)
                ? registry.Get<GrizzlyWeaponModeExecutor>()
                : null;
            if (_modes != null) _modes.OnModeChanged += HandleModeChanged;
        }

        void OnDisable() => SetScoped(false);

        void OnDestroy()
        {
            if (_modes != null) _modes.OnModeChanged -= HandleModeChanged;
        }

        void HandleModeChanged(GrizzlyWeaponModeExecutor.WeaponMode mode)
        {
            if (mode != GrizzlyWeaponModeExecutor.WeaponMode.Sniper) SetScoped(false);
        }

        /// <summary>LT pressed: open the scope. Aiming is free; only firing is gated.</summary>
        public void BeginScope() => SetScoped(true);

        /// <summary>LT released: close the scope, and with it any ride on a round.</summary>
        public void EndScope() => SetScoped(false);

        public void OnPress(GrizzlySniperShotActionSO so, IVesselStatus status)
        {
            // Scope opens regardless of cooldown — aiming is free, firing is gated.
            SetScoped(true);
        }

        public void OnRelease(GrizzlySniperShotActionSO so, IVesselStatus status)
        {
            SetScoped(false);
            Fire(so, status);
        }

        /// <summary>RT pressed (or the single-button release): fire one round if the cooldown has
        /// run and the energy is there. Leaves the scope as it is.</summary>
        public void Fire(GrizzlySniperShotActionSO so, IVesselStatus status)
        {
            if (!so || status?.ResourceSystem == null || sniperGun == null) return;
            if (Time.time - _lastShotTime < so.CooldownSeconds) return;

            var resources = status.ResourceSystem.Resources;
            if (so.EnergyIndex < 0 || so.EnergyIndex >= resources.Count) return;
            if (resources[so.EnergyIndex].CurrentAmount < so.EnergyCost) return;

            status.ResourceSystem.ChangeResourceAmount(so.EnergyIndex, -so.EnergyCost);
            _lastShotTime = Time.time;
            OnCooldownStarted?.Invoke(so.CooldownSeconds);

            audioSystem.PlayGameplaySFX(GameplaySFXCategory.GunFire);

            var gunTf = sniperGun.transform;
            var inheritedDirection = status.IsTranslationRestricted ? gunTf.forward : status.Course;

            sniperGun.FireGun(
                gunTf,
                so.ProjectileSpeed,
                inheritedDirection * status.Speed,
                so.ProjectileScale,
                true,                       // ignoreCooldown (we gate our own)
                so.ProjectileTime,
                0f,                         // no charge semantics on the sniper round
                FiringPatterns.Default,
                0,
                detachAfterSpawn: true,
                stopOnFirstPrismImpact: true);

            // Hand the PiP to the round. Deliberately AFTER the fire call - LastProjectile
            // is only valid once the gun has actually spawned one, and a failed shot must
            // not leave the scope chasing a stale transform.
            var round = sniperGun.LastProjectile;
            if (round != null)
            {
                _round = round;
                // FlightEnded is per-FLIGHT and cleared by Projectile.Initialize, so a
                // pooled reissue can never carry this handler into someone else's shot.
                round.FlightEnded += HandleRoundEnded;
                OnRoundInFlight?.Invoke(round.transform);
            }
        }

        void HandleRoundEnded(Projectile projectile, bool stoppedByImpact)
        {
            if (projectile != _round) return;
            projectile.FlightEnded -= HandleRoundEnded;
            _round = null;
            OnRoundEnded?.Invoke();
        }

        void SetScoped(bool scoped)
        {
            if (_scoped == scoped) return;
            _scoped = scoped;
            OnScopeChanged?.Invoke(scoped);
        }
    }
}
