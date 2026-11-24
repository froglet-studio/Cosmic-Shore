using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An AI seat on an Ark escort or a voyage (<see cref="IArkEscortHost"/>): the vessel's own <see cref="AIPilot"/> flies, this
    /// dispatches to the hull's <b>role</b> (<see cref="IArkEscortRole"/>, chosen by vessel type in
    /// <see cref="ArkEscortRoles.Create"/>) for where to fly, what to press and how far to roam. The roles share one
    /// <see cref="ArkEscortContext"/> the run keeps: the threats ranked, seat claims, the lane-clear test.
    /// A hull without a role of its own chases hearts as before (<see cref="HeartChaserRole"/>).
    /// </summary>
    /// <summary>What a run asks of an escort seat over its role (<see cref="ArkEscortPilot.Errand"/>).</summary>
    public enum ArkEscortErrand
    {
        None = 0,
        /// <summary>Fly to the Ark (a Crystal Run banks what a seat carries there).</summary>
        Bank = 1,
        /// <summary>Let the hull's own pilot hunt crystals.</summary>
        Seek = 2,
    }

    public sealed class ArkEscortPilot : MonoBehaviour
    {
        /// <summary>How far from the Ark a creature counts as a threat to hunt (<c>ai.escort.guardRadius</c>).</summary>
        public static float GuardRadius => EscortGeneRegistry.GuardRadius;

        IArkEscortHost _run;
        AIPilot _pilot;
        IVesselStatus _status;
        int _seat;
        IArkEscortRole _role;
        bool _steering;
        ArkEscortErrand _errand;

        /// <summary>A run's errand for this seat that outranks its role for a while (a Crystal Run's bank run or crystal
        /// hunt, <c>VoyageRun.Wingmates</c>), asked every frame; null, or <see cref="ArkEscortErrand.None"/>, leaves the
        /// role in charge. The role still ticks (presses its abilities) either way.</summary>
        public Func<ArkEscortPilot, ArkEscortErrand> Errand;
        CellRuntimeDataSO _homeCell;
        bool _retargeted;

        public int Seat => _seat;
        public AIPilot Ai => _pilot;
        public IVesselStatus Status => _status;
        public IArkEscortRole Role => _role;
        public IArkEscortHost Run => _run;

        public void Arm(IArkEscortHost run, int seat)
        {
            _run = run;
            _seat = seat;
            if (!_pilot) TryGetComponent(out _pilot);
            _status ??= GetComponent<IVesselStatus>();

            _role?.Disarm();
            var type = _status != null ? _status.VesselType : VesselClassType.Any;
            _role = ArkEscortRoles.Create(type);
            _role.Arm(this);
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ArkEscort] Seat {seat} ({type}) flies as {_role.Name}.");

            _steering = false;
            SetSteering(true);
            var cell = run != null ? run.CurrentCell : null;
            if (cell) OnLegChanged(cell);
        }

        public void Disarm()
        {
            SetSteering(false);
            ReleaseHook(sweep: true);
            _role?.Disarm();
            _role = null;
            if (_retargeted && _pilot) _pilot.RetargetCell(_homeCell);   // null is fine: the pilot then seeks nothing rather than a dead world
            _retargeted = false;
            _run = null;
        }

        /// <summary>The Ark has entered a world: the pilot's crystal list follows it (<see cref="AIPilot.RetargetCell"/>
        /// is otherwise never called on a leg change, so every crystal seeker flew blind in the escort).</summary>
        public void OnLegChanged(Cell cell)
        {
            if (_pilot && cell && cell.RuntimeData && _pilot.CellData != cell.RuntimeData)
            {
                if (!_retargeted) { _homeCell = _pilot.CellData; _retargeted = true; }
                _pilot.RetargetCell(cell.RuntimeData);
            }
            _role?.OnLegChanged(cell);
        }

        /// <summary>This seat's leash, given the squad's: a role may let it out (the Squirrel's nucleus run).</summary>
        public float Leash(float leash)
        {
            var ctx = _run != null ? _run.Context : null;
            return _role != null && ctx != null ? _role.Leash(ctx, leash) : leash;
        }

        void SetSteering(bool on)
        {
            if (on == _steering) return;
            _steering = on;
            if (on) { if (_pilot) _pilot.SetExternalTargetProvider(Target); }
            else ReleaseHook(sweep: false);
        }

        /// <summary>Take this seat's steering hook back from every pilot that holds it. Usually that is this hull's own
        /// pilot, but a pilot swap moves the hook to the AI of the hull the person left (<see cref="AIPilot.TakeModeHooksFrom"/>),
        /// and that pilot kept asking this seat where to fly after this hull was gone: <see cref="Target"/> on a destroyed
        /// component threw a MissingReferenceException every frame (1,294 in one voyage, 2026-11-08). A pilot holding a
        /// mode's hook or another seat's is left alone. <paramref name="sweep"/> looks beyond this hull's pilot (a scene search,
        /// so only when a swap took the hook or the seat is disarmed, never on a role's routine steering toggle).</summary>
        void ReleaseHook(bool sweep)
        {
            Func<Vector3> mine = Target;
            if (_pilot && _pilot.ExternalTargetProvider == mine) _pilot.ClearExternalTargetProvider();
            if (!sweep) return;
            foreach (var other in FindObjectsByType<AIPilot>(FindObjectsSortMode.None))
                if (other && other != _pilot && other.ExternalTargetProvider == mine) other.ClearExternalTargetProvider();
        }

        /// <summary>True when this seat thinks it steers but its pilot holds no hook at all: a swap took it
        /// (<see cref="AIPilot.TakeModeHooksFrom"/> leaves the source with none). A hook someone else set over ours (a crew's)
        /// is not a swap and is left to them, as before.</summary>
        bool HookTaken() => _steering && _pilot && _pilot.ExternalTargetProvider == null;

        Vector3 Target()
        {
            var ctx = _run != null ? _run.Context : null;
            if (ctx == null || !ctx.Ark || _role == null) return transform.position + transform.forward * 100f;
            if (_errand == ArkEscortErrand.Bank) return ctx.AvoidHull(transform.position, ctx.Ark.Position);
            return ctx.AvoidHull(transform.position, _role.Steer(ctx));
        }

        void Update()
        {
            if (_run == null || !_run.IsActive || _role == null) return;
            var ctx = _run.Context;
            if (ctx == null || !ctx.Ark) return;
            _role.Tick(ctx);
            // A swap moved the hook off this hull (it flies on another hull, steering by this one): take it back, and
            // give it to this hull's pilot again if the role still wants to steer.
            if (HookTaken()) { _steering = false; ReleaseHook(sweep: true); }
            _errand = Errand?.Invoke(this) ?? ArkEscortErrand.None;
            SetSteering(_errand switch
            {
                ArkEscortErrand.Bank => true,      // fly the crystals home to the Ark
                ArkEscortErrand.Seek => false,     // the hull's own pilot hunts crystals
                _ => _role.WantsSteering(ctx),
            });
        }

        void OnDestroy() => Disarm();
    }
}
