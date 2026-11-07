using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Decoy Run - the Jacks-only race, and the Jacks' TIME card (DECOY_RUN.md). The Jacks fly at the Rhino rig's plain
    /// 50 u/s, far too slow to race; the race is their herd-and-travel cycle. Swap to the jacks (the hull holds still), run
    /// them on the board until one has laid the decoy past the ring and their midpoint sits behind it on its line, take off
    /// (the hull jumps to that midpoint: a teleport, which threads nothing) and fly to the decoy (a real flight, which
    /// threads the ring). Rings come in groups a cycle can take: doors at I1, skewers (two rings on one line, one zip) at
    /// I2, skewers on rolled flats at I3, a skewer and a hairpin with Time 5 (the second decoy) at I4.
    ///
    /// <para><b>What the mode adds:</b> the rings, the CHALK (two dotted rails of super-shielded mass beside every leg, on
    /// that leg's flat, so the herding view shows which way the next flat lies) and the AI's coach. <b>What it does not:</b>
    /// nothing about the vessel. The jacks' speed, the take-off, the decoys, the travel and the seat swap are the shipped
    /// kit, and the AI drives them through the same calls a person's thumbs and triggers make
    /// (<see cref="IJacksRaceCoach"/>).</para>
    ///
    /// <para><b>Every peer lays the same chalk</b> from the broadcast rings (<see cref="DecoyRunCourse.ChalkFor"/>), so
    /// nothing new is replicated. Super-shielded mass is never dragged by the net, so the jacks cannot herd the chalk away.</para>
    /// </summary>
    public class DecoyRunController : GateRaceController, IPlayerSpawnLine, IJacksRaceCoach
    {
        [Header("Decoy Run")]
        [Tooltip("The prism every chalk dot is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_decoy_run_assets.py. Unset = no chalk, and a warning.")]
        [SerializeField] Prism chalkPrefab;

        [Tooltip("Seconds an AI waits, flying and rolling, for the whole group to lie on its board before it herds what lies there.")]
        [SerializeField, Min(0f)] float aiGroupWaitSeconds = 2.5f;

        const string ChalkOwnerId = "DecoyRun";

        readonly List<Vector3> _chalkScratch = new();
        readonly List<Prism> _chalk = new();
        readonly Dictionary<JacksExecutor, IPlayer> _coached = new();
        readonly Dictionary<IPlayer, JacksExecutor> _jacksOf = new();
        readonly Dictionary<JacksExecutor, float> _waitingSince = new();
        Transform _chalkRoot;
        DecoyRunSettings _settings;

        protected override string ModeName => "Decoy Run";

        protected override RaceCourseSource CreateCourseSource() => new DecoyRunCourseSource();

        public override void OnNetworkDespawn()
        {
            foreach (var jacks in _coached.Keys)
                if (jacks && ReferenceEquals(jacks.RaceCoach, this)) jacks.RaceCoach = null;
            _coached.Clear();
            _jacksOf.Clear();
            _waitingSince.Clear();
            ClearChalk();
            base.OnNetworkDespawn();
        }

        // ── The start line: abreast on flat 0, boards level ──────────────

        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            // The course works about the ORIGIN; the cell places it (the same offset the course broadcast applies).
            poses = DecoyRunCourse.StartPoses(count);
            Vector3 centre = ResolveCellCentre();
            for (int i = 0; i < poses.Length; i++) poses[i].position += centre;
            return true;
        }

        // ── The chalk (every peer, from the broadcast rings) ─────────────

        protected override void OnCourseRaised()
        {
            ClearChalk();
            _settings = DecoyRunCourse.ForIntensity(Intensity);
            _settings.RingCount = _course.Count;
            if (!chalkPrefab)
            {
                CSDebug.LogWarning("[DecoyRun] no chalk prefab on the controller - the flats are not drawn.");
                return;
            }
            DecoyRunCourse.ChalkFor(_course, _settings, DecoyRunCourse.StartStandoff, _chalkScratch);
            _chalkRoot = new GameObject("DecoyRunChalk").transform;
            for (int i = 0; i < _chalkScratch.Count; i++)
            {
                var prism = PrismTrailBuilder.LayOne(chalkPrefab,
                    new PrismLay(new SpawnPoint(_chalkScratch[i], Quaternion.identity, Vector3.one * DecoyRunCourse.ChalkSize),
                                 Domains.Blue, PrismKind.SuperShielded),
                    _chalkRoot, new Trail(), ChalkOwnerId, watchForReveal: true);
                if (prism) _chalk.Add(prism);
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[DecoyRun] intensity {Intensity}: {_course.Count} rings in groups of {_settings.Group}, " +
                $"{(_settings.TiltEachGroup ? "a flat per group" : "one flat")}, {_chalk.Count} chalk dots.");
        }

        void ClearChalk()
        {
            for (int i = 0; i < _chalk.Count; i++)
            {
                var prism = _chalk[i];
                if (prism && !prism.destroyed && prism.ownerID == ChalkOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _chalk.Clear();
            if (_chalkRoot) Destroy(_chalkRoot.gameObject);
            _chalkRoot = null;
        }

        // ── The AI (server): the coach, the aim and the roll ─────────────

        protected override void OnRacerArmed(IPlayer pilot)
        {
            var tf = pilot?.Vessel?.Transform;
            var jacks = tf ? tf.GetComponentInChildren<JacksExecutor>(true) : null;
            if (!jacks) return;
            _coached[jacks] = pilot;
            _jacksOf[pilot] = jacks;
            jacks.RaceCoach = this;
        }

        /// <summary>The armed AI's Jacks, or null (a human, or not armed yet).</summary>
        JacksExecutor JacksOf(IPlayer pilot) =>
            pilot != null && _jacksOf.TryGetValue(pilot, out var jacks) && jacks ? jacks : null;

        bool TryNextRing(IPlayer pilot, out int ring)
        {
            ring = -1;
            if (_course.Count == 0 || pilot == null) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            ring = RingIndexFor(threaded);
            return ring >= 0 && ring < _course.Count;
        }

        /// <summary>
        /// The next cycle for an AI Jacks: the rings one cycle is cut to take from its next ring (a door, a skewer, and a
        /// ring more with the second decoy; <see cref="DecoyRunCourse.RingsACycleTakes"/>) that lie on its board. It waits,
        /// flying and rolling onto the flat, up to <see cref="aiGroupWaitSeconds"/> for all of them to lie there, then
        /// herds what does.
        /// </summary>
        public bool TryNextCycle(JacksExecutor jacks, out JacksRacePlan plan)
        {
            plan = default;
            if (!jacks || !_coached.TryGetValue(jacks, out var pilot) || !TryNextRing(pilot, out int next)) return false;
            int count = _course.Count;
            int zips = jacks.HasTwoDecoys ? 2 : 1;
            int want = DecoyRunCourse.RingsACycleTakes(next, zips, _settings);
            Vector3 hull = jacks.RaceHullPosition, up = jacks.RaceBoardUp;
            int onBoard = DecoyRunCourse.RingsOnBoard(hull, up, _course, next, count, want, _settings);
            if (onBoard == 0) { _waitingSince.Remove(jacks); return false; }
            if (onBoard < want)
            {
                float now = Time.time;
                if (!_waitingSince.TryGetValue(jacks, out float since)) _waitingSince[jacks] = since = now;
                if (now - since < aiGroupWaitSeconds) return false;
            }
            if (DecoyRunCourse.PlanCycle(hull, up, _course, next, count, zips, onBoard, _settings, out plan, out _) == 0) return false;
            _waitingSince.Remove(jacks);
            return true;
        }

        /// <summary>
        /// Flying, an AI Jacks steers at the next ring (and on through it); mid-cycle it holds its heading, because the
        /// hull is held on the board or travelling and the steering must not swing the board away from the flat.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var jacks = JacksOf(pilot);
            var tf = pilot?.Vessel?.Transform;
            if (!jacks || tf == null || !TryNextRing(pilot, out int next)) return false;
            if (jacks.RaceCycleBusy) { target = tf.position + tf.forward * 200f; return true; }
            var gate = _course[next];
            target = gate.Position + gate.Axis * DecoyRunCourse.Overshoot;
            return true;
        }

        /// <summary>
        /// Flying, the AI rolls its board onto the next ring's flat (belly square to the flat, whichever face it already
        /// shows): the one-fish-up-one-down roll a person makes before swapping to the jacks.
        /// </summary>
        protected override bool OverrideBellyTarget(IPlayer pilot, out Vector3 worldDirection)
        {
            worldDirection = default;
            var jacks = JacksOf(pilot);
            var tf = pilot?.Vessel?.Transform;
            if (!jacks || tf == null || jacks.RaceCycleBusy || !TryNextRing(pilot, out int next)) return false;
            Vector3 n = DecoyRunCourse.FlatNormal(_course, next, _settings);
            worldDirection = Vector3.Dot(tf.up, n) >= 0f ? -n : n;
            return true;
        }
    }
}
