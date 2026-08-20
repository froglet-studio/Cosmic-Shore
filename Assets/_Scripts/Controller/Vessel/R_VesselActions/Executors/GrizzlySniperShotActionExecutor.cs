using System;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly sniper: hold to scope (PiP — the HUD listens to OnScopeChanged),
    /// release to fire a fast precision round. The round's super-shield crack is a
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
        /// <summary>HUD: cooldown started, param = seconds.</summary>
        public event Action<float> OnCooldownStarted;

        IVesselStatus _status;
        float _lastShotTime = float.NegativeInfinity;
        bool _scoped;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            if (sniperGun != null)
                sniperGun.Initialize(shipStatus);
            SetScoped(false);
        }

        void OnDisable() => SetScoped(false);

        public void OnPress(GrizzlySniperShotActionSO so, IVesselStatus status)
        {
            // Scope opens regardless of cooldown — aiming is free, firing is gated.
            SetScoped(true);
        }

        public void OnRelease(GrizzlySniperShotActionSO so, IVesselStatus status)
        {
            SetScoped(false);

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
        }

        void SetScoped(bool scoped)
        {
            if (_scoped == scoped) return;
            _scoped = scoped;
            OnScopeChanged?.Invoke(scoped);
        }
    }
}
