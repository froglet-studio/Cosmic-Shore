using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The three-layer prison of <see cref="GameModes.WildlifeLiberation"/> - three concentric
    /// cages with a very wide, empty room between each pair, and every tier of wildlife mixed
    /// through all of it (see <see cref="RoamInner"/>).
    ///
    /// This is NOT Cleave. Cleave is a layered ORANGE whose bone IS the score - dense, tight,
    /// five rinds you scrape through. This is a JAIL: a sparse open lattice of long bars with
    /// big triangular gaps, so few prisms that the arena reads as mostly empty space, because
    /// here the prisms are only the walls and the FAUNA are the objective. Three properties are
    /// load-bearing:
    ///
    ///   • THREE SHELLS, ALWAYS, at a fixed <see cref="ShellRadii"/> of 1050 / 600 / 200 - and
    ///     the shell count is deliberately NOT the intensity dial the way it is in Cleave. The
    ///     shells divide the arena into ROOMS (see <see cref="RoomInner"/>/<see cref="RoomOuter"/>)
    ///     a hunter breaks into; the radial gaps are enormous on purpose - 450u between the outer
    ///     and middle cages, 400u between the middle and the core - so each room is a place you
    ///     fly INTO rather than a rind you pass through. The rooms are architecture only: every
    ///     species roams the whole arena (see <see cref="RoamInner"/>), so a room is a place the
    ///     wildlife passes through, never a tier locked inside one.
    ///   • THE OPENINGS ARE TRIANGLES, from a GEODESIC (subdivided icosahedron) rather than
    ///     Cleave's latitude hoops. That is a fairness property, not a style one: a latitude
    ///     sphere is inherently densest at its poles, which is why Cleave has to tilt every
    ///     rind onto its own axis to stop everyone drilling the top. A geodesic has no poles -
    ///     every approach meets the same weave - so this cage needs no tilt table at all.
    ///   • INTENSITY IS THE ONLY THING THIS TABLE RAMPS, and it ramps SHAPE + WEAVE, never the
    ///     layer count (<see cref="ShellPlans"/>): intensities 1-2 are three geodesic spheres
    ///     that tighten; 3 swaps the outer cage for a BOX (square rail grid, heavy corner posts
    ///     - the "boxing ring"); 4 is three nested boxes at the tightest weave in the mode. The
    ///     WILDLIFE ROSTER is identical at every intensity, so this table carries the entire
    ///     difficulty curve. Cell picks the variant per intensity through
    ///     <c>CellTypeChoiceOptions.IntensityWise</c>, exactly like Cleave.
    ///
    /// Every bar is <see cref="PrismKind.Plain"/> except the sparse <see cref="PrismKind.Danger"/>
    /// traps salted through the CORE cage (the maximum-security room). Nothing is
    /// <see cref="PrismKind.Shielded"/> or <see cref="PrismKind.SuperShielded"/>, and that is a
    /// GEOMETRY decision as much as a gameplay one: a shield reaches 1.5x the prism's own leafSize
    /// (<c>OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE</c>, Docs/ECOSYSTEM.md 35), which on a 26u
    /// bar laid every 34u would fuse this sparse lattice into a solid tube - and it would cost the
    /// one-hit break-in the mode is built on. The cage was previously kept out of the food web by
    /// the per-room fauna pens instead; with those replaced by one arena-wide roam band it is
    /// grazeable, which is a deliberate, stated trade - see <see cref="RoamInner"/>.
    ///
    /// Painted across the full domain triad so the jail reads as neutral, contested structure
    /// rather than any one team's property. Deterministic per seed like every cell environment -
    /// clients build locally with no seed sync.
    ///
    /// Budget: see <c>Tools/Build/wildlife_cage_budget.py</c> (the analytic model the asset
    /// generator imports) and <c>WILDLIFE_LIBERATION.md</c> for the per-shell table and the
    /// collider-budget statement.
    /// </summary>
    public class SpawnableWildlifeCage : CellEnvironmentSpawnableBase
    {
        // ── The arena ────────────────────────────────────────────────────────
        //
        // Fixed at every intensity, for the same reason Cleave fixes its outer radius: the
        // player spawn ring, the AI's aim points, the fauna roam band and the arena silhouette are
        // all defined against these numbers, and the membrane is 1200. Intensity varies the
        // WEAVE and the SHAPE (see ShellPlans), never the radii.

        /// <summary>Outer / middle / core cage radii. Index 0 is the outermost.</summary>
        public static readonly float[] ShellRadii = { 1050f, 600f, 200f };

        /// <summary>Number of cages. Fixed - one per room a hunter breaks into.</summary>
        public const int ShellCount = 3;

        /// <summary>
        /// The outermost cage, exposed so the controller can aim its AI hunters and the asset
        /// generator can place the spawn ring without a second copy of the number.
        /// </summary>
        public const float OuterRadius = 1050f;

        /// <summary>
        /// Clearance a room's usable interior keeps from each wall, so a point described as
        /// "in this room" is never sitting on a bar.
        /// </summary>
        public const float RoomWallClearance = 60f;

        /// <summary>
        /// Inner radius of <paramref name="shell"/>'s room - the shell below it plus clearance,
        /// or 0 for the core (whose room reaches the centre).
        ///
        /// This is the cage's ARCHITECTURE and nothing else: the rooms still exist, because the
        /// three cages still divide the arena into them. What it is NO LONGER is a fauna pen -
        /// see <see cref="RoamInner"/>. Its one live consumer is the AI hunters' patrol, which
        /// steps through the rooms to sweep the arena radially.
        /// </summary>
        public static float RoomInner(int shell) =>
            shell == OpenWaterRoom ? OpenWaterInner
            : shell + 1 < ShellCount ? ShellRadii[shell + 1] + RoomWallClearance
            : 0f;

        /// <summary>Outer radius of <paramref name="shell"/>'s room - just inside that shell.</summary>
        public static float RoomOuter(int shell) =>
            shell == OpenWaterRoom ? OpenWaterOuter : ShellRadii[shell] - RoomWallClearance;

        /// <summary>
        /// THE fauna band, and there is exactly one: every species in this biome roams the WHOLE
        /// arena, from the core out to just inside the membrane.
        ///
        /// It replaces the three-tier pen this mode shipped with, where each species was banded
        /// to one room - the outer swarm on the outer shell, the mid tier in the middle, the
        /// biggest creatures locked in the core. That read as three separate aquariums stacked
        /// around a boss room, so the fight converged wherever a player broke in, and the apex
        /// creatures were only ever findable in one place. Mixing every tier through one volume
        /// is what makes the mode a HUNT: what you meet next is a roll, not a radius.
        ///
        /// Still a spatial DIET + STEERING rule, never a wall (Docs/ECOSYSTEM.md 22) - it is
        /// simply now the same rule for everybody, and wide enough that its only real job is
        /// keeping creatures off the membrane.
        ///
        /// THE COST, stated because the pens were what paid for it: a room-banded creature could
        /// not reach its own cage, so every bar was safely outside the food web. One arena-wide
        /// band puts all three cages inside it, and in a nucleus-less cell herbivores eat
        /// opposing-domain mass - so the triad-painted bars are now grazeable and the cage erodes
        /// as a match runs. That is the food web working, not a bug, and shielding the bars is
        /// NOT the answer: a shield reaches 1.5x leafSize (Docs/ECOSYSTEM.md 35), which at a 34u
        /// bar step would fuse this sparse lattice into a solid tube and cost the one-hit
        /// break-in the mode is built on. If playtest says the erosion is too fast, raise
        /// <see cref="RoamInner"/> off 0 or cut the population - do not shield the cage.
        /// </summary>
        public const float RoamInner = 0f;

        /// <summary>Outer wall of the fauna roam band - just inside the 1200u membrane.</summary>
        public const float RoamOuter = 1180f;

        /// <summary>
        /// The OPEN WATER outside the outer cage, between it and the 1200u membrane - a fourth
        /// room, indexed <see cref="OpenWaterRoom"/>.
        ///
        /// It exists because the first pass put every creature inside the cages and every fight
        /// therefore converged on the middle of the arena. It is now the outermost slice of the
        /// single roam band rather than a room of its own, but the property that mattered still
        /// holds: the player ring sits at 1150, inside it, so there is something to shoot from
        /// the moment you spawn and breaking into a cage is a choice rather than the only way to
        /// score. <see cref="OpenWaterOuter"/> is what <see cref="RoamOuter"/> is measured from.
        /// </summary>
        public const float OpenWaterInner = 1090f;
        public const float OpenWaterOuter = 1180f;

        /// <summary>Room index of the open water - one past the innermost shell.</summary>
        public const int OpenWaterRoom = ShellCount;

        /// <summary>Rooms wildlife can occupy: one per cage, plus the open water outside.</summary>
        public const int RoomCount = ShellCount + 1;

        // ── Shape ────────────────────────────────────────────────────────────

        /// <summary>How one cage is woven.</summary>
        public enum CageForm
        {
            /// <summary>Subdivided icosahedron - TRIANGULAR openings, no poles, uniform weave.</summary>
            Geodesic = 0,
            /// <summary>Cube with a rail grid per face - SQUARE openings and heavy corner posts ("the boxing ring").</summary>
            Boxed = 1,
        }

        /// <summary>One cage's authored weave.</summary>
        readonly struct ShellPlan
        {
            public readonly CageForm Form;
            /// <summary>Subdivision frequency: geodesic faces per icosa face edge, or rail cells per cube face edge.</summary>
            public readonly int Frequency;
            public ShellPlan(CageForm form, int frequency) { Form = form; Frequency = frequency; }
        }

        /// <summary>
        /// The intensity ladder, authored rather than generated: [intensity-1][shell]. Two things
        /// are deliberate here.
        ///
        /// First, intensity NEVER changes the shell count - every intensity has all three rooms,
        /// because each room is somewhere a hunter breaks into and dropping one would delete a
        /// third of the arena rather than make it easier. What rises is how tightly each cage is
        /// woven (harder to shoot a hole through) and, from intensity 3, its SHAPE.
        ///
        /// Second, the high intensities swap the outer cages to <see cref="CageForm.Boxed"/> -
        /// square-and-rectangle rail grids with corner posts. A box is a genuinely different
        /// problem from a sphere: its flat faces mean an approach is either square-on (a long
        /// straight run at a dense wall) or into a corner (three walls converging), where a
        /// geodesic presents the same weave from everywhere. The core stays geodesic at every
        /// intensity so the innermost room keeps the "cell" read.
        /// </summary>
        static readonly ShellPlan[][] ShellPlans =
        {
            // intensity 1 - three spheres, the most open weave (openings 251 / 179 / 79u)
            new[] { new ShellPlan(CageForm.Geodesic,  5), new ShellPlan(CageForm.Geodesic,  4), new ShellPlan(CageForm.Geodesic,  3) },
            // intensity 2 - same shapes, every cage a step tighter (180 / 144 / 60u)
            new[] { new ShellPlan(CageForm.Geodesic,  7), new ShellPlan(CageForm.Geodesic,  5), new ShellPlan(CageForm.Geodesic,  4) },
            // intensity 3 - the outer cage becomes a boxing ring (87 / 103 / 48u)
            new[] { new ShellPlan(CageForm.Boxed,    14), new ShellPlan(CageForm.Geodesic,  7), new ShellPlan(CageForm.Geodesic,  5) },
            // intensity 4 - three nested boxing rings, the tightest weave (67 / 38 / 19u)
            new[] { new ShellPlan(CageForm.Boxed,    18), new ShellPlan(CageForm.Boxed,    18), new ShellPlan(CageForm.Boxed,    12) },
        };
        // TWO things to know before retuning this table.
        //
        // (1) The box frequencies are much higher than the geodesic ones and that is not a
        //     typo: a cube face grid at frequency f contributes 12f² segments against a
        //     geodesic's 30f², and the box is smaller (corners on the radius ⇒ faces at
        //     0.577·r), so matching frequencies would make the "harder" intensities LIGHTER and
        //     more open than the easy ones.
        //
        // (2) THIS TABLE IS THE WHOLE INTENSITY RAMP. The wildlife roster is deliberately
        //     IDENTICAL at every intensity (~594 creatures at seed, ~1,391 at the caps), so
        //     everything that makes intensity 4 harder than intensity 1 is here: the weave
        //     tightens (outer openings 251 → 180 → 87 → 67u) and the shape goes sphere →
        //     sphere → one box → three nested boxes. Prism totals 9,206 → 12,696 → 13,244 →
        //     13,956.
        //
        // Values come from the measured table in wildlife_cage_budget.py. Re-tune there and
        // re-run the asset generator, never by eye - the rounding in the per-segment prism walk
        // is not monotonic in frequency, so an eyeballed bump can easily make a cage lighter.

        [Header("Wildlife cage")]
        [Tooltip("Which row of the intensity ladder this prefab variant builds (1-4). THE " +
                 "INTENSITY DIAL: author one prefab variant per intensity and point each " +
                 "intensity's CellConfigDataSO at the matching variant (Cell picks by " +
                 "IntensityWise). Unlike Cleave this never changes the SHELL COUNT - always " +
                 "three cages - only how each one is woven and shaped.")]
        [SerializeField, Range(1, 4)] int intensityTier = 1;

        // Bar geometry. Long, sparse prisms: this is a jail of bars, not a woven bone rind, so
        // the step is deliberately larger than the prism is long - the bars read as separate
        // rungs with air between them rather than a continuous surface.
        const float BarStep = 34f;          // spacing along a bar run
        const float BarLength = 26f;        // long axis of a bar prism
        const float BarThickness = 4.2f;
        const float PostSize = 7.5f;        // joints / cube corner posts - the cage's anchors

        // ── Flush: no prism of the cage passes through another ───────────────
        //
        // NO PRISM MAY INTERPENETRATE ANOTHER (Docs/ECOSYSTEM.md §49.10), held here at authoring
        // time by sizing each bar from the geometry it already has - no pass at load. Measured by
        // Tools/Build/audit_prism_clipping.py before this: 2,596 (intensity 1) to 7,786
        // (intensity 4) pairs, in three places, every one at a JOINT or on a short run:
        //
        //   1. A run shorter than a bar: n = round(len / 34) bars split the run evenly, so a
        //      run's step can be ~23-28u against a 26u bar that Jit grows to 31u. A bar is now
        //      no longer than the gap to its neighbours' centres on the run (measured along the
        //      run, so the sphere projection - which bunches the bars toward a run's ends - is
        //      priced), which is face to face.
        //   2. The end bars ran into the POST at their joint. An end bar now stops where the
        //      post's own extent along the run ends (Joints.Clearance), so the rail meets the post
        //      face to face rather than through it. That is the post as actually DRAWN: the
        //      shell's jitter is drawn up front, in exactly the order the lay always consumed it
        //      (ShellJitter), so a bar knows the size of the post it meets.
        //   3. At a joint with no post (a box face's interior crossings), or where the post is
        //      smaller than the bars' fan, the end bars of two runs met inside each other. They
        //      now stand off the joint by as much as two bars at the joint's narrowest angle need.
        //
        // The boxed cages' posts are big against their rail step (a 14.25u corner block on a
        // 19.2u step at intensity 4's core), so a post is ALSO capped (PostHalfCap): never so big
        // that the shortest bar beside it could not be laid at the prism floor. Every prism keeps
        // its draw, its position and its rotation; only the bar lengths (and, where the box is
        // that tight, a post) shrink. Tools/Build/wildlife_cage_budget.py mirrors every number.

        /// <summary>The prism prefab's per-axis scale floor (PrismScaleAnimator.minScale): an axis
        /// sized under it is grown back to it at lay time, so no flush size may go below it.</summary>
        const float PrismFloor = 0.5f;

        /// <summary><see cref="Jit"/>'s amount for every bar and post: one factor in [0.8, 1.2).</summary>
        const float CageJitter = 0.2f;

        /// <summary>The widest a bar's half cross-section can be drawn - the jitter's top. A joint
        /// is cleared against the thickest bar that could meet it.</summary>
        const float BarHalfThickMax = BarThickness * (1f + CageJitter) * 0.5f;

        /// <summary>
        /// How far an end bar stands off a GEODESIC joint so that it clears every other bar of
        /// that joint: two bars of half-width w whose runs leave a joint at angle a clear each
        /// other once both start w·cot(a/2) out. The narrowest angle between two runs at any
        /// joint of any shipped frequency is 54.6° (frequency 3; 56-58° above it), taken as 54°,
        /// with 10% for the two bars' small roll and lift differences on the sphere.
        /// </summary>
        static readonly float GeodesicJointClear =
            BarHalfThickMax / Mathf.Tan(27f * Mathf.Deg2Rad) * 1.1f;

        /// <summary>
        /// The same for a BOX joint, where every pair of runs meets at 90°: a bar whose cross-section
        /// is rolled up to 45° about its run (its up is its radial, not the face normal) is at most
        /// w·√2 wide, and two square runs clear once each starts that far out. 2% margin.
        /// </summary>
        static readonly float BoxJointClear = BarHalfThickMax * Mathf.Sqrt(2f) * 1.02f;

        /// <summary>A flush size is aimed a hair under exact contact, so a touching pair stays a
        /// touching pair through float rounding.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>
        /// Every Nth bar prism of the CORE cage is laid as a danger trap. Only the core: the
        /// innermost room holds the biggest, hardest creatures, and salting its walls is what
        /// makes "just ram your way in" a bad plan there specifically. Contact costs the standard
        /// danger punishment (full-stop slow, 4s all-element debuff, boost reset) - a Sparrow
        /// that loses its speed inside the core room is in real trouble.
        /// </summary>
        const int DangerEveryNthCoreBar = 11;

        // Structural triad - all three domains present, per the no-domain-asymmetry spirit.
        // Cosmetic to scoring: the jail is environment mass, hostile to everyone.
        static readonly Domains[] BarDoms = { Domains.Jade, Domains.Ruby, Domains.Gold };

        int Tier => Mathf.Clamp(intensityTier, 1, ShellPlans.Length);

        protected override int DefaultSeed => 40;

        // Hashes the real generation parameters, not a bump-me constant: the four prefab
        // variants share this script, so the tier MUST be in the key or they would serve each
        // other's cached point clouds.
        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableWildlifeCage), Tier, ShellCount,
            System.HashCode.Combine(ShellRadii[0], ShellRadii[1], ShellRadii[2]),
            System.HashCode.Combine(BarStep, BarLength, BarThickness, PostSize, DangerEveryNthCoreBar),
            PlanHash());

        static int PlanHash()
        {
            int h = 17;
            foreach (var row in ShellPlans)
                foreach (var p in row)
                    h = h * 31 + ((int)p.Form * 397 + p.Frequency);
            return h;
        }

        // Worst case is intensity 4 (see wildlife_cage_budget.py); a generous single figure
        // avoids a growth-churn realloc without over-allocating by more than a shell's worth.
        protected override int LayCapacity => 16000;

        protected override void BuildEnvironment()
        {
            var plans = ShellPlans[Tier - 1];
            for (int shell = 0; shell < ShellCount; shell++)
                BuildShell(shell, plans[shell]);
        }

        void BuildShell(int shell, ShellPlan plan)
        {
            float radius = ShellRadii[shell];
            bool isCore = shell == ShellCount - 1;

            var segments = new List<(Vector3 a, Vector3 b)>();
            var nodes = new List<CageNode>();

            if (plan.Form == CageForm.Geodesic)
                BuildGeodesicFrame(radius, plan.Frequency, segments, nodes);
            else
                BuildBoxedFrame(radius, plan.Frequency, segments, nodes);

            // The shortest distance from any end bar's centre to its joint, over the shell - the
            // room the tightest post has to share with the bar beside it (PostHalfCap).
            float halfStepMin = float.MaxValue;
            for (int i = 0; i < segments.Count; i++)
            {
                float len = (segments[i].b - segments[i].a).magnitude;
                if (len < 0.001f) continue;
                halfStepMin = Mathf.Min(halfStepMin, 0.5f * len / BarsOn(len));
            }

            ShellJitter(segments, nodes);
            var joints = new Joints(plan.Form, halfStepMin, nodes, _postAt, _postK);

            // One walking counter across the whole shell so the danger traps spread around the
            // cage instead of clustering on whichever run happens to be laid first.
            int barIndex = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                var (a, b) = segments[i];
                LayBar(a, b, plan.Form == CageForm.Geodesic ? radius : 0f,
                    BarDoms[i % BarDoms.Length], isCore, joints, ref barIndex);
            }

            // Nodes: the chunky knuckles where bars meet. On a box these are the RING POSTS -
            // heavy blocks at the eight corners and lighter ones stepping along the twelve
            // edges - and they are most of what makes a boxed cage read as a boxing ring rather
            // than a wireframe cube.
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                // The jittered post, never bigger than the shell leaves it room for (a cube stays
                // a cube: the cap is uniform).
                float edge = joints.PostEdge(i);
                Emit(n.Position, PostRotation(n.Position), new Vector3(edge, edge, edge),
                    BarDoms[i % BarDoms.Length]);
            }
        }

        // The shell's jitter, drawn up front (see ShellJitter). Reused across shells and builds.
        readonly List<float> _barK = new(8192);
        readonly List<float> _postK = new(2048);
        readonly Dictionary<long, int> _postAt = new(2048);
        int _barDraw;

        /// <summary>
        /// Draw the whole shell's jitter BEFORE laying it, in exactly the order the lay has always
        /// consumed it - one factor per bar, run by run, then one per post - so every prism keeps
        /// the very draw it had, while an end bar can be sized against the post it actually meets
        /// (that post's draw comes after every bar's). Also keys each post by its joint, the same
        /// quantized key the frame dedupe already uses.
        /// </summary>
        void ShellJitter(List<(Vector3 a, Vector3 b)> segments, List<CageNode> nodes)
        {
            _barK.Clear();
            _postK.Clear();
            _postAt.Clear();
            _barDraw = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                float len = (segments[i].b - segments[i].a).magnitude;
                if (len < 0.001f) continue;                       // LayBar lays nothing on it
                for (int b = BarsOn(len); b > 0; b--) _barK.Add(1f + RangeF(-CageJitter, CageJitter));
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                _postK.Add(1f + RangeF(-CageJitter, CageJitter));
                _postAt[Key(nodes[i].Position)] = i;
            }
        }

        /// <summary><see cref="Jit"/> with its factor already drawn: the same per-axis floor.</summary>
        static Vector3 Jittered(Vector3 s, float k) =>
            new(Mathf.Max(PrismFloor, s.x * k), Mathf.Max(PrismFloor, s.y * k), Mathf.Max(PrismFloor, s.z * k));

        /// <summary>Bars on a run of <paramref name="len"/> - the one count expression, shared by
        /// <see cref="LayBar"/> and the shell's half-step scan.</summary>
        static int BarsOn(float len) => Mathf.Max(1, Mathf.RoundToInt(len / BarStep));

        /// <summary>A post's pose: its local Z along the joint's radial.</summary>
        static Quaternion PostRotation(Vector3 p) =>
            SpawnPoint.LookRotation(p.sqrMagnitude > 0.001f ? p.normalized : Vector3.forward, Vector3.up);

        /// <summary>A cube's half-extent along <paramref name="dir"/> per unit of half-size.</summary>
        static float CubeSupport(Quaternion rot, Vector3 dir) =>
            Mathf.Abs(Vector3.Dot(rot * Vector3.right, dir)) +
            Mathf.Abs(Vector3.Dot(rot * Vector3.up, dir)) +
            Mathf.Abs(Vector3.Dot(rot * Vector3.forward, dir));

        /// <summary>
        /// What one shell's joints need: the post at each joint and how big it is laid, and how
        /// far an end bar stands off its joint (<see cref="Clearance"/>). Arithmetic on the
        /// joint's position and the shell's own draws - nothing kept per prism.
        /// </summary>
        readonly struct Joints
        {
            readonly CageForm _form;
            readonly float _halfStepMin;   // see BuildShell
            readonly List<CageNode> _nodes;
            readonly Dictionary<long, int> _postAt;
            readonly List<float> _postK;

            public Joints(CageForm form, float halfStepMin, List<CageNode> nodes,
                Dictionary<long, int> postAt, List<float> postK)
            {
                _form = form;
                _halfStepMin = halfStepMin;
                _nodes = nodes;
                _postAt = postAt;
                _postK = postK;
            }

            /// <summary>
            /// The largest half-size a post posed at <paramref name="rot"/> may take: small enough
            /// that, along every run that can meet it, the shortest end bar of the shell still has
            /// room for the prism floor beside it. A box's runs are the three world axes, so its
            /// post's reach along them is the exact bound; a geodesic's runs lie anywhere in the
            /// tangent plane, so the cube's worst reach (√3) bounds it - and never bites there
            /// (a 4.5u post against a ≥13.9u half-step). It bites only on the tight boxes, where a
            /// 1.9x corner block would otherwise swallow the bar beside it whole.
            /// </summary>
            float PostHalfCap(Quaternion rot)
            {
                float reach;
                if (_form == CageForm.Geodesic) reach = Mathf.Sqrt(3f);
                else
                    reach = Mathf.Max(CubeSupport(rot, Vector3.right),
                            Mathf.Max(CubeSupport(rot, Vector3.up), CubeSupport(rot, Vector3.forward)));
                return (_halfStepMin - PrismFloor * 0.5f) * FlushMargin / reach;
            }

            /// <summary>Edge of post <paramref name="node"/> as laid: its jittered size, capped.</summary>
            public float PostEdge(int node)
            {
                var n = _nodes[node];
                float drawn = Jittered(Vector3.one * (PostSize * n.SizeScale), _postK[node]).x;
                return Mathf.Min(drawn, 2f * PostHalfCap(PostRotation(n.Position)));
            }

            /// <summary>
            /// How far along <paramref name="dir"/> (pointing from the joint into the run) an end
            /// bar's near face must stand from the joint at <paramref name="joint"/>: past the
            /// post's own extent along the run, and far enough out that the joint's other bars
            /// clear it. A box face's interior crossings carry no post.
            /// </summary>
            public float Clearance(Vector3 joint, Vector3 dir)
            {
                float clear = _form == CageForm.Geodesic ? GeodesicJointClear : BoxJointClear;
                if (!_postAt.TryGetValue(Key(joint), out int node)) return clear;
                return Mathf.Max(clear, 0.5f * PostEdge(node) * CubeSupport(PostRotation(joint), dir));
            }
        }

        /// <summary>A structural node and how heavy it is - a cube corner post outweighs an edge post.</summary>
        readonly struct CageNode
        {
            public readonly Vector3 Position;
            public readonly float SizeScale;
            public CageNode(Vector3 position, float sizeScale) { Position = position; SizeScale = sizeScale; }
        }

        /// <summary>
        /// Lays one bar run. <paramref name="projectRadius"/> &gt; 0 pushes each prism back onto
        /// the sphere so a geodesic bar follows the shell's curvature instead of chording inside
        /// it; 0 keeps the run straight, which is what a box's flat faces want.
        /// </summary>
        void LayBar(Vector3 from, Vector3 to, float projectRadius, Domains dom, bool coreShell,
            in Joints joints, ref int barIndex)
        {
            Vector3 along = to - from;
            float len = along.magnitude;
            if (len < 0.001f) return;
            Vector3 dir = along / len;

            // Flush (see the constants above): each bar is no longer than the gap to its
            // neighbours' centres along the run, and an end bar also stops short of its joint.
            float clearFrom = joints.Clearance(from, dir);
            float clearTo = joints.Clearance(to, -dir);

            int n = BarsOn(len);
            float sPrev = 0f;
            Vector3 pos = BarCentre(from, to, 0.5f / n, projectRadius);
            float s = Vector3.Dot(pos - from, dir);
            for (int i = 0; i < n; i++)
            {
                // Centre of the next bar on the run, measured along it (the run's end for the last).
                Vector3 next = i + 1 < n ? BarCentre(from, to, (i + 1.5f) / n, projectRadius) : to;
                float sNext = i + 1 < n ? Vector3.Dot(next - from, dir) : len;

                float room = i == 0 ? 2f * (s - clearFrom) : s - sPrev;
                room = Mathf.Min(room, i == n - 1 ? 2f * (len - s - clearTo) : sNext - s);

                // Danger traps live only in the core cage - see DangerEveryNthCoreBar.
                bool danger = coreShell && (barIndex % DangerEveryNthCoreBar == 0);
                barIndex++;

                Vector3 up = pos.sqrMagnitude > 0.001f ? pos.normalized : Vector3.up;
                var size = Jittered(new Vector3(BarThickness, BarThickness, BarLength), _barK[_barDraw++]);
                size.z = Mathf.Max(PrismFloor, Mathf.Min(size.z, room * FlushMargin));
                Emit(pos, SpawnPoint.LookRotation(along, up), size, dom,
                    danger ? PrismKind.Danger : PrismKind.Plain);

                sPrev = s;
                s = sNext;
                pos = next;
            }
        }

        /// <summary>Where a bar at fraction <paramref name="u"/> of a run is laid: on the chord,
        /// pushed back onto the sphere for a geodesic run (see <see cref="LayBar"/>).</summary>
        static Vector3 BarCentre(Vector3 from, Vector3 to, float u, float projectRadius)
        {
            Vector3 pos = Vector3.Lerp(from, to, u);
            return projectRadius > 0f ? pos.normalized * projectRadius : pos;
        }

        // ── Geodesic frame (subdivided icosahedron) ──────────────────────────

        static readonly Vector3[] IcosaVerts = BuildIcosaVerts();
        static readonly int[] IcosaFaces =
        {
            0,11, 5,  0, 5, 1,  0, 1, 7,  0, 7,10,  0,10,11,
            1, 5, 9,  5,11, 4, 11,10, 2, 10, 7, 6,  7, 1, 8,
            3, 9, 4,  3, 4, 2,  3, 2, 6,  3, 6, 8,  3, 8, 9,
            4, 9, 5,  2, 4,11,  6, 2,10,  8, 6, 7,  9, 8, 1,
        };

        static Vector3[] BuildIcosaVerts()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        /// <summary>
        /// Subdivides each icosahedral face into frequency² small triangles and collects the
        /// unique edges as bar runs. Edge count is exactly 30·frequency², and every opening is a
        /// triangle of near-uniform size anywhere on the sphere - the property that makes this a
        /// FAIR cage (no dense polar cap to drill, so no per-shell tilt table is needed).
        /// </summary>
        void BuildGeodesicFrame(float radius, int frequency, List<(Vector3, Vector3)> segments, List<CageNode> nodes)
        {
            int f = Mathf.Max(1, frequency);
            var seen = new HashSet<(long, long)>();
            var nodeSeen = new HashSet<long>();

            for (int face = 0; face < IcosaFaces.Length; face += 3)
            {
                Vector3 a = IcosaVerts[IcosaFaces[face]];
                Vector3 b = IcosaVerts[IcosaFaces[face + 1]];
                Vector3 c = IcosaVerts[IcosaFaces[face + 2]];

                // Barycentric lattice over the face, projected onto the sphere.
                var grid = new Vector3[f + 1][];
                for (int i = 0; i <= f; i++)
                {
                    grid[i] = new Vector3[f - i + 1];
                    for (int j = 0; j <= f - i; j++)
                    {
                        float wa = (f - i - j) / (float)f, wb = i / (float)f, wc = j / (float)f;
                        grid[i][j] = (a * wa + b * wb + c * wc).normalized * radius;
                    }
                }

                for (int i = 0; i <= f; i++)
                {
                    for (int j = 0; j <= f - i; j++)
                    {
                        var p = grid[i][j];
                        if (nodeSeen.Add(Key(p))) nodes.Add(new CageNode(p, 1f));

                        // The three edges of the upward sub-triangle at (i,j). Every edge in the
                        // mesh is one of these for exactly one cell, so the set is complete; the
                        // dedupe is for edges SHARED with the neighbouring icosa face.
                        if (j + 1 <= f - i) AddSegment(segments, seen, p, grid[i][j + 1]);
                        if (i + 1 <= f - j) AddSegment(segments, seen, p, grid[i + 1][j]);
                        if (i + 1 <= f && j + 1 <= f - i) AddSegment(segments, seen, grid[i + 1][j], grid[i][j + 1]);
                    }
                }
            }
        }

        // ── Boxed frame (the boxing ring) ────────────────────────────────────

        /// <summary>
        /// A cube whose CORNERS sit exactly on the shell radius (half-extent r/√3), with a
        /// frequency × frequency rail grid on every face. Corners on the radius rather than
        /// faces is what keeps the outer box inside the 1200u membrane and the player spawn ring
        /// clear of it; the flat faces then sit at 0.577·r, so a boxed cage is a visibly tighter,
        /// more claustrophobic room than the sphere it replaces - which is the point of it
        /// arriving at the high intensities.
        /// </summary>
        void BuildBoxedFrame(float radius, int frequency, List<(Vector3, Vector3)> segments, List<CageNode> nodes)
        {
            int f = Mathf.Max(1, frequency);
            float e = radius / Mathf.Sqrt(3f);
            var seen = new HashSet<(long, long)>();
            var nodeSeen = new HashSet<long>();

            // Six faces: axis = the face normal's axis, sign = which side.
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    for (int i = 0; i <= f; i++)
                    {
                        for (int j = 0; j <= f; j++)
                        {
                            var p = FacePoint(axis, sign, u, v, i, j, f, e);

                            // Posts on the cube's twelve EDGES only - i.e. wherever this face's
                            // grid meets its boundary. A post at every interior crossing would
                            // add ~6f² prisms per shell for no read, while an edge with no posts
                            // reads as a wireframe. The eight CORNERS get a heavier block still.
                            bool onEdge = i == 0 || i == f || j == 0 || j == f;
                            if (onEdge && nodeSeen.Add(Key(p)))
                            {
                                bool corner = (i == 0 || i == f) && (j == 0 || j == f);
                                nodes.Add(new CageNode(p, corner ? 1.9f : 1.15f));
                            }

                            if (j < f) AddSegment(segments, seen, p, FacePoint(axis, sign, u, v, i, j + 1, f, e));
                            if (i < f) AddSegment(segments, seen, p, FacePoint(axis, sign, u, v, i + 1, j, f, e));
                        }
                    }
                }
            }
        }

        static Vector3 FacePoint(int axis, int sign, int u, int v, int i, int j, int f, float e)
        {
            var p = Vector3.zero;
            p[axis] = sign * e;
            p[u] = Mathf.Lerp(-e, e, i / (float)f);
            p[v] = Mathf.Lerp(-e, e, j / (float)f);
            return p;
        }

        // ── Segment/node dedupe ──────────────────────────────────────────────
        //
        // Both frames generate shared structure twice (a geodesic edge belongs to two icosa
        // faces; a cube's boundary rail belongs to two faces), and laying it twice would double
        // the prisms AND the colliders along every seam. Quantized position keys are the cheap,
        // deterministic way to fold them - the same trick the shield-mesh caches use.

        static void AddSegment(List<(Vector3, Vector3)> segments, HashSet<(long, long)> seen, Vector3 a, Vector3 b)
        {
            long ka = Key(a), kb = Key(b);
            if (ka == kb) return;
            // Order-independent pair key: the same edge reached from either face folds to one.
            // A tuple, not an arithmetic mix - the single keys already use ~50 of a long's bits,
            // so any multiply-and-add would overflow and start folding DISTINCT edges together.
            var pair = ka < kb ? (ka, kb) : (kb, ka);
            if (!seen.Add(pair)) return;
            segments.Add((a, b));
        }

        /// <summary>Quantized position key (0.5u grid) - identical points from different faces agree.</summary>
        static long Key(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.x * 2f);
            long y = Mathf.RoundToInt(p.y * 2f);
            long z = Mathf.RoundToInt(p.z * 2f);
            return ((x + 32768) << 34) | ((y + 32768) << 17) | (z + 32768);
        }
    }
}
