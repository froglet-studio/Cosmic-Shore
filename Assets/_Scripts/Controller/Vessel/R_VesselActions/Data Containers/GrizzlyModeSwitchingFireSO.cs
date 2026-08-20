using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Fire (right trigger) — composite dispatcher over the active weapon:
    /// Explosives (charged cannon), Sniper, or Plasma Claw (Mass 5).
    ///
    /// Follows SparrowModeSwitchingFireSO's shared-asset discipline: SO assets are
    /// shared across every Grizzly, so the pressed-state is keyed per vessel. The
    /// gesture is captured at PRESS: StopAction routes to the SAME sub-action that
    /// started, even if the player cycles weapons mid-hold — essential because the
    /// charged cannon's release is a meaningful input (fire / detonate).
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyModeSwitchingFire", menuName = "ScriptableObjects/Vessel Actions/Grizzly Mode Switching Fire")]
    public class GrizzlyModeSwitchingFireSO : ShipActionSO
    {
        [Header("Weapons")]
        [SerializeField] ShipActionSO explosivesFire;   // GrizzlyChargedShotAction
        [SerializeField] ShipActionSO sniperFire;       // GrizzlySniperShotAction
        [SerializeField] ShipActionSO flamethrowerFire; // GrizzlyFlamethrowerAction (Mass 5)

        sealed class HoldState
        {
            public ShipActionSO Active;
            public ActionExecutorRegistry Registry;
        }

        readonly Dictionary<IVesselStatus, HoldState> _held = new();

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            if (vs == null) return;
            PruneDestroyed();

            // Input replay / RPC echo: close the previous hold through its own registry.
            if (_held.TryGetValue(vs, out var stale))
                stale.Active?.StopAction(stale.Registry ? stale.Registry : execs, vs);

            var mode = execs?.Get<GrizzlyWeaponModeExecutor>()?.EffectiveMode(vs)
                       ?? GrizzlyWeaponModeExecutor.WeaponMode.Explosives;

            var action = mode switch
            {
                GrizzlyWeaponModeExecutor.WeaponMode.Sniper => sniperFire,
                GrizzlyWeaponModeExecutor.WeaponMode.Flamethrower => flamethrowerFire ? flamethrowerFire : explosivesFire,
                _ => explosivesFire,
            };

            _held[vs] = new HoldState { Active = action, Registry = execs };
            action?.StartAction(execs, vs);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            if (vs != null && _held.TryGetValue(vs, out var state))
            {
                state.Active?.StopAction(state.Registry ? state.Registry : execs, vs);
                _held.Remove(vs);
            }
            PruneDestroyed();
        }

        readonly List<IVesselStatus> _prune = new();
        void PruneDestroyed()
        {
            _prune.Clear();
            foreach (var kvp in _held)
            {
                var vs = kvp.Key;
                if (vs == null || vs.Vessel == null || (vs is Component c && !c))
                    _prune.Add(vs);
            }
            foreach (var vs in _prune)
                _held.Remove(vs);
        }
    }
}
