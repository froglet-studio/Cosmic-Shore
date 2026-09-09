using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breakwater - the Sparrow-only STATION race. Fourteen ordered stations hang on a walk
    /// through the cell, every pilot flies the same course in ORDER, and the first domain whose
    /// LEAD RUNNER threads the last station wins.
    ///
    /// <para><b>A station is a switch with a wall in front of it.</b> Each one is a shallow dish
    /// of plates opening back toward the pilot, its throat welded shut by a triple-rake weave of
    /// danger bars with an 18-unit eye at the centre and a keystone collar ringing it. You close
    /// on it with two rockets in the bay and make one choice: FIRE (the skyburst's spherical
    /// blast vaporises a door), SAW (turret stance, stop dead, grind the weave open) or THREAD
    /// (fly the eye at 1.46x hull clearance, with danger bars a hull-width away). What actually
    /// SCORES is none of those - it is the <see cref="RaceGateRing"/> at the port radius. The
    /// three verbs are three PRICES for the same crossing rather than three scoring events,
    /// which is why the mode needs no rule to say a sawn station counts as much as a shot one.
    /// Ammunition is the arena itself (50 hostile prisms buy a rocket), so opening one door
    /// roughly funds the next and the economy closes without a pickup anywhere on the course.</para>
    ///
    /// <para><b>This is Switchback's skeleton, deliberately and completely.</b> A station is a
    /// switch threaded in order - the same fact - so the entire scoring layer is REUSED verbatim:
    /// <c>ScoringMetric.SwitchesThreaded</c>, <see cref="SwitchThreadScoring.Credit"/>,
    /// <c>Player.ReportSwitchThreaded_ServerRpc</c>, <c>GameDataSO.SwitchTargetCount</c>, the
    /// goal-stack row, <c>ScoreDifferenceSource.SwitchesThreaded</c> and the
    /// <c>BestByDomain</c> fold. This mode adds no metric, no stat, no RPC and no scoring code
    /// at all. What it adds is the thing in the way, and the arena that holds it.</para>
    ///
    /// <para><b>Ordered stations are what make the whole thing cheap.</b> Because a pilot may
    /// only thread their NEXT station, <see cref="IRoundStats.SwitchesThreaded"/> is
    /// simultaneously the score, the progress bar, the index of the ring to test this frame, and
    /// the token the server validates a report against. One replicated int carries the race;
    /// there is no per-pilot bitmask, no per-station state, and detection is one segment test per
    /// pilot per frame rather than pilots x stations.</para>
    ///
    /// <para><b>The course travels, the seed does not.</b> The server generates it and BROADCASTS
    /// the geometry. A shared seed would have worked - <see cref="BreakwaterCourse"/> is
    /// deterministic on purpose - but it would rest on <c>Mathf.Sin</c>/<c>Acos</c> agreeing to
    /// the last bit across Mono and IL2CPP, and a single flipped branch in the walk yields a
    /// completely different course rather than a slightly different one. Fourteen stations is 340
    /// bytes; the seed is kept only so a reported course can be reproduced. Everything the ARENA
    /// needs is then derived from those poses by closed form on each peer
    /// (<see cref="BreakwaterStationBuilder"/>), so several thousand prisms cost nothing on the
    /// wire.</para>
    ///
    /// <para><b>Detection is owner-detects / server-records</b>, the platform's familiar
    /// round trip. Each machine tests only the vessels it simulates - the host's human plus every
    /// AI, a client's own human - because a remote vessel's replicated position is interpolated
    /// and would miss or invent crossings. The server credits its own directly; a client forwards
    /// the index and the server re-validates it.</para>
    ///
    /// <para><b>The ARENA is this mode's own structure; the Cell still owns everything else.</b>
    /// A mode owns its gameplay-bearing structure (CLAUDE.md, "The Cell owns the environment"),
    /// and here the stations ARE the gameplay - they are what a rocket is spent on and what a
    /// hull is threaded through. They cannot be an authored <c>EnvironmentPrefab</c> because the
    /// course is ROLLED PER MATCH, so the cell configs author none and this controller stands the
    /// arena up itself once the poses exist. Everything that is not the course still comes from
    /// the Cell exactly as the rule requires: the membrane is the playfield boundary, the
    /// cytoplasm is the atmosphere, the SpawnProfile is the population (empty here - see below)
    /// and the PhaseThresholds are the ladder, measured against this arena's own mass by
    /// <c>Tools/Build/breakwater_arena.py</c>.</para>
    ///
    /// <para><b>No flora, no fauna, and that is a rule rather than a saving.</b> The whole arena
    /// is <see cref="Domains.Blue"/> so every pilot's rounds pay ammunition on every door, and in
    /// a nucleus-less cell herbivores eat opposing-domain mass - so a food web would graze the
    /// plugs open on its own, which is imposed death of the mode's central object.</para>
    /// </summary>
    public class BreakwaterController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag BreakwaterScoringRule.asset - the per-mode scoring strategy (end condition, " +
                 "scores, results). A second ASSET of SwitchbackScoringRuleSO, not a second script: " +
                 "it reads the metric and GameDataSO.SwitchTargetCount and already folds a domain " +
                 "by its BEST pilot, which is exactly this mode's rule too.")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Course")]
        [Tooltip("The cell the course is laid inside. Resolved through Cell.FindByRuntimeData, " +
                 "which answers immediately - unlike CellRuntimeDataSO.Cell, which is still null " +
                 "for the first second while the cell's own Initialize waits behind InitDelayMs.")]
        [SerializeField] CellRuntimeDataSO cellData;

        [Tooltip("The station geometry generator. Instantiated per match and handed the rolled " +
                 "course before it spawns - the cell configs author NO EnvironmentPrefab, because " +
                 "there is no arena to author until the walk has run.")]
        [SerializeField] SpawnableBreakwater arenaPrefab;

        [Tooltip("Course shell, outer edge. 0.9 x the CapsuleMembrane's authored radius (1200), " +
                 "measured rather than read because Cell.MembraneRadius returns 0 until the " +
                 "membrane has spawned and the course is generated before that.")]
        [SerializeField, Min(1f)] float courseOuterRadius = 1080f;

        [Tooltip("Course shell, inner edge. This cell authors NO NUCLEUS, so " +
                 "Cell.ExpectedNucleusWorldRadius returns 0 and this is the value that actually " +
                 "ships - it is the model's SHELL_INNER, the number every measured row was swept " +
                 "against, not a fallback that never runs.")]
        [SerializeField, Min(1f)] float courseInnerRadiusFallback = 420f;

        [Tooltip("Seconds a station's switch ring takes to bloom in. Detection is live at the " +
                 "full mouth from frame one; only the drawing grows into it.")]
        [SerializeField, Min(0f)] float ringBloomSeconds = 0.9f;

        [Tooltip("How long the course may sit at one build stage before the connecting panel is " +
                 "released with an error naming that stage. Well under PrismTrailBuilder's " +
                 "180-second stall cap, which releases without naming the mode - this is the " +
                 "mode-side version, and the whole point is that it SAYS what did not finish. " +
                 "A slow-but-working build has already reached ArenaBuilt by then and is held " +
                 "by the lay's own counter, so this cannot cut one short.")]
        [SerializeField, Min(5f)] float courseBuildWatchdogSeconds = 25f;

        [Tooltip("0 = roll a fresh course each match. Non-zero pins the seed, which is how a " +
                 "reported course is reproduced - including its reseed chain, which is derived " +
                 "rather than re-rolled for exactly that reason.")]
        [SerializeField] int courseSeed;

        [Header("AI")]
        [Tooltip("Distance at which an AI stops lining up on its station's axis and commits to the " +
                 "fly-through point on the far side. Under the shortest leg the ladder produces " +
                 "(275), so a pilot always spends the head of a leg lining up rather than " +
                 "arriving already committed.")]
        [SerializeField, Min(1f)] float aiCommitDistance = 240f;

        [Tooltip("How far back along the station's axis an AI aims while lining up. AIPilot has no " +
                 "arrive-and-stop behaviour, so this point must be genuinely behind the port or " +
                 "the pilot orbits it - 280 is 2.15x the Sparrow's tightest turning circle at the " +
                 "transient ceiling (130.1u), so the approach leg is flyable from any bearing.")]
        [SerializeField, Min(1f)] float aiApproachLead = 280f;

        [Tooltip("How far PAST the port the fly-through point sits. Same reason as the lead: the " +
                 "AI flies at its target and through it. 200 clears the widest dish rim the ladder " +
                 "builds (1.75 x 72 = 126), so the aim point is never inside the horn the pilot is " +
                 "about to fly out of.")]
        [SerializeField, Min(1f)] float aiThroughDistance = 200f;

        [Tooltip("Extra distance an AI will accept flying to take a crystal ON THE WAY to its next " +
                 "station. Installing an external target provider replaces AIPilot's own crystal " +
                 "seeking outright, so without this an AI could never level an element - a crystal " +
                 "is not a weapon trigger for this hull, it is the elemental economy and (since " +
                 "Salvo) a debuff ward. 0 disables the detour, and it costs nothing in a cell that " +
                 "manages no crystals.")]
        [SerializeField, Min(0f)] float aiCrystalDetourSlack = 200f;

        [Tooltip("Seconds between an AI's scans of the live crystal registry. The chosen crystal " +
                 "is re-tested every frame; only the search for a new one is throttled.")]
        [SerializeField, Min(0.1f)] float aiCrystalScanSeconds = 0.5f;

        [Header("Detection")]
        [Tooltip("Ignore a single frame's motion longer than the fastest Sparrow could fly plus a " +
                 "margin. The hull's measured transient ceiling is 235 u/s (full throttle, " +
                 "boosting, top of the overcharge band) - 400 is 1.7x that, so nothing a pilot can " +
                 "FLY is rejected and a respawn, an eject or a hitch cannot be credited.")]
        [SerializeField, Min(1f)] float maxPlausibleSpeed = 400f;

        [Tooltip("Seconds before an unacknowledged station report is assumed lost and the local " +
                 "optimistic progress resyncs to the replicated value. Without this a rejected " +
                 "report would leave a client testing a station it can never be credited for.")]
        [SerializeField, Min(0.5f)] float reportResyncSeconds = 3f;

        int Intensity => Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4);

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // The race ends from OnTurnEndedCustom (server) -> SyncFinalScores_ClientRpc, which
        // raises WinnerCalculated + MiniGameEnd itself. Suppressing the base game-end flow is
        // what stops SyncGameEnd_ClientRpc raising them a second time.
        protected override bool HasEndGame => false;

        readonly List<BreakwaterStation> _course = new();
        readonly List<RaceGateRing> _rings = new();
        readonly Dictionary<IPlayer, PilotRun> _runs = new();
        readonly List<IPlayer> _stalePilots = new();

        bool _courseBuilt;
        bool _finalResultsSent;
        bool _arenaBuildAnnounced;
        bool _warnedCourseMissing;
        int _litStation = -1;

        /// <summary>
        /// How far the course got. Read by nothing but the diagnostics below - and that is its
        /// whole job: every step from here to the arena runs INSIDE the connecting panel's
        /// arena-ready bracket, so anything that throws or never arrives is a covered screen and
        /// no other symptom. A stage stamp is what turns "it hangs on the loading screen" into a
        /// line naming the step that did not finish.
        /// </summary>
        enum BuildStage
        {
            NotStarted, Announced, Generating, Generated, Applying, RingsRaised, ArenaBuilt,
            AwaitingBroadcast, GaveUp
        }

        BuildStage _stage = BuildStage.NotStarted;
        float _stageEnteredAt;
        bool _watchdogFired;

        /// <summary>Clients that asked for the course before the server had one. Answered the
        /// moment it exists - the early return that used to drop them was silent, and a dropped
        /// pull is a client holding its connecting panel on an arena that is never announced.</summary>
        readonly List<ulong> _coursePullQueue = new();

        /// <summary>Per-pilot detection state, on the machine that simulates that pilot.</summary>
        class PilotRun
        {
            public Vector3 LastPosition;
            public bool HasLastPosition;

            /// <summary>
            /// Stations this machine BELIEVES the pilot has threaded. On the server it tracks the
            /// authoritative stat exactly; on a client it may run ahead of the replicated value
            /// while a report is in flight, which is the point - without it a pilot flying a
            /// boosted leg during one round trip would be tested against a station they have
            /// already passed and would miss the next one entirely.
            /// </summary>
            public int Optimistic;
            public float LastReportTime;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _warnedCourseMissing = false;

            // The arena is rolled per match, so at scene start there is nothing for the
            // connecting panel's arena-ready gate to observe and it would release the moment it
            // opened - releasing pilots into an empty cell, ahead of several thousand prisms
            // materialising around them. Announce the pending build so the panel holds through
            // generation, broadcast, ring raise AND the streamed prism lay, exactly as
            // SkimRaceController does through its seed wait.
            _arenaBuildAnnounced = true;
            _watchdogFired = false;
            EnterStage(BuildStage.Announced);
            PrismTrailBuilder.BeginArenaBuild();

            // ORDERING, and it is load-bearing on BOTH paths: base.OnNetworkSpawn has already
            // sent the config sync (server) or asked for it (client), and every message here
            // travels on THIS NetworkObject, so NGO delivers the intensity before the course on
            // either route. That matters because BuildArena spawns at the local Intensity, and a
            // client that built its arena before the config landed would build a different one
            // than the host for the whole match - the sticky-intensity race the base class's own
            // pull comment records. Moving this above base.OnNetworkSpawn would reintroduce it.
            // CONTAINED, because everything below runs inside the bracket opened two lines up:
            // a throw here used to leave it open, which is a covered screen and nothing else
            // until the builder's 180-second stall cap releases with a line that names no mode.
            try
            {
                if (IsServer)
                {
                    GenerateAndBroadcastCourse();
                }
                else
                {
                    EnterStage(BuildStage.AwaitingBroadcast);
                    RequestCourse_ServerRpc();
                }
            }
            catch (System.Exception e)
            {
                FailBuild(IsServer ? "Course generation" : "The course pull", e);
                if (IsServer) CourseUnavailable_ClientRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            ReleaseArenaBuildAnnouncement();
            ClearCourse();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Close the BeginArenaBuild bracket exactly once - when the arena's lay is in flight,
        /// when generation gives up, or on despawn, whichever comes first. A bracket left open
        /// wedges the connecting panel on a build that is never going to happen.
        /// </summary>
        void ReleaseArenaBuildAnnouncement()
        {
            if (!_arenaBuildAnnounced) return;
            _arenaBuildAnnounced = false;
            PrismTrailBuilder.EndArenaBuild();
        }

        void EnterStage(BuildStage stage)
        {
            _stage = stage;
            _stageEnteredAt = Time.unscaledTime;
        }

        /// <summary>
        /// Say what stage the build died in, close the bracket, and let the match start.
        ///
        /// <para><b>Every step between <see cref="OnNetworkSpawn"/> and the arena runs inside the
        /// arena-ready bracket</b>, so an exception anywhere in it leaves the bracket open and the
        /// connecting panel covering the screen. The generic release that eventually catches that
        /// is <c>PrismTrailBuilder</c>'s 180-second stall cap, which names a pending build count
        /// and no mode - three minutes of nothing, then a line that does not say Breakwater. This
        /// is the mode-side version: the same release, immediately, with the stage and the
        /// exception attached.</para>
        /// </summary>
        void FailBuild(string what, System.Exception e)
        {
            CSDebug.LogError($"[Breakwater] {what} threw at stage {_stage} - the arena will be " +
                             $"missing and the connecting panel is being released so the match " +
                             $"can start. {e}");
            EnterStage(BuildStage.GaveUp);
            ReleaseArenaBuildAnnouncement();
        }

        /// <summary>
        /// Fires once if the bracket is still open long enough that the player is looking at a
        /// covered screen wondering. Names the stage, then releases rather than waiting out the
        /// builder's 180-second stall cap. A build that is merely SLOW has already reached
        /// <see cref="BuildStage.ArenaBuilt"/> by then and is held by the lay's own counter, not
        /// by this bracket - so this cannot cut a working build short.
        /// </summary>
        void TickBuildWatchdog()
        {
            if (_watchdogFired) return;
            if (Time.unscaledTime - _stageEnteredAt < courseBuildWatchdogSeconds) return;

            // PHASE 1 - the COURSE is stuck. This bracket is ours, so release it.
            if (_arenaBuildAnnounced)
            {
                _watchdogFired = true;
                CSDebug.LogError(
                    $"[Breakwater] The course has been stuck at stage {_stage} for " +
                    $"{courseBuildWatchdogSeconds:F0}s while the connecting panel holds the screen. " +
                    $"Releasing it. IsServer={IsServer}, rings={_rings.Count}, stations={_course.Count}, " +
                    $"arenaPrefab={(arenaPrefab != null ? "assigned" : "MISSING")}, intensity={Intensity}. " +
                    "Stage AwaitingBroadcast on a client means the host never answered the course pull; " +
                    "Generating means generation is still running; Applying means a ring or the arena " +
                    "threw (look for the error above this one).");
                ReleaseArenaBuildAnnouncement();
                return;
            }

            // PHASE 2 - the course is up and the LAY is what the panel is waiting on. That bracket
            // belongs to SpawnableBreakwater.LaySegmentsAsync and the builder's own counters, so
            // this only REPORTS: releasing somebody else's bracket would drop the screen onto a
            // half-laid arena, which is the exact failure the gate exists to prevent. The builder
            // has its own 180-second stall cap; this line is what tells you which side to look at
            // while you wait for it.
            if (_stage == BuildStage.ArenaBuilt && PrismTrailBuilder.IsLoadGateHolding)
            {
                _watchdogFired = true;
                CSDebug.LogWarning(
                    $"[Breakwater] The course is up ({_course.Count} stations, {_rings.Count} rings) " +
                    $"but the connecting panel has held for {courseBuildWatchdogSeconds:F0}s since the " +
                    $"arena started laying. This is the ARENA LAY, not the course: " +
                    $"laying={PrismTrailBuilder.IsLayingInProgress}, settling=" +
                    $"{PrismTrailBuilder.GrowRemainingCount}. A settling count that never reaches 0 " +
                    "is a prism whose grow-in never completes; laying stuck true is a segment whose " +
                    "LayBudgetedAsync never returned.");
            }
        }

        // ── Course ────────────────────────────────────────────────────────

        void GenerateAndBroadcastCourse()
        {
            // ONE authority for the station count: the same overrides key the turn monitor reads
            // for the target. Read here rather than waiting for the monitor to publish it, so the
            // course cannot be built before the number that describes it exists - and cannot
            // disagree with it either.
            var overrides = EndConditionOverridesSO.Instance;
            int stationCount = overrides != null
                ? overrides.GetBreakwaterStationTarget()
                : EndConditionOverridesSO.DefaultBreakwaterStationTarget;

            // The ONE non-deterministic draw in this mode, on the server only and before the
            // broadcast. The generator itself is pure - a fresh course per match is a design
            // requirement, not a determinism leak, and what reaches the peers is geometry.
            int seed = courseSeed != 0 ? courseSeed : Random.Range(int.MinValue, int.MaxValue);

            var settings = BuildSettings(stationCount);
            int fullCount = settings.StationCount;
            int usedSeed = seed;
            List<BreakwaterStation> course = null;

            // PHASE 1 - RESEED, keeping the full count.
            //
            // Switchback answers a failed walk by HALVING its gate count, and at that mode's
            // failure rate that is a defensible trade. Here it is not: halving answers a rare
            // roll by shipping that ONE match a race half the length of every other - a
            // difference the players in it can see and cannot explain. So a failure is reseeded
            // first and every match stays fifteen stations long.
            //
            // Since the circuit replaced the walk this is BELT AND BRACES rather than a live
            // path: the loop is closed by construction and its amplitude shrink bottoms out on a
            // regular zigzag ring, which is legal, so the generator has no failure mode left to
            // exercise (measured: 0 failures in 1,600 courses). It is kept because the settings
            // it is handed are authorable - the overrides window's station target and this
            // component's shell fields - and a configuration a human can write must degrade
            // rather than hang.
            EnterStage(BuildStage.Generating);
            for (int attempt = 0; ; attempt++)
            {
                course = BreakwaterCourse.Generate(usedSeed, settings);
                if (course != null && course.Count >= settings.StationCount) break;

                course = null;
                if (attempt >= BreakwaterCourse.ReseedAttempts) break;

                usedSeed = DeriveNextSeed(usedSeed);
                CSDebug.LogWarning(
                    $"[Breakwater] Course generation failed for {settings.StationCount} stations in " +
                    $"shell {settings.InnerRadius:F0}..{settings.OuterRadius:F0} (step " +
                    $"{settings.MinStep:F0}..{settings.MaxStep:F0}, separation " +
                    $"{settings.MinSeparation:F0}); reseeding to {usedSeed} " +
                    $"({attempt + 1}/{BreakwaterCourse.ReseedAttempts}).");
            }

            // PHASE 2 - only once every reseed has failed: SHORTEN, floor 3.
            //
            // A shell genuinely too tight for the requested count is a CONFIGURATION fault, and
            // both knobs that cause it are authorable in the shipped editor (the overrides
            // window's station target; this component's shell fields) - so it must degrade rather
            // than hang. Returning empty would leave the match with no rings, no arena, no
            // scoring and no turn end, and one error on the host console only.
            if (course == null)
            {
                // Floor 3, not 2: a course is a start gate plus a CIRCUIT, and a two-station
                // circuit is a line rather than a loop, so BreakwaterCourse.Generate declines it.
                int ask = settings.StationCount;
                while (ask > 3)
                {
                    ask = Mathf.Max(3, ask / 2);
                    settings.StationCount = ask;
                    course = BreakwaterCourse.Generate(usedSeed, settings);
                    if (course != null && course.Count >= ask) break;
                    course = null;
                }

                if (course != null)
                    CSDebug.LogWarning(
                        $"[Breakwater] Course generation failed at {fullCount} stations after " +
                        $"{BreakwaterCourse.ReseedAttempts} reseeds; racing to {course.Count} " +
                        "instead. That is a shorter match than the mode is authored for - widen " +
                        "the shell or shorten the legs.");
            }

            if (course == null)
            {
                // Even two stations will not fit. Nothing this mode can do but say so - loudly,
                // and with the numbers that have to change.
                CSDebug.LogError(
                    $"[Breakwater] Course generation FAILED even at 2 stations in shell " +
                    $"{settings.InnerRadius:F0}..{settings.OuterRadius:F0} (step " +
                    $"{settings.MinStep:F0}..{settings.MaxStep:F0}, separation " +
                    $"{settings.MinSeparation:F0}, port {settings.PortRadius:F0}). Widen the " +
                    "shell or shorten the legs.");

                // EVERY PEER OPENED A BRACKET IN OnNetworkSpawn, so releasing only the server's
                // leaves each client holding its connecting panel on an arena that is never
                // coming - until the panel's own 180-second stall cap gives up, with the one
                // error that explains it on the HOST console. The failure is a configuration
                // fault and is meant to degrade loudly, not to hang three machines silently.
                EnterStage(BuildStage.GaveUp);
                ReleaseArenaBuildAnnouncement();
                CourseUnavailable_ClientRpc();
                return;
            }

            // The generator works about the ORIGIN; the spawn ring, the membrane and the arena
            // are all measured from the CELL. They coincide in the shipped scene and would stop
            // coinciding the moment anyone moved or nested the Cell - at which point the whole
            // course slides off the arena and station 1 is no longer on the spawn ring's pole.
            // Offset once here, before the broadcast, so every peer receives WORLD positions and
            // the generator stays a pure function of its shell.
            Vector3 centre = ResolveCellCentre();
            if (centre != Vector3.zero)
                for (int i = 0; i < course.Count; i++)
                    course[i] = new BreakwaterStation(course[i].Position + centre, course[i].Axis,
                                                      course[i].PortRadius);

            CSDebug.Log($"[Breakwater] Course seed {usedSeed}: {course.Count} stations, intensity " +
                        $"{Intensity}, port radius {settings.PortRadius:F0}.");

            EnterStage(BuildStage.Generated);
            ApplyCourse(course);
            BroadcastCourse(course, default);

            // Anyone who asked before the course existed. See _coursePullQueue.
            for (int i = 0; i < _coursePullQueue.Count; i++)
                BroadcastCourse(_course, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { _coursePullQueue[i] } }
                });
            _coursePullQueue.Clear();
        }

        /// <summary>
        /// The next seed to try after a failed walk. DERIVED from the failing seed rather than
        /// re-rolled, so a PINNED <see cref="courseSeed"/> stays reproducible all the way through
        /// its fallback chain: a seed that reproduces a reported course must reproduce the same
        /// course every time, including on the roll that failed. (Numerical Recipes' LCG constants
        /// - any full-period mix would do; what matters is that it is a function of the input.)
        /// </summary>
        static int DeriveNextSeed(int seed) => unchecked(seed * 1664525 + 1013904223);

        /// <summary>
        /// The station target this match is actually run against - the course's OWN length once
        /// it exists, and the authored override only before that.
        ///
        /// <para>The turn monitor reads this rather than the overrides directly, because the two
        /// numbers must never disagree: a target naming a station the course does not contain is
        /// unreachable, and an unreachable target is a match that cannot end. Generation backs off
        /// when a shell is too tight (above), so the course is the honest authority.</para>
        /// </summary>
        public int AuthoritativeStationCount => _courseBuilt && _course.Count > 0 ? _course.Count : 0;

        BreakwaterCourseSettings BuildSettings(int stationCount)
        {
            var cell = cellData != null ? Cell.FindByRuntimeData(cellData) : null;

            // THIS CELL AUTHORS NO NUCLEUS, so ExpectedNucleusWorldRadius returns 0 and the
            // serialized floor is the value that actually ships - it is the model's SHELL_INNER
            // (420), the number every measured row in Tools/Build/breakwater_arena.py was swept
            // against, not a fallback that never runs. The nucleus is read anyway and taken as a
            // FLOOR, because if one is ever authored the course must not walk through it; that
            // would also invalidate the measured ladder, which is why it says so.
            float nucleus = cell != null ? cell.ExpectedNucleusWorldRadius : 0f;
            float inner = courseInnerRadiusFallback;
            if (nucleus > inner)
            {
                inner = nucleus;
                CSDebug.LogWarning(
                    $"[Breakwater] The cell authors a nucleus of {nucleus:F0}, wider than the " +
                    $"measured inner shell ({courseInnerRadiusFallback:F0}). The course is being " +
                    "pushed outside it, so this match's arena is NOT the one breakwater_arena.py " +
                    "measured the PhaseThresholds against.");
            }

            float outer = Mathf.Max(inner + 120f, courseOuterRadius);

            var s = BreakwaterCourseSettings.ForIntensity(Intensity);
            s.StationCount = Mathf.Max(3, stationCount);
            s.InnerRadius = inner;
            s.OuterRadius = outer;
            return s;
        }

        [ServerRpc(RequireOwnership = false)]
        void RequestCourse_ServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // ASKED TOO EARLY. The server generates in its own OnNetworkSpawn, so in practice the
            // course is already standing - but a silent early return is a client holding its
            // connecting panel on a broadcast that will never come, and "in practice" is not a
            // guarantee about NGO's spawn ordering. Remember them and answer once it exists.
            if (_course.Count == 0)
            {
                ulong asker = rpcParams.Receive.SenderClientId;
                if (!_coursePullQueue.Contains(asker)) _coursePullQueue.Add(asker);
                return;
            }

            // Targeted reply, mirroring MultiplayerMiniGameControllerBase's config pull: a client
            // that spawned after the broadcast has no other way to learn the course, and NGO only
            // holds a message for an unspawned object for a few seconds.
            BroadcastCourse(_course, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
            });
        }

        void BroadcastCourse(IReadOnlyList<BreakwaterStation> course, ClientRpcParams target)
        {
            // Six floats per station, interleaved into one array, plus ONE shared port radius:
            // the ladder gives every station in a course the same port
            // (BreakwaterCourseSettings.PortRadius), so sending it per station would be fourteen
            // copies of one number. Everything else about a station - dish, collar, plug, every
            // prism - is CLOSED FORM from this pose, so this is the entire wire cost of the arena.
            var packed = new float[course.Count * 6];
            for (int i = 0; i < course.Count; i++)
            {
                int o = i * 6;
                packed[o + 0] = course[i].Position.x;
                packed[o + 1] = course[i].Position.y;
                packed[o + 2] = course[i].Position.z;
                packed[o + 3] = course[i].Axis.x;
                packed[o + 4] = course[i].Axis.y;
                packed[o + 5] = course[i].Axis.z;
            }
            SyncCourse_ClientRpc(packed, course[0].PortRadius, target);
        }

        /// <summary>
        /// Told to every peer when generation gave up: there is no course and no arena, so stop
        /// holding the connecting panel for one. The counterpart of the release the server does
        /// for itself on the same branch - see <see cref="ReleaseArenaBuildAnnouncement"/>.
        /// </summary>
        [ClientRpc]
        void CourseUnavailable_ClientRpc()
        {
            if (IsServer) return;   // already released on the failing branch

            CSDebug.LogError("[Breakwater] The host reported that course generation failed; " +
                             "this match has no stations. Check the host console for the shell " +
                             "and leg numbers that have to change.");
            ReleaseArenaBuildAnnouncement();
        }

        [ClientRpc]
        void SyncCourse_ClientRpc(float[] packed, float portRadius, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return;   // the server laid its own copy before broadcasting

            var course = new List<BreakwaterStation>(packed.Length / 6);
            for (int o = 0; o + 5 < packed.Length; o += 6)
                course.Add(new BreakwaterStation(
                    new Vector3(packed[o], packed[o + 1], packed[o + 2]),
                    new Vector3(packed[o + 3], packed[o + 4], packed[o + 5]),
                    portRadius));

            try
            {
                ApplyCourse(course);
            }
            catch (System.Exception e)
            {
                FailBuild("Raising the received course", e);
            }
        }

        /// <summary>
        /// Raise the course on THIS peer: a switch ring at every station's port, then the arena
        /// those rings are the mouths of. Idempotent - the server applies its own copy before
        /// broadcasting, and a client that both received the broadcast and answered its own pull
        /// must not build the course twice.
        /// </summary>
        void ApplyCourse(IReadOnlyList<BreakwaterStation> course)
        {
            if (_courseBuilt) return;
            _courseBuilt = true;
            EnterStage(BuildStage.Applying);

            var theme = gameData ? gameData.ThemeManagerData : null;
            var root = new GameObject("BreakwaterCourse").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < course.Count; i++)
            {
                _course.Add(course[i]);

                var go = new GameObject($"Station_{i + 1:00}");
                go.transform.SetParent(root, false);
                var ring = go.AddComponent<RaceGateRing>();
                ring.Build(i, course[i].Position, course[i].Axis, course[i].PortRadius,
                           theme, ringBloomSeconds);
                _rings.Add(ring);
            }

            EnterStage(BuildStage.RingsRaised);
            BuildArena();
            EnterStage(BuildStage.ArenaBuilt);

            // The rings stand and the arena's lay is in flight: hand the connecting panel over to
            // the lay's own bracket and release. See BuildArena for why that hand-off has no gap.
            ReleaseArenaBuildAnnouncement();
        }

        /// <summary>
        /// Stand up the station geometry - the dishes, collars and plugs the rings are the mouths
        /// of, plus the shoals along the legs.
        ///
        /// <para><b>Every peer builds its own, from the poses it received.</b>
        /// <see cref="BreakwaterStationBuilder"/> is closed form with zero random draws, so a
        /// station is identical on every machine without replicating a prism, a seed, or a stream
        /// position - which is what lets several thousand prisms cost 340 bytes on the wire.</para>
        ///
        /// <para><b>Instantiated rather than spawned off the asset.</b> <c>Cell</c> calls
        /// <c>Spawn</c> on its <c>EnvironmentPrefab</c> asset directly, which is safe because it
        /// passes no state. We pass the COURSE, and writing per-match state onto a prefab asset is
        /// an edit to that asset in the Editor - it would persist into the project and into the
        /// next match. The generator INSTANCE is parented under this controller so it dies with
        /// the match; the prism container it produces is a separate object and is not (below).</para>
        ///
        /// <para><b>The container is left where <c>Spawn</c> puts it: the scene root, at world
        /// identity.</b> The stations were offset to WORLD coordinates before the broadcast, and
        /// a prism's emitted position is local to that container - so container-local IS world,
        /// and re-parenting it under a controller that is not itself at the origin would slide the
        /// entire arena off the course it was built for.</para>
        ///
        /// <para><b>The arena-build bracket can be released the moment this returns, and the
        /// hand-off has no gap.</b> <c>Spawn</c> generates synchronously and reaches
        /// <c>SpawnableBreakwater.LaySegmentsAsync</c>, which opens its OWN
        /// <c>BeginArenaBuild</c> bracket before its first await and streams every segment
        /// through <c>PrismTrailBuilder.LayBudgetedAsync</c> - so by the time this method returns
        /// the arena-ready gate is already being held by the lay itself, in the same frame, and
        /// the connecting panel never sees a moment with nothing pending. (The prefab's
        /// <c>layAcrossFrames</c> flag is NOT what does this and reads as if it contradicts it:
        /// that flag only steers <c>SpawnableBase.SpawnPrismTrail</c>, which this spawnable
        /// overrides past.)</para>
        /// </summary>
        void BuildArena()
        {
            if (arenaPrefab == null)
            {
                CSDebug.LogError("[Breakwater] No arena prefab assigned - the course has rings but " +
                                 "no stations, so every port is an open hoop and the mode's whole " +
                                 "fire/saw/thread choice is missing. Assign SpawnableBreakwater on " +
                                 "the controller.");
                return;
            }

            var arena = Instantiate(arenaPrefab, transform);
            arena.name = "BreakwaterArena";

            // Before Spawn, never after: Spawn runs the generation that reads it. And a COPY,
            // never the live list - ClearCourse empties _course on despawn, and a generator still
            // holding that reference would answer a later re-generation with an empty course.
            // Fourteen readonly structs is a free way to not have to know whether it kept it.
            arena.SetCourse(new List<BreakwaterStation>(_course));

            // The pads in the COURSE'S frame - the course was offset onto the cell before it was
            // broadcast, so these must be too, and every peer computes them from the same cell it
            // built the rest of the arena around.
            arena.SetSpawnPads(BreakwaterCourse.SpawnPadRing(ResolveCellCentre(),
                                                             BreakwaterCourseSettings.DefaultSpawnRingRadius));

            var container = arena.Spawn(Intensity);
            if (container) container.name = "BreakwaterArena (prisms)";
        }

        void ClearCourse()
        {
            // Withering rather than destroying: continuity of existence applies to a marker as
            // much as to a prism, and a scene teardown is the one case where it costs nothing.
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i]) _rings[i].Retire(0.4f);

            // The ARENA is deliberately NOT torn down here, and deliberately not even TRACKED
            // for it. Its prisms are POOLED, and destroying a pooled prism's container corrupts
            // the pool and kills every trail in the scene (the finding
            // Docs/ModePreview/ARCHITECTURE.md records) - so the only correct strike is the one
            // Cell owns, and this controller has no business running it. This mode replays by
            // full scene reload (UseSceneReloadForReplay), so the arena always comes down with
            // the scene that owns its pool. A field held only to be nulled here would claim the
            // arena's lifetime is managed from this class when it is not.
            _rings.Clear();
            _course.Clear();
            _runs.Clear();
            _coursePullQueue.Clear();
            _courseBuilt = false;
            _litStation = -1;
            EnterStage(BuildStage.NotStarted);
        }

        /// <summary>
        /// Which RING a pilot's next crossing is - the one place the circuit fold is applied,
        /// read by the objective arrow, the local next-station highlight and the AI's waypoint
        /// provider alike. Returns -1 when they have finished or the course has not arrived.
        ///
        /// <para>ONE resolver because those three readers must never disagree about which station
        /// a pilot is on: an arrow pointing at the outbound station while the lit ring is its
        /// inbound twin is a bug the pilot experiences as the mode lying to them, and with three
        /// copies of <c>SwitchesThreaded % something</c> it is one edit away at all times.</para>
        /// </summary>
        public int RingIndexFor(IRoundStats stats)
        {
            if (stats == null || _course.Count == 0) return -1;

            int crossing = stats.SwitchesThreaded;
            if (crossing < 0 || crossing >= CrossingTarget) return -1;   // finished

            return BreakwaterCourseSettings.RingForCrossing(crossing, _course.Count);
        }

        /// <summary>
        /// Total crossings this match's course is worth: its laid stations folded over the
        /// authored laps. Derived from the COURSE rather than from the override, for the reason
        /// <c>BreakwaterStationTurnMonitor</c> records - a shell too tight for the authored count
        /// makes the controller lay fewer, and a target naming a crossing the course cannot offer
        /// is a turn that never ends.
        /// </summary>
        public int CrossingTarget
        {
            get
            {
                if (_course.Count == 0) return 0;
                var overrides = EndConditionOverridesSO.Instance;
                int laps = overrides != null
                    ? overrides.GetBreakwaterLaps()
                    : BreakwaterCourseSettings.DefaultLaps;
                return BreakwaterCourseSettings.CrossingTarget(_course.Count, laps);
            }
        }

        /// <summary>
        /// The station <paramref name="player"/> must thread next, or null when they have finished
        /// (or the course has not arrived). Read by <c>BreakwaterObjectiveProvider</c> so the
        /// objective arrow points at the right station for the pilot looking at it.
        /// </summary>
        public bool TryGetNextStation(IPlayer player, out Transform station)
        {
            station = null;

            int index = RingIndexFor(player?.RoundStats);
            if (index < 0 || index >= _rings.Count) return false;

            var ring = _rings[index];
            if (!ring) return false;

            station = ring.transform;
            return true;
        }

        /// <summary>Stations in this match's course, 0 until it arrives.</summary>
        public int StationCount => _rings.Count;

        /// <summary>
        /// Light the LOCAL pilot's next station lime and put the previous one back to neutral.
        ///
        /// <para>Fourteen identical stations scattered through a cell is a course you have to be
        /// told the ORDER of, and here the arena makes it worse rather than better: a station's
        /// dish is a big obvious landmark that says nothing about whose turn it is, and every
        /// pilot's next station looks exactly like every other pilot's. The objective arrow points
        /// a direction; the ring itself says "this one", in the platform's existing free-pickup
        /// lime.</para>
        ///
        /// <para><b>Local only, and no networking is added.</b> Every peer builds its own copy of
        /// the course, so a <see cref="RaceGateRing"/> already belongs to exactly one viewer;
        /// painting one here changes nothing on anyone else's screen. Driven from the pilot's live
        /// progress rather than from the crossing event, so it is correct after a rollback, after
        /// a late course arrival, and for a client whose report is still in flight.</para>
        /// </summary>
        void LightLocalNextStation()
        {
            int next = RingIndexFor(gameData.LocalPlayer?.RoundStats);
            if (next >= _rings.Count) next = -1;   // finished: nothing to light
            if (next == _litStation) return;

            if (_litStation >= 0 && _litStation < _rings.Count && _rings[_litStation])
                _rings[_litStation].SetIsNextForLocalPilot(false);

            if (next >= 0 && _rings[next])
                _rings[next].SetIsNextForLocalPilot(true);

            _litStation = next;
        }

        // ── Detection ─────────────────────────────────────────────────────

        void Update()
        {
            // ABOVE the turn guard, deliberately: the whole window this watches is BEFORE the
            // turn starts, while the connecting panel holds the screen. Below the guard it could
            // never fire on the case it exists for.
            TickBuildWatchdog();

            if (_finalResultsSent) return;
            if (gameData == null || !gameData.IsTurnRunning) return;

            if (_rings.Count == 0)
            {
                // Flying with no course is silent by construction - the pilot simply is never
                // credited - so it has to announce itself. Once: this is a per-frame path.
                if (!_warnedCourseMissing)
                {
                    _warnedCourseMissing = true;
                    CSDebug.LogError("[Breakwater] Turn started with no course on this peer - " +
                                     "stations flown now cannot be credited. The SyncCourse " +
                                     "broadcast was missed and the RequestCourse pull has not " +
                                     "answered yet.");
                }
                return;
            }

            LightLocalNextStation();

            // Doubled so one dropped frame does not read as a teleport, and offset so a stationary
            // vessel's numerical jitter is never near the bound. Above this a segment is not
            // flight: a respawn or an eject sweeps a straight line across the arena that would
            // cross several stations' planes at once, and because the count IS the next index,
            // crediting one of them would ALSO skip the station in front of the pilot.
            float maxStep = maxPlausibleSpeed * Time.deltaTime * 2f + 5f;
            float maxStepSqr = maxStep * maxStep;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];

                // Only ever test a vessel THIS machine simulates. IsNetworkOwner - not
                // IsLocalUser - because the host owns every AI and a mode that used the narrower
                // test would silently never advance one (the gate the Bends records for combat
                // hits, reached from the other direction).
                if (p == null || !p.IsNetworkOwner) continue;

                var vessel = p.Vessel;
                if (vessel == null || vessel is Object uo && !uo) { Forget(p); continue; }
                var t = vessel.Transform;
                if (!t) { Forget(p); continue; }

                var stats = p.RoundStats;
                if (stats == null) { Forget(p); continue; }

                if (!_runs.TryGetValue(p, out var run))
                {
                    run = new PilotRun { Optimistic = stats.SwitchesThreaded };
                    _runs[p] = run;
                }

                Reconcile(run, stats.SwitchesThreaded);

                Vector3 cur = t.position;
                if (!run.HasLastPosition)
                {
                    run.LastPosition = cur;
                    run.HasLastPosition = true;
                    continue;
                }

                Vector3 prev = run.LastPosition;
                run.LastPosition = cur;

                // A respawn, an eject or a frame-rate hitch is not a station.
                if ((cur - prev).sqrMagnitude > maxStepSqr) continue;

                // The OPTIMISTIC crossing count folded to a ring - on the return lap crossing 14
                // is station 13, not a fifteenth station that does not exist. Guarded on the
                // CROSSING target rather than on the ring count, because a pilot on lap 2 has a
                // count above _rings.Count and is very much still racing.
                int crossing = run.Optimistic;
                if (crossing < 0 || crossing >= CrossingTarget) continue;   // finished the course

                int index = BreakwaterCourseSettings.RingForCrossing(crossing, _course.Count);
                if (index < 0 || index >= _rings.Count) continue;

                var ring = _rings[index];
                if (!ring || !ring.CrossedMouth(prev, cur)) continue;

                run.Optimistic = crossing + 1;
                run.LastReportTime = Time.time;

                // THE CROSSING, NEVER THE RING. The server validates a report against the pilot's
                // own SwitchesThreaded (`gateIndex != stats.SwitchesThreaded` rejects it), and
                // that counter counts CROSSINGS - so on the return lap the ring index and the
                // crossing diverge, and reporting the ring would have every lap-2 report rejected
                // as a duplicate of one already paid. The fold is for choosing which ring to TEST;
                // the token that travels is the crossing.
                if (IsServer) SwitchThreadScoring.Credit(stats, crossing);
                else if (p is Player netPlayer) netPlayer.ReportSwitchThreaded_ServerRpc(crossing);
            }

            PruneDepartedPilots(players);
        }

        /// <summary>
        /// Keep the optimistic count honest against the replicated one: adopt the server's value
        /// when it catches up or overtakes, and fall BACK to it when a report has gone
        /// unacknowledged for too long. Without the second half a rejected or dropped report
        /// would strand this pilot testing a station the server will never credit.
        /// </summary>
        void Reconcile(PilotRun run, int confirmed)
        {
            if (confirmed >= run.Optimistic) { run.Optimistic = confirmed; return; }
            if (Time.time - run.LastReportTime > reportResyncSeconds) run.Optimistic = confirmed;
        }

        void Forget(IPlayer p)
        {
            if (_runs.TryGetValue(p, out var run)) run.HasLastPosition = false;
        }

        void PruneDepartedPilots(List<IPlayer> players)
        {
            if (_runs.Count <= players.Count) return;

            _stalePilots.Clear();
            foreach (var key in _runs.Keys)
                if (!players.Contains(key)) _stalePilots.Add(key);
            for (int i = 0; i < _stalePilots.Count; i++) _runs.Remove(_stalePilots[i]);
        }

        // ── AI ────────────────────────────────────────────────────────────

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            base.OnCountdownTimerEnded();
            ArmRacers();
        }

        /// <summary>
        /// Point every AI at its own next station, as TWO waypoints rather than one.
        ///
        /// <para>AIPilot has no arrive-and-stop behaviour - it steers at its target forever and
        /// flies through on arrival - so handing it the port's centre produces a pilot orbiting
        /// the mouth, which is the defect PeelTheCage and Dog Fight both record. Instead: while
        /// far out, aim at a point BEHIND the station on its own axis, which lines the approach up
        /// with the port; inside <see cref="aiCommitDistance"/>, aim at a point BEYOND it, which
        /// flies the pilot through.</para>
        ///
        /// <para><b>This is also the whole of the AI's WEAPON handling, and that is the point.</b>
        /// An AI Sparrow's own prefab already authors FullAuto and SkyBurst on its
        /// <c>AIPilot.abilities</c> list, and the platform's <c>ConfigureAIPilot</c> is all that is
        /// needed to start them. A round leaves along its muzzle container's forward
        /// (<c>Gun.FireProjectile</c>) - the NOSE - and the two waypoints above hold that nose on
        /// the plug for the entire run-in, so an AI opens its own doors with NO new AI code, no
        /// aim solver and no "should I shoot this" rule: it is pointed at the station because it
        /// is flying to the station, and whatever it fires goes down the axis into the weave.</para>
        ///
        /// <para>That last step rests on one AUTHORED field: <c>Sparrow.prefab</c>'s AIPilot has
        /// <c>drift: 0</c>. Drift is the one state in which AIPilot frees the nose from the steer
        /// direction (<c>ResolveDriftLookDirection</c>) so a committed vessel can aim somewhere
        /// else - a Dolphin wants exactly that, and this hull must not have it, or an AI would
        /// spend its run-in pointed at a mass cluster off to one side and fire every round into
        /// empty space. Turning drift on for the Sparrow would silently take the mode's whole
        /// no-code AI gunnery with it.</para>
        ///
        /// <para><b>And when it fires nothing, it flies straight down that axis - which is the
        /// EYE.</b> The approach and fly-through points both sit on the station's axis, and the
        /// axis passes through the eye by construction (the plug's rakes are offset half a pitch
        /// precisely so no line crosses the centre). At 18 units against a 12.32-unit hull that is
        /// a 1.46x clearance, so an AI that never opens a single door still threads every station
        /// it reaches. <b>The pre-open eye is the DESIGNED FLOOR on AI competence</b> - the mode
        /// cannot produce an AI that is stuck, only one that is slow.</para>
        ///
        /// <para><b>Known cost:</b> <c>AIPilot.UseAbilityCoroutine</c> fires an ability on a plain
        /// duration/cooldown timer with no range test, so an AI's SkyBurst goes off on schedule
        /// whether or not a station is in front of it - expect a wasted rocket on a long leg. That
        /// is a property of the platform's AI ability driver, not of this mode, and it is left
        /// alone deliberately: a range gate here would be a second, mode-local ability driver.</para>
        ///
        /// <para>Which side is "behind" is LATCHED when the station changes, not recomputed. A
        /// pilot that drifts just past the plane without threading would otherwise see the sides
        /// swap and swing away - the same latch Dog Fight's break-off needed, for the same
        /// reason.</para>
        ///
        /// <para><b>Plus an on-the-way crystal detour.</b> Installing an external target provider
        /// replaces AIPilot's own crystal seeking outright - the trap RAMPAGE.md records - so
        /// without this an AI would fly past every crystal in the cell and never level an element.
        /// The detour is bounded by a distance budget rather than by a radius: a crystal is taken
        /// only when going through it costs less than <see cref="aiCrystalDetourSlack"/> of extra
        /// flying, so it can never pull a pilot off the course, and the test stops holding the
        /// instant the pilot is past it.</para>
        ///
        /// <para>Steering only: no ability, throttle or weapon is touched here, and the provider
        /// is per-pilot, so nothing leaks into another mode.</para>
        /// </summary>
        void ArmRacers()
        {
            Vector3 centre = ResolveCellCentre();

            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                int lockedIndex = -1;
                float side = 1f;
                float nextCrystalScan = 0f;
                Crystal detour = null;

                pilot.SetExternalTargetProvider(() =>
                {
                    var selfTf = captured.Vessel?.Transform;
                    if (selfTf == null) return centre;

                    int index = RingIndexFor(captured.RoundStats);
                    if (index < 0 || index >= _course.Count) return centre;   // finished: loiter

                    var station = _course[index];
                    Vector3 self = selfTf.position;

                    if (index != lockedIndex)
                    {
                        lockedIndex = index;
                        side = Vector3.Dot(self - station.Position, station.Axis) >= 0f ? 1f : -1f;
                        detour = null;   // a new leg is a new line; re-decide what is on the way
                    }

                    // A crystal ON THE WAY, if the cell manages any. Installing an external target
                    // provider replaces AIPilot's own crystal seeking outright (the trap RAMPAGE.md
                    // records), so without this an AI would never collect one and could never
                    // level an element - and in a mode whose comeback buff IS element levels, an
                    // AI that cannot level is one the comeback cannot reach.
                    if (aiCrystalDetourSlack > 0f)
                    {
                        if (Time.time >= nextCrystalScan)
                        {
                            nextCrystalScan = Time.time + aiCrystalScanSeconds;
                            detour = FindDetourCrystal(self, station.Position, captured.Domain);
                        }

                        // Re-tested every frame, not latched: "on the way" is a cheap monotone
                        // test that stops holding the moment the pilot is past the crystal, so
                        // the detour cannot become an orbit the way an aim point can.
                        if (detour)
                        {
                            Vector3 c = detour.transform.position;
                            if (Vector3.Distance(self, c) + Vector3.Distance(c, station.Position)
                                <= Vector3.Distance(self, station.Position) + aiCrystalDetourSlack)
                                return c;
                            detour = null;
                        }
                    }

                    return (station.Position - self).sqrMagnitude > aiCommitDistance * aiCommitDistance
                        ? station.Position + station.Axis * (side * aiApproachLead)
                        : station.Position - station.Axis * (side * aiThroughDistance);
                });
            }
        }

        /// <summary>
        /// The nearest crystal this pilot may collect that costs less than
        /// <see cref="aiCrystalDetourSlack"/> of extra flying between here and the next station.
        ///
        /// <para>Filtered to MANAGED crystals (<c>Crystal.CrystalManager</c> is set only by
        /// <c>CrystalManager.SpawnWithDomain</c>) for the reason RampageObjectiveProvider records:
        /// <c>Crystal.Active</c> also holds every lifeform heart the food web drops, and steering
        /// at those would take the pilot off the course chasing whatever just died. This cell
        /// carries no food web, so the filter buys nothing here today - it is kept because the
        /// registry is global and a heart dropped in another cell must never become a target.</para>
        /// </summary>
        Crystal FindDetourCrystal(Vector3 self, Vector3 station, Domains domain)
        {
            var crystals = Crystal.Active;
            float budget = Vector3.Distance(self, station) + aiCrystalDetourSlack;

            Crystal best = null;
            float bestCost = float.MaxValue;

            for (int i = 0, n = crystals.Count; i < n; i++)
            {
                var crystal = crystals[i];
                if (crystal == null || crystal.CrystalManager == null) continue;
                if (!crystal.CanBeCollected(domain)) continue;

                Vector3 c = crystal.transform.position;
                float cost = Vector3.Distance(self, c) + Vector3.Distance(c, station);
                if (cost > budget || cost >= bestCost) continue;

                bestCost = cost;
                best = crystal;
            }

            return best;
        }

        Vector3 ResolveCellCentre()
        {
            var cell = cellData != null ? Cell.FindByRuntimeData(cellData) : null;
            return cell ? cell.transform.position : Vector3.zero;
        }

        // ── Race end ──────────────────────────────────────────────────────

        /// <summary>
        /// Server-side winner detection. Runs on EVERY peer via SyncTurnEnd_ClientRpc, and
        /// BEFORE ExecuteServerTurnEnd -> SetupNewRound, so the latch below is set in time to
        /// suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (!rule.IsObjectiveReached(gameData, out var winningDomain)) return;

            _finalResultsSent = true;

            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);

            // Display name only - VICTORY/DEFEAT is decided by WinnerDomain. The representative
            // is the domain's lead runner, which under this mode's fold IS the pilot whose run
            // won it.
            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.SwitchesThreaded)
                .FirstOrDefault();

            rule.AssignScores(gameData, winningDomain, finishTime);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            SyncFinalScoresSnapshot(winnerRep?.Name ?? "", winningDomain);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var names = new FixedString64Bytes[count];
            var scores = new float[count];
            var domains = new int[count];
            var stations = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(statsList[i].Name);
                scores[i] = statsList[i].Score;
                domains[i] = (int)statsList[i].Domain;
                stations[i] = statsList[i].SwitchesThreaded;
            }

            SyncFinalScores_ClientRpc(names, scores, domains, stations,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains,
                                      int[] stationsThreaded, FixedString64Bytes winnerName, int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Breakwater] Client could not match RoundStats for '{sName}'.");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The station count travels too: without it a client's scoreboard reads 0 stations
                // for every remote pilot, and BuildResults' tiebreak sorts on nothing.
                stat.SwitchesThreaded = stationsThreaded[i];
            }

            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }
    }
}
