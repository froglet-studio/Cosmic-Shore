using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Perch Run - the Goby-only race, and the Goby's TIME card (PERCH_RUN.md). The Goby cruises slowly and DARTS: a charged
    /// length along the nose, flown in one second, stopping on the first block on its line. A dart costs the same second
    /// however far it goes, so the race is how few darts a course takes, and the course makes the dart's two numbers -
    /// its LENGTH and its LINE - the decisions: open rings at I1, hairpins at I2 (a full charge overshoots out of reach of
    /// the next ring), coral blockers at I3 (a dart at the ring's centre stops on the head; a rim shot clears it), and
    /// doubles at I4 (two rings on one line that one dart threads).
    ///
    /// <para><b>What the mode adds:</b> the rings, the CORAL (a super-shielded block per blocked leg, square to its leg) and
    /// the AI's coach. <b>What it does not:</b> nothing about the vessel. The dart, its charge, its stop on a block, the
    /// perches and the Pistol are the shipped kit; the AI fires the dart the button fires (<see cref="IGobyRaceCoach"/>).
    /// The one Goby change that came with this race is the reach ring a charging dart draws where it will stop
    /// (GobyPistolDuo.TryDartReach), which serves every mode the Goby flies.</para>
    ///
    /// <para><b>Every peer lays the same coral</b> from the broadcast rings (<see cref="PerchRunCourse.CoralFor"/>), so
    /// nothing new is replicated.</para>
    /// </summary>
    public class PerchRunController : GateRaceController, IPlayerSpawnLine, IGobyRaceCoach
    {
        [Header("Perch Run")]
        [Tooltip("The prism every coral head is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_perch_run_assets.py. Unset = no coral, and a warning.")]
        [SerializeField] Prism coralPrefab;

        const string CoralOwnerId = "PerchRun";

        readonly List<Coral> _coral = new();
        readonly List<Prism> _coralPrisms = new();
        readonly Dictionary<GobyPistolDuo, IPlayer> _coached = new();
        Transform _coralRoot;

        protected override string ModeName => "Perch Run";

        protected override RaceCourseSource CreateCourseSource() => new PerchRunCourseSource();

        public override void OnNetworkDespawn()
        {
            foreach (var goby in _coached.Keys)
                if (goby && ReferenceEquals(goby.RaceCoach, this)) goby.RaceCoach = null;
            _coached.Clear();
            ClearCoral();
            base.OnNetworkDespawn();
        }

        // ── The start line: abreast, the start standoff behind ring 0 ────

        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            // The course works about the ORIGIN; the cell places it (the same offset the course broadcast applies).
            poses = PerchRunCourse.StartPoses(count);
            Vector3 centre = ResolveCellCentre();
            for (int i = 0; i < poses.Length; i++) poses[i].position += centre;
            return true;
        }

        // ── The coral (every peer, from the broadcast rings) ─────────────

        protected override void OnCourseRaised()
        {
            ClearCoral();
            var settings = PerchRunCourse.ForIntensity(Intensity);
            settings.RingCount = _course.Count;
            PerchRunCourse.CoralFor(_course, settings, _coral);
            if (_coral.Count == 0) return;
            if (!coralPrefab)
            {
                CSDebug.LogWarning("[PerchRun] no coral prefab on the controller - the blockers are not laid.");
                return;
            }
            _coralRoot = new GameObject("PerchRunCoral").transform;
            for (int i = 0; i < _coral.Count; i++)
            {
                var c = _coral[i];
                var prism = PrismTrailBuilder.LayOne(coralPrefab,
                    new PrismLay(new SpawnPoint(c.Position, c.Rotation, Vector3.one * c.Size), Domains.Blue, PrismKind.SuperShielded),
                    _coralRoot, new Trail(), CoralOwnerId, watchForReveal: true);
                if (prism) _coralPrisms.Add(prism);
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[PerchRun] intensity {Intensity}: {_course.Count} rings, {_coralPrisms.Count} coral heads.");
        }

        void ClearCoral()
        {
            for (int i = 0; i < _coralPrisms.Count; i++)
            {
                var prism = _coralPrisms[i];
                if (prism && !prism.destroyed && prism.ownerID == CoralOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _coralPrisms.Clear();
            if (_coralRoot) Destroy(_coralRoot.gameObject);
            _coralRoot = null;
        }

        // ── The AI (server): the coach ───────────────────────────────────

        protected override void OnRacerArmed(IPlayer pilot)
        {
            var tf = pilot?.Vessel?.Transform;
            var goby = tf ? tf.GetComponentInChildren<GobyPistolDuo>(true) : null;
            if (!goby) return;
            _coached[goby] = pilot;
            goby.RaceCoach = this;
        }

        /// <summary>
        /// The AI Goby's next dart: <see cref="PerchRunCourse.PlanDart"/> from its hull at its next ring (a double when
        /// one line threads two, a rim shot round a blocker, charged to just past the last ring it threads).
        /// </summary>
        public bool TryRaceAim(GobyPistolDuo goby, out Vector3 aim, out float dartLength)
        {
            aim = default;
            dartLength = 0f;
            if (!goby || !_coached.TryGetValue(goby, out var pilot) || pilot == null || _course.Count == 0) return false;
            var hull = goby.RaceHull;
            if (!hull) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            int next = RingIndexFor(threaded);
            if (next < 0 || next >= _course.Count) return false;
            aim = PerchRunCourse.PlanDart(hull.position, _course, next, _coral, out dartLength);
            return true;
        }
    }
}
