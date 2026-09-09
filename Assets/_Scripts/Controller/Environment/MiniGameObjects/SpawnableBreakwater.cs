using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The arena of <see cref="GameModes.Breakwater"/>: fourteen ordered STATIONS hung on a walk
    /// through the cell, plus the SHOALS - fields of rubble strung along the legs between them.
    ///
    /// <para>The stations are the mode. Each is a shallow dish opening back toward the pilot, its
    /// throat welded shut by a triple-rake weave of danger bars around an eye a hull can just
    /// thread, and closing on one with two rockets in the bay is a choice between <b>fire</b>,
    /// <b>saw</b> and <b>thread</b>. All of that geometry belongs to
    /// <see cref="BreakwaterStationBuilder"/>; this class contributes the two things a station
    /// builder cannot know - <b>where the stations are</b> (they are handed in, per match) and
    /// <b>what lies between them</b>.</para>
    ///
    /// <para><b>The course is HANDED IN, not generated here, and that is the whole reason this
    /// class has a setter.</b> Every other environment in the game is a pure function of a
    /// serialized seed, so each peer builds a byte-identical world for free. A Breakwater course
    /// is rolled per MATCH and BROADCAST by the server as geometry (see
    /// <see cref="BreakwaterCourse"/> on why the geometry travels and the seed does not), so the
    /// arena's shape arrives from outside and the controller must call
    /// <see cref="SetCourse"/> before <c>Spawn</c>. What that buys is exactness: given the same
    /// fourteen poses, everything below is closed form, so the arena is identical on every machine
    /// without one prism crossing the wire.</para>
    ///
    /// <para><b>Everything laid here is <see cref="Domains.Blue"/>, and it is a rule rather than a
    /// palette choice - twice over.</b> First, <c>StatsManager.IsFriendlyEnvironmentPrism</c> is
    /// DOMAIN-ONLY, so Blue mass is hostile to every pilot: the arena is the ammunition (50 hostile
    /// prisms buy a rocket), and painting a station in a playable colour would mean one team's
    /// rounds paid no ammo on it while everyone else's did. Second, and worse, the Sparrow's
    /// CHARGE-5 upgrade spares own-domain mass - so a station wearing a playable colour would be
    /// literally <b>unopenable</b> by that domain, with an upgrade the comeback system hands to
    /// whoever is LOSING. That is the trap <c>WILDLIFE_LIBERATION.md</c> records (a creature kill
    /// that borrowed the friendly-fire flag switched itself off for the pilot sharing the swarm's
    /// colour, in the one mode scored on killing creatures), and here it is closed by
    /// construction: no call site in this file can pass a domain, because none of them takes one.
    /// </para>
    ///
    /// <para><b>Collider budget: ZERO always-on mesh colliders are authored.</b> Every prism is
    /// <see cref="PrismKind.Plain"/> or <see cref="PrismKind.Danger"/>, and both ride the
    /// LOD-cullable <c>BoxCollider</c> that <c>PrismColliderLodManager</c> reclaims outside its
    /// 200 m radius. <c>Tools/Build/breakwater_arena.py</c> MEASURES the rest rather than
    /// asserting it - walking every sampled point of 400 courses per intensity, at most
    /// <b>2 / 3 / 3 / 4</b> stations ever fall inside the LOD radius at once, for a worst case of
    /// <b>556 / 675 / 462 / 510</b> active prism colliders against a 1500 band. (The count is not
    /// monotonic in intensity because a lower intensity has fewer prisms per station but a course
    /// that folds tighter; that is exactly why it is measured off the real walk rather than
    /// derived from the authored separation.) The fourteen switch rings the controller hangs on
    /// top of these carry no collider at all.</para>
    ///
    /// <para><b>Never shield anything here to make it tougher - and note the reason is NOT the
    /// collider.</b> The claim usually attached to a budget like the one above is that a shield
    /// swaps the box for an always-on convex <c>MeshCollider</c>; checked against the shipped code
    /// that is currently FALSE - <c>shieldMeshCollider.enabled = true</c> appears nowhere in
    /// <c>PrismOctahedronShield</c> or <c>PrismStellatedOctahedronShield</c>, only
    /// <c>= false</c>. So a shielded plug would not blow this budget today, and quoting the
    /// collider as the reason would leave the door open the day that field is wired up.
    /// The real reason is GEOMETRY, and it is not contingent on anything: a shield engages the
    /// CIRCUMSCRIBING octahedron and reaches 1.5x leafSize (<c>Docs/ECOSYSTEM.md</c> §35), which
    /// on a 12-unit rake pitch fuses the weave into a solid tube and deletes both the saw and the
    /// thread - both of the choices the mode is built on. Toughness here is bought with more bars
    /// or a narrower port, never with a tier.</para>
    ///
    /// <para><b>No flora and no fauna, deliberately</b> - authored in the cell's SpawnProfile, not
    /// here, but the reason belongs beside the arena it protects: this cell has no nucleus, so
    /// herbivores eat opposing-domain mass, and the whole arena is Blue. A food web would graze
    /// the doors open on its own - imposed death of the mode's central object, arriving through a
    /// system that is otherwise exactly right.</para>
    ///
    /// <para><b>The inherited <c>seed</c> and <c>density</c> knobs are inert by design.</b> There
    /// is no <c>System.Random</c> draw anywhere below: the stations are closed form and the shoals
    /// draw from <see cref="CellEnvironmentSpawnableBase.Hash01"/>, a pure static function of
    /// integer indices. So is <c>intensityLevel</c> - intensity reaches this builder only through
    /// the PORT RADIUS carried on each station it is handed, which is why the model prices the
    /// shoals identically at all four levels. Do not introduce a draw to "add variety" without
    /// giving up the mirror that makes the cell's PhaseThresholds exact.</para>
    /// </summary>
    public class SpawnableBreakwater : CellEnvironmentSpawnableBase
    {
        /// <summary>Matches <c>Tools/Build/breakwater_arena.py</c>'s seed. Never consulted (see
        /// the class remarks) - kept because the base contract requires it.</summary>
        protected override int DefaultSeed => 48;

        /// <summary>The widest shipped arena is 4,144 prisms (intensity 1: 14 x 257 + 546).</summary>
        protected override int LayCapacity => 6000;

        // ── Shoals: the numbers the MODEL prices (const, not authored) ──────────────────────
        //
        // These three are the arena's mass, and Tools/Build/breakwater_arena.py multiplies them
        // out to derive the cell's PhaseThresholds. Changing one silently makes every authored
        // threshold describe a different arena, so they are constants rather than inspector
        // fields: an inspector field is a second place the number lives, and the model cannot
        // read a prefab. 13 legs x 6 x 7 = 546 prisms, 34,944 volume, at EVERY intensity.

        const int ShoalClustersPerLeg = 6;
        const int ShoalPrismsPerCluster = 7;

        /// <summary>Shoal prisms are laid at EXACTLY this on all three axes, with <b>no</b>
        /// jitter. The model prices them at <c>4^3</c> flat, and <see cref="CellEnvironmentSpawnableBase.Jit"/> would draw from
        /// the <c>System.Random</c> stream this class otherwise never touches - two reasons for
        /// the same restraint. Variety comes from the clump's shape and from rotation, neither of
        /// which moves a single unit of volume.</summary>
        const float ShoalCube = 4f;

        /// <summary>Upper end of the clump's per-prism radius factor - see
        /// <see cref="ClusterRadius"/>, which needs it to know how big a cluster actually is.</summary>
        const float ClumpRadiusFactorMax = 1.4f;

        const float ClumpRadiusFactorMin = 1f;

        // ── Shoals: the numbers the model does NOT price (authored) ─────────────────────────

        [Header("Shoals")]
        [Tooltip("Closest a shoal cluster may sit to the LEG it is strung along, in world units. " +
                 "The floor under the racing line: a pilot flying station to station must never " +
                 "meet rubble they did not choose to fly at.")]
        [SerializeField, Min(0f)] float shoalOffsetMin = 40f;

        [Tooltip("Furthest a shoal cluster may sit off its leg. It is also the mode's ammunition " +
                 "budget between doors - too far and a pilot who missed a plug cannot rebuild a " +
                 "rocket without leaving the race.")]
        [SerializeField, Min(1f)] float shoalOffsetMax = 110f;

        [Tooltip("Clearance a cluster keeps from a station's DISH RIM (added to " +
                 "BreakwaterStationBuilder.DishRadius). Rubble inside this could be shot from " +
                 "the wrong side and open a plug the pilot never approached.")]
        [SerializeField, Min(0f)] float shoalStationClearance = 40f;

        [Tooltip("Clearance a cluster keeps from EVERY leg of the course, not just its own - so " +
                 "ammunition scatter can never block a racing line somewhere else on the course.")]
        [SerializeField, Min(0f)] float shoalSplineClearance = 30f;

        [Tooltip("Closest a shoal cluster may sit to a pilot's spawn pad, in world units. The " +
                 "spawn ring is inside the course shell, so rubble can otherwise be strung " +
                 "through it.")]
        [SerializeField, Min(0f)] float shoalSpawnPadClearance = 60f;

        [Tooltip("Radius of one 7-prism clump. At 8 no two cubes can interpenetrate at any " +
                 "rotation - see EmitCluster for the arithmetic - so rubble keeps reading as " +
                 "rubble rather than as one lumpy solid.")]
        [SerializeField, Min(1f)] float shoalClumpRadius = 8f;

        [Tooltip("Placement proposals per cluster before the best-so-far is taken. The window is " +
                 "DERIVED before the first draw, so this only ever has to answer collisions with " +
                 "NON-adjacent stations and with other legs.")]
        [SerializeField, Range(1, 32)] int shoalPlacementAttempts = 12;

        // ── The handed-in course ────────────────────────────────────────────────────────────

        readonly List<BreakwaterStation> _course = new(16);

        /// <summary>
        /// Hang the arena on this course. <b>Call before <c>Spawn</c></b> - the controller rolls
        /// the course, broadcasts it, and poses this spawnable with it on every peer.
        ///
        /// <para><b>Positions are in the frame the spawned container will be placed in.</b> The
        /// base lays every prism at a <c>localPosition</c> under that container, and the Cell
        /// parents an environment at <c>localPosition = Vector3.zero</c>, so a caller that has
        /// already applied the cell offset (as the type contract says it has) gets what it asked
        /// for. Nothing here adds an offset of its own, deliberately: the controller also places
        /// the fourteen switch RINGS, and a ring and the plug it is drawn at the rim of must be
        /// posed in one frame by one owner or they part company.</para>
        ///
        /// <para>The list is COPIED. It feeds <see cref="BuildParameterHash"/>, so a caller that
        /// later mutated its own list would silently invalidate a cache that had already been
        /// consulted - and the arena would be a blend of two courses.</para>
        /// </summary>
        public void SetCourse(IReadOnlyList<BreakwaterStation> stations)
        {
            _course.Clear();
            if (stations != null)
                for (int i = 0; i < stations.Count; i++)
                    _course.Add(stations[i]);

            // Belt AND braces, and they cover different failures: folding the stations into the
            // parameter hash is what makes a NEW course rebuild (the cache is hash-keyed), and
            // the explicit invalidate is what makes a hash COLLISION harmless. Same pairing as
            // SpawnableBase.SetSeed.
            InvalidateCache();
        }

        Vector3[] _spawnPads;

        /// <summary>
        /// The pilots' spawn pads, IN THE SAME FRAME as the course (see <see cref="SetCourse"/>) -
        /// so the caller applies the cell offset to both or to neither.
        ///
        /// <para>The shoals are strung along legs that pass through the spawn ring, so without
        /// this a cluster of rubble can materialise on a pad. Unlike the course's own pad
        /// rejection this is only a scoring term with a fallback, so it cannot change how many
        /// prisms the arena lays - which is what keeps the measured PhaseThresholds exact.</para>
        /// </summary>
        public void SetSpawnPads(IReadOnlyList<Vector3> pads)
        {
            _spawnPads = pads == null || pads.Count == 0 ? null : new Vector3[pads.Count];
            if (_spawnPads != null)
                for (int i = 0; i < pads.Count; i++) _spawnPads[i] = pads[i];

            InvalidateCache();
        }

        /// <summary>The course this arena was last posed with, for the controller's own use (the
        /// switch rings are hung at these same poses).</summary>
        public IReadOnlyList<BreakwaterStation> Course => _course;

        // ── Segments: one laid structure each ───────────────────────────────────────────────

        /// <summary>
        /// One laid structure - a contiguous slice of
        /// <see cref="CellEnvironmentSpawnableBase._cachedLays"/> plus the dimension it declares.
        /// The base lays an environment as ONE trail, which is wrong here for the same reason it
        /// was wrong in the Switchyard: <c>PrismscapeTopology.DimensionOf</c> reads the authored
        /// trail, so a dish (a cone SHELL) and a plug (a woven SOLID) sharing one trail would
        /// route a rider onto the wrong ride. Nobody rides a Breakwater station today - it is a
        /// Sparrow-only mode - which is exactly why the declaration has to be right now: an
        /// honest dimension costs one enum value at lay time, and a dishonest one is a bug that
        /// waits for the first vessel that can attach.
        /// </summary>
        readonly struct Segment
        {
            public readonly int Start;
            public readonly int Count;
            public readonly PrismscapeDimension Dimension;
            public readonly string Label;

            public Segment(int start, int count, PrismscapeDimension dimension, string label)
            {
                Start = start;
                Count = count;
                Dimension = dimension;
                Label = label;
            }
        }

        readonly List<Segment> _segments = new(128);

        /// <summary>Everything one station's builder emitted, buffered so the parts can be sorted
        /// into their two trails WITHOUT depending on the order they arrived in - see
        /// <see cref="BuildStation"/>. Reused across all fourteen.</summary>
        readonly List<PrismLay> _stationScratch = new(320);

        /// <summary>Cached so fourteen stations do not mint fourteen closures during a load. It
        /// captures only <see cref="_stationScratch"/>, never the station, which is what lets one
        /// delegate serve every call.</summary>
        Action<Vector3, Quaternion, Vector3, PrismKind> _captureStationPrism;

        // ── Build ───────────────────────────────────────────────────────────────────────────

        protected override void BuildEnvironment()
        {
            _segments.Clear();

            if (_course.Count < 2)
            {
                // Loud, and it names the fix. A Breakwater cell that was never posed builds an
                // EMPTY arena, and an empty arena is indistinguishable on screen from a load that
                // has not finished - the failure that would otherwise be diagnosed as a hang. One
                // line per build attempt, which for this failure means one line: a spawnable that
                // was not posed is not going to be posed on the next frame either.
                CSDebug.LogWarning(
                    $"[SpawnableBreakwater] Spawned with {_course.Count} station(s). The " +
                    "controller must call SetCourse with the broadcast course BEFORE Spawn; " +
                    "nothing will be laid.", this);
                return;
            }

            for (int i = 0; i < _course.Count; i++)
                BuildStation(i);

            BuildShoals();
        }

        /// <summary>
        /// One station, as TWO segments: the dish (a cone shell -
        /// <see cref="PrismscapeDimension.Surface"/>) and the throat, meaning the keystone collar
        /// and the danger weave together (a solid - <see cref="PrismscapeDimension.Volume"/>).
        ///
        /// <para><b>The parts are sorted by GEOMETRY, not by emit order.</b>
        /// <see cref="BreakwaterStationBuilder"/> happens to emit outside-in, but its own
        /// documentation says nothing depends on that ("the lay is a set"), and slicing the lay
        /// list on an assumed order is precisely the kind of dependency that survives review and
        /// breaks the day somebody reorders three lines for a nicer reveal. So the builder's
        /// output is buffered and then walked twice, once per segment.</para>
        ///
        /// <para><b>The discriminator has real margin, not a cliff.</b> Danger is the weave.
        /// Among the Plain prisms, the collar's twelve cubes sit at
        /// <c>BreakwaterStationBuilder.CollarRadius</c> = 22 from the axis and the dish's
        /// innermost ring sits at the PORT radius (42 at the tightest intensity, 72 at the
        /// widest), so the midpoint of those two separates them by 10 units either way at the
        /// worst case and by 25 at the best. That is a tolerance with a chasm in the middle of
        /// it rather than a boundary a float can land on - the shape the Switchyard's
        /// <c>BurrMatchRadius</c> settled on after a quantize-to-whole-units key was rejected for
        /// having one.</para>
        /// </summary>
        void BuildStation(int index)
        {
            var station = _course[index];

            _stationScratch.Clear();
            _captureStationPrism ??= (pos, rot, scale, kind) =>
                _stationScratch.Add(new PrismLay(new SpawnPoint(pos, rot, scale), Domains.Blue, kind));

            BreakwaterStationBuilder.Build(station, _captureStationPrism);

            Vector3 axis = station.Axis.sqrMagnitude > 1e-6f ? station.Axis.normalized : Vector3.forward;
            float split = (BreakwaterStationBuilder.CollarRadius + station.PortRadius) * 0.5f;
            float splitSq = split * split;

            // Two passes over the buffer, each emitting only its own part, so each segment lands
            // as one contiguous run of _cachedLays. Emit is the base's - it applies the spawn
            // clearance rejection, which is why the parts are re-emitted rather than appended.
            EmitStationPart(index, station.Position, axis, splitSq, throat: false,
                PrismscapeDimension.Surface, "DISH");
            EmitStationPart(index, station.Position, axis, splitSq, throat: true,
                PrismscapeDimension.Volume, "THROAT");
        }

        /// <summary>Emit every buffered prism on one side of the dish/throat split, and record the
        /// contiguous range as a segment.</summary>
        void EmitStationPart(int index, Vector3 centre, Vector3 axis, float splitSq, bool throat,
                             PrismscapeDimension dimension, string label)
        {
            int start = _cachedLays.Count;

            for (int i = 0; i < _stationScratch.Count; i++)
            {
                var lay = _stationScratch[i];
                bool isThroat = lay.Kind == PrismKind.Danger ||
                                Vector3.ProjectOnPlane(lay.Point.Position - centre, axis).sqrMagnitude < splitSq;
                if (isThroat != throat) continue;

                Emit(lay.Point.Position, lay.Point.Rotation, lay.Point.Scale, Domains.Blue, lay.Kind);
            }

            int count = _cachedLays.Count - start;
            if (count > 0)
                _segments.Add(new Segment(start, count, dimension, $"{label}{index}"));
        }

        // ── Shoals ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The rubble between the stations: six clusters of seven 4-unit cubes strung along each
        /// of the thirteen legs, off to the side of the flight line.
        ///
        /// <para><b>It is the mode's ammunition, and that is why it is not scenery.</b> A rocket
        /// costs 50 hostile prisms; a station's dish roughly funds the next door; the shoals are
        /// what a pilot who missed - who threaded when they should have fired, or sawed and fell
        /// behind - can go and mine without leaving the course. Placing it 40 to 110 units off
        /// the leg is the whole trade: near enough to be worth a detour, far enough that nobody
        /// meets it by accident.</para>
        ///
        /// <para><b>Every cluster is <see cref="PrismKind.Plain"/>, deliberately.</b> Danger
        /// rubble strewn along the racing line would be an unsignalled hazard between stations,
        /// and this mode's danger is concentrated entirely in the weave - a place the pilot chose
        /// to fly into, having looked at it.</para>
        ///
        /// <para><b>The count is EXACT and must stay so.</b>
        /// <c>Tools/Build/breakwater_arena.py</c> prices the shoals at
        /// <c>legs x 6 x 7 = 546</c> prisms with no rejection term, and the cell's PhaseThresholds
        /// are that number plus the stations'. So a cluster that cannot find a legal spot is
        /// PLACED AT ITS BEST CANDIDATE rather than dropped: degrading a clearance by a few units
        /// is invisible in play, while dropping clusters would make the authored thresholds
        /// describe an arena heavier than the one that shipped - the model would be measuring a
        /// world that does not exist, which is the one failure this whole family of closed-form
        /// builders is written to prevent. (Measured over 120 seeds x 4 intensities: 546 shoal
        /// prisms on every course, and the fallback never fired.)</para>
        /// </summary>
        void BuildShoals()
        {
            float clusterRadius = ClusterRadius;

            for (int leg = 0; leg < _course.Count - 1; leg++)
            {
                Vector3 a = _course[leg].Position;
                Vector3 b = _course[leg + 1].Position;
                Vector3 along = b - a;
                float length = along.magnitude;

                // Unreachable: BreakwaterCourse.MinSeparationFor floors every leg well above
                // this. Guarded anyway because the alternative is a normalize of a zero vector
                // and 42 prisms stacked on one point.
                if (length < 1f) continue;
                along /= length;

                Vector3 u = Vector3.ProjectOnPlane(Vector3.up, along);
                if (u.sqrMagnitude < 1e-4f) u = Vector3.ProjectOnPlane(Vector3.right, along);
                u.Normalize();
                Vector3 v = Vector3.Cross(along, u);

                // The binding clearance is the wider of the two endpoint stations'. Every station
                // on a course shares one port radius today (the ladder is per-course, not
                // per-station), so this is the same number twice - written as a max because a
                // future mixed-port course must not silently place rubble inside the bigger dish.
                float clear = Mathf.Max(EndClearance(_course[leg]), EndClearance(_course[leg + 1]))
                              + clusterRadius;

                // THE WINDOW IS DERIVED, NOT SAMPLED. A cluster at (s along, w lateral) clears
                // both endpoints iff s^2 + w^2 >= clear^2 and (L-s)^2 + w^2 >= clear^2, so a
                // lateral offset admits any s at all only when w^2 >= clear^2 - (L/2)^2. Drawing
                // w from that floor upward and s from the window it opens makes the endpoint
                // clearances hold BY CONSTRUCTION, and leaves the retry loop below with only the
                // non-adjacent stations and the other legs to answer.
                //
                // MEASURED, over the real walk rather than guessed: a naive uniform draw over
                // (s in [0,L], w in [40,110]) clears the two endpoints only 13.5% of the time at
                // intensity 1, so 12 proposals would have sent about one cluster in six to the
                // fallback below. With the window derived, 37,440 clusters over 120 seeds x 4
                // intensities took the fallback ZERO times - and the tightest window the
                // derivation ever opened was 9.46 units of lateral freedom, on intensity 1's
                // shortest legs, which is exactly where the arithmetic said it would bind.
                float half = length * 0.5f;
                float floorOffset = Mathf.Sqrt(Mathf.Max(0f, clear * clear - half * half));
                float wLo = Mathf.Max(shoalOffsetMin, floorOffset);
                float wHi = Mathf.Max(wLo, shoalOffsetMax);

                for (int cluster = 0; cluster < ShoalClustersPerLeg; cluster++)
                {
                    Vector3 best = a + along * half + u * wHi;
                    float bestMargin = float.NegativeInfinity;

                    for (int attempt = 0; attempt < shoalPlacementAttempts; attempt++)
                    {
                        float w = Mathf.Lerp(wLo, wHi, ShoalHash(leg, cluster, attempt, 0));
                        float sLo = Mathf.Sqrt(Mathf.Max(0f, clear * clear - w * w));
                        float sHi = length - sLo;
                        float s = sHi > sLo ? Mathf.Lerp(sLo, sHi, ShoalHash(leg, cluster, attempt, 1)) : half;

                        // Clusters are spread in AZIMUTH around the leg, one per sixth, so six of
                        // them can share a tight s window (which is all intensity 1's shortest leg
                        // offers) and still stand a full chord apart.
                        float phi = (cluster + ShoalHash(leg, cluster, attempt, 2)) *
                                    (Mathf.PI * 2f / ShoalClustersPerLeg);
                        Vector3 centre = a + along * s + (Mathf.Cos(phi) * u + Mathf.Sin(phi) * v) * w;

                        float margin = ClusterMargin(centre, clusterRadius);
                        if (margin > bestMargin)
                        {
                            bestMargin = margin;
                            best = centre;
                        }
                        if (margin >= 0f) break;
                    }

                    EmitCluster(best, leg, cluster);
                }
            }
        }

        /// <summary>A station's no-rubble radius: its dish rim plus the authored clearance. One
        /// expression, read by the derived window AND by the rejection test, so the two cannot
        /// disagree about what "clear of a station" means.</summary>
        float EndClearance(in BreakwaterStation station) =>
            BreakwaterStationBuilder.DishRadius(station.PortRadius) + shoalStationClearance;

        /// <summary>
        /// How much room a cluster centred here has to spare - negative when it violates
        /// something. Both tests measure to the cluster's SURFACE by adding
        /// <paramref name="clusterRadius"/>, because a clearance stated for "a cluster" and
        /// measured to its centre lets a 30-unit rule be satisfied by 3 units of air and 27 units
        /// of prism.
        /// </summary>
        float ClusterMargin(Vector3 centre, float clusterRadius)
        {
            float margin = float.PositiveInfinity;

            for (int i = 0; i < _course.Count; i++)
                margin = Mathf.Min(margin,
                    (_course[i].Position - centre).magnitude - EndClearance(_course[i]) - clusterRadius);

            for (int i = 0; i < _course.Count - 1; i++)
                margin = Mathf.Min(margin,
                    DistanceToSegment(centre, _course[i].Position, _course[i + 1].Position)
                    - shoalSplineClearance - clusterRadius);

            // THE SPAWN PADS, for the same reason the walk rejects a station that reaches one:
            // pilots spawn at 480 on the equator, inside the shell this rubble is strung through.
            // A cluster is small enough that the odds are long, but this is a SCORING term with a
            // best-margin fallback, so it can never drop a cluster and the arena's prism count is
            // exactly the number the PhaseThresholds were measured against either way.
            if (_spawnPads != null)
                for (int i = 0; i < _spawnPads.Length; i++)
                    margin = Mathf.Min(margin,
                        (_spawnPads[i] - centre).magnitude - shoalSpawnPadClearance - clusterRadius);

            return margin;
        }

        /// <summary>
        /// One clump: a centre cube plus six on a small phyllotaxis shell around it.
        ///
        /// <para>Scale is <see cref="ShoalCube"/> on every axis with <b>no jitter</b> (see the
        /// constant). The shell radius and every prism's rotation vary instead, which is enough
        /// for a field of them to read as rubble rather than as a lattice and moves no volume at
        /// all.</para>
        ///
        /// <para><b>Nothing interpenetrates, at any rotation, and it is arithmetic rather than
        /// luck.</b> A rotated 4-cube occupies a sphere of radius 3.46, so two of them need 6.93
        /// units between centres. Centre to shell is at least <c>shoalClumpRadius</c> = 8. Shell
        /// to shell: six phyllotaxis directions have a minimum unit chord of 1.2599, which at the
        /// radius floor is 10.08 units - and since each prism draws its own radius factor, the
        /// worst mixed pair (one at 8, one at 11.2, 78.5 degrees apart) still stands 12.4 units
        /// apart. Both margins survive the whole 1.0-1.4 factor range, so the clump can be
        /// widened without re-checking this.</para>
        /// </summary>
        void EmitCluster(Vector3 centre, int leg, int cluster)
        {
            int start = _cachedLays.Count;
            Vector3 scale = new Vector3(ShoalCube, ShoalCube, ShoalCube);

            for (int i = 0; i < ShoalPrismsPerCluster; i++)
            {
                Vector3 offset = Vector3.zero;
                if (i > 0)
                {
                    float factor = Mathf.Lerp(ClumpRadiusFactorMin, ClumpRadiusFactorMax,
                        ShoalHash(leg, cluster, ClumpSalt, i));
                    offset = ClumpDirection(i - 1, ShoalPrismsPerCluster - 1) * (shoalClumpRadius * factor);
                }

                var rot = Quaternion.Euler(
                    360f * ShoalHash(leg, cluster, ClumpSalt + 1, i),
                    360f * ShoalHash(leg, cluster, ClumpSalt + 2, i),
                    360f * ShoalHash(leg, cluster, ClumpSalt + 3, i));

                Emit(centre + offset, rot, scale, Domains.Blue, PrismKind.Plain);
            }

            int count = _cachedLays.Count - start;
            if (count > 0)
                _segments.Add(new Segment(start, count, PrismscapeDimension.Volume,
                    $"SHOAL{leg}-{cluster}"));
        }

        /// <summary>The attempt index the clump's own draws use. Well past
        /// <see cref="shoalPlacementAttempts"/>'s serialized ceiling of 32, so a clump can never
        /// share a hash slot with a placement proposal.</summary>
        const int ClumpSalt = 64;

        /// <summary>The half-extent of a whole cluster: its furthest prism centre plus that
        /// prism's own circumscribing half-diagonal. Derived rather than authored so that
        /// widening the clump automatically widens every clearance it has to satisfy.</summary>
        float ClusterRadius => shoalClumpRadius * ClumpRadiusFactorMax + ShoalCube * 0.5f * 1.7320508f;

        /// <summary>Phyllotaxis point on the unit sphere - the same construction the Orrery, the
        /// Drum and the Switchyard's burrs use, so a clump of rubble is built out of the
        /// vocabulary the rest of the game's scatter is built out of.</summary>
        static Vector3 ClumpDirection(int i, int n)
        {
            float y = 1f - 2f * (i + 0.5f) / n;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        /// <summary>
        /// The shoals' only source of variety, and it is a PURE FUNCTION OF INDICES.
        ///
        /// <para>Not <c>UnityEngine.Random</c> (global mutable state), not the inherited
        /// <c>System.Random</c> (whose sequence is a property of the runtime rather than of the
        /// seed - the trap <c>Docs/WEEKLY_CHALLENGE.md</c> records), and deliberately not
        /// <see cref="CellEnvironmentSpawnableBase.N01"/> either, which is seeded: the arena is
        /// rebuilt on every peer from a BROADCAST course, so a draw that depends on nothing but
        /// that course is one fewer thing that has to agree across machines - and it survives a
        /// <c>SetSeed</c> call that only one peer made.</para>
        /// </summary>
        static float ShoalHash(int leg, int cluster, int attempt, int slot) =>
            Hash01(unchecked(leg * 73856093 ^ cluster * 19349663 ^ attempt * 83492791 ^ slot * 40503));

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq);
            return (p - (a + ab * t)).magnitude;
        }

        // ── Laying: one trail per structure ─────────────────────────────────────────────────

        /// <summary>
        /// Lays every segment as its own trail.
        ///
        /// <para>Segments are laid SEQUENTIALLY inside ONE <c>BeginArenaBuild</c> bracket rather
        /// than fired off concurrently, for the reason the Switchyard records: the lay budget is a
        /// shared per-frame counter, so 106 concurrent lays would still place only a frame's worth
        /// of prisms - but each would first request its own 256-prism async CLONE batch, i.e. the
        /// whole arena cloned in one frame, which is the exact spike the budget exists to prevent.
        /// The bracket is also what holds the arena-ready gate closed across the gaps BETWEEN
        /// segments, when no lay is in flight and an absence-of-activity check would misread the
        /// pause as "arena done" and drop the connecting screen onto a half-built course.</para>
        ///
        /// <para>Every trail is registered UP FRONT, before a prism is laid: a <c>Trail</c> is a
        /// live object its consumers read as it fills, so nothing downstream has to wait on the
        /// lay to have a valid handle.</para>
        /// </summary>
        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedLays == null || _segments.Count == 0) return;

            var trailsBySegment = new Trail[_segments.Count];
            for (int i = 0; i < _segments.Count; i++)
            {
                // isLoop FALSE everywhere: nothing here is a closed ribbon. The dimension is the
                // load-bearing half - see Segment.
                var trail = new Trail(false) { Dimension = _segments[i].Dimension };
                trails.Add(trail);
                trailsBySegment[i] = trail;
            }

            if (Application.isPlaying)
                LaySegmentsAsync(container, trailsBySegment).Forget();
            else
                LaySegmentsSync(container, trailsBySegment);
        }

        async UniTaskVoid LaySegmentsAsync(GameObject container, Trail[] trailsBySegment)
        {
            // Announced BEFORE the first await: the gate must already be closed when the very
            // first frame of the build ticks it.
            PrismTrailBuilder.BeginArenaBuild();
            try
            {
                for (int i = 0; i < _segments.Count; i++)
                {
                    if (!container) return;
                    var seg = _segments[i];
                    await PrismTrailBuilder.LayBudgetedAsync(
                        prism, SegmentLays(seg), container.transform, trailsBySegment[i],
                        $"{container.name}::{seg.Label}", LayBudgetMsPerFrame);
                }
            }
            finally
            {
                PrismTrailBuilder.EndArenaBuild();
            }
        }

        void LaySegmentsSync(GameObject container, Trail[] trailsBySegment)
        {
            for (int i = 0; i < _segments.Count; i++)
                PrismTrailBuilder.LaySync(prism, SegmentLays(_segments[i]), container.transform,
                    trailsBySegment[i], $"{container.name}::{_segments[i].Label}");
        }

        /// <summary>
        /// One segment's lays, decimated INSIDE the segment. The base applies preview thinning to
        /// the whole cached list at once; doing that here would stride across segment boundaries
        /// and hand a station's dish trail somebody else's shoal.
        /// </summary>
        List<PrismLay> SegmentLays(in Segment seg)
        {
            var slice = new List<PrismLay>(seg.Count);
            for (int i = 0; i < seg.Count; i++)
                slice.Add(_cachedLays[seg.Start + i]);
            return PrismLayDecimation.Apply(slice);
        }

        // ── Cache contract ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>Every station's pose is folded in.</b> The course is the arena's only real input and
        /// it arrives after construction, so a hash that covered only the serialized fields would
        /// hand the second match of a session the first match's course - the cache would be
        /// perfectly valid and completely wrong. Position, axis AND port radius all count: two
        /// courses can share every position and differ in how a single station is turned.
        /// </summary>
        protected override int BuildParameterHash()
        {
            int hash = System.HashCode.Combine(
                nameof(SpawnableBreakwater),
                /* layout revision - bump to invalidate caches after a code-level change */ 1,
                _course.Count,
                System.HashCode.Combine(shoalOffsetMin, shoalOffsetMax, shoalStationClearance,
                    shoalSplineClearance),
                System.HashCode.Combine(shoalClumpRadius, shoalPlacementAttempts,
                    shoalSpawnPadClearance, _spawnPads?.Length ?? 0));

            for (int i = 0; i < _course.Count; i++)
                hash = System.HashCode.Combine(hash, _course[i].Position, _course[i].Axis,
                    _course[i].PortRadius);

            if (_spawnPads != null)
                for (int i = 0; i < _spawnPads.Length; i++)
                    hash = System.HashCode.Combine(hash, _spawnPads[i]);

            return hash;
        }
    }
}
