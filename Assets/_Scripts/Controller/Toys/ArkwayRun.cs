using System.Collections.Generic;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
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

        /// <summary>Speed under a cell's core — the pace of the slow pass through a world.</summary>
        public float ArkSpeed = 18f;

        /// <summary>Multiple of <see cref="ArkSpeed"/> the Ark makes in open water between
        /// cells. The corridor's spacing is dead time; a ship crosses it under way.</summary>
        public float ArkCruiseSpeedFactor = 4f;

        public float ArkHullLength = 110f;

        /// <summary>Units of travel between wake prisms. 0 = no wake.</summary>
        public float ArkWakeSpacing = 45f;

        /// <summary>Wake prism scale. Deliberately far larger than a vessel's trail prism
        /// (~2×2×4): a ship's wake is not another pilot's line.</summary>
        public Vector3 ArkWakeScale = new(6f, 6f, 12f);

        /// <summary>Backstop on standing wake prisms, not a lifespan - the corridor retiring
        /// behind the Ark is what ordinarily removes them.</summary>
        public int ArkWakeBudget = 400;

        public IReadOnlyList<CellConfigDataSO> Cells;

        /// <summary>Crystal seated at each traversal cell's core. Null falls back to the omni
        /// crystal on <c>Resources/ModePreviewLibrary</c>.</summary>
        public Crystal CrystalPrefab;
        public float CellSpacing = 3200f;
        public float MaxTurnDegrees = 25f;
        public int PrismStride = 4;
        public float PopulationScale = 0.5f;
        public int Seed;

        public float LeashRadiusFactor = 3f;
        public float LeashRadiusFallback = 1300f;
        public float LeashGraceSeconds = 5f;

        public bool RevertCellOnStart = true;
        public float ReturnStationRadius = 16f;
        public Color ReturnStationColor = new(1f, 0.78f, 0.25f, 1f);
    }

    /// <summary>
    /// The Arkway as a MODE: the voyage that starts when the player flies the Arkway toy and
    /// ends when they disembark (the station standing at the entrance they sailed from), leave
    /// freestyle, fly the toy again — or when the food web eats the Ark's last hull prism, which
    /// RESETS the toy.
    ///
    /// The Ark leaves a WAKE — well-spaced, large conserved prisms in its own domain, laid on
    /// distance rather than on a clock — which is ordinary grazeable food-web mass and the honest
    /// way a 150-prism hull comes to matter to a swarm grazing a whole world.
    ///
    /// The player's own ribbon and that wake are recycled with the CORRIDOR: as each traversal
    /// cell is struck, everything laid up to the point the Ark entered it goes with the world it
    /// was laid in —
    /// the same rule <see cref="Cell.RequestCellSwap"/> already applies to loose trail mass in a
    /// swapped world. That, rather than the Wanderway's rolling tether, is what lets a voyage
    /// run indefinitely.
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
    ///   • A LEASH: stay within a few cell radii of the Ark. Beyond it a telegraphed countdown
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
    public sealed class ArkwayRun : MonoBehaviour, IObjectiveProvider
    {
        const float TickSeconds = 0.2f;

        /// <summary>How close to home counts as "already back" — inside this the run ends
        /// without repositioning the vessel.</summary>
        const float HomeArrivalDistance = 120f;

        /// <summary>Seconds the voyage-start hint and the fallen-Ark banner stay up.</summary>
        const float BannerSeconds = 4f;

        /// <summary>Scale a recycled trail prism withers to before it returns to its pool
        /// (the Wanderway tether's own exit — continuity of existence is not waived).</summary>
        static readonly Vector3 RetiredScale = new(0.02f, 0.02f, 0.02f);
        const float WitherSeconds = 0.8f;

        /// <summary>Trail prisms recycled per tick. <see cref="Trail.RemoveOldest"/> re-indexes
        /// the whole ribbon, so an unbounded drain is quadratic — this spreads a cell's worth
        /// over a few seconds, far faster than any vessel lays.</summary>
        const int TrailRecycleBudget = 64;

        /// <summary>How far along the departure heading the entrance station stands. The
        /// player's home pose is INSIDE the Arkway toy's own trigger (they flew through it), so
        /// planting the station there would draw a second ring inside the first — two switches
        /// occupying one place, each doing something different. This puts it just outside, in
        /// the open water at the mouth of the corridor.</summary>
        const float EntranceForwardOffset = 240f;

        /// <summary>How far ABEAM of the departure axis the entrance stands — on the Ark's port
        /// side, opposite the flank the player docks on. Twice the docking offset, so a pilot
        /// who holds course from the dock never brushes the ring, and well clear of the Arkway
        /// toy's own trigger.</summary>
        const float EntranceLateralOffset = 180f;

        /// <summary>The objective arrow hides while the Ark is on screen — but only when it is
        /// also CLOSE enough to read as a ship. A 110-unit hull two thousand units down the
        /// corridor axis is on screen and a speck, and "on screen" was the whole of the arrow's
        /// hide rule, which is why an Ark that sailed ahead had no marker.</summary>
        const float ArrowHideOnScreenWithin = 900f;

        /// <summary>Longest the run waits for the load veil after its own bracket closes. The
        /// veil self-releases on a 180 s no-progress stall, so this only fires if something
        /// else is holding the gate.</summary>
        const float VeilWaitCapSeconds = 200f;

        ArkwayConfig _cfg;
        ToyContext _context;
        Container _container;
        CellConveyor _conveyor;
        System.Action _onEnded;

        Ark _ark;
        WanderwayReturnToy _entrance;
        ArkwayVoyageHud _hud;
        ObjectiveIndicator _arrow;

        // Trail recycling: one mark per corridor advance, consumed one per cell retirement.
        readonly Queue<(Prism primary, Prism secondary, Prism wake)> _trailMarks = new();
        Prism _primaryRollTo, _secondaryRollTo;
        readonly List<(Prism prism, float dueAt)> _withering = new();

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

        // Where the build is right now. Read by End() and by the catch below so any exit that
        // happens before the player sees the Ark names the step it happened at.
        string _stage = "idle";
        float _underwayAt = -1f;

        /// <summary>True while a voyage is in progress (including the veiled first build).</summary>
        public bool IsRunning => _running || _beginning;

        /// <summary>True while the corridor and hull are still building behind the veil —
        /// the window in which the player can see nothing and a toy pass must not toggle.</summary>
        public bool IsBuilding => _beginning;

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
                _stage = "reverting the host cell";
                var hostCell = RevertCellToBareCanvas(vessel);

                // The revert gathers every POOLED prism the host cell tracks — an Ark laid while
                // it is still retiring would be swept up with the old world. Wait it out.
                float deadline = Time.unscaledTime + 30f;
                while (hostCell && hostCell.IsSwappingConfig && Time.unscaledTime < deadline)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (gen != _generation) return; // ended while the revert settled (End() warned)

                _stage = "waiting for the previous corridor to clear";
                // The PREVIOUS voyage's corridor may still be retiring (its cells drain only as
                // they leave view). Give it a moment to clear on its own; whatever remains is
                // force-struck below, AFTER the veil is up - with the screen covered the strike
                // is unseen by construction, which is the one licence a gate-less strike has.
                float idleDeadline = Time.unscaledTime + 8f;
                while ((_conveyor.IsDraining || _conveyor.HasCells)
                       && Time.unscaledTime < idleDeadline && gen == _generation)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (gen != _generation) return;

                // The departure pose is the one the toy FIRED at (_home), not where the vessel
                // has drifted to by now: the two awaits above take 5-30 s and nothing pauses a
                // vessel, so reading the live transform here stood the corridor, the Ark and
                // the entrance hundreds of units apart from each other and from home.
                Vector3 origin = _home.position;
                Vector3 course = _home.rotation * Vector3.forward;

                _stage = "standing the corridor";
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
                        CSDebug.LogWarning("[Arkway] The corridor could not stand its first cells - " +
                                           "no voyage. (CellConveyor warned above with the reason.)");
                        _beginning = false;
                        _stage = "idle";
                        _onEnded?.Invoke();
                        return;
                    }

                    _stage = "laying the Ark's hull";

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

                    if (_ark.TotalCount == 0)
                        CSDebug.LogError($"[Arkway] The Ark's hull laid ZERO prisms (prefab " +
                                         $"'{(_cfg.PrismPrefab ? _cfg.PrismPrefab.name : "null")}'). " +
                                         "The Ark exists but is invisible.");

                    _ark.HullDestroyed += OnArkHullDestroyed;
                    _stage = "holding the arena bracket";

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

                _stage = "waiting for the veil";
                // THE BRACKET IS NOT THE BUILD. EndArenaBuild only says this run has queued
                // its work; the veil stays up until every traversal cell's lay has drained and
                // settled - 30-90 s - and a veiled build is not a pause. Everything that opens
                // the voyage (the dock repose, the entrance, the arrow, the Ark under way) must
                // therefore wait for the veil to actually come down, or it all happens behind
                // an opaque screen while the pilot is blind and still flying: the Ark sails off,
                // the recall drops them beside a ship that then leaves, and the voyage opens on
                // empty water. Four play tests read as "no Ark at all" this way.
                float veilDeadline = Time.unscaledTime + VeilWaitCapSeconds;
                bool grazeWarned = false;
                while (PrismTrailBuilder.IsLoadGateHolding && gen == _generation)
                {
                    if (Time.unscaledTime > veilDeadline)
                    {
                        CSDebug.LogWarning($"[Arkway] The load veil was still holding after {VeilWaitCapSeconds:F0}s - " +
                                           "opening the voyage under it.");
                        break;
                    }
                    // An overview gesture (Escape / pad Start) while staring at the veil drops
                    // freestyle. End here, loudly, rather than let the first voyage tick do it.
                    if (_context?.IsFreestyleActive != null && !_context.IsFreestyleActive())
                    {
                        End(returnToCell: true, "the player left freestyle during the build");
                        return;
                    }
                    // The home cell is on its bare canvas (no fauna), so nothing should be able
                    // to reach a stationary hull here. If something does, say so once - a hull
                    // grazed to nothing under the veil ends the voyage before it is seen.
                    if (!grazeWarned && _ark && _ark.TotalCount > 0 && _ark.AliveCount < _ark.TotalCount)
                    {
                        grazeWarned = true;
                        CSDebug.LogWarning($"[Arkway] The Ark's hull is being grazed BEHIND THE VEIL " +
                                           $"({_ark.AliveCount}/{_ark.TotalCount} plates left) - something is " +
                                           "feeding in the home cell's bare canvas.");
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                if (gen != _generation) return;
                if (!_ark)
                {
                    End(returnToCell: true, "the Ark was lost before the veil came down");
                    return;
                }

                _stage = "opening the voyage";
                // The wake is armed only now - never inside the bracket and never under the
                // veil. Belt and braces with LayOne's watchForReveal: false - one keeps the
                // wake out of the reveal watch, this keeps it out of the build entirely.
                if (_cfg.ArkWakeSpacing > 0.5f)
                    _ark.ConfigureWake(_cfg.PrismPrefab, vessel.Domain, _cfg.ArkWakeSpacing,
                        _cfg.ArkWakeScale, _cfg.ArkWakeBudget, transform.parent);

                PlantEntrance();
                EnsureObjectiveArrow();
                _conveyor.CellRetired -= OnCellRetired;
                _conveyor.CellRetired += OnCellRetired;
                EnsureHud();
                _hud.ShowBanner("STAY WITH THE ARK", BannerSeconds);

                // DOCK the pilot at the Ark's flank on the frame the screen opens. They flew
                // blind for the whole build and are wherever that took them; the Ark has not
                // moved. The repose lands on the last veiled frame, so it is unseen, and speed
                // carries through so they are not left dead in the water.
                RecallToArk(vessel);

                _wasFreestyle = true;
                _running = true;
                _stage = "under way";
                _underwayAt = Time.unscaledTime;

                // The Ark sets sail HERE - beside a pilot who can see it - and nowhere earlier.
                AimArk();
                _ark.SetUnderway(true);

                LogVoyageStart();
            }
            catch (System.OperationCanceledException)
            {
                // Scene teardown mid-build — the conveyor and toybox root take the remainder.
                // (The run's own cancellation token is GetCancellationTokenOnDestroy, so this is
                // ALSO what an unexpected destroy of the run looks like - name it.)
                CSDebug.LogWarning($"[Arkway] Voyage build cancelled at stage '{_stage}' - the run " +
                                   "was destroyed (scene teardown, or the toybox root was torn down).");
            }
            catch (System.Exception e)
            {
                // A UniTaskVoid reports an unhandled exception through UniTaskScheduler, but with
                // no [Arkway] tag and no stage - and a voyage that throws leaves the player
                // behind a veil that will come down onto nothing. Name it, then end cleanly so
                // they are at least taken home.
                CSDebug.LogError($"[Arkway] Voyage build THREW at stage '{_stage}': {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                if (gen == _generation) End(returnToCell: true, $"exception at stage '{_stage}'");
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
            // SCENE cells only: the previous voyage's traversal satellites persist until they
            // leave view, and the nearest of them is a world the corridor is about to strike,
            // not the host.
            var containing = Cell.FindCellContaining(at, sceneCellsOnly: true);
            var cell = containing ? containing : Cell.FindNearestActiveCell(at, sceneCellsOnly: true);

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

            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            // The overview button / gamepad Start exit freestyle; that edge ends the voyage and
            // takes the player home, exactly as the entrance station does.
            bool freestyle = _context?.IsFreestyleActive == null || _context.IsFreestyleActive();
            if (_wasFreestyle && !freestyle)
            {
                // A voyage that ends within seconds of opening is not a player leaving - it is
                // the freestyle flag reading false on the first tick, which ends the voyage the
                // player just built. Say so.
                if (_underwayAt >= 0f && Time.unscaledTime - _underwayAt < 5f)
                    CSDebug.LogWarning($"[Arkway] IsFreestyleActive read FALSE " +
                                       $"{Time.unscaledTime - _underwayAt:F1}s after the voyage opened - " +
                                       "ending it. If the player did not leave freestyle, the flag is stale.");
                End(returnToCell: true, "left freestyle");
                return;
            }
            _wasFreestyle = freestyle;
            if (!freestyle) return;

            if (!_ark)
            {
                // The Ark object died without the hull-lost event (teardown edge).
                End(returnToCell: true, "the Ark object was destroyed without a hull-lost event");
                return;
            }

            TickCorridor();
            // TickCorridor's cannot-stand-a-next-cell branch Ends the run (nulling _ark) —
            // never fall through into the leash tick on that frame.
            if (!_running) return;
            TickLeash();
            TickTrailRecycle();
            RetireDueWithering();
        }

        void TickCorridor()
        {
            // Re-aim every tick, not just on arrival: a freshly stood cell reports
            // MembraneRadius 0 until its membrane has spawned (the ModePreviewArena.FramingRadius
            // bug class), so the approach band read once at departure would be the fallback for
            // the whole leg.
            AimArk();

            if (!_ark.HasArrived(CellConveyor.ArriveDistance)) return;

            if (_conveyor.AdvancePastTarget())
            {
                MarkTrail();
                AimArk();
                LogCensus();
            }
            else
            {
                CSDebug.LogWarning("[Arkway] The corridor could not stand a next cell - the " +
                                   "voyage ends at this one.");
                End(returnToCell: true, "the corridor could not stand a next cell");
            }
        }

        /// <summary>
        /// Where everything IS at the moment the veil comes down — the one frame that decides
        /// whether the player opens the voyage looking at an Ark or at empty water. It reports
        /// the Ark's distance from the vessel because that is the number that went wrong: an Ark
        /// under way behind the veil is thousands of units downrange by the time anyone can see,
        /// and "no Ark at all" and "the Ark is 2,800 units ahead" are indistinguishable on screen.
        /// </summary>
        void LogVoyageStart()
        {
            // Deliberately ALWAYS ON, one line per voyage: this toy has opened on "no Ark" in
            // four play tests with nothing in the console. It moves to the CellLifecycle
            // channel once three consecutive play tests open on a visible Ark (ARKWAY_PLAN.md,
            // Phase 0).
            var t = LocalVessel()?.Transform;
            float distance = _ark && t ? Vector3.Distance(_ark.Position, t.position) : -1f;
            CSDebug.Log(
                $"[Arkway] Voyage under way. Ark {(_ark ? _ark.TotalCount : 0)} hull prisms, " +
                $"{distance:F0}u from the vessel, target {_conveyor.CurrentTargetCentre} " +
                $"({Vector3.Distance(_ark ? _ark.Position : Vector3.zero, _conveyor.CurrentTargetCentre):F0}u away). " +
                $"{_conveyor.Census()}");
        }

        /// <summary>
        /// Everything this voyage is holding, once per crossing, on a channel that is off by
        /// default. An infinite toy needs a way to answer "what is growing?" from a play test —
        /// the numbers here are exactly the ones that would grow if any of the corridor's
        /// recycling stopped working, and no amount of reading the code substitutes for seeing
        /// them climb.
        /// </summary>
        void LogCensus()
        {
            if (!CSDebug.IsVerbose(CSLogChannel.CellLifecycle)) return;
            var pen = LocalVessel()?.VesselPrismController;
            int trail = pen && pen.Trail != null ? pen.Trail.TrailList.Count : 0;
            int trail2 = pen && pen.SecondaryTrail != null ? pen.SecondaryTrail.TrailList.Count : 0;
            CSDebug.LogVerbose(CSLogChannel.CellLifecycle,
                $"[Arkway] {_conveyor.Census()}, ark hull {(_ark ? _ark.AliveCount : 0)}/" +
                $"{(_ark ? _ark.TotalCount : 0)}, wake {(_ark ? _ark.WakeCount : 0)}, " +
                $"trail {trail}+{trail2}, marks {_trailMarks.Count}, withering {_withering.Count}");
        }

        /// <summary>
        /// Point the Ark at the corridor's current target and re-state its arrival profile: the
        /// slow band is that cell's OWN membrane, so the Ark is under way across the open water
        /// between cells and eases down as it crosses into the next one. Re-read on every
        /// advance because each traversal cell is a different world with a different radius.
        /// </summary>
        void AimArk()
        {
            if (!_ark) return;
            float approach = Mathf.Max(1f, _cfg.ArkSpeed);
            float cruise = approach * Mathf.Max(1f, _cfg.ArkCruiseSpeedFactor);
            _ark.SetSpeedProfile(approach, cruise, _conveyor.CurrentCellRadius);
            _ark.SetDestination(_conveyor.CurrentTargetCentre);
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

            // The Ark's FLANK: abeam and slightly back, facing along the course, well clear of
            // the hull plates (~0.22 × length). Never the bow, which would put a recalled pilot
            // in front of a ship under way.
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
            End(returnToCell: true, "the Ark's last hull prism was destroyed");
        }

        // ── The objective indicator: the Ark ─────────────────────────────────

        /// <summary>
        /// <see cref="IObjectiveProvider"/>: the game's standard edge-of-screen arrow points at
        /// the ARK for the whole voyage, and at nothing else.
        ///
        /// The Ark IS the objective here in a way no other mode's target is: the voyage is an
        /// escort, the leash is measured from the hull, and the one thing that ends a voyage
        /// against the player's will is the food web reaching it. A pilot who has ranged out to
        /// the leash — now three cell radii — cannot see a 110-unit ship, so "which way is the
        /// Ark" is the question the arrow exists to answer. The arrow hides itself whenever the
        /// hull is already on screen, so it never competes with the ship it names.
        ///
        /// Deliberately NOT the cell's crystal or the entrance station: an arrow that names two
        /// things names neither, and both of those are visible landmarks of their own (a lit
        /// core, a ring at the harbour) while the Ark is a small dark hull in open water.
        /// </summary>
        public bool TryGetObjective(out Transform target)
        {
            target = null;
            if (!_running || !_ark) return false;
            if (_context?.IsFreestyleActive != null && !_context.IsFreestyleActive()) return false;
            target = _ark.transform;
            return true;
        }

        /// <summary>
        /// Stand up this voyage's arrow. It MUST parent under the full-screen Canvas ROOT — the
        /// indicator stretches to its parent and clamps to that rect's edges, so a mid-hierarchy
        /// container like "Game UI" pins the arrow in a corner (the same note
        /// <see cref="PaintingRunner"/> carries, and the same one-time scene lookup).
        ///
        /// One arrow per live voyage, destroyed with it. A painting run standing its own arrow
        /// at the same time would draw two — degenerate, and the same bounded class as the
        /// Arkway and the Wanderway both running (no cross-toy coordinator exists for any pair).
        /// </summary>
        void EnsureObjectiveArrow()
        {
            if (_arrow) return;

            var hud = FindAnyObjectByType<MenuMiniGameHUD>(FindObjectsInactive.Include);
            Canvas canvas = hud ? hud.GetComponentInParent<Canvas>(true) : null;
            if (!canvas) canvas = FindAnyObjectByType<Canvas>();
            if (!canvas)
            {
                CSDebug.LogWarning("[Arkway] No Canvas found for the objective arrow - the voyage " +
                                   "sails without one.");
                return;
            }

            _arrow = ObjectiveIndicator.CreateRuntime(canvas.transform, this);
            if (!_arrow)
            {
                CSDebug.LogWarning("[Arkway] ObjectiveIndicator.CreateRuntime returned nothing - no arrow.");
                return;
            }
            _arrow.HideOnScreenWithin = ArrowHideOnScreenWithin;
        }

        void DestroyObjectiveArrow()
        {
            if (!_arrow) return;
            Destroy(_arrow.gameObject);
            _arrow = null;
        }

        // ── The way home: the entrance you sailed from ───────────────────────

        /// <summary>
        /// The way home is a STATION AT THE ENTRANCE — planted at the pose you flew the toy
        /// from, and it stays there.
        ///
        /// The Wanderway's return station follows the player because it rides the tail of that
        /// run's rolling tether: there, following IS the trail cleanup, and the station is a
        /// readout of where the recycled ribbon ends. The Arkway has no tether — its cleanup is
        /// the corridor recycling whole cells (<see cref="OnCellRetired"/>) — so it inherited
        /// the motion without the mechanism that gave it meaning, and a way home that chases
        /// the ship you are escorting is a landmark that is never anywhere.
        ///
        /// Planted a short way down the departure heading from <see cref="_home"/> — which is
        /// where <see cref="ReturnHome"/> puts you — and ABEAM of it, on the Ark's port side:
        /// the pilot docks on the starboard flank, so a ring on the departure axis would sit
        /// exactly where a pilot holding course flies, and a station that ends the voyage the
        /// moment it opens is the failure this replaced. Off the axis it marks the harbour
        /// without being in anyone's way, and it never sits inside the Arkway toy's own ring.
        /// The Ark sails on and leaves it behind, and that is the point: it is the harbour you
        /// left. The voyage's other two exits (another pass through the toy, the overview
        /// button) are what end a voyage from out in the corridor.
        /// </summary>
        void PlantEntrance()
        {
            if (_entrance || !_hasHome) return;

            float body = Mathf.Max(8f, _cfg.ReturnStationRadius);
            Vector3 heading = _home.rotation * Vector3.forward;
            Vector3 port = -(_home.rotation * Vector3.right);
            Vector3 at = _home.position + heading * EntranceForwardOffset + port * EntranceLateralOffset;
            // Mouth along the departure heading, so it reads as a hoop from the corridor —
            // the direction anyone coming home is arriving from.
            var placement = new ToyPlacement(at, at + heading, body, body * 2.2f);
            var go = ToyFactory.CreateRoot("Arkway_Entrance", transform, placement,
                _cfg.ReturnStationColor, "DISEMBARK\n<size=60%>fly through to head home</size>");

            _entrance = go.AddComponent<WanderwayReturnToy>();
            _entrance.Configure(() => End(returnToCell: true, "the player flew the entrance station"));
            // Radius only — ending a voyage is NEUTRAL (it hands you back your cell, not a
            // domain), so the ring keeps the switch vocabulary's neutral paint.
            _entrance.ConfigureSwitchRing(placement.TriggerRadius);
            _entrance.Initialize(null, _context, placement);
        }

        void DestroyEntrance()
        {
            if (!_entrance) return;
            Destroy(_entrance.gameObject);
            _entrance = null;
        }

        // ── The trail: recycled with the cell it was laid in ─────────────────

        /// <summary>
        /// Note where the player's ribbon had reached as the Ark crossed into a cell. One mark
        /// per corridor advance, consumed one per cell retirement — the corridor retires cells
        /// in the order it stood them, so the two queues stay paired with no index arithmetic.
        ///
        /// A mark is the HEAD PRISM, not a count: <see cref="Trail.RemoveOldest"/> shifts every
        /// survivor toward the head, so any recorded index or length goes stale the moment a
        /// roll runs. A prism reference does not.
        /// </summary>
        void MarkTrail()
        {
            var pen = LocalVessel()?.VesselPrismController;
            _trailMarks.Enqueue((
                pen ? HeadOf(pen.Trail) : null,
                pen ? HeadOf(pen.SecondaryTrail) : null,
                _ark ? _ark.WakeHead : null));
        }

        static Prism HeadOf(Trail trail) =>
            trail != null && trail.TrailList.Count > 0 ? trail.TrailList[^1] : null;

        /// <summary>
        /// A traversal cell has been struck: the player's trail up to the point they entered it
        /// goes with it. This is not a trail cap and not decay — it is the rule a struck world
        /// already lives by (<see cref="Cell.RequestCellSwap"/> with <c>clearLooseTrailMass</c>),
        /// applied per traversal cell, and it only ever runs inside a live voyage the player
        /// opted into. The removal is unseen by construction: a cell is struck only once its
        /// whole membrane is off screen, and the player is leashed to the Ark, cells ahead.
        /// </summary>
        void OnCellRetired()
        {
            if (!_running || _trailMarks.Count == 0) return;
            var (primary, secondary, wake) = _trailMarks.Dequeue();
            // A newer mark is further along the ribbon and subsumes any roll still in flight.
            if (primary) _primaryRollTo = primary;
            if (secondary) _secondaryRollTo = secondary;
            // The Ark's own wake goes with the cell it was laid in, exactly as the player's
            // does. It is retired in one pass rather than budgeted: the wake is two orders of
            // magnitude shorter than a pilot's ribbon (one prism per 45 units of travel).
            if (wake && _ark) _ark.RetireWakeBefore(wake);
        }

        void TickTrailRecycle()
        {
            var pen = LocalVessel()?.VesselPrismController;
            if (!pen) return;
            int budget = TrailRecycleBudget;
            RollTo(pen.Trail, ref _primaryRollTo, ref budget);
            RollTo(pen.SecondaryTrail, ref _secondaryRollTo, ref budget);
        }

        /// <summary>
        /// Recycle the oldest trail prisms up to (not including) <paramref name="mark"/>. Only
        /// POOLED prisms are recycled — the same <c>OnReturnToPool != null</c> test the Cell
        /// uses to tell a vessel's loose trail mass from instantiated mass; an unpooled prism
        /// has nowhere to go, so the roll stops rather than shrinking it into an invisible
        /// collider. BOTH ribbons are rolled: a double-trail vessel puts every other prism in
        /// the secondary, and rolling only the primary would leak half the ribbon.
        /// </summary>
        void RollTo(Trail trail, ref Prism mark, ref int budget)
        {
            if (trail == null || mark == null) return;

            // -1 = the mark has left this ribbon (already rolled past, eaten, exploded);
            // 0 = the roll has reached it. Either way the boundary is spent — drop it and
            // wait for the next cell's mark rather than guessing where it used to be.
            int stop = trail.GetBlockIndex(mark);
            if (stop <= 0) { mark = null; return; }

            while (stop-- > 0 && budget-- > 0)
            {
                var oldest = trail.TrailList[0];
                if (!oldest) { trail.RemoveOldest(); continue; }   // eaten/exploded — drop the slot
                if (oldest.OnReturnToPool == null) return;         // not ours to recycle

                // Already gone home ahead of us — a strike (Cell.StrikeSatelliteWorld's own
                // clearLooseTrailMass) returns a cell's loose trail prisms to the pool WITHOUT
                // removing them from the vessel's ribbon, so the slot can outlive the prism and
                // the prism can already have been re-issued to a fresh lay. Withering one of
                // those would shrink live mass and double-return it. Drop the slot only.
                if (!oldest.gameObject.activeInHierarchy || oldest.Trail != trail)
                {
                    trail.RemoveOldest();
                    continue;
                }

                trail.RemoveOldest();
                BeginWither(oldest);
            }
            if (stop < 0) mark = null;
        }

        /// <summary>
        /// Start a detached prism's exit. Continuity of existence is NOT waived: the prism
        /// withers on the GPU clock (one grow-clock re-stamp — the setter IS the stamp) and is
        /// handed back to the pool only once it has shrunk away.
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

        /// <summary>Hand everything still mid-wither straight back to the pool (voyage end /
        /// teardown) — a prism left shrunk to nothing is an invisible collider.</summary>
        void FlushWithering()
        {
            for (int i = 0; i < _withering.Count; i++)
                if (_withering[i].prism) _withering[i].prism.ReturnToPool();
            _withering.Clear();
        }

        // ── End ──────────────────────────────────────────────────────────────

        /// <summary>
        /// End the voyage: bring the player home, retire the Ark (withered back to its pool),
        /// strike the corridor, and re-arm the toy. Idempotent. The player is reposed FIRST so
        /// the corridor's strike happens far away and out of sight — the unseen-removal clause
        /// every satellite teardown rides.
        /// </summary>
        public void End(bool returnToCell, string reason = "unspecified")
        {
            if (!_running && !_beginning) return;

            // A voyage that ends BEFORE it reached the player is a fault, and it must be loud:
            // four play tests in a row opened on "no Ark" with nothing in the console, because
            // every exit from the build was either silent or on a verbose channel. The ordinary
            // exits (a player-initiated end) stay on the channel.
            if (_beginning)
                CSDebug.LogWarning($"[Arkway] Voyage ended DURING ITS BUILD (stage '{_stage}'): {reason}. " +
                                   "The player never saw the Ark.");
            else
                CSDebug.LogVerbose(CSLogChannel.CellLifecycle, $"[Arkway] Voyage ended: {reason}.");

            _generation++; // a Begin still in flight must not resurrect this voyage
            _running = false;
            _beginning = false;
            _leashBreachedAt = -1f;
            _hud?.HideCountdown();

            var vessel = LocalVessel();
            DestroyEntrance();
            DestroyObjectiveArrow();

            if (_conveyor) _conveyor.CellRetired -= OnCellRetired;
            _trailMarks.Clear();
            _primaryRollTo = _secondaryRollTo = null;
            FlushWithering();

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
            End(returnToCell: false, "ArkwayRun destroyed");
        }
    }
}
