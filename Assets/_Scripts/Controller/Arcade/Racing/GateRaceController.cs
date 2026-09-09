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
    /// The shared machinery of a GATE RACE: a course of switch rings, flown in ORDER by every
    /// pilot, with the first domain's lead runner home the winner. Switchback flies an open
    /// chain of them and Headlong flies laps of a closed circuit; everything below is the same
    /// in both, and a second copy of it would have drifted at the first tuning pass.
    ///
    /// <para><b>Ordered gates are what make the whole thing cheap.</b> Because a pilot may only
    /// thread their next gate, <see cref="IRoundStats.SwitchesThreaded"/> is simultaneously the
    /// score, the progress bar, the index of the ring to test this frame, and the token the
    /// server validates a report against. One replicated int carries the race; there is no
    /// per-pilot bitmask, no per-gate state, and detection is one segment test per pilot per
    /// frame rather than pilots x gates. A LAPPED course changes nothing about that - the count
    /// keeps rising past the ring count and <see cref="RingIndexFor"/> wraps it, so a lap is not
    /// a thing the race has to remember either.</para>
    ///
    /// <para><b>The course travels, the seed does not.</b> The server generates it and BROADCASTS
    /// the geometry. Generating locally from a shared seed would have worked - the generators are
    /// deterministic on purpose - but it would rest on <c>Mathf.Sin</c>/<c>Acos</c> agreeing to
    /// the last bit across Mono and IL2CPP, and a single flipped branch yields a completely
    /// different course rather than a slightly different one.</para>
    ///
    /// <para><b>Detection is owner-detects / server-records</b>
    /// (<c>Player.ReportSwitchThreaded_ServerRpc</c>). Each machine tests only the vessels it
    /// simulates - the host's human plus every AI, a client's own human - because a remote
    /// vessel's replicated position is interpolated and would miss or invent crossings. The
    /// server credits its own directly; a client forwards the index and the server re-validates
    /// it against its own copy of the count, so a pilot can neither skip a gate nor be paid
    /// twice for one.</para>
    ///
    /// <para>A subclass supplies three things and inherits the rest: the mode's NAME (for logs),
    /// its COURSE (<see cref="BuildCourse"/>), and whether that course WRAPS
    /// (<see cref="LapsPerRace"/>).</para>
    /// </summary>
    public abstract class GateRaceController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag the mode's ScoringRule asset - the per-mode scoring strategy (end condition, scores, results).")]
        [SerializeField] protected ScoringRuleSO rule;

        [Header("Course")]
        [Tooltip("The cell the course is laid inside. Resolved through Cell.FindByRuntimeData, " +
                 "which answers immediately - unlike CellRuntimeDataSO.Cell, which is still null " +
                 "for the first second while the cell's own Initialize waits behind InitDelayMs.")]
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Tooltip("Course shell, outer edge. 0.9 x the CapsuleMembrane's authored radius (1200), " +
                 "measured rather than read because Cell.MembraneRadius returns 0 until the " +
                 "membrane has spawned and the course is generated before that.")]
        [SerializeField, Min(1f)] protected float courseOuterRadius = 1080f;

        [Tooltip("Course shell, inner edge, used only when the cell cannot be resolved. " +
                 "Normally derived as the nucleus radius x Inner Radius Nucleus Factor.")]
        [SerializeField, Min(1f)] protected float courseInnerRadiusFallback = 480f;

        [Tooltip("How far outside the nucleus the course's inner shell sits. The nucleus is the " +
                 "crystal respawn volume and the Dolphin's own seeding band's inner clamp; a gate " +
                 "inside it would sit in the middle of that traffic.")]
        [SerializeField, Min(1f)] protected float innerRadiusNucleusFactor = 1.22f;


        [Tooltip("Seconds a gate's ring takes to bloom in. Detection is live at the full mouth " +
                 "from frame one; only the drawing grows into it.")]
        [SerializeField, Min(0f)] float gateBloomSeconds = 0.9f;

        [Tooltip("0 = roll a fresh course each match. Non-zero pins the seed, which is how a " +
                 "reported course is reproduced.")]
        [SerializeField] protected int courseSeed;

        [Tooltip("How long the server keeps re-trying a course build that could not run yet. " +
                 "A course built from PURE GEOMETRY (Switchback, Headlong) either works on the " +
                 "first frame or never, and spends this on nothing; a course built from SCENE " +
                 "state (Skein reads the cable off the cell config) needs it, because that " +
                 "state does not exist at OnNetworkSpawn. 0 = the built-in default.")]
        [SerializeField, Min(0f)] float courseBuildTimeoutSeconds;

        /// <summary>
        /// Retry window when <see cref="courseBuildTimeoutSeconds"/> is 0.
        ///
        /// <para><b>Why a sentinel and not just an initializer.</b> The two cases are not the
        /// same and it is worth being exact, because getting it backwards costs an afternoon.
        /// A field ABSENT from an asset's YAML keeps its C# initializer - proven here by
        /// <c>GunVesselTransformer</c>, whose nine ride-tuning fields appear in no prefab and
        /// whose play-tested behaviour depends on values like <c>throttleRestPosition = 0.5f</c>.
        /// A field PRESENT with a stale value overrides the initializer, which is why
        /// <c>Cell.retireSuctionSeconds</c> carries a 0-sentinel: it is serialized as 0 in twelve
        /// scenes. (<c>Cell.PhaseTickIntervalSeconds</c> is a <c>const</c> and is not evidence
        /// either way.)</para>
        ///
        /// <para>So the initializer alone would work TODAY - the field is in no scene - and stops
        /// working the first time anyone opens a gate-race scene and saves it, because Unity then
        /// writes <c>courseBuildTimeoutSeconds: 0</c> and that 0 wins forever after. A one-attempt
        /// window is precisely the bug the retry exists to fix, restored silently by a save.</para>
        ///
        /// <para>6 s against a known ~1 s wait (<c>InitDelayMs</c>): generous, because the cost
        /// of waiting too long is a slightly later countdown and the cost of waiting too little
        /// is a match with no rings in it.</para>
        /// </summary>
        const float DefaultCourseBuildTimeoutSeconds = 6f;

        float CourseBuildTimeout =>
            courseBuildTimeoutSeconds > 0f ? courseBuildTimeoutSeconds : DefaultCourseBuildTimeoutSeconds;

        [Header("AI")]
        [Tooltip("Distance at which an AI stops lining up on its gate's axis and commits to the " +
                 "fly-through point on the far side.")]
        [SerializeField, Min(1f)] float aiCommitDistance = 260f;

        [Tooltip("How far back along the gate's axis an AI aims while lining up. AIPilot has no " +
                 "arrive-and-stop behaviour, so this point must be genuinely behind the ring or " +
                 "the pilot orbits it.")]
        [SerializeField, Min(1f)] float aiApproachLead = 300f;

        [Tooltip("How far PAST the gate the fly-through point sits. Same reason: the AI flies at " +
                 "its target and through it, so the target has to be on the other side of the mouth.")]
        [SerializeField, Min(1f)] float aiThroughDistance = 220f;

        [Tooltip("Extra distance an AI will accept flying to take a crystal ON THE WAY to its next " +
                 "gate. The crystal is the Dolphin's only blast trigger, and the external target " +
                 "provider below replaces AIPilot's own crystal seeking outright - so without this " +
                 "an AI could never fire the mode's whole interference layer. 0 disables the detour.")]
        [SerializeField, Min(0f)] float aiCrystalDetourSlack = 220f;

        [Tooltip("Seconds between an AI's scans of the live crystal registry. The chosen crystal " +
                 "is re-tested every frame; only the search for a new one is throttled.")]
        [SerializeField, Min(0.1f)] float aiCrystalScanSeconds = 0.5f;

        [Header("Detection")]
        [Tooltip("Ignore a single frame's motion longer than the fastest Dolphin could fly plus a " +
                 "margin - a respawn or an eject must never read as having threaded a gate.")]
        [SerializeField, Min(1f)] float maxPlausibleSpeed = 400f;

        [Tooltip("Seconds before an unacknowledged gate report is assumed lost and the local " +
                 "optimistic progress resyncs to the replicated value. Without this a rejected " +
                 "report would leave a client testing a gate it can never be credited for.")]
        [SerializeField, Min(0.5f)] float reportResyncSeconds = 3f;

        protected int Intensity => Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4);

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // The race ends from OnTurnEndedCustom (server) -> SyncFinalScores_ClientRpc, which
        // raises WinnerCalculated + MiniGameEnd itself. Suppressing the base game-end flow is
        // what stops SyncGameEnd_ClientRpc raising them a second time.
        protected override bool HasEndGame => false;

        // ── What a subclass supplies ──────────────────────────────────────

        /// <summary>Mode name for log lines, so a message names the mode that emitted it.</summary>
        protected abstract string ModeName { get; }

        /// <summary>
        /// Laps of the ring set that make one race. 1 = an open chain flown once (Switchback);
        /// more = a closed circuit (Headlong), where the ring count and the RACE LENGTH stop
        /// being the same number.
        /// </summary>
        protected virtual int LapsPerRace => 1;

        /// <summary>Gate-threadings that finish the race - the target the monitor ends on.</summary>
        protected int RaceLength => _rings.Count * Mathf.Max(1, LapsPerRace);

        /// <summary>
        /// Which RING a pilot on <paramref name="threaded"/> gates must fly next. The identity
        /// for an open chain; on a circuit it wraps, which is the whole of what a lap is.
        /// </summary>
        protected int RingIndexFor(int threaded) =>
            _rings.Count == 0 ? -1 : (LapsPerRace > 1 ? threaded % _rings.Count : threaded);

        /// <summary>
        /// The mode's course, in CELL-LOCAL coordinates, or null if it cannot be built.
        /// <paramref name="gateCount"/> is the authored target; a mode whose course wraps should
        /// treat it as the RACE length and lay <c>gateCount / LapsPerRace</c> rings.
        /// </summary>
        protected abstract List<RaceGate> BuildCourse(int seed, int gateCount, float inner, float outer);

        /// <summary>
        /// Why the last <see cref="BuildCourse"/> returned null, in one sentence, for the ONE
        /// error the platform prints if the retry window closes.
        ///
        /// <para>A subclass must set this rather than logging its own: <c>BuildCourse</c> is
        /// retried every frame until it works, so a <c>LogError</c> inside it is a per-frame
        /// path and would bury the console under hundreds of copies of a message that was only
        /// ever true about one frame.</para>
        /// </summary>
        protected string CourseFailureDetail { get; set; }

        /// <summary>
        /// A mode-specific AI aim point, consulted BEFORE the gate logic. Return false (the
        /// default) to fly at the next gate the ordinary way.
        ///
        /// <para>Exists because a gate race does not have to be flown in FREE FLIGHT. Skein's
        /// vessel ATTACHES to a rail, and while attached the aim point is the rail rather than
        /// the ring: aiming at a ring while riding leaves the range roughly constant as the
        /// cable corkscrews, and <c>OrbitDetector</c> trips on swept angle without progress -
        /// at which point <c>LookingAtCrystal</c> goes false, <c>ram</c> disengages, and the
        /// pilot drops from 150 to 30 u/s mid-grind. A lead point down the pilot's own rail
        /// makes the range fall every frame AND holds the bearing near the tangent, which is
        /// both halves of that problem.</para>
        ///
        /// <para>Steering only, and only while the override answers - an unattached pilot falls
        /// straight through to the gate aiming, including the crystal detour.</para>
        /// </summary>
        protected virtual bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            return false;
        }

        /// <summary>The cell shell the course is laid inside, resolved from the live cell.</summary>
        protected void ResolveShell(out float inner, out float outer)
        {
            var cell = cellData != null ? Cell.FindByRuntimeData(cellData) : null;
            // ExpectedNucleusWorldRadius measures the CONFIG's nucleus prefab without
            // instantiating it, so unlike NucleusWorldRadius it answers correctly this early.
            float nucleus = cell != null ? cell.ExpectedNucleusWorldRadius : 0f;
            inner = nucleus > 0f ? nucleus * innerRadiusNucleusFactor : courseInnerRadiusFallback;
            outer = Mathf.Max(inner + 120f, courseOuterRadius);
        }

        protected readonly List<RaceGate> _course = new();
        protected readonly List<RaceGateRing> _rings = new();
        readonly Dictionary<IPlayer, PilotRun> _runs = new();
        readonly List<IPlayer> _stalePilots = new();

        bool _courseBuilt;

        // Course-build retry, server only. The seed is captured ONCE so a course is never a
        // function of how many frames the scene took to become answerable.
        int _pendingCourseSeed;
        bool _awaitingCourse;
        float _courseRetryDeadline;
        bool _finalResultsSent;
        bool _arenaBuildAnnounced;
        bool _warnedCourseMissing;
        int _litGate = -1;

        /// <summary>Per-pilot detection state, on the machine that simulates that pilot.</summary>
        class PilotRun
        {
            public Vector3 LastPosition;
            public bool HasLastPosition;

            /// <summary>
            /// Gates this machine BELIEVES the pilot has threaded. On the server it tracks the
            /// authoritative stat exactly; on a client it may run ahead of the replicated value
            /// while a report is in flight, which is the point - without it a pilot flying a
            /// boosted leg during one round trip would be tested against a gate they have
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

            // This mode lays NO prisms, so the connecting panel's arena-ready gate has nothing to
            // observe and would release the moment it opened - and a pilot released before the
            // course arrives flies gate 1 (which sits straight ahead of every spawn point) and is
            // credited nothing, permanently, with nothing on screen to say why. Announce the
            // pending build so the panel holds through the generation + broadcast, exactly as
            // SkimRaceController does through its seed wait.
            _arenaBuildAnnounced = true;
            PrismTrailBuilder.BeginArenaBuild();

            _awaitingCourse = false;

            if (IsServer) BeginCourseGeneration();
            else RequestCourse_ServerRpc();
        }

        public override void OnNetworkDespawn()
        {
            _awaitingCourse = false;
            ReleaseArenaBuildAnnouncement();
            ClearCourse();
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Close the BeginArenaBuild bracket exactly once - when the course lands, when
        /// generation gives up, or on despawn, whichever comes first. A bracket left open
        /// wedges the connecting panel on a build that is never going to happen.
        /// </summary>
        void ReleaseArenaBuildAnnouncement()
        {
            if (!_arenaBuildAnnounced) return;
            _arenaBuildAnnounced = false;
            PrismTrailBuilder.EndArenaBuild();
        }

        // ── Course ────────────────────────────────────────────────────────

        /// <summary>
        /// Start building the course, and keep trying until it works or the window closes.
        ///
        /// <para><b>Why a window and not one attempt.</b> A gate race whose course is PURE
        /// GEOMETRY (Switchback's walk, Headlong's relaxation) is answerable on the frame this
        /// runs and a retry costs it nothing. A gate race whose course is a property of the
        /// SCENE is not: Skein's rings sit on the cable authored on the cell config, and
        /// <c>Cell</c> does not latch its config until <c>Initialize</c> runs on
        /// <c>OnInitializeGame</c>, a full second after <c>OnNetworkSpawn</c>. With a single
        /// attempt that is not a slow build, it is a build that never happens again - no rings,
        /// no scoring, no turn end, and one error on the host console. Skein shipped exactly
        /// that.</para>
        ///
        /// <para>The seed is drawn ONCE, here, so retrying cannot change which course this match
        /// gets: a course that depended on how many frames the cell took to answer would differ
        /// between two runs of the same build for no reason a player could see.</para>
        /// </summary>
        void BeginCourseGeneration()
        {
            _pendingCourseSeed = courseSeed != 0 ? courseSeed : Random.Range(int.MinValue, int.MaxValue);
            _courseRetryDeadline = Time.time + CourseBuildTimeout;
            _awaitingCourse = true;
            TickCourseGeneration();
        }

        /// <summary>One attempt, plus the decision to keep waiting or to give up loudly.</summary>
        void TickCourseGeneration()
        {
            if (!_awaitingCourse) return;

            if (TryGenerateAndBroadcastCourse())
            {
                _awaitingCourse = false;
                return;
            }

            if (Time.time < _courseRetryDeadline) return;

            _awaitingCourse = false;

            // Nothing this mode can do but say so - loudly, and with the numbers that have to
            // change. Returning silently used to hang the match outright: no rings, no
            // scoring, no turn end, and one error on the host console only.
            ResolveShell(out float inner, out float outer);
            CSDebug.LogError(
                $"[{ModeName}] Course generation FAILED for {AuthoredGateTarget()} gates in " +
                $"shell {inner:F0}..{outer:F0} after {CourseBuildTimeout:F1}s of retries. " +
                (string.IsNullOrEmpty(CourseFailureDetail)
                    ? "Widen the shell or shorten the course."
                    : CourseFailureDetail));
            ReleaseArenaBuildAnnouncement();
        }

        /// <summary>The attempt itself. False means "not this frame" - the caller decides
        /// whether that is a wait or a failure.</summary>
        bool TryGenerateAndBroadcastCourse()
        {
            // ONE authority for the race length: the same overrides key the turn monitor reads
            // for the target. Read here rather than waiting for the monitor to publish it, so the
            // course cannot be built before the number that describes it exists - and cannot
            // disagree with it either.
            int gateCount = AuthoredGateTarget();
            int seed = _pendingCourseSeed;

            ResolveShell(out float inner, out float outer);
            List<RaceGate> course = BuildCourse(seed, gateCount, inner, outer);

            if (course == null || course.Count == 0) return false;

            // The generators work about the ORIGIN; the spawn ring, the membrane and the nucleus
            // are all measured from the CELL. They coincide in the shipped scenes and would stop
            // coinciding the moment anyone moved or nested the Cell - at which point the whole
            // course slides off the arena and gate 0 is no longer on the spawn ring's pole.
            // Offset once here, before the broadcast, so every peer receives world positions and
            // the generators stay pure functions of their shell.
            Vector3 centre = ResolveCellCentre();
            if (centre != Vector3.zero)
                for (int i = 0; i < course.Count; i++)
                    course[i] = new RaceGate(course[i].Position + centre, course[i].Axis,
                                             course[i].Radius);

            CSDebug.Log($"[{ModeName}] Course seed {seed}: {course.Count} rings, " +
                        $"{LapsPerRace} lap(s), intensity {Intensity}.");

            ApplyCourse(course);
            BroadcastCourse(course, default);
            return true;
        }

        /// <summary>
        /// The gate target this match is actually run against - the course's OWN length once it
        /// exists, and the authored override only before that.
        ///
        /// <para>The turn monitor reads this rather than the overrides directly, because the two
        /// numbers must never disagree: a target naming a gate the course does not contain is
        /// unreachable, and an unreachable target is a match that cannot end. A course can come
        /// out shorter than asked (Switchback backs off when a shell is too tight) or longer
        /// (Headlong rounds its ring count up so a lap is whole), so the COURSE is the honest
        /// authority and this - laps included - is the number the race is run against.</para>
        /// </summary>
        public int AuthoritativeGateCount => _courseBuilt && _course.Count > 0 ? RaceLength : 0;

        /// <summary>
        /// The authored race length, before the course has had its say - the mode's own key in
        /// <see cref="EndConditionOverridesSO"/> (FrogletTools &gt; Game Modes &gt; End Game
        /// Conditions; never a per-scene field).
        ///
        /// <para>Abstract rather than defaulted, and public rather than protected, for the same
        /// reason: <c>RaceGateTurnMonitor</c> asks the CONTROLLER instead of reading a key of its
        /// own, so the monitor never has to know which mode it is monitoring - and a new gate
        /// race cannot silently inherit another mode's finish line.</para>
        /// </summary>
        public abstract int AuthoredGateTarget();


        [ServerRpc(RequireOwnership = false)]
        void RequestCourse_ServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer || _course.Count == 0) return;

            // Targeted reply, mirroring MultiplayerMiniGameControllerBase's config pull: a client
            // that spawned after the broadcast has no other way to learn the course, and NGO only
            // holds a message for an unspawned object for a few seconds.
            BroadcastCourse(_course, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
            });
        }

        void BroadcastCourse(IReadOnlyList<RaceGate> course, ClientRpcParams target)
        {
            // Six floats per gate, interleaved into one array: the same primitive-array shape the
            // final-score snapshots use, which is the serialization this project has proven.
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
            SyncCourse_ClientRpc(packed, course[0].Radius, target);
        }

        [ClientRpc]
        void SyncCourse_ClientRpc(float[] packed, float ringRadius, ClientRpcParams rpcParams = default)
        {
            if (IsServer) return;   // the server laid its own copy before broadcasting

            var course = new List<RaceGate>(packed.Length / 6);
            for (int o = 0; o + 5 < packed.Length; o += 6)
                course.Add(new RaceGate(
                    new Vector3(packed[o], packed[o + 1], packed[o + 2]),
                    new Vector3(packed[o + 3], packed[o + 4], packed[o + 5]),
                    ringRadius));

            ApplyCourse(course);
        }

        void ApplyCourse(IReadOnlyList<RaceGate> course)
        {
            if (_courseBuilt) return;
            _courseBuilt = true;

            var theme = gameData ? gameData.ThemeManagerData : null;
            var root = new GameObject($"{ModeName}Course").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < course.Count; i++)
            {
                _course.Add(course[i]);

                var go = new GameObject($"Gate_{i + 1:00}");
                go.transform.SetParent(root, false);
                var ring = go.AddComponent<RaceGateRing>();
                ring.Build(i, course[i], theme, gateBloomSeconds, FindCoincidentRing(course, i));
                _rings.Add(ring);
            }

            // The geometry exists: release the connecting panel.
            ReleaseArenaBuildAnnouncement();
        }

        /// <summary>
        /// An EARLIER gate standing in exactly this place, or null.
        ///
        /// <para>An open chain may legitimately visit one point twice: Skein's course opens and
        /// closes on the same spine collar, so its first and last gates are one hoop. Drawn as
        /// two rings that is a duplicate object, and the consequence is not cosmetic - the
        /// highlight becomes invisible, because lighting one lime leaves its neutral twin drawn
        /// in the same place and the renderer picks between them. <see cref="RaceGateRing"/>
        /// draws one and forwards the other.</para>
        ///
        /// <para>A LAPPED course (Headlong) never lands here: it stores one ring per index and
        /// <see cref="RingIndexFor"/> wraps the count, so its repeats are laps rather than
        /// duplicate gates. The tolerance is a unit rather than an epsilon because the two
        /// positions come from the SAME expression when they coincide at all - anything within
        /// a unit of another gate is one gate, and two distinct gates a unit apart would be a
        /// course bug in their own right.</para>
        /// </summary>
        RaceGateRing FindCoincidentRing(IReadOnlyList<RaceGate> course, int index)
        {
            for (int j = 0; j < index && j < _rings.Count; j++)
            {
                if ((course[j].Position - course[index].Position).sqrMagnitude > 1f) continue;
                if (Mathf.Abs(course[j].Radius - course[index].Radius) > 1f) continue;
                if (_rings[j]) return _rings[j];
            }
            return null;
        }

        void ClearCourse()
        {
            // Withering rather than destroying: continuity of existence applies to a marker as
            // much as to a prism, and a scene teardown is the one case where it costs nothing.
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i]) _rings[i].Retire(0.4f);

            _rings.Clear();
            _course.Clear();
            _runs.Clear();
            _courseBuilt = false;
            _litGate = -1;
        }

        /// <summary>
        /// The gate <paramref name="player"/> must thread next, or null when they have finished
        /// (or the course has not arrived). Read by <see cref="RaceGateObjectiveProvider"/> so
        /// the objective arrow points at the right ring for the pilot looking at it.
        /// </summary>
        public bool TryGetNextGate(IPlayer player, out Transform gate)
        {
            gate = null;
            if (player?.RoundStats == null) return false;

            int index = player.RoundStats.SwitchesThreaded;
            if (index < 0 || index >= RaceLength) return false;

            var ring = _rings[RingIndexFor(index)];
            if (!ring) return false;

            gate = ring.transform;
            return true;
        }

        /// <summary>Gates in this match's course, 0 until it arrives.</summary>
        public int GateCount => _rings.Count;

        /// <summary>
        /// Light the LOCAL pilot's next gate lime and put the previous one back to neutral.
        ///
        /// <para>Twenty identical rings scattered through a cell is a course you have to be told
        /// the ORDER of. The objective arrow points a direction but not at a specific ring, and at
        /// the far end of a leg several line up behind one another - so the gate itself says
        /// "this one", in the platform's existing free-pickup lime.</para>
        ///
        /// <para><b>Local only, and no networking is added.</b> Every peer builds its own copy of
        /// the course, so a <see cref="RaceGateRing"/> already belongs to exactly one viewer;
        /// painting one here changes nothing on anyone else's screen. Driven from the pilot's live
        /// progress rather than from the crossing event, so it is correct after a rollback, after
        /// a late course arrival, and for a client whose report is still in flight.</para>
        /// </summary>
        void LightLocalNextGate()
        {
            int next = gameData.LocalPlayer?.RoundStats != null
                ? gameData.LocalPlayer.RoundStats.SwitchesThreaded
                : -1;
            if (next >= RaceLength) next = -1;   // finished: nothing to light
            if (next >= 0) next = RingIndexFor(next);
            if (next == _litGate) return;

            if (_litGate >= 0 && _litGate < _rings.Count && _rings[_litGate])
                _rings[_litGate].SetIsNextForLocalPilot(false);

            if (next >= 0 && _rings[next])
                _rings[next].SetIsNextForLocalPilot(true);

            _litGate = next;
        }

        // ── Detection ─────────────────────────────────────────────────────

        void Update()
        {
            // Ahead of every guard below: the course is built while the turn has NOT started
            // (that is what the arena-build announcement is holding the connecting panel for),
            // so a retry gated on IsTurnRunning would never run.
            if (_awaitingCourse) TickCourseGeneration();

            if (_finalResultsSent) return;
            if (gameData == null || !gameData.IsTurnRunning) return;

            if (_rings.Count == 0)
            {
                // Flying with no course is silent by construction - the pilot simply is never
                // credited - so it has to announce itself. Once: this is a per-frame path.
                if (!_warnedCourseMissing)
                {
                    _warnedCourseMissing = true;
                    CSDebug.LogError($"[{ModeName}] Turn started with no course on this peer - " +
                                     "gates flown now cannot be credited. The SyncCourse broadcast " +
                                     "was missed and the RequestCourse pull has not answered yet.");
                }
                return;
            }

            LightLocalNextGate();

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

                // A respawn, an eject or a frame-rate hitch is not a gate.
                if ((cur - prev).sqrMagnitude > maxStepSqr) continue;

                int index = run.Optimistic;
                if (index < 0 || index >= RaceLength) continue;   // finished the course

                var ring = _rings[RingIndexFor(index)];
                if (!ring || !ring.CrossedMouth(prev, cur)) continue;

                run.Optimistic = index + 1;
                run.LastReportTime = Time.time;

                if (IsServer) SwitchThreadScoring.Credit(stats, index);
                else if (p is Player netPlayer) netPlayer.ReportSwitchThreaded_ServerRpc(index);
            }

            PruneDepartedPilots(players);
        }

        /// <summary>
        /// Keep the optimistic count honest against the replicated one: adopt the server's value
        /// when it catches up or overtakes, and fall BACK to it when a report has gone
        /// unacknowledged for too long. Without the second half a rejected or dropped report
        /// would strand this pilot testing a gate the server will never credit.
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
        /// Point every AI at its own next gate, as TWO waypoints rather than one.
        ///
        /// <para>AIPilot has no arrive-and-stop behaviour - it steers at its target forever and
        /// flies through on arrival - so handing it the ring's centre produces a pilot orbiting
        /// the hoop, which is the defect PeelTheCage and Dog Fight both record. Instead: while
        /// far out, aim at a point BEHIND the ring on its own axis, which lines the approach up
        /// with the mouth; inside the commit distance, aim at a point BEYOND it, which flies the
        /// pilot through.</para>
        ///
        /// <para>Which side is "behind" is LATCHED when the gate changes, not recomputed. A pilot
        /// that drifts just past the plane without threading would otherwise see the sides swap
        /// and swing away - the same latch Dog Fight's break-off needed, for the same reason.</para>
        ///
        /// <para><b>Plus an on-the-way crystal detour.</b> Installing an external target provider
        /// replaces AIPilot's own crystal seeking outright - the trap The Bends records - and a
        /// crystal is the Dolphin's ONLY blast trigger, so a racing AI would otherwise never fire
        /// the mode's whole interference layer. The detour is bounded by a distance budget rather
        /// than by a radius: a crystal is taken only when going through it costs less than
        /// <see cref="aiCrystalDetourSlack"/> of extra flying, so it can never pull a pilot off
        /// the course, and the test stops holding the instant the pilot is past it.</para>
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

                    // A mode whose vessel is ATTACHED to geometry aims at the geometry, not the
                    // ring. Asked first, every frame, so the answer can change the instant the
                    // pilot latches on or launches off.
                    if (TryOverrideAim(captured, out Vector3 overridden)) return overridden;

                    int index = captured.RoundStats?.SwitchesThreaded ?? 0;
                    if (index < 0 || index >= RaceLength) return centre;   // finished: loiter

                    // RingIndexFor, not the raw index: on a lapped circuit an AI reading
                    // _course[index] straight would run off the end of the list after one lap
                    // and loiter for the rest of the race. lockedIndex stays RAW on purpose -
                    // arriving at the same ring on the next lap IS a new leg.
                    var gate = _course[RingIndexFor(index)];
                    Vector3 self = selfTf.position;

                    if (index != lockedIndex)
                    {
                        lockedIndex = index;
                        side = Vector3.Dot(self - gate.Position, gate.Axis) >= 0f ? 1f : -1f;
                        detour = null;   // a new leg is a new line; re-decide what is on the way
                    }

                    // A crystal ON THE WAY, if there is one. Installing an external target
                    // provider replaces AIPilot's own crystal seeking outright (the trap Bends
                    // records), so without this an AI would never collect one, never fire the
                    // blast, and the mode's interference layer would be human-only.
                    if (aiCrystalDetourSlack > 0f)
                    {
                        if (Time.time >= nextCrystalScan)
                        {
                            nextCrystalScan = Time.time + aiCrystalScanSeconds;
                            detour = FindDetourCrystal(self, gate.Position, captured.Domain);
                        }

                        // Re-tested every frame, not latched: "on the way" is a cheap monotone
                        // test that stops holding the moment the pilot is past the crystal, so
                        // the detour cannot become an orbit the way an aim point can.
                        if (detour)
                        {
                            Vector3 c = detour.transform.position;
                            if (Vector3.Distance(self, c) + Vector3.Distance(c, gate.Position)
                                <= Vector3.Distance(self, gate.Position) + aiCrystalDetourSlack)
                                return c;
                            detour = null;
                        }
                    }

                    return (gate.Position - self).sqrMagnitude > aiCommitDistance * aiCommitDistance
                        ? gate.Position + gate.Axis * (side * aiApproachLead)
                        : gate.Position - gate.Axis * (side * aiThroughDistance);
                });
            }
        }

        /// <summary>
        /// The nearest crystal this pilot may collect that costs less than
        /// <see cref="aiCrystalDetourSlack"/> of extra flying between here and the next gate.
        ///
        /// <para>Filtered to MANAGED crystals (<c>Crystal.CrystalManager</c> is set only by
        /// <c>CrystalManager.SpawnWithDomain</c>) for the reason RampageObjectiveProvider
        /// records: <c>Crystal.Active</c> also holds every lifeform heart the food web drops and
        /// every crystal a Dolphin seeds, and steering at those would take the pilot off the
        /// course chasing whatever just died.</para>
        /// </summary>
        Crystal FindDetourCrystal(Vector3 self, Vector3 gate, Domains domain)
        {
            var crystals = Crystal.Active;
            float budget = Vector3.Distance(self, gate) + aiCrystalDetourSlack;

            Crystal best = null;
            float bestCost = float.MaxValue;

            for (int i = 0, n = crystals.Count; i < n; i++)
            {
                var crystal = crystals[i];
                if (crystal == null || crystal.CrystalManager == null) continue;
                if (!crystal.CanBeCollected(domain)) continue;

                Vector3 c = crystal.transform.position;
                float cost = Vector3.Distance(self, c) + Vector3.Distance(c, gate);
                if (cost > budget || cost >= bestCost) continue;

                bestCost = cost;
                best = crystal;
            }

            return best;
        }

        protected Vector3 ResolveCellCentre()
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
            var gates = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(statsList[i].Name);
                scores[i] = statsList[i].Score;
                domains[i] = (int)statsList[i].Domain;
                gates[i] = statsList[i].SwitchesThreaded;
            }

            SyncFinalScores_ClientRpc(names, scores, domains, gates,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains,
                                      int[] gatesThreaded, FixedString64Bytes winnerName, int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[{ModeName}] Client could not match RoundStats for '{sName}'.");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The gate count travels too: without it a client's scoreboard reads 0 gates for
                // every remote pilot, and BuildResults' tiebreak sorts on nothing.
                stat.SwitchesThreaded = gatesThreaded[i];
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
