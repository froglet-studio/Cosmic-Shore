using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of the Grizzly's two TRIGGERS (LT or RT). What the trigger does follows the ACTIVE
    /// WEAPON (<see cref="GrizzlyWeaponModeExecutor"/>, cycled on LB / RB / E), design ask
    /// 2026-10-16:
    /// <list type="bullet">
    /// <item><b>Explosives</b> - the TRIGGER BOMBS: pull arms, release fires (sized by the pull's
    /// peak pressure), pull again freezes, release detonates. GRIZZLY_TRIGGER_BOMBS.md.</item>
    /// <item><b>Plasma claw</b> - LT sprays a cone forward from the LEFT front of the hull, RT from
    /// the RIGHT front, for as long as it is held; live pressure sets the cone's size, always the
    /// Grizzly's shortest-range weapon.</item>
    /// <item><b>Sniper</b> - LT holds the SCOPE (the view rides the round in flight for as long as
    /// LT is held) and LT's pressure is its ZOOM (2026-10-17); RT FIRES, gated by the sniper's cooldown.</item>
    /// </list>
    /// The bombs are OFF while another weapon is active: switching away retires any live bomb
    /// (<see cref="GrizzlyTriggerBombExecutor"/>), so the pilot carries speed on momentum or Rush.
    ///
    /// The class keeps its bomb name and GUID because the bomb executor's autopilot finds the
    /// trigger it presses by this type (<c>ResolveBoundInput</c>). The two assets (Left / Right)
    /// carry which trigger they are and share the claw and sniper configs.
    ///
    /// Press and release both carry meaning. The weapon is captured at PRESS, per vessel, so a
    /// release always reaches the weapon that started (a mid-hold weapon change ends the hold
    /// through the executors' own mode-change handlers).
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyBombAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Bomb")]
    public class GrizzlyBombActionSO : ShipActionSO
    {
        public enum TriggerSide { Left = 0, Right = 1 }

        [SerializeField, Tooltip("Which trigger this asset is bound to. Must match the input event it is listed under on the vessel prefab.")]
        TriggerSide side = TriggerSide.Right;

        [Header("Other weapons on this trigger")]
        [SerializeField, Tooltip("Plasma claw config. In claw mode this trigger sprays a cone from its own side of the hull's front, sized by live pressure. Empty = the trigger does nothing in claw mode.")]
        GrizzlyFlamethrowerActionSO claw;
        [SerializeField, Tooltip("Sniper config. In sniper mode LT holds the scope and RT fires (cooldown and energy from this asset). Empty = the trigger does nothing in sniper mode.")]
        GrizzlySniperShotActionSO sniper;

        public TriggerSide Side => side;

        readonly Dictionary<IVesselStatus, GrizzlyWeaponModeExecutor.WeaponMode> _held = new();
        readonly List<IVesselStatus> _prune = new();

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (execs == null) return;
            PruneDestroyed();

            var mode = execs.Get<GrizzlyWeaponModeExecutor>()?.EffectiveMode(vesselStatus)
                       ?? GrizzlyWeaponModeExecutor.WeaponMode.Explosives;
            if (vesselStatus != null) _held[vesselStatus] = mode;

            switch (mode)
            {
                case GrizzlyWeaponModeExecutor.WeaponMode.Flamethrower:
                    if (claw) execs.Get<GrizzlyFlamethrowerActionExecutor>()?.BeginSpray(claw, vesselStatus, side);
                    break;

                case GrizzlyWeaponModeExecutor.WeaponMode.Sniper:
                    var sniperExec = execs.Get<GrizzlySniperShotActionExecutor>();
                    if (!sniper || sniperExec == null) break;
                    if (side == TriggerSide.Left) sniperExec.BeginScope(sniper);
                    else sniperExec.Fire(sniper, vesselStatus);
                    break;

                default:
                    execs.Get<GrizzlyTriggerBombExecutor>()?.OnPress(side);
                    break;
            }
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (execs == null) return;

            var mode = GrizzlyWeaponModeExecutor.WeaponMode.Explosives;
            if (vesselStatus != null && _held.TryGetValue(vesselStatus, out var pressed))
            {
                mode = pressed;
                _held.Remove(vesselStatus);
            }
            else
            {
                // A release with no press on record (replay, autopilot): the current weapon.
                mode = execs.Get<GrizzlyWeaponModeExecutor>()?.EffectiveMode(vesselStatus) ?? mode;
            }

            switch (mode)
            {
                case GrizzlyWeaponModeExecutor.WeaponMode.Flamethrower:
                    execs.Get<GrizzlyFlamethrowerActionExecutor>()?.EndSpray(side);
                    break;

                case GrizzlyWeaponModeExecutor.WeaponMode.Sniper:
                    if (side == TriggerSide.Left) execs.Get<GrizzlySniperShotActionExecutor>()?.EndScope();
                    break;   // RT fires on press; its release means nothing

                default:
                    execs.Get<GrizzlyTriggerBombExecutor>()?.OnRelease(side);
                    break;
            }
        }

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
