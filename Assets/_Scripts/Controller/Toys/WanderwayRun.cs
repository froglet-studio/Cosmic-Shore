using System.Collections.Generic;
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
    ///   • A BARE CANVAS. Starting a run reverts the host cell to its bare-canvas config — the
    ///     one that grows nothing (<see cref="Cell.BareCanvasConfig"/>) — through the one sanctioned entry
    ///     point, <see cref="Cell.RequestCellSwap"/>. That is the same explicit, player-initiated
    ///     world change the Cell Selector performs (suction out → veil → bloom in); the wander is
    ///     what you look at, not an authored world you are flying through.
    ///   • A ROLLING TETHER. The trail follows you as a ribbon of exactly
    ///     <see cref="ConveyorConfig.TetherPrisms"/> prisms: as you lay at the head, the oldest
    ///     prism at the tail withers and RECYCLES back into the pool it came from, so the next
    ///     prism you lay is very often the one that just left. Turn around and your trail is
    ///     there; fly on and a little flying lays you a fresh path home. Bounded memory, endless
    ///     runner.
    ///   • A WAY HOME AT ITS TAIL. The return station rides the oldest end of that ribbon, so the
    ///     way out is always exactly one tether-length behind you. Fly back into it and the run
    ///     ends: the belt stops and you are returned to the cell.
    ///
    /// <b>AUTHORIZED EXCEPTION — this is the one place trail mass is recycled.</b> Passive removal
    /// of trail mass is forbidden platform-wide (CLAUDE.md ▸ <i>Mass is conserved</i> / <i>Don't
    /// cheat emergence</i>), and the reverted <c>maxTrailBlocks</c> ring buffer is the named
    /// counter-example. The rolling tether IS that mechanism, granted a deliberate carve-out for
    /// the Wanderway so it can be a truly infinite runner at fixed memory: recycling everything
    /// maintains the local illusion without ever growing the world. It is scoped to a live
    /// <see cref="WanderwayRun"/> and dies with it — the trail behaves normally everywhere else,
    /// including elsewhere in freestyle. Do not generalise it, and do not "fix" it by reverting.
    /// The exception is recorded in <c>Docs/ECOSYSTEM.md</c> §0.
    ///
    /// Recycling still honours CONTINUITY OF EXISTENCE, which is a separate law and is NOT waived:
    /// a retiring prism does not pop. It withers on the GPU clock — one grow-clock re-stamp toward
    /// <see cref="RetiredScale"/>, exactly the belt's own collapse (Docs/PRISM_ANIMATION.md §5 C8)
    /// — and only returns to the pool once it has shrunk away.
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

        /// <summary>Scale a retiring tail prism withers to before it returns to the pool. Matches
        /// the conveyor's collapse target in spirit; near-zero because this prism is leaving.</summary>
        static readonly Vector3 RetiredScale = new(0.02f, 0.02f, 0.02f);

        /// <summary>Seconds a tail prism withers for before it is handed back to the pool.</summary>
        const float WitherSeconds = 0.8f;

        /// <summary>Frame-rate-independent smoothing rate for the return station's tail-follow.</summary>
        const float StationFollowRate = 4f;

        readonly List<(Prism prism, float dueAt)> _withering = new();
        WanderwayReturnToy _returnToy;
        Vector3 _returnTarget;

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

            RevertCellToBareCanvas(localVessel);
            ArmTether();
        }

        /// <summary>
        /// Hand the cell its BARE CANVAS config - the one that grows nothing. Deliberately
        /// requested even when the cell is ALREADY on that config: re-selecting the same config is
        /// the documented freestyle RESET (clear the world, grow it back fresh), which is exactly
        /// what starting a wander should mean. The swap raises its own load veil and the
        /// conveyor's stock build joins the same hold, so the player pays one cover, not two.
        ///
        /// <para><see cref="Cell.BareCanvasConfig"/>, not <c>EnvironmentFreeConfig</c>: a wander
        /// wants an EMPTY world, and "authors no environment" stopped implying that once the
        /// Lattice cell existed - it builds instantly and then grows a 21,600-prism forest out of
        /// eight seeds, which is the world a wander is trying to leave (Docs/ECOSYSTEM.md
        /// §36.10).</para>
        /// </summary>
        void RevertCellToBareCanvas(IVesselStatus localVessel)
        {
            if (!_cfg.RevertCellOnStart) return;

            var at = localVessel?.Transform ? localVessel.Transform.position : transform.position;
            var cell = Cell.FindCellContaining(at) ?? Cell.FindNearestActiveCell(at);
            if (!cell)
            {
                CSDebug.LogWarning("[Wanderway] No active cell to revert - starting the wander in place.");
                return;
            }

            var canvas = cell.BareCanvasConfig;
            if (!canvas)
            {
                CSDebug.LogWarning($"[Wanderway] Cell '{cell.name}' has no environment-free config " +
                                   "(every CellConfig authors an EnvironmentPrefab) - leaving the world as it is. " +
                                   "Add a bare config (no EnvironmentPrefab, no flora or fauna in its " +
                                   "SpawnProfile - e.g. Barren) to the cell's list to get the bare canvas.");
                return;
            }

            cell.RequestCellSwap(canvas, clearLooseTrailMass: true);
        }

        // ── Tether ───────────────────────────────────────────────────────────

        void ArmTether()
        {
            FlushWithering();
            DestroyReturnToy();
        }

        void Update()
        {
            if (!_running || _cfg == null) return;

            FollowTail(); // every frame - the tick below is too coarse for a smooth glide

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
            RetireDueWithering();

            var vessel = LocalVessel();
            var controller = vessel?.VesselPrismController;
            if (!controller) return;

            RollTether(controller);
            UpdateReturnStation(controller);
        }

        /// <summary>
        /// Hold the trail at exactly <see cref="ConveyorConfig.TetherPrisms"/> live prisms: for
        /// every prism laid past the budget, detach the OLDEST and start it withering back to the
        /// pool. That closed loop is what makes the wander infinite at fixed memory — the prism
        /// leaving the tail is the stock the next lay pulls from.
        ///
        /// Only POOLED prisms are recycled (<c>OnReturnToPool != null</c>), the same test
        /// <see cref="Cell"/> uses to tell a vessel's loose trail mass from instantiated mass. An
        /// unpooled prism has nowhere to go, so it is left in place rather than shrunk into an
        /// invisible collider.
        ///
        /// BOTH ribbons are rolled. Vessels with a double-trail spawn pattern put every other
        /// prism in <see cref="VesselPrismController.SecondaryTrail"/>, so rolling only the
        /// primary would leak half the tether's mass and the ribbon would keep growing.
        ///
        /// The budget is applied PER RIBBON, not split across them: the ribbons are laid in
        /// parallel along the same path, so it is the per-ribbon count that sets how far back the
        /// trail reaches — and that LENGTH is what the player reads, and what makes the return
        /// station sit a predictable distance behind them on every vessel. A double-trail vessel
        /// therefore holds 2× the prisms for the same visible tether; the stock is pooled and
        /// fixed either way.
        /// </summary>
        void RollTether(VesselPrismController controller)
        {
            int budget = Mathf.Max(1, _cfg.TetherPrisms);
            RollOne(controller.Trail, budget);
            RollOne(controller.SecondaryTrail, budget);
        }

        void RollOne(Trail trail, int budget)
        {
            if (trail == null) return;

            // Bound the work per tick: a burst (a lag spike, a speed boost) drains over a few
            // ticks rather than stalling one.
            int guard = 64;
            while (trail.TrailList.Count > budget && guard-- > 0)
            {
                var oldest = trail.TrailList[0];

                // Already gone (eaten, exploded) - just drop the slot, nothing to retire.
                if (!oldest) { trail.RemoveOldest(); continue; }

                // Not ours to recycle: stop here rather than skipping past it, so the ribbon
                // stays contiguous and we retry next tick.
                if (oldest.OnReturnToPool == null) break;

                trail.RemoveOldest();
                BeginWither(oldest);
            }
        }

        /// <summary>
        /// Start a detached prism's exit. Continuity of existence is NOT waived by the recycling
        /// carve-out: the prism withers on the GPU clock (one grow-clock re-stamp — the setter is
        /// the stamp) and is handed back to the pool only once it has shrunk away.
        /// </summary>
        void BeginWither(Prism prism)
        {
            prism.TargetScale = RetiredScale;
            _withering.Add((prism, Time.time + WitherSeconds));
        }

        void RetireDueWithering()
        {
            for (int i = _withering.Count - 1; i >= 0; i--)
            {
                var (prism, dueAt) = _withering[i];
                if (prism && Time.time < dueAt) continue;
                _withering.RemoveAt(i);
                if (prism) prism.ReturnToPool();
            }
        }

        /// <summary>Hand everything still mid-wither straight back to the pool (run ending / teardown).</summary>
        void FlushWithering()
        {
            for (int i = 0; i < _withering.Count; i++)
                if (_withering[i].prism) _withering[i].prism.ReturnToPool();
            _withering.Clear();
        }

        // ── The way home ─────────────────────────────────────────────────────

        /// <summary>
        /// The return station rides the TAIL of the rolling tether, so the way out sits a fixed
        /// tether-length behind you for the whole wander — turn around at any point and it is
        /// there at the end of your trail. Created as soon as there is trail to ride.
        /// </summary>
        void UpdateReturnStation(VesselPrismController controller)
        {
            var list = controller.Trail?.TrailList;
            if (list == null) return;

            // The oldest LIVE prism - the tail.
            Prism tail = null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] && list[i].isActiveAndEnabled) { tail = list[i]; break; }
            if (!tail) return;

            if (!_returnToy) PlantReturnToy(tail.transform.position);
            else _returnTarget = tail.transform.position;
        }

        void PlantReturnToy(Vector3 at)
        {
            float body = Mathf.Max(8f, _cfg.ReturnStationRadius);
            // Every other toy's switch ring faces the cell centre, because that is the axis you
            // approach it on. This one has no such axis - it rides the tether's tail and you come
            // back at it from wherever you wandered - so it faces the VESSEL, and keeps facing it
            // as it follows the tail (see FollowTail). A portal you only ever see edge-on teaches
            // nothing.
            var placement = new ToyPlacement(at, ReturnLookTarget(at), body, body * 2.2f);
            var go = ToyFactory.CreateRoot("Wanderway_Return", transform, placement,
                _cfg.ReturnStationColor, "RETURN\n<size=60%>fly through to end the wander</size>");

            _returnToy = go.AddComponent<WanderwayReturnToy>();
            _returnToy.Configure(() => End(returnToCell: true));
            // Only the radius is ours - the ring's colour is the switch vocabulary's, and ending a
            // wander is NEUTRAL (it hands you back your cell, not a domain). The station's own
            // colour still lives on its body and label.
            _returnToy.ConfigureSwitchRing(placement.TriggerRadius);
            _returnToy.Initialize(null, _context, placement);
            _returnTarget = at;
        }

        /// <summary>Where the station's ring should face: the local vessel, else straight ahead.</summary>
        Vector3 ReturnLookTarget(Vector3 at)
        {
            var t = LocalVessel()?.Transform;
            return t && (t.position - at).sqrMagnitude > 1f ? t.position : at + Vector3.forward;
        }

        /// <summary>
        /// Glide the station onto the tail every frame rather than snapping it on the tick — the
        /// tail advances a prism at a time and a station that teleported after it would read as a
        /// pop. One transform write on one object; the clock-material law governs prisms, not toys.
        /// </summary>
        void FollowTail()
        {
            if (!_returnToy) return;
            var t = _returnToy.transform;
            float k = 1f - Mathf.Exp(-StationFollowRate * Time.deltaTime);
            t.position = Vector3.Lerp(t.position, _returnTarget, k);

            // ...and keeps its mouth turned toward you, on the same easing, so the ring reads as a
            // hoop to aim at from anywhere rather than a disc that happens to be edge-on.
            Vector3 toVessel = ReturnLookTarget(t.position) - t.position;
            if (toVessel.sqrMagnitude > 1f)
            {
                Vector3 dir = toVessel.normalized;
                // Straight above/below the station, world-up is colinear with the look direction
                // and LookRotation's implicit up degenerates (the guard BillboardLabel carries for
                // the same reason). Roll itself is invisible here - a torus is symmetric about its
                // own axis - so swapping the hint costs nothing.
                Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir, up), k);
            }
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
            FlushWithering();
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
