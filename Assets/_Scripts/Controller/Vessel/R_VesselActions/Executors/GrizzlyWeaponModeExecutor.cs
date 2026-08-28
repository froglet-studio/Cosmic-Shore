using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Holds the Grizzly's ACTIVE WEAPON per vessel (executors are per-vessel
    /// components — shared SO assets must never carry this state).
    ///
    /// All THREE weapons are in the cycle from level 1 - Explosives, Sniper,
    /// Flamethrower. Mass 5 no longer gates whether the plasma claw EXISTS; it
    /// gates what its burn DOES (destroy vs steal), which is sampled at ignite
    /// time in GrizzlyFlamethrowerActionExecutor. A whole weapon mode being
    /// invisible for most of a match was too much content behind one gate.
    /// </summary>
    public sealed class GrizzlyWeaponModeExecutor : ShipActionExecutorBase
    {
        public enum WeaponMode { Explosives = 0, Sniper = 1, Flamethrower = 2 }

        /// <summary>HUD: active weapon changed.</summary>
        public event Action<WeaponMode> OnModeChanged;

        public WeaponMode CurrentMode { get; private set; } = WeaponMode.Explosives;

        public override void Initialize(IVesselStatus shipStatus)
        {
            SetMode(WeaponMode.Explosives);
        }

        public void Cycle(IVesselStatus status)
        {
            var next = CurrentMode switch
            {
                WeaponMode.Explosives => WeaponMode.Sniper,
                WeaponMode.Sniper => WeaponMode.Flamethrower,
                _ => WeaponMode.Explosives,
            };
            SetMode(next);
        }

        /// <summary>
        /// The mode actually in force. Kept as the single read-point for callers even
        /// though every mode is now always available - the plasma claw degrades from
        /// steal to destroy below Mass 5 rather than disappearing.
        /// </summary>
        public WeaponMode EffectiveMode(IVesselStatus status) => CurrentMode;

        void SetMode(WeaponMode mode)
        {
            if (CurrentMode == mode) return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }
    }
}
