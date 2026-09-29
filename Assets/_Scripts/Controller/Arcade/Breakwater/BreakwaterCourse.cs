using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One station of a Breakwater course: where the breakwater hangs, which way it faces, and
    /// how wide its port is.
    ///
    /// <para><b>The port radius is ONE number doing three jobs</b> - it is the drawn switch ring,
    /// the crossing test's lateral bound, and the rim the plug's rakes are clipped to. A switch's
    /// ring IS its trigger volume, drawn at its own radius (Docs/ToySystem/ARCHITECTURE.md, "The
    /// switch"), and here the plug is built to that same rim, so a station can never advertise a
    /// mouth it does not have or weld shut a mouth wider than the one it drew.</para>
    /// </summary>
    public readonly struct BreakwaterStation
    {
        /// <summary>World position, after the cell offset has been applied by the caller.</summary>
        public readonly Vector3 Position;

        /// <summary>Unit; the direction the course flows through the port.</summary>
        public readonly Vector3 Axis;

        public readonly float PortRadius;

        public BreakwaterStation(Vector3 position, Vector3 axis, float portRadius)
        {
            Position = position;
            Axis = axis;
            PortRadius = portRadius;
        }
    }

    /// <summary>
    /// Tuning for one generated course. Every number here is geometry a Sparrow has to fly or
    /// shoot, so none of it is picked by eye: the whole ladder is swept over 400 seeds x 4
    /// intensities by <c>Tools/Build/breakwater_arena.py</c>, which is the AUTHORITY this struct
    /// mirrors - the same file the asset generator imports to derive the cell's PhaseThresholds,
    /// so the arena's difficulty and the arena's mass cannot drift apart.
    /// </summary>
    public struct BreakwaterCourseSettings
    {
        public int StationCount;

        /// <summary>Course stays outside this - the nucleus, or the authored floor when there is none.</summary>
        public float InnerRadius;

        /// <summary>...and inside this - the membrane, with margin.</summary>
        public float OuterRadius;

        /// <summary>Leg length between consecutive stations.</summary>
        public float MinStep;

        public float MaxStep;

        /// <summary>Heading change a pilot must make at a corner.</summary>
        public float MaxTurnDegrees;

        /// <summary>No two stations closer than this. DERIVED - see <see cref="BreakwaterCourse.MinSeparationFor"/>.</summary>
        public float MinSeparation;

        /// <summary>How far a station may be twisted off the flow line.</summary>
        public float AxisJitterDegrees;

        /// <summary>Hard cap on how edge-on a station may ever present to the leg you arrive on.</summary>
        public float MaxPresentDegrees;

        /// <summary>The port rim: the switch ring, and the radius the plug's rakes are clipped to.</summary>
        public float PortRadius;

        /// <summary>
        /// World positions no station's structure may reach - the pilots' spawn pads.
        ///
        /// <para>Null or empty disables the test, which is what every unit test that only cares
        /// about the walk's shape passes.</para>
        /// </summary>
        public Vector3[] SpawnPads;

        /// <summary>
        /// Air left between a station's bounding sphere and the nearest spawn pad. See
        /// <see cref="DefaultSpawnPadClearance"/>.
        /// </summary>
        public float SpawnPadClearance;

        /// <summary>
        /// The Sparrow's own circumscribing radius, in world units: 12.32.
        ///
        /// <para>MEASURED from the shipped prefab rather than guessed, and the circumscribing
        /// radius rather than a half-width because a pilot may be rolled to any angle when they
        /// arrive - it is the clearance a port has to offer in every orientation. It is the floor
        /// under both of this mode's tightest gaps: the eye a pilot threads and the port an
        /// unopened plug still leaves.</para>
        /// </summary>
        public const float SparrowHullRadius = 12.32f;

        /// <summary>
        /// The threadable gap at the plug's centre: 18 units, <b>1.46x the hull radius</b>.
        ///
        /// <para>That number is the whole of the third choice this mode offers. Fire and you spend
        /// a rocket; saw and you stop dead and give up the race for a few seconds; thread and you
        /// spend nothing at all - but you fly a 1.46x clearance with danger bars a hull-width
        /// away, so a fractional roll into one is a full-stop slow and an all-element debuff. It
        /// is deliberately a CONSTANT across the ladder: the port narrows as the intensity
        /// climbs, so at intensity 1 threading is the miser's option beside a wide easy door and
        /// at intensity 4 it is very nearly the only gap left. Nothing about the eye had to change
        /// to say that.</para>
        /// </summary>
        public const float EyeRadius = 18f;

        /// <summary>Shell floor: the course never walks inside this radius. Matches the model's SHELL_INNER.</summary>
        public const float DefaultInnerRadius = 420f;

        /// <summary>Shell ceiling: 0.9x the CapsuleMembrane's authored 1200. Matches the model's SHELL_OUTER.</summary>
        public const float DefaultOuterRadius = 1080f;

        // THE START GATE'S DISTANCE IS SOLVED, NOT AUTHORED - it is wherever a point on the polar
        // axis is exactly one chord from the circuit's entry station, which is what makes the
        // entry leg a legal leg. A DefaultFirstStationDistance used to live here; once the circuit
        // replaced the walk it was read by nothing, and a config that cannot affect anything is
        // worse than no config at all, so it was removed rather than left looking meaningful.
        //
        // The POLE is +Y by construction rather than by choice: SpawnPadRing lays the pads on
        // y = 0, mirroring CellSpawnFormation.EquatorialRing, so the axis those pads are symmetric
        // about is the Y axis. That is the whole of the fairness rule.

        /// <summary>
        /// How many laps of the CIRCUIT a race is: <b>2</b>.
        ///
        /// <para>The course is a polar START GATE plus a closed circuit of the remaining stations,
        /// flown forward and repeated. It replaced an out-and-back that re-flew the same stations
        /// REVERSED, which play-tested as being sent back through the rings you came.</para>
        ///
        /// <para><b>The start gate is what makes a circuit fair, and it is not decoration.</b>
        /// Fairness here is "pilots spawn on an EquatorialRing, the first gate sits on that ring's
        /// pole, so every pad is equidistant". Make that first gate the first gate of a closed
        /// LOOP instead and the approach is AXIAL while a closed loop's tangent at an axial point
        /// is PERPENDICULAR - measured over 400 seeds x 4 intensities, presentation at that gate
        /// ran 12.8-90.0 deg with up to <b>73.5 deg of spread ACROSS PADS</b>: one pilot gets a
        /// 14 deg face-on approach and another 90 deg edge-on to the same ring. That is
        /// structural, not tuning - inside the 420..1080 shell no circle can cross the polar axis
        /// at radius >= 420 with a near-axial tangent, because <c>c + R &lt;= 1080</c>,
        /// <c>R^2 - c^2 &gt;= 420^2</c> and <c>c/R &gt;= 0.866</c> are jointly unsatisfiable.</para>
        ///
        /// <para>So the start gate sits on the axis with its axis ALONG the pole, which makes every
        /// pad equidistant <b>and</b> face-on - measured spread 0.0000 on both, strictly fairer
        /// than the old rule, which equalised distance only.</para>
        ///
        /// <para><b>The one cost, stated plainly:</b> the merge from the start gate onto the
        /// circuit is a hard corner - 66.9 deg worst, ~61 deg mean. It is exempt from
        /// <see cref="BreakwaterCourseSettings.MaxTurnDegrees"/> (which describes the circuit) and
        /// bounded only by Dubins, which the 300-unit minimum leg guarantees at ANY angle:
        /// <c>2R sin(66.9) = 239.4 &lt; 300</c>. It happens once per race and reads as a racing
        /// start - launch, thread the gate, hook onto the racing line.</para>
        /// </summary>
        public const int DefaultLaps = 2;

        /// <summary>
        /// Total ring crossings a race is: <b>29</b> for a start gate plus a fourteen-station
        /// circuit over two laps.
        ///
        /// <para>The start gate is threaded ONCE and the circuit every lap, so this is
        /// <c>1 + (stations - 1) * laps</c>. Raising the lap count costs no arena mass at all - it
        /// re-flies stations already laid.</para>
        /// </summary>
        public static int CrossingTarget(int stations, int laps) =>
            stations <= 1 ? Mathf.Max(1, stations)
                          : 1 + (stations - 1) * Mathf.Max(1, laps);
        /// <summary>
        /// The station a leg leaving <paramref name="index"/> arrives at - the ONE definition of
        /// "what follows what" on this course.
        ///
        /// <para>It exists because a circuit has a leg a linear walk never had: the CLOSING leg,
        /// from the last circuit station back to the first. Everything that walks the course by
        /// consecutive pairs (<c>i</c> to <c>i + 1</c>, stopping at <c>Count - 1</c>) silently
        /// skips it - which is how the shoals came to leave one leg of every lap without
        /// ammunition, and the shoal clearance test came to ignore that leg's corridor entirely.
        /// </para>
        ///
        /// <para>Station 0 is the start gate and leads into the circuit; the last station leads
        /// back to station 1, never to 0, because the start gate is threaded once.</para>
        /// </summary>
        public static int NextStation(int index, int stations) =>
            stations <= 2 ? 0 : (index >= stations - 1 ? 1 : index + 1);


        /// <summary>
        /// Which ring the <paramref name="crossing"/>-th crossing is - the fold that lets ONE
        /// replicated int still carry the whole race.
        ///
        /// <para>This is what keeps the ordered-gate property Switchback established intact under
        /// laps: <c>IRoundStats.SwitchesThreaded</c> is still simultaneously the score, the
        /// progress bar, the token the server validates a report against AND - through this fold -
        /// the index of the ring to test this frame. Without it a second lap would need per-lap
        /// state, and the whole race stops fitting in the one int the metric already replicates.
        /// </para>
        ///
        /// <para>Crossing 0 is the start gate; everything after it walks the circuit FORWARD and
        /// wraps, so the fold is a plain modulo. It was a zigzag while the course was flown out
        /// and back, and that is the whole of what changed here.</para>
        ///
        /// <para><b>The fold picks which ring to TEST; the token that travels is the CROSSING.</b>
        /// The server validates a report with <c>gateIndex != stats.SwitchesThreaded</c>, so
        /// reporting the folded ring would have every lap-2 report rejected as a duplicate.</para>
        /// </summary>
        public static int RingForCrossing(int crossing, int stations)
        {
            if (stations <= 1) return 0;
            if (crossing <= 0) return 0;

            int circuit = stations - 1;
            return 1 + (((crossing - 1) % circuit) + circuit) % circuit;   // negative-safe
        }

        /// <summary>
        /// The equatorial spawn ring's radius: 480, matching the scene's
        /// <c>spawnRingRadiusFloor</c> and the model's SPAWN_RING_RADIUS.
        /// </summary>
        public const float DefaultSpawnRingRadius = 480f;

        /// <summary>
        /// How much air a station must leave around a spawn pad, beyond its own bounding sphere:
        /// <b>four hull radii</b>.
        ///
        /// <para><b>The spawn ring is INSIDE the course shell</b> - pads at 480 against a
        /// 420..1080 walk - and nothing else in the generator knows the ring exists. Measured over
        /// 400 seeds x 4 intensities x 2/3/4 seats, that put a pad inside a station's structure on
        /// 7 of 14,400 pad-cases and within a hull radius of one on 23 more: a pilot who starts
        /// the match embedded in Danger prisms, with no counterplay and nothing to explain it.
        /// </para>
        ///
        /// <para>Four hull radii rather than one because a pilot spawns facing the cell and needs
        /// room to SEE the wall and turn, not merely to not be inside it - and it is free:
        /// measured, the rejection costs the walk nothing at any clearance from 0 to 60 (0
        /// failures in 800 seeds x 4 intensities).</para>
        /// </summary>
        public const float DefaultSpawnPadClearance = 4f * SparrowHullRadius;

        /// <summary>
        /// The port rim for an intensity: <b>72 / 60 / 50 / 42</b>. A measured ladder rather than
        /// a formula, because both of its ends are pinned by shipped facts and the interesting
        /// property is WHERE IT CROSSES between them.
        ///
        /// <list type="bullet">
        /// <item><b>The hard end is pinned under the blast.</b> The door-cutter is
        /// <c>AOEExplosion.prefab</c>'s spherical blast - 50 units of radius at resting Charge,
        /// 85 at Charge 10. At intensity 4 the port (42) sits UNDER the resting radius, so a pilot
        /// with no upgrade at all still clears a whole plug with one rocket: the hardest course
        /// must never require an element level the comeback system hands to whoever is losing.</item>
        /// <item><b>The easy end is pinned over it.</b> At intensity 1 the port (72) is WIDER than
        /// the resting blast, so one rocket cannot take the whole plug and WHERE you cut becomes a
        /// real choice - cut on the eye and you have widened the thread, cut on the rim and you
        /// have opened a door off the racing line. That choice is the easy level's teaching, and
        /// it exists only because 72 > 50.</item>
        /// </list>
        ///
        /// <para>The port moves two things with one number, which is why it is THE axis: a smaller
        /// port means a smaller dish (x1.75), so the hardest course is also the poorest ammo bank
        /// - fewer prisms in reach means fewer of the 50 that buy the next rocket.</para>
        /// </summary>
        public static float PortRadiusForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new[] { 72f, 60f, 50f, 42f }[i - 1];
        }

        /// <summary>
        /// The shipped course shape per intensity. INTENSITY IS THE COURSE AND THE DOOR, not the
        /// arena's size: station COUNT is constant (it is the end-game target, authored once in
        /// <c>EndConditionOverridesSO</c>) so a match is the same length at every level and the
        /// four are comparable - the same reasoning as Rampage, where the forest is identical at
        /// all four and only the pressure changes.
        ///
        /// <para><b>The leg and turn rows run OPPOSITE ways, and that is measured rather than
        /// chosen.</b> An earlier cut ran intensity 1 at legs 340-520 with a 40 degree cap, on the
        /// obvious reasoning that the gentlest corners belong at the easiest level - and it failed
        /// to generate on <b>21% of seeds</b>. Inside a 660-unit-thick shell a long leg with
        /// little turn available walks into the wall and cannot come back: long legs and tight
        /// corners are the same constraint pulling in opposite directions. So the ladder shortens
        /// the legs as it tightens the doors and lets the corners open, which is also the right
        /// feel - a hard course is a busy one, not a sprawling one.</para>
        ///
        /// <para>Every row is swept by <c>Tools/Build/breakwater_arena.py</c> and asserted by
        /// <c>BreakwaterCourseTests</c>: no seed fails to generate, the turn and presentation caps
        /// hold, no two ports come within the derived separation, and every corner clears the
        /// Sparrow's turning circle at the TRANSIENT CEILING (min turn radius 130.1u = full
        /// throttle boosting at the top of the overcharge band) - the state in which a racer is
        /// least able to correct.</para>
        /// </summary>
        public static BreakwaterCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float port = PortRadiusForIntensity(i);
            float minStep = new[] { 300f, 300f, 300f, 300f }[i - 1];

            return new BreakwaterCourseSettings
            {
                MinStep = minStep,
                MaxStep = new[] { 460f, 433f, 407f, 380f }[i - 1],
                MaxTurnDegrees = new[] { 45f, 55f, 65f, 75f }[i - 1],
                AxisJitterDegrees = new[] { 22f, 28f, 34f, 40f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 54f, 58f, 62f }[i - 1],
                PortRadius = port,
                MinSeparation = BreakwaterCourse.MinSeparationFor(port, minStep),

                // The shell and the pole are properties of the ARENA rather than of the level, so
                // they are the same at all four and are seeded here rather than left to the
                // caller: the model hardcodes them, and a caller that forgot one would walk a
                // course the measured PhaseThresholds do not describe. A controller with a real
                // cell to read may still overwrite them - Cell.ExpectedNucleusWorldRadius returns
                // 0 when there is no nucleus, so the inner shell needs exactly this fallback.
                InnerRadius = DefaultInnerRadius,
                OuterRadius = DefaultOuterRadius,
                SpawnPads = BreakwaterCourse.SpawnPadRing(Vector3.zero, DefaultSpawnRingRadius),
                SpawnPadClearance = DefaultSpawnPadClearance,

                // StationCount is deliberately NOT set. It is the end-game target and lives in
                // EndConditionOverridesSO, read by BOTH the turn monitor (the number that ends the
                // race) and the controller (how many stations to hang) - one authored value in one
                // place, so the course and the number counting it cannot drift.
            };
        }
    }

    /// <summary>
    /// Builds a Breakwater course: an ORDERED chain of stations scattered through a cell, each
    /// randomly placed and randomly oriented, that a Sparrow can actually fly.
    ///
    /// <para><b>Pure and deterministic.</b> No <c>UnityEngine.Random</c> (global state), no
    /// <c>System.Random</c> (implementation-defined across runtimes - the trap
    /// Docs/WEEKLY_CHALLENGE.md records), no <c>Time</c>, no scene access. The generator owns a
    /// fully specified xorshift32, so the same seed yields the same course on any machine and the
    /// whole thing is unit-testable offline. The server still SENDS the resulting geometry rather
    /// than the seed, so peers cannot disagree even if a transcendental differs in its last bit -
    /// determinism here buys reproducibility and testability, not the network contract.</para>
    ///
    /// <para><b>This is a deliberate FORK of <see cref="SwitchbackCourse"/>, not a reuse.</b> The
    /// two walks share a shape but not a stream: Breakwater draws the step BEFORE the heading,
    /// backtracks on a whole-station budget rather than per-station try counters, and seeds its
    /// first heading off the pole instead of off the inbound ray. Extracting a shared walk would
    /// change Switchback's RNG consumption order and silently ship that mode a different course
    /// for every seed it has ever been play-tested on. A common walk is filed for the third race,
    /// where there will be two shipped call sites to justify the shape.</para>
    ///
    /// <para><b>It is a bit-for-bit mirror of <c>Tools/Build/breakwater_arena.py</c>.</b> That
    /// file's proofs - flyability, the collider budget, the prism clamp - are statements about the
    /// arena that actually ships only while the two agree, so the RNG stream (integer arithmetic,
    /// identical on both sides) and the ORDER it is consumed in are part of the contract, not an
    /// implementation detail. The geometry is <c>float</c> here and <c>double</c> there, so a
    /// candidate sitting within float epsilon of the shell wall or the separation floor could in
    /// principle be classified differently; the sweep's tightest observed margin is 0.9 units
    /// against a 168-unit floor, five orders of magnitude clear of it.</para>
    ///
    /// <para><b>Two properties hold BY CONSTRUCTION, not by luck</b>, and both are asserted in
    /// <c>BreakwaterCourseTests</c>:</para>
    /// <list type="number">
    /// <item><b>The turn cap.</b> The heading only ever advances when a station is PLACED, and
    /// every proposal - including the one that steers away from a wall - is clamped to
    /// <see cref="BreakwaterCourseSettings.MaxTurnDegrees"/> of the previous leg. A wall can
    /// therefore never manufacture a hairpin: when there is no legal escape the walk BACKTRACKS
    /// instead of bending the rule. (Letting the heading rotate between failed attempts is the
    /// tempting shortcut and it is wrong - two 60 degree rotations compose into a 120 degree
    /// corner between two placed stations.)</item>
    /// <item><b>The presentation cap.</b> A station faces the flow BISECTOR of its corner, which
    /// sits half the turn angle off each leg. The jitter that makes it "randomly oriented" is
    /// therefore spent from what is LEFT of the cap after the corner has taken its half:
    /// <c>presentation &lt;= halfTurn + jitter &lt;= MaxPresentDegrees</c> against both the
    /// arriving and the departing leg. Without that budget a sharp corner plus full jitter yields
    /// a plug standing edge-on to the flight line - which is not a hard station, it is one whose
    /// eye cannot be threaded and whose dish shields its own plug from the blast.</item>
    /// </list>
    /// </summary>
    public static class BreakwaterCourse
    {
        /// <summary>
        /// Attempts at one station before the walk gives up and backtracks. 32 rather than
        /// Switchback's 24 because this shell is thinner (660 units against a 1200-unit membrane)
        /// and the separation floor is tighter relative to the leg, so more proposals are spent
        /// against the wall.
        /// </summary>
        public const int AttemptsPerStation = 32;

        /// <summary>
        /// How many times a CALLER should re-roll the seed before falling back to a shorter race.
        ///
        /// <para>The residual failure rate at <see cref="AttemptsPerStation"/> is about 0.1% per
        /// seed. Switchback's back-off - halve the gate count - answers a one-in-a-thousand roll by
        /// shipping that one match a different, shorter race, which is a worse outcome than the
        /// wait: three reseeds take the failure rate to about 1e-9 and keep every match the same
        /// length. Halving stays underneath as the last resort, because generation must never
        /// return empty - that would hang the turn outright.</para>
        ///
        /// <para><see cref="Generate"/> deliberately does NOT reseed itself. A generator that
        /// silently retried would make its own failure rate unobservable, and the caller is the
        /// only party that knows which seed it may legitimately move to (the server broadcasts the
        /// geometry, so it is free to pick another; a test asserting the shipped settings is
        /// not).</para>
        /// </summary>
        public const int ReseedAttempts = 3;

        /// <summary>
        /// Deterministic 32-bit xorshift. Specified arithmetic on unsigned ints, so it is
        /// identical on every runtime - unlike <c>System.Random</c>, whose sequence is a property
        /// of the implementation rather than of the seed. The Python model reproduces this
        /// verbatim, including the seed-0 fallback, which is what makes its proofs statements
        /// about the shipped arena.
        /// </summary>
        struct Rng
        {
            uint _s;

            public Rng(int seed)
            {
                // 0 is the xorshift fixed point: it would emit nothing but zeros forever.
                uint s = unchecked((uint)seed);
                _s = s != 0u ? s : 0x9E3779B9u;
            }

            public uint NextUInt()
            {
                uint x = _s;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _s = x;
                return x;
            }

            /// <summary>Uniform in [0,1).</summary>
            public float Unit() => NextUInt() / 4294967296f;

            public float Range(float a, float b) => a + (b - a) * Unit();
        }

        /// <summary>
        /// The minimum distance between any two stations, <c>min(0.9 * minStep, 4 * portRadius)</c>.
        ///
        /// <para><b>DERIVED, never authored</b>, and the derivation is load-bearing in both
        /// directions. Four port radii is "the two mouths are clearly separate places" - close
        /// enough and a pilot cannot tell which ring is theirs, and the ordered-station rule stops
        /// reading as a course. But the value can never reach the minimum leg, because the separation is
        /// tested against EVERY other station <b>including the two a station is joined to</b>: a
        /// separation above the shortest leg rejects most of the chord band outright, and the
        /// generator shrinks its wander to nothing trying to satisfy it. An earlier cut that
        /// authored the separation directly failed 21% of seeds this way, and the symptom was a
        /// course that would not generate rather than a course that looked wrong - which is why it
        /// is computed from the two numbers it is actually a function of.</para>
        /// </summary>
        public static float MinSeparationFor(float portRadius, float minStep) =>
            Mathf.Min(0.9f * minStep, 4f * portRadius);

        /// <summary>
        /// The course, or <c>null</c> when the walk could not satisfy its own constraints inside
        /// the attempt budget. About one seed in a thousand at the shipped settings - the caller
        /// re-rolls (<see cref="ReseedAttempts"/>) rather than shortening the race.
        /// </summary>
        /// <summary>The base ring aims here, leaving the rest of the turn cap for wander.</summary>
        const float CircuitDesignTurn = 0.85f;

        /// <summary>Entry offsets searched when solving the start gate onto the polar axis.</summary>
        static readonly float[] EntryFactors = { 0.80f, 0.86f, 0.92f, 0.97f };

        /// <summary>
        /// Degrees. The join score is QUANTISED before it is compared, so a float-noise tie
        /// between two entry candidates cannot flip which branch wins - the offline model and the
        /// shipped C# would otherwise be able to disagree about a whole course over 1e-4 of a
        /// degree, and the model is what proves this file.
        /// </summary>
        const float JoinScoreQuantum = 0.1f;

        /// <summary>
        /// The whole course: a polar START GATE (index 0) plus a closed CIRCUIT (1..N-1).
        ///
        /// <para><b>The circuit is CONSTRUCTED, not searched.</b> The walk this replaced could not
        /// be steered home - a closing walk failed 55-76% of seeds, because the minimum leg cannot
        /// drop below the Sparrow's own <c>2R</c> and the shell is only 2,160 across. So the loop
        /// is built closed and every constraint becomes an analytic bound:</para>
        ///
        /// <para><b>1. A zigzag ring hits an exact turn angle in closed form.</b> For
        /// <c>P_i = R(cos t_i, sin t_i) +/- z*axis</c> with N even (so the zigzag closes),
        /// consecutive legs alternate <c>s_i +/- 2z*axis</c>, giving
        /// <c>cos(turn) = (|s|^2 cos(phi) - 4z^2) / (|s|^2 + 4z^2)</c> for <c>phi = 2*pi/N</c>.
        /// Solving it for a target chord and turn yields the mode's intensity dial directly:
        /// <c>|s| = chord * sqrt((1+cos T)/(1+cos phi))</c> ALONG the track and
        /// <c>2z = sqrt(chord^2 - |s|^2)</c> ACROSS it.</para>
        ///
        /// <para><b>2. Wander is LOW-FREQUENCY</b> (harmonics k = 1, 2). A smooth deformation
        /// moves neighbouring stations TOGETHER, so it changes the loop's outline a lot while
        /// barely moving adjacent spacing. Per-station jitter does the opposite: it had to be cut
        /// to ~20% of nominal to fit the chord band, which made every course look alike.</para>
        ///
        /// <para><b>3. The amplitude shrinks until the caps hold</b>, and at amplitude 0 the loop
        /// is a regular zigzag ring, which is legal by construction - so the shrink ALWAYS
        /// terminates. That is what replaces rejection sampling, and it is why this method has no
        /// failure rate to report.</para>
        /// </summary>
        public static List<BreakwaterStation> Generate(int seed, BreakwaterCourseSettings s)
        {
            // A one-station "race" has no ordering to score and no leg to lay shoals along, and a
            // two-station circuit is a line rather than a loop.
            if (s.StationCount < 3) return null;

            int n = s.StationCount - 1;                       // the circuit; index 0 is the start gate
            float chord = 0.5f * (s.MinStep + s.MaxStep);

            var pick = new Rng(unchecked(seed ^ 0x5BF03635));
            int offset = (int)(pick.Unit() * n) % n;
            float bearing = pick.Range(0f, 2f * Mathf.PI);

            for (int k = 0; k < 30; k++)
            {
                float amp = Mathf.Pow(0.9f, k);
                var pts = BuildCircuit(seed, s, amp, n);
                var legs = CircuitLegs(pts);

                int bestScore = int.MaxValue;
                Vector3 bestStart = Vector3.zero;
                List<Vector3> bestPts = null;
                List<Vector3> bestLegs = null;

                for (int j = 0; j < n; j++)
                {
                    for (int f = 0; f < EntryFactors.Length; f++)
                    {
                        if (!TryPlaceCircuit(pts, legs, (offset + j) % n, bearing, chord,
                                             EntryFactors[f], out var start,
                                             out var order, out var ordered))
                            continue;
                        if (!CircuitHoldsCaps(start, order, ordered, s, out float joinStart,
                                              out float joinCircuit))
                            continue;

                        int score = Mathf.RoundToInt(Mathf.Max(joinStart, joinCircuit) / JoinScoreQuantum);
                        if (score >= bestScore) continue;
                        bestScore = score;
                        bestStart = start;
                        bestPts = order;
                        bestLegs = ordered;
                    }
                }

                if (bestPts != null) return FinishCourse(bestStart, bestPts, bestLegs, s, seed);
            }

            return null;
        }

        /// <summary>The closed loop, centred on the cell, before the start gate is solved for.</summary>
        static List<Vector3> BuildCircuit(int seed, in BreakwaterCourseSettings s, float amp, int n)
        {
            var rng = new Rng(seed);
            float chord = 0.5f * (s.MinStep + s.MaxStep);
            RingGeometry(chord, s.MaxTurnDegrees * CircuitDesignTurn, n, out float radius, out float z);

            Vector3 axis = SafeNormalize(new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f),
                                                     rng.Range(-1f, 1f)), Vector3.up);
            Vector3 u = Perpendicular(axis);
            Vector3 v = Vector3.Cross(axis, u);

            float r1 = 0.20f * amp * rng.Unit(), p1 = rng.Range(0f, 2f * Mathf.PI);
            float r2 = 0.13f * amp * rng.Unit(), p2 = rng.Range(0f, 2f * Mathf.PI);
            float o1 = 0.42f * amp * rng.Unit(), q1 = rng.Range(0f, 2f * Mathf.PI);
            float o2 = 0.26f * amp * rng.Unit(), q2 = rng.Range(0f, 2f * Mathf.PI);
            float jit = 0.05f * amp;
            float phi = 2f * Mathf.PI / n;

            var pts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float t = phi * i;
                float th = t + jit * phi * rng.Range(-1f, 1f);
                float r = radius * (1f + r1 * Mathf.Cos(t + p1) + r2 * Mathf.Cos(2f * t + p2)
                                    + jit * rng.Range(-1f, 1f));
                float h = z * (i % 2 == 0 ? 1f : -1f) * (1f + jit * rng.Range(-1f, 1f))
                          + radius * (o1 * Mathf.Cos(t + q1) + o2 * Mathf.Cos(2f * t + q2)) * 0.35f;
                pts.Add(u * (r * Mathf.Cos(th)) + v * (r * Mathf.Sin(th)) + axis * h);
            }
            return pts;
        }

        /// <summary>
        /// The along-track radius and across-track half-offset of a zigzag ring that hits
        /// <paramref name="turnDegrees"/> EXACTLY. This is the mode's intensity dial in closed
        /// form: raising the turn cap collapses the along-track component and opens the
        /// across-track one, which is what asks for a rolled, strafing entry.
        /// </summary>
        public static void RingGeometry(float chord, float turnDegrees, int n,
                                        out float radius, out float halfAcross)
        {
            float phi = 2f * Mathf.PI / n;
            float c = Mathf.Cos(turnDegrees * Mathf.Deg2Rad);
            float sLen = chord * Mathf.Sqrt((1f + c) / (1f + Mathf.Cos(phi)));
            float across = Mathf.Sqrt(Mathf.Max(0f, chord * chord - sLen * sLen));
            radius = sLen / (2f * Mathf.Sin(Mathf.PI / n));
            halfAcross = 0.5f * across;
        }

        /// <summary>Leg directions of a CLOSED loop - the index arithmetic wraps, which is the
        /// whole difference between a circuit and the walk this replaced.</summary>
        static List<Vector3> CircuitLegs(List<Vector3> pts)
        {
            int n = pts.Count;
            var legs = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                legs.Add(SafeNormalize(pts[(i + 1) % n] - pts[i], Vector3.forward));
            return legs;
        }

        /// <summary>
        /// Spend the loop's three rotational degrees of freedom instead of randomising them: two
        /// put the entry station where a POLAR start gate is exactly one chord away, and the third
        /// spins the loop so its tangent there already points down the entry leg.
        ///
        /// <para>Randomising them was the first cut and it is why the entry leg was almost never
        /// in the chord band - the start gate has to sit on the axis for the start to be fair, so
        /// its distance from the loop is not free.</para>
        /// </summary>
        static bool TryPlaceCircuit(List<Vector3> source, List<Vector3> sourceLegs, int entry,
                                    float bearing, float chord, float factor,
                                    out Vector3 start, out List<Vector3> ordered,
                                    out List<Vector3> orderedLegs)
        {
            start = Vector3.zero;
            ordered = null;
            orderedLegs = null;

            int n = source.Count;
            float r = source[entry].magnitude;
            if (r < 1e-6f) return false;

            float alpha = Mathf.Asin(Mathf.Min(0.999f, chord * factor / r));
            var target = new Vector3(Mathf.Sin(alpha) * Mathf.Cos(bearing), Mathf.Cos(alpha),
                                     Mathf.Sin(alpha) * Mathf.Sin(bearing));

            var pts = new List<Vector3>(source);
            var legs = new List<Vector3>(sourceLegs);

            Vector3 d = SafeNormalize(pts[entry], Vector3.up);
            Vector3 ax = Vector3.Cross(d, target);
            float sn = ax.magnitude;
            if (sn > 1e-9f)
            {
                Vector3 q = ax / sn;
                float th = Mathf.Atan2(sn, Vector3.Dot(d, target));
                for (int i = 0; i < n; i++)
                {
                    pts[i] = RotateAbout(pts[i], q, th);
                    legs[i] = RotateAbout(legs[i], q, th);
                }
            }

            // The start gate must lie ON the polar axis, so its distance from the entry station is
            // solved rather than chosen: |S - P|^2 = chord^2 with S = (0, sy, 0).
            Vector3 p = pts[entry];
            float disc = chord * chord - (p.x * p.x + p.z * p.z);
            if (disc < 0f) return false;
            start = new Vector3(0f, p.y - Mathf.Sqrt(disc), 0f);
            float sr = start.magnitude;
            if (sr < BreakwaterCourseSettings.DefaultInnerRadius ||
                sr > BreakwaterCourseSettings.DefaultOuterRadius) return false;

            Vector3 nrm = SafeNormalize(pts[entry], Vector3.up);
            Vector3 ed = SafeNormalize(pts[entry] - start, Vector3.up);
            Vector3 a1 = legs[entry] - nrm * Vector3.Dot(legs[entry], nrm);
            Vector3 a2 = ed - nrm * Vector3.Dot(ed, nrm);
            if (a1.magnitude > 1e-6f && a2.magnitude > 1e-6f)
            {
                a1 = a1.normalized;
                a2 = a2.normalized;
                float spin = Mathf.Atan2(Vector3.Dot(Vector3.Cross(a1, a2), nrm), Vector3.Dot(a1, a2));
                for (int i = 0; i < n; i++)
                {
                    pts[i] = RotateAbout(pts[i], nrm, spin);
                    legs[i] = RotateAbout(legs[i], nrm, spin);
                }
            }

            ordered = new List<Vector3>(n);
            orderedLegs = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                ordered.Add(pts[(entry + i) % n]);
                orderedLegs.Add(legs[(entry + i) % n]);
            }
            return true;
        }

        /// <summary>Every cap the circuit must hold, plus the two join angles the caller scores on.</summary>
        static bool CircuitHoldsCaps(Vector3 start, List<Vector3> pts, List<Vector3> legs,
                                     in BreakwaterCourseSettings s,
                                     out float joinStart, out float joinCircuit)
        {
            int n = pts.Count;
            Vector3 entry = pts[0] - start;
            float entryLen = entry.magnitude;
            Vector3 ed = SafeNormalize(entry, Vector3.up);
            joinStart = Angle(Vector3.up, ed);
            joinCircuit = Angle(ed, legs[0]);

            if (entryLen < s.MinStep || entryLen > s.MaxStep) return false;

            float sep = s.MinSeparation;

            for (int i = 0; i < n; i++)
            {
                float chord = (pts[(i + 1) % n] - pts[i]).magnitude;
                if (chord < s.MinStep || chord > s.MaxStep) return false;
                if (Angle(legs[(i - 1 + n) % n], legs[i]) > s.MaxTurnDegrees) return false;

                float rad = pts[i].magnitude;
                if (rad < s.InnerRadius || rad > s.OuterRadius) return false;

                // A CHORD of a loop can pass closer to the cell centre than either of its ends,
                // which a walk's per-station shell test never had to consider.
                Vector3 ab = pts[(i + 1) % n] - pts[i];
                float t = Mathf.Clamp01(-Vector3.Dot(pts[i], ab) / Mathf.Max(1e-9f, Vector3.Dot(ab, ab)));
                if ((pts[i] + ab * t).magnitude < s.InnerRadius) return false;

                if ((pts[i] - start).sqrMagnitude < sep * sep) return false;
                for (int j = i + 1; j < n; j++)
                    if ((pts[i] - pts[j]).sqrMagnitude < sep * sep) return false;

                if (ReachesSpawnPad(s, pts[i])) return false;
            }

            return !ReachesSpawnPad(s, start);
        }

        /// <summary>
        /// Attach the axes. <b>The start gate's axis is the POLE ITSELF</b>, and that is what makes
        /// every spawn pad equidistant AND face-on - equidistance alone is what a circuit breaks,
        /// because a closed loop's tangent at an axial point is perpendicular to an axial approach.
        /// </summary>
        static List<BreakwaterStation> FinishCourse(Vector3 start, List<Vector3> pts,
                                                    List<Vector3> legs,
                                                    in BreakwaterCourseSettings s, int seed)
        {
            var rng = new Rng(unchecked(seed ^ 0x1B873593));
            int n = pts.Count;
            var stations = new List<BreakwaterStation>(n + 1)
            {
                new BreakwaterStation(start, Vector3.up, s.PortRadius)
            };

            for (int i = 0; i < n; i++)
            {
                Vector3 incoming = legs[(i - 1 + n) % n];
                Vector3 outgoing = legs[i];
                Vector3 bis = SafeNormalize(incoming + outgoing, outgoing);
                float halfTurn = Angle(bis, incoming);
                float allowed = Mathf.Max(0f, Mathf.Min(s.AxisJitterDegrees, s.MaxPresentDegrees - halfTurn));
                Vector3 axis = allowed > 0.01f ? SafeNormalize(Deflect(ref rng, bis, allowed), bis) : bis;
                stations.Add(new BreakwaterStation(pts[i], axis, s.PortRadius));
            }

            return stations;
        }

        // ── geometry helpers (pure) ──────────────────────────────────────────

        /// <summary>
        /// Bounding radius of one station's geometry about its own centre.
        ///
        /// <para>The dish rim is the farthest point: it sits at
        /// <c>DishRatio * port</c> in the port plane and <c>(DishRatio - 1) * port / tan(a)</c>
        /// BEHIND it along the axis, so the two combine as a hypotenuse. A sphere is deliberately
        /// coarse - this number only ever REJECTS a candidate, so erring outward costs the walk a
        /// little freedom and can never let prism near a pad.</para>
        /// </summary>
        public static float StationReach(float portRadius)
        {
            const float halfAngle = BreakwaterStationBuilder.DishHalfAngleDegrees * Mathf.Deg2Rad;
            float invTan = Mathf.Cos(halfAngle) / Mathf.Sin(halfAngle);
            float ratio = BreakwaterStationBuilder.DishRatio;

            // The plate's own half-diagonal, so a plate straddling the rim is inside the sphere.
            float plateHalf = new Vector3(BreakwaterStationBuilder.DishPlateWidth,
                                          BreakwaterStationBuilder.DishPlateWidth,
                                          BreakwaterStationBuilder.DishPlateThickness).magnitude * 0.5f;

            float axial = (ratio - 1f) * invTan;
            return portRadius * Mathf.Sqrt(ratio * ratio + axial * axial) + plateHalf;
        }

        /// <summary>
        /// The spawn pads a course must keep clear of: the UNION over every seat count the card
        /// allows (2, 3, 4).
        ///
        /// <para><c>CellSpawnFormation.EquatorialRing</c> puts slot <c>i</c> of <c>n</c> at
        /// <c>i * 360/n</c> degrees from +Z on <c>y = 0</c>, so the union of 2, 3 and 4 seats is
        /// {0, 90, 120, 180, 240, 270}. Taking the UNION rather than the live roster is what keeps
        /// the course independent of how many pilots turned up: the geometry is generated once and
        /// broadcast once, and must not change if a seat is added between the two.</para>
        /// </summary>
        public static Vector3[] SpawnPadRing(Vector3 centre, float radius)
        {
            var bearings = new[] { 0f, 90f, 120f, 180f, 240f, 270f };
            var pads = new Vector3[bearings.Length];

            for (int i = 0; i < bearings.Length; i++)
            {
                float t = bearings[i] * Mathf.Deg2Rad;
                pads[i] = centre + new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t)) * radius;
            }

            return pads;
        }

        /// <summary>True when a station centred here would reach a spawn pad.</summary>
        static bool ReachesSpawnPad(in BreakwaterCourseSettings s, Vector3 cand)
        {
            if (s.SpawnPads == null || s.SpawnPads.Length == 0) return false;

            float reject = StationReach(s.PortRadius) + Mathf.Max(0f, s.SpawnPadClearance);
            float sq = reject * reject;

            for (int i = 0; i < s.SpawnPads.Length; i++)
                if ((s.SpawnPads[i] - cand).sqrMagnitude < sq) return true;

            return false;
        }

        static Vector3 SafeNormalize(Vector3 v, Vector3 fallback) =>
            v.sqrMagnitude > 1e-10f ? v.normalized : fallback;

        /// <summary>Unsigned angle in degrees between two unit vectors.</summary>
        public static float Angle(Vector3 a, Vector3 b) =>
            Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>Any unit vector perpendicular to <paramref name="v"/>, chosen deterministically.</summary>
        static Vector3 Perpendicular(Vector3 v)
        {
            Vector3 a = Mathf.Abs(v.x) < 0.9f ? Vector3.right : Vector3.up;
            return SafeNormalize(Vector3.Cross(v, a), Vector3.up);
        }

        /// <summary>
        /// Rotate <paramref name="v"/> by a random angle up to <paramref name="maxDegrees"/> about
        /// a random perpendicular axis - a uniform draw on the CONE around v.
        ///
        /// <para>The angle is drawn as <c>max * sqrt(u)</c> rather than <c>max * u</c>: a cone's
        /// area grows with the angle, so a linear draw crowds every deflection near zero and the
        /// course comes out nearly straight. This is the same shape as the fauna-band fix in
        /// Docs/ECOSYSTEM.md - a uniform draw in a radial coordinate is not a uniform
        /// dispersal.</para>
        ///
        /// <para><b>There is deliberately no early-out for a zero or negative angle.</b> It would
        /// make the number of draws a function of the ARGUMENT, so one degenerate corner would
        /// shift every subsequent draw and the model would walk a different course from the same
        /// seed. The only caller that can pass a small angle guards it at the call site, where the
        /// model guards it, at the same 0.01 threshold.</para>
        /// </summary>
        static Vector3 Deflect(ref Rng rng, Vector3 v, float maxDegrees)
        {
            // Two draws, in this order, always: the azimuth around the cone, then its opening.
            Vector3 spin = SafeNormalize(
                RotateAbout(Perpendicular(v), v, rng.Range(0f, 360f) * Mathf.Deg2Rad),
                Perpendicular(v));
            float angle = maxDegrees * Mathf.Sqrt(rng.Unit());
            return SafeNormalize(RotateAbout(v, spin, angle * Mathf.Deg2Rad), Vector3.forward);
        }

        /// <summary>Rodrigues rotation of <paramref name="v"/> about the unit <paramref name="axis"/>.</summary>
        static Vector3 RotateAbout(Vector3 v, Vector3 axis, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }
    }
}
