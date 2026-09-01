using System.Collections.Generic;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Tunables for the Arkway, authored on the toy definition.</summary>
    public sealed class ArkwayConfig
    {
        /// <summary>The toy's player-facing name — shown on the build veil ("GROWING ARKWAY…").</summary>
        public string DisplayName = "ARKWAY";

        public Prism PrismPrefab;
        public float ArkSpeed = 18f;
        public float ArkHullLength = 110f;

        public IReadOnlyList<CellConfigDataSO> Cells;
        public float CellSpacing = 3200f;
        public float MaxTurnDegrees = 25f;
        public int PrismStride = 4;
        public float PopulationScale = 0.5f;
        public int Seed;

        public float LeashRadiusFactor = 1f;
        public float LeashRadiusFallback = 1300f;
        public float LeashGraceSeconds = 5f;

        public bool RevertCellOnStart = true;
        public float ReturnStationRadius = 16f;
        public Color ReturnStationColor = new(1f, 0.78f, 0.25f, 1f);
    }

    /// <summary>
    /// The Arkway as a MODE: the voyage that starts when the player flies the Arkway toy and
    /// ends when they disembark (the return dinghy trailing the Ark), leave freestyle, fly the
    /// toy again — or when the food web eats the Ark's last hull prism, which RESETS the toy.
    ///
    /// The voyage is three composed pieces:
    ///
    ///   • A CORRIDOR OF CELLS (<see cref="CellConveyor"/>): three real satellite cells at once
    ///     — previous / current / next — recycled forever as the Ark advances.
    ///   • The ARK (<see cref="Ark"/>): a prism-bodied mothership in the player's domain whose
    ///     unhurried course IS the pace of the voyage. Its hull is grazeable conserved mass, so
    ///     whichever domain's fauna each cell spawns decides whether the Ark is being defended
    ///     or devoured — and the fauna colour is the cell's controlling colour, which is the
    ///     cell's VOLUME. Protecting the Ark is taking the cell. No aggro, no script.
    ///   • A LEASH: stay within a cell radius of the Ark. Beyond it a telegraphed countdown
    ///     runs, and then the Ark recalls you to its side — the voyage is an escort, not a solo
    ///     wander (and the corridor's whole apparatus stands where the Ark is).
    ///
    /// Like the Wanderway run, starting a voyage hands the host cell its bare canvas through
    /// <see cref="Cell.RequestCellSwap"/> — the corridor is the world you look at, and three
    /// standing cells beside a heavy home world is a collider budget nobody authored. The
    /// first two cells and the Ark's hull build behind the same <see cref="EnvironmentLoadVeil"/>
    /// hold; later cells stream in unveiled beside live play, which is what a satellite build
    /// is for.
    /// </summary>
    public sealed class ArkwayRun : MonoBehaviour
    {
        const float TickSeconds = 0.2f;

        /// <summary>How close to home counts as "already back" — inside this the run ends
        /// without repositioning the vessel.</summary>
        const float HomeArrivalDistance = 120f;

        /// <summary>How far behind the Ark the player is recalled to, and where the return
        /// dinghy trails, as a fraction of the hull length.</summary>
        const float RecallBehindFactor = 1.4f;

        /// <summary>Frame-rate-independent smoothing for the dinghy's follow.</summary>
        const float DinghyFollowRate = 4f;

        /// <summary>Seconds the voyage-start hint and the fallen-Ark banner stay up.</summary>
        const float BannerSeconds = 4f;

        ArkwayConfig _cfg;
        ToyContext _context;
        Container _container;
        CellConveyor _conveyor;
        System.Action _onEnded;

        Ark _ark;
        WanderwayReturnToy _dinghy;
        ArkwayVoyageHud _hud;

        Pose _home;
        bool _hasHome;
        bool _running;
        bool _beginning;
        bool _wasFreestyle;
        float _nextTickAt;
        float _leashBreachedAt = -1f;

        // Voyage generation: End() bumps it, and the async Begin re-checks it after every await -
        // so an End taken DURING the veiled build (a second toy pass, a scene edge) can never be
        // resurrected by the build completing afterwards. The same hazard class as the microscene
        // conveyor's _stopRequestedDuringPrime, expressed as a generation because Begin has
        // several suspension points, not one.
        int _generation;

        /// <summary>True while a voyage is in progress (including the veiled first build).</summary>
        public bool IsRunning => _running || _beginning;

        public void Configure(ArkwayConfig cfg, ToyContext context, Container container,
            CellConveyor conveyor, System.Action onEnded = null)
        {
            _cfg = cfg;
            _context = context;
            _container = container;
            _conveyor = conveyor;
            _onEnded = onEnded;
        }

        // ── Begin ────────────────────────────────────────────────────────────

        /// <summary>
        /// Start a voyage from the local vessel's current pose. Idempotent: a second call while
        /// one is live is a no-op, so re-entering the toy cannot double-start.
        /// </summary>
        public void Begin(IVesselStatus localVessel)
        {
            if (IsRunning) return;
            if (localVessel?.Transform == null) return;

            _beginning = true;
            _home = new Pose(localVessel.Transform.position, localVessel.Transform.rotation);
            _hasHome = true;

            BeginVoyageAsync(localVessel, this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid BeginVoyageAsync(IVesselStatus vessel, CancellationToken ct)
        {
            int gen = ++_generation;
            try
            {
                var hostCell = RevertCellToBareCanvas(vessel);

                // The revert gathers every POOLED prism the host cell tracks — an Ark laid while
                // it is still retiring would be swept up with the old world. Wait it out.
                float deadline = Time.unscaledTime + 30f;
                while (hostCell && hostCell.IsSwappingConfig && Time.unscaledTime < deadline)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (gen != _generation) return; // ended while the revert settled

                // The PREVIOUS voyage's corridor may still be retiring (its cells drain only as
                // they leave view). Give it a moment to clear on its own; whatever remains is
                // force-struck below, AFTER the veil is up - with the screen covered the strike
                // is unseen by construction, which is the one licence a gate-less strike has.
                float idleDeadline = Time.unscaledTime + 8f;
                while ((_conveyor.IsDraining || _conveyor.HasCells)
                       && Time.unscaledTime < idleDeadline && gen == _generation)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (gen != _generation) return;

                Vector3 origin = vessel.Transform ? vessel.Transform.position : transform.position;
                Vector3 course = vessel.Transform ? vessel.Transform.forward : Vector3.forward;

                // Announce the build BEFORE raising the veil so the ready-poll can never see a
                // pre-lay all-clear (the microscene conveyor's own bracket).
                PrismTrailBuilder.BeginArenaBuild();
                EnvironmentLoadVeil.Hold(_cfg.DisplayName);
                try
                {
                    if (_conveyor.IsDraining || _conveyor.HasCells)
                        await _conveyor.StrikeAllAsync(ct); // behind the veil - unseen
                    // A routine off-screen retire can still be finishing its frame-sliced root
                    // drain; Begin refuses while ANY drain is live, so let it land. Bounded:
                    // drains destroy a fixed slice per frame and nothing re-queues here.
                    while (_conveyor.IsDraining && gen == _generation)
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    if (gen != _generation) return;

                    if (!_conveyor.Begin(_cfg, _container, origin, course))
                    {
                        _beginning = false;
                        _onEnded?.Invoke();
                        return;
                    }

                    // The Ark sets sail beside the player, bound for the first cell. It wears
                    // the player's domain AT DEPARTURE — an escort flies its flag from the dock.
                    var arkPos = origin + course * 150f;
                    _ark = Ark.Create(transform.parent, arkPos,
                        Quaternion.LookRotation(course, Vector3.up));
                    await _ark.LayHullAsync(_cfg.PrismPrefab, vessel.Domain,
                        _cfg.ArkHullLength, _cfg.ArkSpeed, ct);

                    // Ended mid-build (a second toy pass during the veiled lay): End() bumped the
                    // generation and — because _ark is assigned before this await — has already
                    // retired the Ark and struck the corridor. Just don't resurrect the run.
                    if (gen != _generation) return;

                    _ark.HullDestroyed += OnArkHullDestroyed;
                    _ark.SetDestination(_conveyor.CurrentTargetCentre);

                    // A satellite's environment build is DEFERRED ~0.75s (Cell.BuildEnvironmentNow
                    // runs behind DeferredEnvironmentBuild) and the Ark's small hull lays fast —
                    // hold the arena-build bracket across that window, or the ready-poll can see
                    // a pre-lay all-clear and drop the veil before the cells' lays have queued.
                    float bracketUntil = Time.unscaledTime + 2f;
                    while (Time.unscaledTime < bracketUntil && gen == _generation)
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                finally
                {
                    PrismTrailBuilder.EndArenaBuild();
                }
                if (gen != _generation) return;

                PlantDinghy();
                EnsureHud();
                _hud.ShowBanner("STAY WITH THE ARK", BannerSeconds);

                _wasFreestyle = true;
                _running = true;
            }
            catch (System.OperationCanceledException)
            {
                // Scene teardown mid-build — the conveyor and toybox root take the remainder.
            }
            finally
            {
                _beginning = false;
            }
        }

        /// <summary>
        /// Hand the host cell its BARE CANVAS config — the Wanderway run's own opening move,
        /// with its exact fallbacks. Returns the host cell (or null) so the caller can wait out
        /// the swap.
        /// </summary>
        Cell RevertCellToBareCanvas(IVesselStatus localVessel)
        {
            var at = localVessel?.Transform ? localVessel.Transform.position : transform.position;
            // Unity's lifetime-aware operator, not `??` - a destroyed Cell is non-null by
            // reference and would slip straight through the null-coalescing form.
            var containing = Cell.FindCellContaining(at);
            var cell = containing ? containing : Cell.FindNearestActiveCell(at);

            if (!_cfg.RevertCellOnStart) return cell;
            if (!cell)
            {
                CSDebug.LogWarning("[Arkway] No active cell to revert - starting the voyage in place.");
                return null;
            }

            var canvas = cell.BareCanvasConfig;
            if (!canvas)
            {
                CSDebug.LogWarning($"[Arkway] Cell '{cell.name}' has no environment-free config - " +
                                   "leaving the world as it is.");
                return cell;
            }

            cell.RequestCellSwap(canvas, clearLooseTrailMass: true);
            return cell;
        }

        // ── The voyage tick ──────────────────────────────────────────────────

        void Update()
        {
            if (!_running || _cfg == null) return;

            FollowArk(); // every frame — the dinghy glides, it never teleports

            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            // The overview button / gamepad Start exit freestyle; that edge ends the voyage and
            // takes the player home, exactly as the dinghy does.
            bool freestyle = _context?.IsFreestyleActive == null || _context.IsFreestyleActive();
            if (_wasFreestyle && !freestyle)
            {
                End(returnToCell: true);
                return;
            }
            _wasFreestyle = freestyle;
            if (!freestyle) return;

            if (!_ark)
            {
                // The Ark object died without the hull-lost event (teardown edge) — end quietly.
                End(returnToCell: true);
                return;
            }

            TickCorridor();
            // TickCorridor's cannot-stand-a-next-cell branch Ends the run (nulling _ark) —
            // never fall through into the leash tick on that frame.
            if (!_running) return;
            TickLeash();
        }

        void TickCorridor()
        {
            if (!_ark.HasArrived(CellConveyor.ArriveDistance)) return;

            if (_conveyor.AdvancePastTarget())
            {
                _ark.SetDestination(_conveyor.CurrentTargetCentre);
            }
            else
            {
                CSDebug.LogWarning("[Arkway] The corridor could not stand a next cell - the " +
                                   "voyage ends at this one.");
                End(returnToCell: true);
            }
        }

        /// <summary>
        /// "A cell radius proximity to the Ark": beyond the leash a telegraphed countdown runs;
        /// re-enter to clear it, or the Ark recalls you to its side (pose + kept speed — the
        /// same repose every return path uses). The voyage never ends over distance: an escort
        /// pulls you back, it does not abandon you.
        /// </summary>
        void TickLeash()
        {
            var vessel = LocalVessel();
            var t = vessel?.Transform;
            if (!t) return;

            float leash = _conveyor.CurrentCellRadius * Mathf.Max(1f, _cfg.LeashRadiusFactor);
            float distSqr = (t.position - _ark.Position).sqrMagnitude;

            bool breached = _leashBreachedAt >= 0f;
            if (!breached)
            {
                if (distSqr <= leash * leash) return; // inside — nothing to do
                _leashBreachedAt = Time.unscaledTime; // breach STARTS only past the full leash
                EnsureHud();
            }
            else if (distSqr <= leash * leash * 0.9f) // ~0.95 × leash — genuine re-entry
            {
                _leashBreachedAt = -1f;
                _hud?.HideCountdown();
                return;
            }
            // A breach CLEARS only at the hysteresis re-entry above. In between — including the
            // band just inside the leash — the countdown keeps ticking AND keeps displaying:
            // a band that returned early here froze the number on screen while the grace ran
            // out underneath it, and the eventual recall arrived untelegraphed.

            float remaining = _cfg.LeashGraceSeconds - (Time.unscaledTime - _leashBreachedAt);
            if (remaining > 0f)
            {
                _hud.ShowCountdown($"RETURN TO THE ARK — {Mathf.CeilToInt(remaining)}");
                return;
            }

            _leashBreachedAt = -1f;
            _hud.HideCountdown();
            RecallToArk(vessel);
        }

        void RecallToArk(IVesselStatus vessel)
        {
            if (vessel?.Vessel == null || !_ark) return;

            // The Ark's FLANK, never its stern: the disembark dinghy rides the stern line
            // (DinghyTarget), and a recall that materialises the vessel inside the dinghy's
            // armed trigger would end the voyage as punishment for straying — the opposite of
            // what a recall means. Abeam and slightly back, facing along the course, well clear
            // of both the hull plates (~0.22 × length) and the dinghy (~1.46 × length away).
            var arkT = _ark.transform;
            Vector3 flank = _ark.Position
                            + arkT.right * (_cfg.ArkHullLength * 0.9f)
                            - _ark.Forward * (_cfg.ArkHullLength * 0.25f);
            Repose(vessel, new Pose(flank, Quaternion.LookRotation(_ark.Forward, Vector3.up)));
        }

        /// <summary>
        /// Teleport with the trail spawner PENNED for the jump (the mode preview's idiom around
        /// every SetPose): a spawner live across a teleport lays mass bridging the whole
        /// distance. Speed carries through, so the player never arrives dead in the water.
        /// </summary>
        void Repose(IVesselStatus vessel, Pose pose)
        {
            if (vessel?.Vessel == null) return;
            float speed = Mathf.Max(0f, vessel.Speed);

            var pen = vessel.VesselPrismController;
            if (pen) pen.SetSpawnerPaused(true);
            vessel.Vessel.SetPose(pose);
            vessel.Vessel.SetInitialSpeed(speed);
            if (pen) UnpenNextFrame(pen).Forget();
        }

        async UniTaskVoid UnpenNextFrame(VesselPrismController pen)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            if (pen) pen.SetSpawnerPaused(false);
        }

        void OnArkHullDestroyed()
        {
            EnsureHud();
            _hud.ShowBanner("THE ARK HAS FALLEN", BannerSeconds);
            End(returnToCell: true);
        }

        // ── The way home: a dinghy trailing the Ark ──────────────────────────

        /// <summary>
        /// The return station trails the Ark itself — you are sworn to the Ark's side, so the
        /// way home is always beside you, and always BEHIND the Ark so defending its flanks
        /// never threads it by accident.
        /// </summary>
        void PlantDinghy()
        {
            if (_dinghy || !_ark) return;

            float body = Mathf.Max(8f, _cfg.ReturnStationRadius);
            Vector3 at = DinghyTarget();
            var placement = new ToyPlacement(at, at + _ark.Forward, body, body * 2.2f);
            var go = ToyFactory.CreateRoot("Arkway_Dinghy", transform, placement,
                _cfg.ReturnStationColor, "DISEMBARK\n<size=60%>fly through to head home</size>");

            _dinghy = go.AddComponent<WanderwayReturnToy>();
            _dinghy.Configure(() => End(returnToCell: true));
            // Radius only — ending a voyage is NEUTRAL (it hands you back your cell, not a
            // domain), so the ring keeps the switch vocabulary's neutral paint.
            _dinghy.ConfigureSwitchRing(placement.TriggerRadius);
            _dinghy.Initialize(null, _context, placement);
        }

        Vector3 DinghyTarget() =>
            _ark ? _ark.Position - _ark.Forward * (_cfg.ArkHullLength * RecallBehindFactor)
                 : transform.position;

        void FollowArk()
        {
            if (!_dinghy || !_ark) return;
            var t = _dinghy.transform;
            float k = 1f - Mathf.Exp(-DinghyFollowRate * Time.deltaTime);
            t.position = Vector3.Lerp(t.position, DinghyTarget(), k);

            // Mouth toward the vessel, so the ring reads as a hoop from wherever you defend.
            var vesselT = LocalVessel()?.Transform;
            Vector3 look = vesselT ? vesselT.position - t.position : _ark.Forward;
            if (look.sqrMagnitude > 1f)
            {
                Vector3 dir = look.normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(dir, up), k);
            }
        }

        void DestroyDinghy()
        {
            if (!_dinghy) return;
            Destroy(_dinghy.gameObject);
            _dinghy = null;
        }

        // ── End ──────────────────────────────────────────────────────────────

        /// <summary>
        /// End the voyage: bring the player home, retire the Ark (withered back to its pool),
        /// strike the corridor, and re-arm the toy. Idempotent. The player is reposed FIRST so
        /// the corridor's strike happens far away and out of sight — the unseen-removal clause
        /// every satellite teardown rides.
        /// </summary>
        public void End(bool returnToCell)
        {
            if (!_running && !_beginning) return;
            _generation++; // a Begin still in flight must not resurrect this voyage
            _running = false;
            _beginning = false;
            _leashBreachedAt = -1f;
            _hud?.HideCountdown();

            var vessel = LocalVessel();
            DestroyDinghy();

            if (returnToCell) ReturnHome(vessel);

            if (_ark)
            {
                _ark.HullDestroyed -= OnArkHullDestroyed;
                _ark.RetireAsync(this.GetCancellationTokenOnDestroy()).Forget();
                _ark = null;
            }

            // QUEUED, off-screen-gated retirement - not a force strike: the corridor can still
            // be inside the camera's view (its cells are a few thousand units out, not the
            // preview's 120k), and whole worlds popping out in view is the removal the
            // microscene conveyor's gate exists to forbid. Each cell drains as it leaves view;
            // the next voyage's Begin force-clears any remainder behind its raised veil.
            if (_conveyor)
                _conveyor.RetireAllWhenUnseen();

            _onEnded?.Invoke();
        }

        void ReturnHome(IVesselStatus vessel)
        {
            if (!_hasHome || vessel?.Vessel == null || !vessel.Transform) return;
            if ((vessel.Transform.position - _home.position).sqrMagnitude
                < HomeArrivalDistance * HomeArrivalDistance) return;

            Repose(vessel, _home);
        }

        IVesselStatus LocalVessel()
        {
            var vessel = _context?.GameData?.LocalPlayer?.Vessel;
            if (vessel == null) return null;
            if (vessel is Object uo && !uo) return null; // destroyed mid-swap
            return vessel.VesselStatus;
        }

        void EnsureHud()
        {
            if (_hud) return;
            var go = new GameObject("ArkwayVoyageHud");
            go.transform.SetParent(transform, false);
            _hud = go.AddComponent<ArkwayVoyageHud>();
        }

        void OnDestroy()
        {
            // Scene teardown: never call back into the toy (it may already be destroyed).
            _onEnded = null;
            End(returnToCell: false);
        }
    }
}
