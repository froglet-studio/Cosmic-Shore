using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breach - the Orca-only race, and the Orca's TIME card (BREACH.md). The Orca is a heavy four-seat hull that cruises
    /// at about 30 u/s, and the helm's Time card is a 3 s BOOST TANK at five times the speed, refilling only while the burn
    /// is let go. So the race is where in each leg the helm burns: on the straights (I1), not into the short hard turns,
    /// which a burning hull swings too wide to make (I2), not round the WALLS of danger prisms that stand across a ring's
    /// line, whose dog-leg back into the ring is a cruise turn (I3), and both at I4.
    ///
    /// <para><b>What the mode adds:</b> the rings, the walls (danger prisms, every peer laying the same ones from the
    /// broadcast rings, each grown back <see cref="wallRegrowSeconds"/> after it breaks so every racer meets it) and the AI's
    /// coach. <b>What it does not:</b> nothing about the vessel. The tank, the shield, the three guns and the missiles are
    /// the shipped crew; the AI gunners shoot hostile prisms, so they breach walls on their own. The one Orca change that
    /// came with this race is <see cref="OrcaCrew.RaceCoach"/>, which lets a race tell the AI helm when to burn (the shipped
    /// AI helm never burns).</para>
    /// </summary>
    public class BreachController : GateRaceController, IPlayerSpawnLine, IOrcaRaceCoach
    {
        [Header("Breach")]
        [Tooltip("The prism every wall is laid from: the environment prism (SpawnablePrism), laid as danger. Authored by " +
                 "Tools/Build/author_breach_assets.py. Unset = no walls, and a warning.")]
        [SerializeField] Prism wallPrefab;

        [Tooltip("Seconds a broken wall prism stays gone before it grows back (once no hull is at its wall), so every racer " +
                 "meets the wall and a crew that breaches it breaches it for itself.")]
        [SerializeField, Min(0.5f)] float wallRegrowSeconds = 6f;

        [Tooltip("Wall prisms grown back a frame, at most.")]
        [SerializeField, Min(1)] int wallRegrowPerFrame = 40;

        const string WallOwnerId = "Breach";

        readonly List<(Vector3 position, int ring)> _wall = new();
        readonly List<Prism> _wallPrisms = new();
        readonly List<float> _goneAt = new();
        readonly Dictionary<OrcaCrew, IPlayer> _coached = new();
        readonly Dictionary<IPlayer, OrcaCrew> _crews = new();
        Transform _wallRoot;
        int _regrowCursor;

        protected override string ModeName => "Breach";

        protected override RaceCourseSource CreateCourseSource() => new BreachCourseSource();

        public override void OnNetworkDespawn()
        {
            foreach (var crew in _coached.Keys)
                if (crew && ReferenceEquals(crew.RaceCoach, this)) crew.RaceCoach = null;
            _coached.Clear();
            _crews.Clear();
            ClearWalls();
            base.OnNetworkDespawn();
        }

        // ── The start line: abreast, the start standoff behind ring 0 ────

        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            // The course works about the ORIGIN; the cell places it (the same offset the course broadcast applies).
            poses = BreachCourse.StartPoses(count);
            Vector3 centre = ResolveCellCentre();
            for (int i = 0; i < poses.Length; i++) poses[i].position += centre;
            return true;
        }

        // ── The walls (every peer, from the broadcast rings) ─────────────

        BreachSettings Settings()
        {
            var settings = BreachCourse.ForIntensity(Intensity);
            settings.RingCount = _course.Count;
            return settings;
        }

        protected override void OnCourseRaised()
        {
            ClearWalls();
            BreachCourse.WallsFor(_course, Settings(), _wall);
            if (_wall.Count == 0) return;
            if (!wallPrefab)
            {
                CSDebug.LogWarning("[Breach] no wall prefab on the controller - the walls are not laid.");
                return;
            }
            _wallRoot = new GameObject("BreachWalls").transform;
            for (int i = 0; i < _wall.Count; i++)
            {
                _wallPrisms.Add(LayWallPrism(i, forReveal: true));
                _goneAt.Add(-1f);
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Breach] intensity {Intensity}: {_course.Count} rings, {_wallPrisms.Count} wall prisms.");
        }

        Prism LayWallPrism(int i, bool forReveal)
        {
            var (position, ring) = _wall[i];
            var rotation = Quaternion.LookRotation(_course[ring].Axis);
            var scale = new Vector3(BreachCourse.WallPrismSize, BreachCourse.WallPrismSize, BreachCourse.WallPrismDepth);
            return PrismTrailBuilder.LayOne(wallPrefab,
                new PrismLay(new SpawnPoint(position, rotation, scale), Domains.Blue, PrismKind.Danger),
                _wallRoot, new Trail(), WallOwnerId, admitAuthoredScale: true, watchForReveal: forReveal);
        }

        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == WallOwnerId;

        /// <summary>Every peer, every frame: a broken wall prism grows back <see cref="wallRegrowSeconds"/> later, once no
        /// hull is at its wall (growing it on a hull would stop that hull dead).</summary>
        void LateUpdate()
        {
            if (_wallPrisms.Count == 0 || !wallPrefab) return;
            float now = Time.time;
            var settings = Settings();
            int budget = wallRegrowPerFrame;
            for (int n = 0; n < _wallPrisms.Count && budget > 0; n++)
            {
                int i = (_regrowCursor + n) % _wallPrisms.Count;
                if (Holds(_wallPrisms[i])) { _goneAt[i] = -1f; continue; }
                if (_goneAt[i] < 0f) { _goneAt[i] = now; continue; }
                if (now - _goneAt[i] < wallRegrowSeconds || HullAtWall(_wall[i].ring, settings)) continue;
                _wallPrisms[i] = LayWallPrism(i, forReveal: false);
                _goneAt[i] = -1f;
                budget--;
            }
            _regrowCursor = (_regrowCursor + 1) % _wallPrisms.Count;
        }

        bool HullAtWall(int ring, in BreachSettings settings)
        {
            Vector3 c = BreachCourse.WallCentre(_course, ring, settings);
            float reach = BreachCourse.WallRadius(_course[ring], settings) + 30f;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - c).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        void ClearWalls()
        {
            for (int i = 0; i < _wallPrisms.Count; i++)
            {
                var prism = _wallPrisms[i];
                if (Holds(prism) && !EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _wallPrisms.Clear();
            _goneAt.Clear();
            _wall.Clear();
            _regrowCursor = 0;
            if (_wallRoot) Destroy(_wallRoot.gameObject);
            _wallRoot = null;
        }

        // ── The AI (server): the coach ───────────────────────────────────

        protected override void OnRacerArmed(IPlayer pilot)
        {
            var tf = pilot?.Vessel?.Transform;
            var crew = tf ? tf.GetComponentInChildren<OrcaCrew>(true) : null;
            if (!crew) return;
            _coached[crew] = pilot;
            _crews[pilot] = crew;
            crew.RaceCoach = this;
        }

        /// <summary>Round a wall: until the hull is past ring's wall, the AI steers for the point beside its edge on the
        /// inside of the turn (<see cref="BreachCourse.RoundPoint"/>); then the gate race's own steering takes it in.</summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            if (!TryNext(pilot, out int next, out var hull)) return false;
            var settings = Settings();
            if (BreachCourse.PastWall(hull.position, _course, next, settings)) return false;
            target = BreachCourse.RoundPoint(_course, next, settings);
            return true;
        }

        /// <summary>The AI helm's burn: <see cref="BreachCourse.BurnWanted"/> from the hull, its tank and whether it burns.</summary>
        public bool HelmBurns(OrcaCrew crew)
        {
            if (!crew || !_coached.TryGetValue(crew, out var pilot) || pilot == null) return false;
            if (!TryNext(pilot, out int next, out var hull)) return false;
            return BreachCourse.BurnWanted(hull.position, hull.forward, _course, next, Settings(), crew.BoostTankSeconds, crew.Boosting);
        }

        bool TryNext(IPlayer pilot, out int next, out Transform hull)
        {
            next = -1;
            hull = pilot?.Vessel?.Transform;
            if (!hull || _course.Count == 0) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            next = RingIndexFor(threaded);
            return next >= 0 && next < _course.Count;
        }
    }
}
