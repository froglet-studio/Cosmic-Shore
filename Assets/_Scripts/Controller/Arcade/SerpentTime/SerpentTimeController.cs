using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpentine (named 2026-10-31; <c>GameModes.SerpentTime</c>, and every id and file keep that name) - the Serpent-only circuit race, and the Serpent's TIME card: the <b>dirty</b> race (Shombith,
    /// 2026-10-27, <c>Docs/Studios/SERPENT_TIME.md</c>). A closed loop of gates is cut through the cell and every
    /// pilot flies LAPS of it in order; the first domain whose lead runner threads the last gate of the last lap wins,
    /// scored on finish TIME.
    ///
    /// <para><b>What makes it dirty</b> (each piece is a rule of this mode; the vessel is the shipped Serpent):</para>
    /// <list type="bullet">
    /// <item><b>One ring a gate</b> (2026-10-31; Shombith: "make all intensities have only 1 gate"), at Breakwater's
    /// sizes: 72 / 60 / 50 / 42 u at I1-I4 (<see cref="SerpentTimeCourse.ForIntensity"/>). Ring clusters (several rings
    /// side by side, any one threading the gate, <see cref="SerpentTimeCluster"/>) are still supported through
    /// <see cref="ringsPerGateByIntensity"/>, and ship off.</item>
    /// <item><b>A shape at every ring</b> (2026-10-31, after Breakwater's stations; <see cref="SerpentineGate"/>): a plain
    /// shell flaring back round the ring, a danger weave across its mouth with 2-3 exits cut for the Serpent (the places
    /// to leave from), and a plain gold core at the centre. Coil at I1-I2, Maw at I3, Iris at I4. Break any core block (a
    /// sniper round, a ram) and the whole shape disintegrates, bursting outward from the core; it grows back
    /// <see cref="shapeRegrowSeconds"/> later, once no hull is inside it. Thread an exit, or open the ring. Nothing about
    /// the Serpent changes: the round and the ram break prisms as they do everywhere.</item>
    /// <item><b>The start.</b> Everyone starts on a small ring behind gate 0, pointed through it (<see cref="TryBuildSpawnPoses"/>),
    /// the course built early from the same seed it is later built from.</item>
    /// <item><b>The cell's current.</b> The cytoplasm's shards point along the race line instead of at a crystal
    /// (<see cref="SnowChanger.IFlow"/>), streaming round the circuit, so the course reads from anywhere in the cell
    /// without drawing a track.</item>
    /// <item><b>Each intensity a different place</b> (2026-10-31, Shombith: "different experiences in different
    /// environments"): its own cell config with its own creatures (at most 25 live): I1 the Shallows (a few tadpoles),
    /// I2 the Grazing Grounds (tadpoles and brittlestars), I3 the Hunting Grounds (sharks, piranha, tadpoles), I4 the
    /// Deep (sharks, piranha schools, brittlestars, tadpoles). The cell keeps its own colours (a per-place tint was tried
    /// and removed, 2026-10-31). And the core shrinks, so the shot gets harder
    /// (<see cref="SerpentineGate.CoreFor"/>).</item>
    /// <item><b>Gate debris.</b> Loose neutral prisms lie round every gate and grow back, so a Serpent that cloaks
    /// there has the mass its Seed Wall grows from. The debris is the environment's; the wall is the shipped one.</item>
    /// <item><b>The AI plays dirty</b> (input-only, through the replicated press like a human's): with its rifle ready
    /// it lines up on a standing shape's core and shoots it open; otherwise it threads the nearest exit. It shoots a
    /// rival's wall out of its ring the same way, and cloaks just past a ring a rival still needs, so the wall grows in
    /// that rival's way.</item>
    /// </list>
    ///
    /// <para><b>The vessel is never changed here</b> (Shombith, 2026-10-31: "the vessel kit as designed always stays
    /// the same, we just make the game mode interesting"). No press is gated, no ability is rationed, no contact effect
    /// is added: the Sniper Shot, the Seed Wall, the cloak and the pellets do exactly what they do in every other mode.
    /// The mode owns the track (the clusters, the debris) and its AI's choices. The AI runs on the server; the ring
    /// clusters are laid on every peer from the course each peer was sent.</para>
    ///
    /// <para><b>The fuel question stays.</b> The Serpent cruises at 60 u/s; its Time ability is a tank of four Solid
    /// Fuel Pellets (each press burns one for 6 s, burns stack; the tank refills only while its wall eats its trail). See <see cref="SerpentTimeCourse"/> and
    /// SERPENTTIME.md.</para>
    /// </summary>
    public class SerpentTimeController : GateRaceController, IPlayerSpawnLine
    {
        [Header("Serpent Time circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree.")]
        [SerializeField, Min(1)] int laps = SerpentTimeCourseSource.DefaultLaps;

        [Header("Ring clusters")]
        [Tooltip("Rings standing at each gate on intensity 1-4; any one of them threads the gate. 1 = the plain gate " +
                 "(every intensity since 2026-10-31).")]
        [SerializeField] int[] ringsPerGateByIntensity = { 1, 1, 1, 1 };
        [Tooltip("Seconds each cluster ring takes to bloom in when the course is raised.")]
        [SerializeField, Min(0f)] float clusterBloomSeconds = 0.9f;

        [Header("The shapes at the rings (SerpentineGate)")]
        [Tooltip("Prisms a disintegrating shape blows apart each frame, from the core outward, so it bursts over a few " +
                 "frames instead of all in one.")]
        [SerializeField, Min(1)] int shapeBurstPerFrame = 48;
        [Tooltip("Speed (u/s) a disintegrating shape's prisms fly apart at, outward from its core.")]
        [SerializeField, Min(0f)] float shapeBurstSpeed = 40f;
        [Tooltip("Seconds after a shape disintegrates before it grows back (it waits on, while any hull is inside its " +
                 "reach). 0 = it never grows back.")]
        [SerializeField, Min(0f)] float shapeRegrowSeconds = 25f;
        [Tooltip("Prisms a growing-back shape lays each frame.")]
        [SerializeField, Min(1)] int shapeRegrowPerFrame = 40;

        [Header("Start line")]
        [Tooltip("How far behind gate 0 (u), against its axis, the start ring sits. Everyone starts there pointed through it.")]
        [SerializeField, Min(1f)] float startLineStandoff = 300f;
        [Tooltip("Radius (u) of the start ring round gate 0's axis.")]
        [SerializeField, Min(0f)] float startLineRadius = 90f;

        [Header("The cell's current")]
        [Tooltip("Shards within this distance (u) of the race line are drawn as long streaks along it; past it they shrink " +
                 "to motes, still pointing the way.")]
        [SerializeField, Min(1f)] float currentStreakDistance = 450f;
        [Tooltip("How hard a shard off the line leans back toward it (0 = only along the line). The current reads as a " +
                 "river with a pull, so a pilot off course sees which way the line is.")]
        [SerializeField, Range(0f, 2f)] float currentPull = 0.6f;

        [Header("Gate debris (the walls' fuel)")]
        [Tooltip("The prism every piece of gate debris is laid from: the environment prism (SpawnablePrism). " +
                 "Authored by Tools/Build/author_serpent_time_assets.py. Unset = no debris, and a warning.")]
        [SerializeField] Prism debrisPrefab;
        [Tooltip("Loose prisms kept round each gate. A Seed Wall pulls its bricks from loose mass near its seed, so " +
                 "this is what a wall laid at a gate is built from (with the trails flown through it).")]
        [SerializeField, Min(0)] int debrisPerGate = 48;
        [Tooltip("Every this many seconds, each gate short of its debris grows one piece back, so a gate is never " +
                 "stripped bare for the rest of the race.")]
        [SerializeField, Min(0.1f)] float debrisRegrowSeconds = 1.5f;
        [Tooltip("How far beyond the gate's shape (u) the debris lies: nearest and farthest.")]
        [SerializeField] Vector2 debrisDistanceBeyondRings = new(26f, 120f);
        [Tooltip("Size of one piece of debris (u).")]
        [SerializeField] Vector3 debrisScale = new(2.6f, 2.6f, 2.6f);

        [Header("Dirty AI")]
        [Tooltip("Chance an AI cloaks just past a ring a rival still needs (when its cloak is ready).")]
        [SerializeField, Range(0f, 1f)] float aiCloakChance = 0.8f;
        [Tooltip("A rival counts as needing the ring only within this distance (u).")]
        [SerializeField, Min(0f)] float aiCloakRivalRange = 1400f;
        [Tooltip("Seconds between an AI's dirty cloaks (the shipped cloak recovers in 15 s).")]
        [SerializeField, Min(0f)] float aiCloakCooldownSeconds = 15.5f;
        [Tooltip("Share of a ring's mouth rival prisms must cover before the AI calls the ring walled.")]
        [SerializeField, Range(0.05f, 1f)] float aiBlockedCoverage = 0.4f;
        [Tooltip("Extra cost (u) the AI puts on a walled ring (it shoots it open when its rifle is ready).")]
        [SerializeField, Min(0f)] float aiBlockedCost = 120f;
        [Tooltip("The AI keeps its ring unless another is cheaper by this much (u).")]
        [SerializeField, Min(0f)] float aiRingSwitchHysteresis = 60f;
        [Tooltip("Within this distance (u) of its ring the AI stops changing its mind, unless the ring is walled and its rifle is not ready.")]
        [SerializeField, Min(0f)] float aiChoiceLockDistance = 250f;
        [Tooltip("The AI shoots a wall in its ring only from this range (u): near enough to aim, far enough to clear it in time.")]
        [SerializeField] Vector2 aiShotRange = new(90f, 1100f);
        [Tooltip("Seconds the AI holds the scope before it may fire.")]
        [SerializeField, Min(0f)] float aiScopeSettleSeconds = 0.35f;
        [Tooltip("Seconds the AI tries to line up a shot before it lowers the scope.")]
        [SerializeField, Min(0.1f)] float aiShotGiveUpSeconds = 3f;

        protected override string ModeName => "Serpentine";

        /// <summary>Regatta's gate steering, which made the AI race Switchback and Warpline: commit from
        /// the approach point and aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new SerpentTimeCourseSource { Laps = laps };

        const float ChoiceSeconds = 0.25f;
        const float PressHoldSeconds = 0.2f;
        const float WallSlabHalfDepth = 12f;

        enum ShotPhase { Idle, Scoping, Fired }

        /// <summary>One pilot's state in this race, on the machine that flies it (and, for an AI, on the server).</summary>
        sealed class Pilot
        {
            public IPlayer Player;
            public bool FliesHere;
            public R_VesselActionHandler Handler;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Rifle;
            public AIPilot Autopilot;
            public bool FilterInstalled;

            // AI: which ring of the gate it flies for.
            public int ChoiceIndex = -1;
            public int ChoiceSlot = -1;
            public float NextChoiceAt;
            public readonly bool[] SlotBlocked = new bool[MaxRings];
            public readonly Vector3[] SlotCentroid = new Vector3[MaxRings];

            // AI: dirty cloak and wall shots.
            public int LastThreaded = -1;
            public float NextCloakAt;
            public float ReleaseCloakAt = -1f;
            public ShotPhase Shot;
            public float ShotPhaseAt;
            public float NextShotAt;
            public System.Random Rng;

            // AI: which exit of its gate's shape it threads (-1 = none chosen yet), and for which gate.
            public int ExitIndex = -1;
            public int ExitGate = -1;
        }

        /// <summary>Owner id no pilot carries: the prisms of the shapes at the rings.</summary>
        const string ShapeOwnerId = "SerpentineGate";

        /// <summary>One gate's shape: its parts, the prisms laid for them (index for index), and its state.</summary>
        sealed class GateShape
        {
            public int Gate;
            public RaceGate Ring;
            public SerpentineGate.Shape Shape;
            public int Intensity;
            public readonly List<SerpentineGate.Part> Parts = new();
            public readonly List<Prism> Prisms = new();
            public Trail Trail;
            /// <summary>Whole and standing: the core is intact.</summary>
            public bool Standing;
            /// <summary>Disintegrating: <see cref="Cursor"/> walks the prisms, blowing them apart.</summary>
            public bool Bursting;
            /// <summary>Growing back: <see cref="Cursor"/> walks the parts, relaying the missing ones.</summary>
            public bool Regrowing;
            public int Cursor;
            public float RegrowAt = -1f;
        }

        const int MaxRings = 3;

        /// <summary>Owner id no pilot carries: the debris is nobody's trail, and only pieces still carrying it count.</summary>
        const string DebrisOwnerId = "SerpentTimeDebris";

        /// <summary>How far (u) a gate's debris keeps from another gate's outermost ring.</summary>
        const float DebrisClearOfOtherGates = 20f;

        /// <summary>One gate's debris: where it lies, its own seeded draw, and the pieces laid so far.</summary>
        sealed class GateDebris
        {
            public Vector3 Centre;
            public float Nearest, Farthest;
            public System.Random Rng;
            public Trail Trail;
            public readonly List<Prism> Pieces = new();
        }

        readonly List<GateShape> _shapes = new();
        Transform _shapeRoot;
        static readonly ProfilerMarker s_tickMarker = new("SerpentTime.Tick");
        static readonly ProfilerMarker s_aiMarker = new("SerpentTime.AI");

        readonly List<GateDebris> _debris = new();
        Transform _debrisRoot;
        float _nextDebrisRegrowAt;

        readonly Dictionary<IPlayer, Pilot> _pilots = new();
        readonly List<RaceGateRing[]> _clusters = new();
        readonly List<RaceGate[]> _clusterGates = new();
        static readonly List<Prism> s_prisms = new(128);

        bool _localRaceStarted;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            ReleasePilots();
            ClearShapes();
            ClearCurrent();
            ClearDebris();
            base.OnNetworkDespawn();
            _clusters.Clear();
            _clusterGates.Clear();
            _localRaceStarted = false;
        }

        /// <summary>Hand every vessel back as the race found it: no AI filter, no held trigger.</summary>
        void ReleasePilots()
        {
            foreach (var pilot in _pilots.Values)
            {
                if (pilot.Autopilot) pilot.Autopilot.SetCycledAbilityFilter(null);
                if (pilot.Handler && pilot.FliesHere && pilot.Shot != ShotPhase.Idle)
                {
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                }
            }
            _pilots.Clear();
        }

        // ── Ring clusters (every peer) ────────────────────────────────────

        protected override void OnCourseRaised()
        {
            _clusters.Clear();
            _clusterGates.Clear();

            int rings = Mathf.Min(MaxRings, SerpentTimeCluster.RingsFor(Intensity, ringsPerGateByIntensity));
            LayGateDebris(rings);
            LayShapes(rings);
            // The next match draws its own seed (the start line latched this one).
            _startSeedLatched = false;
            _currentPending = _course.Count >= 2;
            TryLayCurrent();
            if (rings <= 1) return;

            var theme = gameData ? gameData.ThemeManagerData : null;
            for (int i = 0; i < _rings.Count; i++)
            {
                var courseRing = _rings[i];
                var cluster = new RaceGateRing[rings];
                var gates = new RaceGate[rings];
                for (int slot = 0; slot < rings; slot++)
                {
                    gates[slot] = SerpentTimeCluster.Ring(_course[i], i, slot, rings);
                    // A child of the course's ring, so the ring's retirement takes the cluster with it.
                    var go = new GameObject($"Ring_{slot + 1}");
                    go.transform.SetParent(courseRing ? courseRing.transform : transform, false);
                    var ring = go.AddComponent<RaceGateRing>();
                    ring.Build(i, gates[slot], theme, clusterBloomSeconds);
                    cluster[slot] = ring;
                }
                if (courseRing) courseRing.SetVisualHidden(true);
                _clusters.Add(cluster);
                _clusterGates.Add(gates);
            }
        }

        // ── The shapes at the rings (every peer) ──────────────────────────

        SerpentineGate.Shape CurrentShape => SerpentineGate.ShapeFor(Intensity);

        /// <summary>
        /// Lay every gate's shape (<see cref="SerpentineGate.Build"/>) from the environment prism, on every peer from the
        /// course it was sent, as the debris is: a shape is closed form, so every peer's copy is the same. Behind the
        /// connecting panel (watchForReveal), so the arena-ready gate waits for them. One ring a gate only: a cluster of
        /// rings has no single mouth for a shape to close.
        /// </summary>
        void LayShapes(int rings)
        {
            ClearShapes();
            if (rings > 1 || !debrisPrefab) return;
            _shapeRoot = new GameObject("SerpentineShapes").transform;
            var shape = CurrentShape;
            for (int i = 0; i < _course.Count; i++)
            {
                var gs = new GateShape { Gate = i, Ring = _course[i], Shape = shape, Intensity = Intensity, Trail = new Trail() };
                SerpentineGate.Build(_course[i], i, Intensity, gs.Parts);
                for (int k = 0; k < gs.Parts.Count; k++) gs.Prisms.Add(LayPart(gs, k, forReveal: true));
                gs.Standing = true;
                _shapes.Add(gs);
            }
        }

        Prism LayPart(GateShape gs, int k, bool forReveal)
        {
            var part = gs.Parts[k];
            var prism = PrismTrailBuilder.LayOne(debrisPrefab,
                new PrismLay(new SpawnPoint(part.Position, part.Rotation, part.Scale), Domains.Blue, part.Kind),
                _shapeRoot, gs.Trail, ShapeOwnerId, watchForReveal: forReveal);
            if (prism && !forReveal) prism.CompletesAsLiveRibbon = true;
            return prism;
        }

        /// <summary>Still a prism of its shape: alive, and not taken (a destroyed prism goes back to the pool and is
        /// re-issued to somebody else; a Seed Wall that pulls one in re-owns it).</summary>
        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == ShapeOwnerId;

        /// <summary>
        /// Every peer, every frame: a standing shape whose core lost a block (a sniper round, a ram, anything that
        /// breaks a prism) disintegrates; a disintegrating one blows <see cref="shapeBurstPerFrame"/> more prisms apart,
        /// core outward (the parts are listed core, weave, shell); a fallen one grows back <see cref="shapeRegrowSeconds"/>
        /// later, once no hull is inside its reach, <see cref="shapeRegrowPerFrame"/> prisms a frame.
        /// </summary>
        void TickShapes()
        {
            float now = Time.time;
            for (int g = 0; g < _shapes.Count; g++)
            {
                var gs = _shapes[g];
                if (gs.Standing)
                {
                    for (int k = 0; k < gs.Parts.Count; k++)
                    {
                        if (!gs.Parts[k].Core) break;   // the core is listed first
                        if (Holds(gs.Prisms[k])) continue;
                        gs.Standing = false;
                        gs.Bursting = true;
                        gs.Cursor = 0;
                        break;
                    }
                }

                if (gs.Bursting)
                {
                    int budget = shapeBurstPerFrame;
                    while (budget > 0 && gs.Cursor < gs.Prisms.Count)
                    {
                        var prism = gs.Prisms[gs.Cursor++];
                        if (!Holds(prism)) continue;
                        Vector3 impact = (prism.transform.position - gs.Ring.Position).normalized * shapeBurstSpeed;
                        if (prism.prismProperties is { IsSuperShielded: true }) prism.DeactivateShields(impact);
                        prism.Damage(impact, Domains.Blue, string.Empty, devastate: true);
                        budget--;
                    }
                    if (gs.Cursor >= gs.Prisms.Count)
                    {
                        gs.Bursting = false;
                        gs.RegrowAt = shapeRegrowSeconds > 0f ? now + shapeRegrowSeconds : -1f;
                    }
                    continue;
                }

                if (!gs.Standing && !gs.Regrowing && gs.RegrowAt >= 0f && now >= gs.RegrowAt && !HullWithin(gs))
                {
                    gs.Regrowing = true;
                    gs.Cursor = 0;
                }

                if (gs.Regrowing)
                {
                    int budget = shapeRegrowPerFrame;
                    while (budget > 0 && gs.Cursor < gs.Parts.Count)
                    {
                        int k = gs.Cursor++;
                        if (Holds(gs.Prisms[k])) continue;
                        gs.Prisms[k] = LayPart(gs, k, forReveal: false);
                        budget--;
                    }
                    if (gs.Cursor >= gs.Parts.Count)
                    {
                        gs.Regrowing = false;
                        gs.RegrowAt = -1f;
                        gs.Standing = true;
                    }
                }
            }
        }

        /// <summary>Is any hull inside this shape's reach (so growing it back now would lay prisms on it)?</summary>
        bool HullWithin(GateShape gs)
        {
            float reach = SerpentineGate.OuterReach(gs.Ring.Radius, gs.Intensity);
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var tf = players[i]?.Vessel?.Transform;
                if (tf && (tf.position - gs.Ring.Position).sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        GateShape ShapeAt(int gateIndex) =>
            gateIndex >= 0 && gateIndex < _shapes.Count && _shapes[gateIndex].Gate == gateIndex ? _shapes[gateIndex] : null;

        void ClearShapes()
        {
            for (int g = 0; g < _shapes.Count; g++)
            {
                var prisms = _shapes[g].Prisms;
                for (int i = 0; i < prisms.Count; i++)
                    if (Holds(prisms[i]) && !EnvironmentPrismPool.TryRelease(prisms[i]))
                        Destroy(prisms[i].gameObject);
            }
            _shapes.Clear();
            if (_shapeRoot && _shapeRoot.childCount == 0) Destroy(_shapeRoot.gameObject);
            _shapeRoot = null;
        }

        // ── The start line (server, during the spawn chain) ───────────────

        int _startSeed;
        bool _startSeedLatched;

        /// <summary>One seed for the start line and the course: whichever asks first draws it, the other reuses it.</summary>
        protected override int PickCourseSeed()
        {
            if (!_startSeedLatched)
            {
                _startSeed = base.PickCourseSeed();
                _startSeedLatched = true;
            }
            return _startSeed;
        }

        /// <summary>
        /// Everyone starts on a ring behind gate 0, pointed through it (Regatta's and Skein's start, <see cref="CellSpawnFormation.BuildFacingRing"/>):
        /// the platform's cell ring stood every pilot on the equator facing the centre, with gate 0 hundreds of units up
        /// on the pole. The course is pure geometry, so it is built here from the seed it will be built from
        /// (<see cref="PickCourseSeed"/>), the same shell and the same gate count. Declines (the platform ring) when the
        /// cell cannot answer yet, rather than guess a shell the course would not be built in.
        /// </summary>
        public bool TryBuildSpawnPoses(int count, out Pose[] poses)
        {
            poses = null;
            RaceGate first;
            if (_course.Count > 0) first = _course[0];
            else
            {
                if (gameData == null || !cellData || !Cell.FindByRuntimeData(cellData)) return false;
                ResolveShell(out float inner, out float outer);
                var request = new RaceCourseRequest(PickCourseSeed(), Intensity, AuthoredGateTarget(), inner, outer,
                                                    ResolveCourseConfig());
                var course = CourseSource.Build(request, out _);
                if (course == null || course.Count == 0) return false;
                first = new RaceGate(course[0].Position + ResolveCellCentre(), course[0].Axis, course[0].Radius);
            }
            poses = CellSpawnFormation.BuildFacingRing(count, first.Position, first.Axis, startLineStandoff, startLineRadius);
            return true;
        }

        // ── The cell's current (every peer) ───────────────────────────────

        /// <summary>
        /// The race line as a current for the cytoplasm: each shard points along the circuit where it is nearest it (gate
        /// to gate, in race order), leaning back toward the line by <see cref="currentPull"/>, and is drawn long near the line
        /// and as a mote far from it. A closed polyline of a lap's gates: one segment test per gate per shard, on the
        /// shards' own time-sliced reorientation.
        /// </summary>
        sealed class CourseCurrent : SnowChanger.IFlow
        {
            readonly Vector3[] _points;
            readonly float _streak, _pull;

            public CourseCurrent(List<RaceGate> course, float streak, float pull)
            {
                _points = new Vector3[course.Count];
                for (int i = 0; i < course.Count; i++) _points[i] = course[i].Position;
                _streak = Mathf.Max(1f, streak);
                _pull = pull;
            }

            public bool TryFlow(Vector3 position, out Vector3 direction, out float stretch01)
            {
                direction = Vector3.forward;
                stretch01 = 0f;
                int n = _points.Length;
                if (n < 2) return false;

                float best = float.MaxValue;
                Vector3 closest = _points[0], along = Vector3.forward;
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = _points[i], b = _points[(i + 1) % n];
                    Vector3 ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    if (len2 < 1e-4f) continue;
                    float t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / len2);
                    Vector3 c = a + ab * t;
                    float d2 = (position - c).sqrMagnitude;
                    if (d2 >= best) continue;
                    best = d2;
                    closest = c;
                    along = ab / Mathf.Sqrt(len2);
                }

                float distance = Mathf.Sqrt(best);
                Vector3 toLine = distance > 1e-3f ? (closest - position) / distance : Vector3.zero;
                direction = along + toLine * (_pull * Mathf.Clamp01(distance / _streak));
                stretch01 = 1f - Mathf.Clamp01(distance / _streak);
                return true;
            }
        }

        SnowChanger _currentOn;
        bool _currentPending;

        /// <summary>Hand the course to the cell's cytoplasm, once the cell has made one (retried each frame until then).</summary>
        void TryLayCurrent()
        {
            if (!_currentPending) return;
            var cell = cellData ? Cell.FindByRuntimeData(cellData) : null;
            var snow = cell ? cell.Cytoplasm : null;
            if (!snow) return;
            _currentPending = false;
            _currentOn = snow;
            snow.SetFlow(new CourseCurrent(_course, currentStreakDistance, currentPull));
        }

        void ClearCurrent()
        {
            _currentPending = false;
            if (_currentOn) _currentOn.SetFlow(null);
            _currentOn = null;
        }

        // ── Gate debris (every peer) ──────────────────────────────────────

        /// <summary>
        /// Lay <see cref="debrisPerGate"/> loose prisms round every gate, beyond its outermost ring, in random directions
        /// and poses: the mass a Seed Wall laid at the gate is built from (the studio's rule, <c>serpent.html</c>
        /// <c>addDebris</c>). Every peer lays its own copy from the course it was sent - a wall is assembled on every
        /// peer out of that peer's prisms - so each gate's draw is seeded from the gate itself and the copies match.
        /// Laid behind the connecting panel, so the arena-ready gate waits for them.
        /// </summary>
        void LayGateDebris(int rings)
        {
            ClearDebris();
            if (debrisPerGate <= 0) return;
            if (!debrisPrefab)
            {
                CSDebug.LogWarning("[Serpent Time] No debris prefab on the controller: walls grow only from trails. " +
                                   "Re-run Tools/Build/author_serpent_time_assets.py.");
                return;
            }

            // At the scene root, at world identity: a laid prism's position is local to its parent, and the course is
            // already in world coordinates.
            _debrisRoot = new GameObject("SerpentTimeDebris").transform;

            for (int i = 0; i < _course.Count; i++)
            {
                var gate = _course[i];
                float reach = GateReach(gate, rings);
                // Never into another gate's rings: on a tight course two gates can sit closer than two debris shells.
                float farthest = reach + Mathf.Max(debrisDistanceBeyondRings.x, debrisDistanceBeyondRings.y);
                for (int j = 0; j < _course.Count; j++)
                    if (j != i)
                        farthest = Mathf.Min(farthest, Vector3.Distance(gate.Position, _course[j].Position)
                                                       - GateReach(_course[j], rings) - DebrisClearOfOtherGates);
                farthest = Mathf.Max(farthest, reach + 1f);
                var debris = new GateDebris
                {
                    Centre = gate.Position,
                    Nearest = Mathf.Min(reach + Mathf.Min(debrisDistanceBeyondRings.x, debrisDistanceBeyondRings.y), farthest),
                    Farthest = farthest,
                    Rng = new System.Random(DebrisSeed(i, gate.Position)),
                    Trail = new Trail(),
                };
                _debris.Add(debris);
                for (int n = 0; n < debrisPerGate; n++) LayDebris(debris, forReveal: true);
            }
            _nextDebrisRegrowAt = Time.time + debrisRegrowSeconds;
        }

        /// <summary>How far a gate's own things reach from its centre: its cluster's outermost ring, or its shape.</summary>
        float GateReach(in RaceGate gate, int rings)
        {
            if (rings > 1)
            {
                SerpentTimeCluster.Layout(rings, out float ringScale, out float spread);
                return gate.Radius * (spread + ringScale);
            }
            return SerpentineGate.OuterReach(gate.Radius, Intensity);
        }

        /// <summary>The same number on every peer: the gate's index and its position (the course is broadcast).</summary>
        static int DebrisSeed(int gateIndex, Vector3 position) => unchecked(
            gateIndex * 7919 ^ Mathf.RoundToInt(position.x) * 73856093 ^
            Mathf.RoundToInt(position.y) * 19349663 ^ Mathf.RoundToInt(position.z) * 83492791);

        void LayDebris(GateDebris gate, bool forReveal)
        {
            var r = gate.Rng;
            Vector3 direction;
            do direction = new Vector3(Signed(r), Signed(r), Signed(r));
            while (direction.sqrMagnitude > 1f || direction.sqrMagnitude < 0.05f);
            float distance = Mathf.Lerp(gate.Nearest, gate.Farthest, (float)r.NextDouble());
            var rotation = Quaternion.Euler((float)r.NextDouble() * 360f, (float)r.NextDouble() * 360f,
                                            (float)r.NextDouble() * 360f);

            // A regrown piece comes mid-race, so it must not hold the arena-ready gate (a continuous source can never
            // all-clear it) nor queue behind the cell's flora for its creation.
            var prism = PrismTrailBuilder.LayOne(debrisPrefab,
                new PrismLay(new SpawnPoint(gate.Centre + direction.normalized * distance, rotation, debrisScale), Domains.Blue),
                _debrisRoot, gate.Trail, DebrisOwnerId, watchForReveal: forReveal);
            if (!prism) return;
            if (!forReveal) prism.CompletesAsLiveRibbon = true;
            gate.Pieces.Add(prism);
        }

        static float Signed(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        /// <summary>Still loose debris: alive, still the debris's, not stolen into a wall (a steal re-colours it).</summary>
        static bool IsLooseDebris(Prism prism) =>
            prism && !prism.destroyed && prism.ownerID == DebrisOwnerId && prism.Domain == Domains.Blue;

        /// <summary>
        /// The debris grows back: every <see cref="debrisRegrowSeconds"/>, each gate holding fewer than
        /// <see cref="debrisPerGate"/> loose pieces grows one (the studio's rule). A piece a wall took, a hull broke or a
        /// shot cleared no longer counts.
        /// </summary>
        void TickDebrisRegrow()
        {
            if (_debris.Count == 0 || !debrisPrefab || Time.time < _nextDebrisRegrowAt) return;
            _nextDebrisRegrowAt = Time.time + debrisRegrowSeconds;

            for (int g = 0; g < _debris.Count; g++)
            {
                var gate = _debris[g];
                int loose = 0;
                for (int i = gate.Pieces.Count - 1; i >= 0; i--)
                {
                    var piece = gate.Pieces[i];
                    if (IsLooseDebris(piece)) loose++;
                    // Gone, or handed to someone else (the pool re-issues a destroyed prism): stop tracking it.
                    else if (!piece || piece.destroyed || piece.ownerID != DebrisOwnerId) gate.Pieces.RemoveAt(i);
                }
                if (loose < debrisPerGate) LayDebris(gate, forReveal: false);
            }
        }

        void ClearDebris()
        {
            for (int g = 0; g < _debris.Count; g++)
            {
                var pieces = _debris[g].Pieces;
                for (int i = 0; i < pieces.Count; i++)
                    if (IsLooseDebris(pieces[i]) && !EnvironmentPrismPool.TryRelease(pieces[i]))
                        Destroy(pieces[i].gameObject);
            }
            _debris.Clear();
            // A piece a wall still holds stays parented here; the scene's teardown takes it.
            if (_debrisRoot && _debrisRoot.childCount == 0) Destroy(_debrisRoot.gameObject);
            _debrisRoot = null;
        }

        bool HasCluster(int ringIndex) => ringIndex >= 0 && ringIndex < _clusters.Count;

        protected override bool ThreadsGate(int ringIndex, Vector3 prev, Vector3 cur)
        {
            if (!HasCluster(ringIndex)) return base.ThreadsGate(ringIndex, prev, cur);
            var cluster = _clusters[ringIndex];
            for (int i = 0; i < cluster.Length; i++)
                if (cluster[i] && cluster[i].CrossedMouth(prev, cur)) return true;
            return false;
        }

        protected override void OnLocalNextGateChanged(int previousRing, int nextRing)
        {
            LightCluster(previousRing, false);
            LightCluster(nextRing, true);
        }

        void LightCluster(int ringIndex, bool lit)
        {
            if (!HasCluster(ringIndex)) return;
            var cluster = _clusters[ringIndex];
            for (int i = 0; i < cluster.Length; i++)
                if (cluster[i]) cluster[i].SetIsNextForLocalPilot(lit);
        }

        // ── Pilots (the machine that flies each hull) ──

        /// <summary>The same rule as the rifle's own: not spawned is the only machine there is, else the owner.</summary>
        static bool FliesHere(IPlayer player)
        {
            var status = player?.Vessel?.VesselStatus;
            if (status == null) return false;
            var handler = status.ActionHandler;
            if (handler == null || !handler.IsSpawned) return true;
            return status.IsNetworkOwner;
        }

        Pilot PilotFor(IPlayer player)
        {
            if (player == null) return null;
            if (_pilots.TryGetValue(player, out var pilot))
            {
                if (pilot.Handler == null || !pilot.Handler) RefreshVessel(pilot);
                return pilot;
            }

            pilot = new Pilot
            {
                Player = player,
                FliesHere = FliesHere(player),
                Rng = new System.Random(unchecked(System.Environment.TickCount * 17 + (int)player.PlayerNetId)),
            };
            _pilots[player] = pilot;
            RefreshVessel(pilot);
            return pilot;
        }

        void RefreshVessel(Pilot pilot)
        {
            var status = pilot.Player.Vessel?.VesselStatus;
            if (status == null) return;
            pilot.Handler = status.ActionHandler;
            var root = status.Transform;
            if (root)
            {
                pilot.Scope = root.GetComponentInChildren<SniperScopeActionExecutor>(true);
                pilot.Rifle = root.GetComponentInChildren<SniperShotActionExecutor>(true);
            }
            // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a human's hull.
            pilot.Autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;

        }

        bool IsScoped(Pilot pilot) => pilot.Scope && pilot.Scope.IsScoped;

        void TickLayCurrentIfPending() { if (_currentPending) TryLayCurrent(); }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (!IsSpawned || gameData == null || !gameData.IsTurnRunning) return;

            using var _ = s_tickMarker.Auto();
            TickLayCurrentIfPending();
            TickShapes();
            TickDebrisRegrow();

            if (!_localRaceStarted)
            {
                _localRaceStarted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.SerpentTimeWallHint, local.Domain);
            }
        }

        // ── The dirty AI (server) ─────────────────────────────────────────

        /// <summary>
        /// The ring of this gate the AI flies for: the cheapest by distance (here, through it, on to the next gate) plus
        /// a cost for a walled ring (it shoots it open when its rifle is ready). Re-decided a few times a second, with
        /// hysteresis, and held once the pilot is close. A gate of one ring with its shape standing: <see cref="ChooseShapeLine"/>.
        /// </summary>
        protected override RaceGate ChooseAIGate(IPlayer player, int threaded, int ringIndex, RaceGate gate)
        {
            if (!HasCluster(ringIndex)) return ChooseShapeLine(player, ringIndex, gate);
            var pilot = PilotFor(player);
            var tf = player.Vessel?.Transform;
            if (pilot == null || !tf) return _clusterGates[ringIndex][0];

            var gates = _clusterGates[ringIndex];
            bool fresh = pilot.ChoiceIndex != threaded || pilot.ChoiceSlot < 0 || pilot.ChoiceSlot >= gates.Length;
            if (fresh || Time.time >= pilot.NextChoiceAt)
                ChooseRing(pilot, tf.position, threaded, ringIndex, gates, fresh);
            return gates[pilot.ChoiceSlot];
        }

        void ChooseRing(Pilot pilot, Vector3 self, int threaded, int ringIndex, RaceGate[] gates, bool fresh)
        {
            pilot.NextChoiceAt = Time.time + ChoiceSeconds;
            Vector3 next = threaded + 1 < RaceLength ? _course[RingIndexFor(threaded + 1)].Position : gates[0].Position;

            int best = 0;
            float bestCost = float.MaxValue, keptCost = float.MaxValue;
            for (int slot = 0; slot < gates.Length; slot++)
            {
                pilot.SlotBlocked[slot] = Blocked(ringIndex, slot, gates[slot], pilot.Player.Domain, out pilot.SlotCentroid[slot]);
                float cost = Vector3.Distance(self, gates[slot].Position) + Vector3.Distance(gates[slot].Position, next)
                             + (pilot.SlotBlocked[slot] ? aiBlockedCost : 0f);
                if (cost < bestCost) { bestCost = cost; best = slot; }
                if (!fresh && slot == pilot.ChoiceSlot) keptCost = cost;
            }

            if (!fresh)
            {
                var kept = gates[pilot.ChoiceSlot];
                bool stuck = pilot.SlotBlocked[pilot.ChoiceSlot] && !RifleReady(pilot);
                bool close = (kept.Position - self).sqrMagnitude < aiChoiceLockDistance * aiChoiceLockDistance;
                if ((close && !stuck) || keptCost <= bestCost + aiRingSwitchHysteresis) return;
            }

            pilot.ChoiceIndex = threaded;
            pilot.ChoiceSlot = best;
        }

        /// <summary>
        /// Where the AI flies at a gate whose shape stands: at the CORE while it can shoot it (rifle ready, or a shot
        /// already lining up, and far enough out to fire), so its nose comes onto the core; otherwise through an exit, the
        /// one nearest it when it first looked, held for the gate. A fallen shape: the ring itself.
        /// </summary>
        RaceGate ChooseShapeLine(IPlayer player, int ringIndex, RaceGate gate)
        {
            var gs = ShapeAt(ringIndex);
            if (gs == null || !gs.Standing) return gate;
            var pilot = PilotFor(player);
            var tf = player.Vessel?.Transform;
            if (pilot == null || !tf) return gate;

            float distance = Vector3.Distance(tf.position, gate.Position);
            bool shooting = pilot.Shot != ShotPhase.Idle ||
                            (RifleReady(pilot) && distance > aiShotRange.x && distance < aiShotRange.y);
            if (shooting) return gate;

            int exits = SerpentineGate.ExitCount(gs.Shape);
            if (pilot.ExitGate != ringIndex || pilot.ExitIndex < 0 || pilot.ExitIndex >= exits)
            {
                pilot.ExitGate = ringIndex;
                pilot.ExitIndex = 0;
                float best = float.MaxValue;
                for (int e = 0; e < exits; e++)
                {
                    float d = (SerpentineGate.Exit(gs.Ring, gs.Gate, gs.Intensity, e).Position - tf.position).sqrMagnitude;
                    if (d < best) { best = d; pilot.ExitIndex = e; }
                }
            }
            return SerpentineGate.Exit(gs.Ring, gs.Gate, gs.Intensity, pilot.ExitIndex);
        }

        /// <summary>
        /// Is this ring closed to the AI, and where does it shoot to open it? A standing shape closes it, aimed at its core
        /// (break a core block and the shape disintegrates); otherwise a rival wall (<see cref="IsWalled"/>).
        /// </summary>
        bool Blocked(int gateIndex, int slot, in RaceGate ring, Domains domain, out Vector3 aim)
        {
            var gs = slot == 0 ? ShapeAt(gateIndex) : null;
            if (gs != null && gs.Standing)
            {
                aim = gs.Ring.Position;
                return true;
            }
            return IsWalled(ring, domain, out aim);
        }

        /// <summary>
        /// Does rival mass close enough of this ring's mouth to call it walled? Measured as the prisms' face area against
        /// the mouth's, counting only prisms in a slab round the ring's plane and inside its mouth. Any rival prism counts
        /// (a Seed Wall's bricks are the usual ones); the question is asked of the prisms, never of the vessel's code.
        /// </summary>
        bool IsWalled(in RaceGate ring, Domains domain, out Vector3 centroid)
        {
            centroid = ring.Position;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return false;

            index.QuerySphere(ring.Position, ring.Radius + WallSlabHalfDepth, s_prisms);
            float area = 0f;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < s_prisms.Count; i++)
            {
                var prism = s_prisms[i];
                if (!prism || prism.destroyed || prism.Domain == domain) continue;
                // Loose neutral debris and the shapes (both Blue) are not a wall; the shape is Blocked's own question.
                if (prism.Domain == Domains.Blue || prism is HealthPrism) continue;

                Vector3 offset = prism.transform.position - ring.Position;
                float along = Vector3.Dot(offset, ring.Axis);
                if (Mathf.Abs(along) > WallSlabHalfDepth) continue;
                if ((offset - ring.Axis * along).sqrMagnitude > ring.Radius * ring.Radius) continue;

                // A brick's broad face: the product of its two largest extents (a wall stands its bricks face-on).
                Vector3 s = prism.transform.lossyScale;
                float a = Mathf.Abs(s.x), b = Mathf.Abs(s.y), c = Mathf.Abs(s.z);
                area += a * b * c / Mathf.Max(1e-3f, Mathf.Min(a, Mathf.Min(b, c)));
                sum += prism.transform.position;
                count++;
            }
            if (count == 0) return false;
            centroid = sum / count;
            return area / (Mathf.PI * ring.Radius * ring.Radius) >= aiBlockedCoverage;
        }

        protected override void OnServerTick()
        {
            if (gameData == null || !gameData.IsTurnRunning || _rings.Count == 0) return;
            using var _ = s_aiMarker.Auto();

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player?.Vessel == null) continue;
                var pilot = PilotFor(player);
                if (pilot == null || !pilot.FliesHere || !pilot.Handler) continue;
                if (!pilot.Autopilot || !pilot.Autopilot.AutoPilotEnabled) continue;

                if (!pilot.FilterInstalled)
                {
                    // The cycler pressed the cloak every few seconds; here the AI cloaks when it hurts a rival.
                    pilot.Autopilot.SetCycledAbilityFilter(a => a is not CloakSeedWallActionSO);
                    pilot.FilterInstalled = true;
                }

                TickDirtyCloak(pilot);
                TickWallShot(pilot);
            }
        }

        /// <summary>Just past a ring a rival still needs (and close), cloak: the seed lands at the ring and the wall grows there.</summary>
        void TickDirtyCloak(Pilot pilot)
        {
            if (pilot.ReleaseCloakAt >= 0f && Time.time >= pilot.ReleaseCloakAt)
            {
                pilot.ReleaseCloakAt = -1f;
                pilot.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
            }

            int threaded = pilot.Player.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded == pilot.LastThreaded) return;
            bool justThreaded = pilot.LastThreaded >= 0 && threaded > pilot.LastThreaded;
            pilot.LastThreaded = threaded;
            if (!justThreaded || threaded <= 0) return;

            if (Time.time < pilot.NextCloakAt || pilot.Shot != ShotPhase.Idle || IsScoped(pilot)) return;
            if (!RivalNeeds(pilot, RingIndexFor(threaded - 1))) return;

            pilot.NextCloakAt = Time.time + aiCloakCooldownSeconds;
            if (pilot.Rng.NextDouble() >= aiCloakChance) return;

            pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.RightStickAction);
            pilot.ReleaseCloakAt = Time.time + PressHoldSeconds;
        }

        bool RivalNeeds(Pilot pilot, int ringIndex)
        {
            var self = pilot.Player.Vessel?.Transform;
            if (!self) return false;
            float range2 = aiCloakRivalRange * aiCloakRivalRange;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var rival = players[i];
                if (rival == null || rival == pilot.Player || rival.Domain == pilot.Player.Domain) continue;
                var tf = rival.Vessel?.Transform;
                if (!tf || rival.RoundStats == null) continue;
                int next = rival.RoundStats.SwitchesThreaded;
                if (next < 0 || next >= RaceLength || RingIndexFor(next) != ringIndex) continue;
                if ((tf.position - self.position).sqrMagnitude <= range2) return true;
            }
            return false;
        }

        /// <summary>
        /// The AI's ring is walled (or its shape stands) and its rifle is ready: raise the scope (LT), and once it has
        /// settled and the nose is on the wall or the shape's core, fire (RT), then lower both. Every press goes
        /// the replicated way, so every peer sees the shot. A gate of one ring has no cluster to choose from, so its one ring
        /// is asked here, on the same cadence.
        /// </summary>
        void TickWallShot(Pilot pilot)
        {
            var tf = pilot.Player.Vessel?.Transform;
            if (!tf) return;
            float now = Time.time;
            int threaded = pilot.Player.RoundStats?.SwitchesThreaded ?? 0;
            int ringIndex = threaded < RaceLength ? RingIndexFor(threaded) : -1;
            if (ringIndex >= 0 && !HasCluster(ringIndex) && (pilot.ChoiceIndex != threaded || now >= pilot.NextChoiceAt))
            {
                pilot.ChoiceIndex = threaded;
                pilot.ChoiceSlot = 0;
                pilot.NextChoiceAt = now + ChoiceSeconds;
                pilot.SlotBlocked[0] = Blocked(ringIndex, 0, _course[ringIndex], pilot.Player.Domain, out pilot.SlotCentroid[0]);
            }
            bool aimed = ringIndex >= 0 && pilot.ChoiceIndex == threaded && pilot.ChoiceSlot >= 0;
            bool walled = aimed && pilot.SlotBlocked[pilot.ChoiceSlot];
            Vector3 target = aimed ? pilot.SlotCentroid[pilot.ChoiceSlot] : tf.position;
            float distance = Vector3.Distance(tf.position, target);

            switch (pilot.Shot)
            {
                case ShotPhase.Idle:
                    if (!walled || now < pilot.NextShotAt || pilot.ReleaseCloakAt >= 0f) return;
                    if (distance < aiShotRange.x || distance > aiShotRange.y) return;
                    if (!RifleReady(pilot)) return;
                    pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.LeftStickAction);
                    pilot.Shot = ShotPhase.Scoping;
                    pilot.ShotPhaseAt = now;
                    return;

                case ShotPhase.Scoping:
                    if (!walled || distance < aiShotRange.x || now - pilot.ShotPhaseAt > aiShotGiveUpSeconds)
                    {
                        pilot.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                        pilot.Shot = ShotPhase.Idle;
                        pilot.NextShotAt = now + 1f;
                        return;
                    }
                    if (!IsScoped(pilot) || now - pilot.ShotPhaseAt < aiScopeSettleSeconds) return;
                    if (!OnTarget(pilot, tf, target, distance)) return;
                    pilot.Handler.PerformShipControllerActionsReplicated(InputEvents.RightStickAction);
                    pilot.Shot = ShotPhase.Fired;
                    pilot.ShotPhaseAt = now;
                    return;

                case ShotPhase.Fired:
                    if (now - pilot.ShotPhaseAt < PressHoldSeconds) return;
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                    pilot.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                    pilot.Shot = ShotPhase.Idle;
                    pilot.NextShotAt = now + 0.5f;
                    return;
            }
        }

        /// <summary>The shipped rifle's own cooldown is the only limit on a shot.</summary>
        static bool RifleReady(Pilot pilot) => !pilot.Rifle || pilot.Rifle.CooldownRemaining <= 0f;

        /// <summary>Is the wall's centre inside the rifle's cone (its radius floored at 6 u, as the rifle's own path is)?</summary>
        static bool OnTarget(Pilot pilot, Transform tf, Vector3 target, float distance)
        {
            float half = pilot.Rifle ? pilot.Rifle.ConeHalfAngleDegrees : 1.5f;
            Vector3 to = target - tf.position;
            float along = Vector3.Dot(to, tf.forward);
            if (along <= 0f) return false;
            float lateral = (to - tf.forward * along).magnitude;
            return lateral <= Mathf.Max(6f, along * Mathf.Tan(half * Mathf.Deg2Rad));
        }
    }
}
