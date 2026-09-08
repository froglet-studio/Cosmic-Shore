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
    /// Switchback - the Dolphin-only gate race. A course of randomly placed and randomly
    /// oriented SWITCH rings is scattered through the cell, and every pilot flies the SAME
    /// course in ORDER. The first domain whose lead runner threads the last gate wins.
    ///
    /// <para><b>Ordered gates are what make the whole thing cheap.</b> Because a pilot may only
    /// thread their next gate, <see cref="IRoundStats.SwitchesThreaded"/> is simultaneously the
    /// score, the progress bar, the index of the ring to test this frame, and the token the
    /// server validates a report against. One replicated int carries the race; there is no
    /// per-pilot bitmask, no per-gate state, and detection is one segment test per pilot per
    /// frame rather than pilots x gates.</para>
    ///
    /// <para><b>The course travels, the seed does not.</b> The server generates it and BROADCASTS
    /// the geometry. Generating locally from a shared seed would have worked - the generator is
    /// deterministic on purpose - but it would rest on <c>Mathf.Sin</c>/<c>Acos</c> agreeing to
    /// the last bit across Mono and IL2CPP, and a single flipped branch in the walk yields a
    /// completely different course rather than a slightly different one. 20 gates is 480 bytes;
    /// the seed is kept only so a course can be reproduced in a bug report.</para>
    ///
    /// <para><b>Detection is owner-detects / server-records</b>, the platform's fourth use of the
    /// pattern (<c>Player.ReportSwitchThreaded_ServerRpc</c>). Each machine tests only the
    /// vessels it simulates - the host's human plus every AI, a client's own human - because a
    /// remote vessel's replicated position is interpolated and would miss or invent crossings.
    /// The server credits its own directly; a client forwards the index and the server
    /// re-validates it.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no new weapon, no new ability, no cell of its
    /// own. The Dolphin's shipped kit already supplies the racing: skim to bank energy, drift to
    /// carve a corner, boost down a straight - and the crystal blast, which debuffs a rival pilot
    /// in every mode since The Bends wired it, is the interference. The passive crystal seeding
    /// keeps a handful of collectable omni crystals in the cytoplasm, so the ammunition for that
    /// is on the course rather than authored by this mode.</para>
    /// </summary>
    public class SwitchbackController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag SwitchbackScoringRule.asset - the per-mode scoring strategy (end condition, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        [Header("Course")]
        [Tooltip("The cell the course is laid inside. Resolved through Cell.FindByRuntimeData, " +
                 "which answers immediately - unlike CellRuntimeDataSO.Cell, which is still null " +
                 "for the first second while the cell's own Initialize waits behind InitDelayMs.")]
        [SerializeField] CellRuntimeDataSO cellData;

        [Tooltip("Course shell, outer edge. 0.9 x the CapsuleMembrane's authored radius (1200), " +
                 "measured rather than read because Cell.MembraneRadius returns 0 until the " +
                 "membrane has spawned and the course is generated before that.")]
        [SerializeField, Min(1f)] float courseOuterRadius = 1080f;

        [Tooltip("Course shell, inner edge, used only when the cell cannot be resolved. " +
                 "Normally derived as the nucleus radius x Inner Radius Nucleus Factor.")]
        [SerializeField, Min(1f)] float courseInnerRadiusFallback = 480f;

        [Tooltip("How far outside the nucleus the course's inner shell sits. The nucleus is the " +
                 "crystal respawn volume and the Dolphin's own seeding band's inner clamp; a gate " +
                 "inside it would sit in the middle of that traffic.")]
        [SerializeField, Min(1f)] float innerRadiusNucleusFactor = 1.22f;

        [Tooltip("Where gate 1 sits, as a distance along the spawn formation's POLE. Pilots spawn " +
                 "on an EQUATORIAL ring, so every one of them is exactly sqrt(spawnRadius^2 + d^2) " +
                 "from a point on that ring's axis - which is the only placement that gives an " +
                 "identical run to the first gate. Changing the scene's spawn formation to " +
                 "Symmetric breaks that fairness.")]
        [SerializeField, Min(1f)] float firstGateDistance = 620f;

        [Tooltip("Seconds a gate's ring takes to bloom in. Detection is live at the full mouth " +
                 "from frame one; only the drawing grows into it.")]
        [SerializeField, Min(0f)] float gateBloomSeconds = 0.9f;

        [Tooltip("0 = roll a fresh course each match. Non-zero pins the seed, which is how a " +
                 "reported course is reproduced.")]
        [SerializeField] int courseSeed;

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

        int Intensity => Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4);

        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // The race ends from OnTurnEndedCustom (server) -> SyncFinalScores_ClientRpc, which
        // raises WinnerCalculated + MiniGameEnd itself. Suppressing the base game-end flow is
        // what stops SyncGameEnd_ClientRpc raising them a second time.
        protected override bool HasEndGame => false;

        readonly List<SwitchbackGate> _course = new();
        readonly List<SwitchbackGateRing> _rings = new();
        readonly Dictionary<IPlayer, PilotRun> _runs = new();
        readonly List<IPlayer> _stalePilots = new();

        bool _courseBuilt;
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

            if (IsServer) GenerateAndBroadcastCourse();
            else RequestCourse_ServerRpc();
        }

        public override void OnNetworkDespawn()
        {
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

        void GenerateAndBroadcastCourse()
        {
            // ONE authority for the gate count: the same overrides key the turn monitor reads for
            // the target. Read here rather than waiting for the monitor to publish it, so the
            // course cannot be built before the number that describes it exists - and cannot
            // disagree with it either.
            var overrides = EndConditionOverridesSO.Instance;
            int gateCount = overrides != null
                ? overrides.GetSwitchbackGateTarget()
                : EndConditionOverridesSO.DefaultSwitchbackGateTarget;

            int seed = courseSeed != 0 ? courseSeed : Random.Range(int.MinValue, int.MaxValue);

            // A shell too tight for the requested gate count is a CONFIGURATION fault, and both
            // knobs that cause it are authorable in the shipped editor (the overrides window's
            // gate target; this component's shell fields). Returning empty used to hang the match
            // outright - no rings, no scoring, no turn end, and the one error on the host console
            // only - so back off instead: halve the ask until the walk succeeds, floor 2.
            // Whatever comes back is then the AUTHORITATIVE target (see AuthoritativeGateCount),
            // so a shortened course still has a finish line rather than an unreachable one.
            List<SwitchbackGate> course = null;
            var settings = BuildSettings(gateCount);
            int ask = settings.GateCount;
            while (ask >= 2)
            {
                settings.GateCount = ask;
                course = SwitchbackCourse.Generate(seed, settings);
                if (course != null && course.Count >= ask) break;

                course = null;
                if (ask == 2) break;
                ask = Mathf.Max(2, ask / 2);
                CSDebug.LogWarning(
                    $"[Switchback] Course generation failed for {settings.GateCount} gates in shell " +
                    $"{settings.InnerRadius:F0}..{settings.OuterRadius:F0} (step " +
                    $"{settings.MinStep:F0}..{settings.MaxStep:F0}, separation " +
                    $"{settings.MinSeparation:F0}); retrying with {ask}.");
            }

            if (course == null)
            {
                // Even two gates will not fit. Nothing this mode can do but say so - loudly, and
                // with the numbers that have to change.
                CSDebug.LogError(
                    $"[Switchback] Course generation FAILED even at 2 gates in shell " +
                    $"{settings.InnerRadius:F0}..{settings.OuterRadius:F0} (step " +
                    $"{settings.MinStep:F0}..{settings.MaxStep:F0}, separation " +
                    $"{settings.MinSeparation:F0}). Widen the shell or shorten the legs.");
                ReleaseArenaBuildAnnouncement();
                return;
            }

            // The generator works about the ORIGIN; the spawn ring, the membrane and the nucleus
            // are all measured from the CELL. They coincide in the shipped scene and would stop
            // coinciding the moment anyone moved or nested the Cell - at which point the whole
            // course slides off the arena and gate 1 is no longer on the spawn ring's pole. Offset
            // once here, before the broadcast, so every peer receives world positions and the
            // generator stays a pure function of its shell.
            Vector3 centre = ResolveCellCentre();
            if (centre != Vector3.zero)
                for (int i = 0; i < course.Count; i++)
                    course[i] = new SwitchbackGate(course[i].Position + centre, course[i].Axis,
                                                   course[i].Radius);

            CSDebug.Log($"[Switchback] Course seed {seed}: {course.Count} gates, intensity {Intensity}, " +
                        $"ring radius {settings.RingRadius:F0}.");

            ApplyCourse(course);
            BroadcastCourse(course, default);
        }

        /// <summary>
        /// The gate target this match is actually run against - the course's OWN length once it
        /// exists, and the authored override only before that.
        ///
        /// <para>The turn monitor reads this rather than the overrides directly, because the two
        /// numbers must never disagree: a target naming a gate the course does not contain is
        /// unreachable, and an unreachable target is a match that cannot end. Generation backs
        /// off when a shell is too tight (above), so the course is the honest authority.</para>
        /// </summary>
        public int AuthoritativeGateCount => _courseBuilt && _course.Count > 0 ? _course.Count : 0;

        SwitchbackCourseSettings BuildSettings(int gateCount)
        {
            var cell = cellData != null ? Cell.FindByRuntimeData(cellData) : null;

            // ExpectedNucleusWorldRadius measures the CONFIG's nucleus prefab without
            // instantiating it, so unlike NucleusWorldRadius it answers correctly this early.
            float nucleus = cell != null ? cell.ExpectedNucleusWorldRadius : 0f;
            float inner = nucleus > 0f ? nucleus * innerRadiusNucleusFactor : courseInnerRadiusFallback;
            float outer = Mathf.Max(inner + 120f, courseOuterRadius);

            var s = SwitchbackCourseSettings.ForIntensity(Intensity);
            s.GateCount = Mathf.Max(2, gateCount);
            s.InnerRadius = inner;
            s.OuterRadius = outer;
            s.FirstGateDirection = Vector3.up;   // the equatorial spawn ring's pole
            s.FirstGateDistance = Mathf.Clamp(firstGateDistance, inner, outer);
            return s;
        }

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

        void BroadcastCourse(IReadOnlyList<SwitchbackGate> course, ClientRpcParams target)
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

            var course = new List<SwitchbackGate>(packed.Length / 6);
            for (int o = 0; o + 5 < packed.Length; o += 6)
                course.Add(new SwitchbackGate(
                    new Vector3(packed[o], packed[o + 1], packed[o + 2]),
                    new Vector3(packed[o + 3], packed[o + 4], packed[o + 5]),
                    ringRadius));

            ApplyCourse(course);
        }

        void ApplyCourse(IReadOnlyList<SwitchbackGate> course)
        {
            if (_courseBuilt) return;
            _courseBuilt = true;

            var theme = gameData ? gameData.ThemeManagerData : null;
            var root = new GameObject("SwitchbackCourse").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < course.Count; i++)
            {
                _course.Add(course[i]);

                var go = new GameObject($"Gate_{i + 1:00}");
                go.transform.SetParent(root, false);
                var ring = go.AddComponent<SwitchbackGateRing>();
                ring.Build(i, course[i], theme, gateBloomSeconds);
                _rings.Add(ring);
            }

            // The geometry exists: release the connecting panel.
            ReleaseArenaBuildAnnouncement();
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
        /// (or the course has not arrived). Read by <see cref="SwitchbackObjectiveProvider"/> so
        /// the objective arrow points at the right ring for the pilot looking at it.
        /// </summary>
        public bool TryGetNextGate(IPlayer player, out Transform gate)
        {
            gate = null;
            if (player?.RoundStats == null) return false;

            int index = player.RoundStats.SwitchesThreaded;
            if (index < 0 || index >= _rings.Count) return false;

            var ring = _rings[index];
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
        /// the course, so a <see cref="SwitchbackGateRing"/> already belongs to exactly one viewer;
        /// painting one here changes nothing on anyone else's screen. Driven from the pilot's live
        /// progress rather than from the crossing event, so it is correct after a rollback, after
        /// a late course arrival, and for a client whose report is still in flight.</para>
        /// </summary>
        void LightLocalNextGate()
        {
            int next = gameData.LocalPlayer?.RoundStats != null
                ? gameData.LocalPlayer.RoundStats.SwitchesThreaded
                : -1;
            if (next >= _rings.Count) next = -1;   // finished: nothing to light
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
            if (_finalResultsSent) return;
            if (gameData == null || !gameData.IsTurnRunning) return;

            if (_rings.Count == 0)
            {
                // Flying with no course is silent by construction - the pilot simply is never
                // credited - so it has to announce itself. Once: this is a per-frame path.
                if (!_warnedCourseMissing)
                {
                    _warnedCourseMissing = true;
                    CSDebug.LogError("[Switchback] Turn started with no course on this peer - " +
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
                if (index < 0 || index >= _rings.Count) continue;   // finished the course

                var ring = _rings[index];
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

                    int index = captured.RoundStats?.SwitchesThreaded ?? 0;
                    if (index < 0 || index >= _course.Count) return centre;   // finished: loiter

                    var gate = _course[index];
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
                    CSDebug.LogError($"[Switchback] Client could not match RoundStats for '{sName}'.");
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
