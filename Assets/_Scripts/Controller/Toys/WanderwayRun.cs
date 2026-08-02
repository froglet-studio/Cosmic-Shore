using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wanderway as a MODE rather than a switch: the run that starts when the player flies the
    /// Wanderway toy and ends when they take the return station (or leave freestyle).
    ///
    /// Three things make the wander its own place:
    ///
    ///   • A BARE CANVAS. Starting a run reverts the host cell to its environment-free config —
    ///     the Blob (<see cref="Cell.EnvironmentFreeConfig"/>) — through the one sanctioned entry
    ///     point, <see cref="Cell.RequestCellSwap"/>. That is the same explicit, player-initiated
    ///     world change the Cell Selector performs (suction out → veil → bloom in); the wander is
    ///     what you look at, not an authored world you are flying through.
    ///   • A FINITE TETHER. The trail you lay on the way out is a fixed budget, not an endless
    ///     ribbon: once <see cref="ConveyorConfig.TetherPrisms"/> have been laid the vessel's
    ///     spawner goes PEN-UP (<see cref="VesselPrismController.SetSpawnerPaused"/> — the same
    ///     mechanism the painting toy uses between strokes). Nothing is removed, aged out, or
    ///     capped: not creating mass is allowed, un-creating it is not (CLAUDE.md ▸ Mass is
    ///     conserved; the reverted `maxTrailBlocks` ring buffer is the named counter-example).
    ///   • A WAY HOME AT ITS END. The moment the tether completes, the return station blooms at
    ///     the LAST prism you laid — the end of your lifeline. Fly back into it and the run ends:
    ///     the belt stops, the pen drops, and you are returned to the cell.
    ///
    /// The overview button takes the same exit. It (and gamepad Start) route through
    /// <c>MenuCrystalClickHandler.ToggleTransition</c>, which drops freestyle — so the run watches
    /// <see cref="ToyContext.IsFreestyleActive"/> and ends itself on that edge, giving one exit
    /// path for the station, the button, and the pad.
    /// </summary>
    public sealed class WanderwayRun : MonoBehaviour
    {
        const float TickSeconds = 0.2f;

        /// <summary>How close to home counts as "already back" — inside this the run ends without
        /// repositioning the vessel (ending the run AT the toy must not jerk the player's pose).</summary>
        const float HomeArrivalDistance = 120f;

        ConveyorConfig _cfg;
        ToyContext _context;
        MicrosceneConveyor _conveyor;
        System.Action _onEnded;

        Pose _home;
        bool _hasHome;
        bool _running;
        bool _wasFreestyle;
        float _nextTickAt;

        int _trailBaseline = -1;
        bool _penUp;
        WanderwayReturnToy _returnToy;

        /// <summary>True while a wander is in progress.</summary>
        public bool IsRunning => _running;

        /// <param name="onEnded">Raised after the run ends by ANY route (return station, overview
        /// button, a second pass through the toy) so the Wanderway toy can re-sync its label
        /// instead of polling.</param>
        public void Configure(ConveyorConfig cfg, ToyContext context, MicrosceneConveyor conveyor,
            System.Action onEnded = null)
        {
            _cfg = cfg;
            _context = context;
            _conveyor = conveyor;
            _onEnded = onEnded;
        }

        // ── Begin ────────────────────────────────────────────────────────────

        /// <summary>
        /// Start a wander from the local vessel's current pose. Idempotent: a second call while a
        /// run is live is a no-op, so re-entering the toy cannot double-swap the cell.
        /// </summary>
        public void Begin(IVesselStatus localVessel)
        {
            if (_running) return;
            _running = true;
            _wasFreestyle = true;

            if (localVessel?.Transform)
            {
                _home = new Pose(localVessel.Transform.position, localVessel.Transform.rotation);
                _hasHome = true;
            }

            RevertCellToBlob(localVessel);
            ArmTether(localVessel);
        }

        /// <summary>
        /// Hand the cell back to its cheap environment-free config. Deliberately requested even
        /// when the cell is ALREADY on that config: re-selecting the same config is the documented
        /// freestyle RESET (clear the world, grow it back fresh), which is exactly what starting a
        /// wander should mean. The swap raises its own load veil and the conveyor's stock build
        /// joins the same hold, so the player pays one cover, not two.
        /// </summary>
        void RevertCellToBlob(IVesselStatus localVessel)
        {
            if (!_cfg.RevertCellOnStart) return;

            var at = localVessel?.Transform ? localVessel.Transform.position : transform.position;
            var cell = Cell.FindCellContaining(at) ?? Cell.FindNearestActiveCell(at);
            if (!cell)
            {
                CSDebug.LogWarning("[Wanderway] No active cell to revert - starting the wander in place.");
                return;
            }

            var blob = cell.EnvironmentFreeConfig;
            if (!blob)
            {
                CSDebug.LogWarning($"[Wanderway] Cell '{cell.name}' has no environment-free config " +
                                   "(every CellConfig authors an EnvironmentPrefab) - leaving the world as it is. " +
                                   "Add a Blob-style config to the cell's list to get the bare canvas.");
                return;
            }

            cell.RequestCellSwap(blob, clearLooseTrailMass: true);
        }

        // ── Tether ───────────────────────────────────────────────────────────

        void ArmTether(IVesselStatus localVessel)
        {
            _trailBaseline = -1;
            SetPenUp(localVessel, false);
            DestroyReturnToy();
        }

        void Update()
        {
            if (!_running || _cfg == null) return;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            // The overview button / gamepad Start exit freestyle; that edge ends the wander and
            // takes the player home, exactly as the return station does.
            bool freestyle = _context?.IsFreestyleActive == null || _context.IsFreestyleActive();
            if (_wasFreestyle && !freestyle)
            {
                End(returnToCell: true);
                return;
            }
            _wasFreestyle = freestyle;
            if (!freestyle) return;

            TickTether();
        }

        void TickTether()
        {
            var vessel = LocalVessel();
            var controller = vessel?.VesselPrismController;
            if (!controller) return;

            // Baseline on the first tick with a live controller rather than at Begin: the cell
            // swap that opens a run clears loose trail mass, and the vessel may be mid-swap.
            // Re-baseline if the count ever FALLS - the swap's trail clear and a vessel swap both
            // reset the list, and a stale high baseline would make the tether unreachable.
            int laid = controller.TrailLength;
            if (_trailBaseline < 0 || laid < _trailBaseline)
            {
                _trailBaseline = laid;
                return;
            }

            if (_penUp) return;
            if (laid - _trailBaseline < Mathf.Max(1, _cfg.TetherPrisms)) return;

            // The tether is complete: drop the pen and plant the way home at its far end.
            SetPenUp(vessel, true);
            PlantReturnToy(controller);
        }

        void SetPenUp(IVesselStatus vessel, bool penUp)
        {
            var controller = vessel?.VesselPrismController;
            if (controller) controller.SetSpawnerPaused(penUp);
            _penUp = penUp;
        }

        /// <summary>
        /// Bloom the return station at the last prism of the tether. Sized off the conveyor's own
        /// placement scale so it reads at wander distances, and parented to this run so it is torn
        /// down with the wander (and with the scene).
        /// </summary>
        void PlantReturnToy(VesselPrismController controller)
        {
            DestroyReturnToy();

            // The last LIVE prism, walking back past any the pool reclaimed or fauna ate, so the
            // station lands on real mass rather than at a recycled prism's parking spot.
            var vessel = LocalVessel();
            Vector3 at = vessel?.Transform ? vessel.Transform.position : transform.position;
            var list = controller.Trail?.TrailList;
            if (list != null)
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i] && list[i].isActiveAndEnabled) { at = list[i].transform.position; break; }

            float body = Mathf.Max(8f, _cfg.ReturnStationRadius);
            var placement = new ToyPlacement(at, at + Vector3.forward, body, body * 2.2f);
            var go = ToyFactory.CreateRoot("Wanderway_Return", transform, placement,
                _cfg.ReturnStationColor, "RETURN\n<size=60%>fly through to end the wander</size>");

            _returnToy = go.AddComponent<WanderwayReturnToy>();
            _returnToy.Configure(() => End(returnToCell: true));
            _returnToy.Initialize(null, _context, placement);
        }

        void DestroyReturnToy()
        {
            if (!_returnToy) return;
            Destroy(_returnToy.gameObject);
            _returnToy = null;
        }

        // ── End ──────────────────────────────────────────────────────────────

        /// <summary>
        /// End the wander: stop the belt, drop the pen, retire the return station and (optionally)
        /// put the player back at the cell. Idempotent.
        /// </summary>
        public void End(bool returnToCell)
        {
            if (!_running) return;
            _running = false;

            var vessel = LocalVessel();
            SetPenUp(vessel, false);
            DestroyReturnToy();

            if (_conveyor && _conveyor.IsRunning) _conveyor.StopBelt();

            if (returnToCell) ReturnHome(vessel);

            _onEnded?.Invoke();
        }

        /// <summary>
        /// Put the vessel back where the wander started. Uses the same repose the menu vessel-swap
        /// uses (<c>IVessel.SetPose</c> + <c>SetInitialSpeed</c>), so speed carries through instead
        /// of the player arriving dead in the water. Skipped when they are already home, so ending
        /// the run AT the toy never jerks their pose.
        /// </summary>
        void ReturnHome(IVesselStatus vessel)
        {
            if (!_hasHome || vessel?.Vessel == null || !vessel.Transform) return;
            if ((vessel.Transform.position - _home.position).sqrMagnitude
                < HomeArrivalDistance * HomeArrivalDistance) return;

            float speed = Mathf.Max(0f, vessel.Speed);
            vessel.Vessel.SetPose(_home);
            vessel.Vessel.SetInitialSpeed(speed);
        }

        IVesselStatus LocalVessel()
        {
            var vessel = _context?.GameData?.LocalPlayer?.Vessel;
            if (vessel == null) return null;
            if (vessel is Object uo && !uo) return null; // destroyed mid-swap
            return vessel.VesselStatus;
        }

        void OnDestroy()
        {
            // Scene teardown: clear the pen and drop the station, but never call back into the toy
            // (it may already be destroyed, and a Rebloom during teardown is meaningless).
            _onEnded = null;
            End(returnToCell: false);
        }
    }
}
