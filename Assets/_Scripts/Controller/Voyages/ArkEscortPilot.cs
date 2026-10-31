using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An AI seat on an Ark escort (<see cref="ArkEscortRun"/>): the vessel's own <see cref="AIPilot"/> flies, this
    /// dispatches to the hull's <b>role</b> (<see cref="IArkEscortRole"/>, chosen by vessel type in
    /// <see cref="ArkEscortRoles.Create"/>) for where to fly, what to press and how far to roam. The roles share one
    /// <see cref="ArkEscortContext"/> the run keeps: the threats ranked, seat claims, the lane-clear test.
    /// A hull without a role of its own chases hearts as before (<see cref="HeartChaserRole"/>).
    /// </summary>
    public sealed class ArkEscortPilot : MonoBehaviour
    {
        /// <summary>How far from the Ark a creature counts as a threat to hunt (<c>ai.escort.guardRadius</c>).</summary>
        public static float GuardRadius => EscortGeneRegistry.GuardRadius;

        ArkEscortRun _run;
        AIPilot _pilot;
        IVesselStatus _status;
        int _seat;
        IArkEscortRole _role;
        bool _steering;
        CellRuntimeDataSO _homeCell;
        bool _retargeted;

        public int Seat => _seat;
        public AIPilot Ai => _pilot;
        public IVesselStatus Status => _status;
        public IArkEscortRole Role => _role;
        public ArkEscortRun Run => _run;

        public void Arm(ArkEscortRun run, int seat)
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
            var cell = run ? run.CurrentCell : null;
            if (cell) OnLegChanged(cell);
        }

        public void Disarm()
        {
            SetSteering(false);
            _role?.Disarm();
            _role = null;
            if (_retargeted && _pilot && _homeCell) _pilot.RetargetCell(_homeCell);
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
            var ctx = _run ? _run.Context : null;
            return _role != null && ctx != null ? _role.Leash(ctx, leash) : leash;
        }

        void SetSteering(bool on)
        {
            if (on == _steering) return;
            _steering = on;
            if (!_pilot) return;
            if (on) _pilot.SetExternalTargetProvider(Target);
            else _pilot.ClearExternalTargetProvider();
        }

        Vector3 Target()
        {
            var ctx = _run ? _run.Context : null;
            if (ctx == null || !ctx.Ark || _role == null) return transform.position + transform.forward * 100f;
            return ctx.AvoidHull(transform.position, _role.Steer(ctx));
        }

        void Update()
        {
            if (!_run || !_run.IsActive || _role == null) return;
            var ctx = _run.Context;
            if (ctx == null || !ctx.Ark) return;
            _role.Tick(ctx);
            SetSteering(_role.WantsSteering(ctx));
        }

        void OnDestroy() => Disarm();
    }
}
