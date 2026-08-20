using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Holds the Grizzly's ACTIVE WEAPON per vessel (executors are per-vessel
    /// components — shared SO assets must never carry this state).
    ///
    /// Explosives ↔ Sniper always; Flamethrower joins the cycle at Mass 5
    /// (the plasma-claw unlock — one unlock with fire-stealing, per the class doc).
    /// </summary>
    public sealed class GrizzlyWeaponModeExecutor : ShipActionExecutorBase
    {
        public enum WeaponMode { Explosives = 0, Sniper = 1, Flamethrower = 2 }

        /// <summary>HUD: active weapon changed.</summary>
        public event Action<WeaponMode> OnModeChanged;

        public WeaponMode CurrentMode { get; private set; } = WeaponMode.Explosives;

        IVesselStatus _status;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            SetMode(WeaponMode.Explosives);
        }

        public void Cycle(IVesselStatus status)
        {
            var s = status ?? _status;
            bool clawUnlocked = s?.ElementalAbilityHandler != null &&
                                s.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);

            var next = CurrentMode switch
            {
                WeaponMode.Explosives => WeaponMode.Sniper,
                WeaponMode.Sniper => clawUnlocked ? WeaponMode.Flamethrower : WeaponMode.Explosives,
                _ => WeaponMode.Explosives,
            };
            SetMode(next);
        }

        /// <summary>A Mass relock while the claw is active falls back to Explosives.</summary>
        public WeaponMode EffectiveMode(IVesselStatus status)
        {
            if (CurrentMode != WeaponMode.Flamethrower) return CurrentMode;
            var s = status ?? _status;
            bool clawUnlocked = s?.ElementalAbilityHandler != null &&
                                s.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            return clawUnlocked ? WeaponMode.Flamethrower : WeaponMode.Explosives;
        }

        void SetMode(WeaponMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }
    }
}
