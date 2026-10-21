using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the prisms of ONE Breakwater station: a dish that flares back toward the pilot, a
    /// keystone collar ringing the eye, and a triple-rake weave of danger bars welding the throat
    /// shut. The whole mode is fourteen of these in order, and the choice the pilot makes at each
    /// one - fire, saw, or thread - is a choice about this geometry, so the geometry is where the
    /// mode's difficulty actually lives.
    ///
    /// <para><b>CLOSED FORM. Zero random draws, anywhere.</b> That is not a style preference, it
    /// buys two things nothing else does. (1) <c>Tools/Build/breakwater_arena.py</c> MIRRORS this
    /// file exactly, so the cell's <c>PhaseThresholds</c>, the collider budget and the prism-clamp
    /// proof are statements about the arena that actually ships rather than estimates of it -
    /// the moment a draw appears here, every one of those numbers becomes a guess. (2) A station
    /// is rebuilt from a BROADCAST POSE on every peer, so a station is identical on every machine
    /// without replicating a prism, a seed, or a stream position. The dish's plate jitter is a
    /// hash of the ring and plate indices for exactly this reason - see <see cref="Hash01"/>,
    /// and note it is deliberately free of any station index so all fourteen jitter alike and
    /// the model (which has no station index at all) can reproduce it.</para>
    ///
    /// <para><b>Everything emitted is <see cref="PrismKind.Plain"/> or
    /// <see cref="PrismKind.Danger"/>, and the reason is GEOMETRY, not the collider.</b> A shield
    /// does NOT cost a collider - every kind rides the same LOD-cullable <c>BoxCollider</c> and
    /// <c>shieldMeshCollider.enabled = true</c> appears nowhere (see
    /// <see cref="CosmicShore.Gameplay.SpawnableBreakwater"/>, which records the measurement). What
    /// is not contingent is the reach: a shield engages the circumscribing octahedron at 1.5x
    /// leafSize (<c>Docs/ECOSYSTEM.md</c> §35), which on a 12-unit rake pitch fuses the weave into a
    /// solid wall and deletes both the saw and the thread.</para>
    ///
    /// <para><b>What the three parts teach, in the order a pilot meets them.</b> The DISH is a
    /// horn you cannot miss and cannot be hurt by - it says "the station is here, and it is
    /// pointed at you" from a long way out, and it is also the ammunition (50 hostile prisms buy
    /// a rocket, so the plates you break opening one door roughly fund the next). The COLLAR is
    /// the aim point: twelve blocks covering 69% of the eye's rim, so a rocket a little off axis
    /// still clips something and detonates where the pilot wanted it to. The PLUG is the only
    /// part that bites. That grading is the whole design - <b>the tight thread is forgiving and
    /// the sloppy one is not</b>.</para>
    /// </summary>
    public static class BreakwaterStationBuilder
    {
        // ── The plug ────────────────────────────────────────────────────────────────────────
        // Three rakes of parallel bars, offset half a pitch so no line ever runs THROUGH the eye
        // and the thread stays open at every rake angle by construction rather than by luck.

        /// <summary>Rake bearings within the port plane, degrees. Three at 60 apart is the
        /// coarsest weave with no straight-line gap wider than the pitch at any bearing - two
        /// rakes leave a lattice of diamond holes a pilot can cheat through off-axis.</summary>
        static readonly float[] RakeAngles = { 0f, 60f, 120f };

        /// <summary>Perpendicular spacing between parallel bars. With a 3-unit cross-section the
        /// weave is mostly hole, which is what makes SAWING it open a real option instead of a
        /// wall of hit points.</summary>
        public const float RakePitch = 12f;

        /// <summary>A rake line closer than this to the rim is skipped: its chord is a stub that
        /// costs a prism and a collider and closes nothing.</summary>
        const float RakeEdgeMargin = 3f;

        /// <summary>Longest bar the builder will emit. Well under
        /// <c>PrismScaleAnimator.maxScale</c> (100), which is the point - see
        /// <see cref="LargestEmittedAxis"/> for why an over-length axis would be invisible.</summary>
        const float MaxBarLength = 62f;

        /// <summary>Bar cross-section, both in-plane and across the port plane.</summary>
        const float BarCross = 3f;

        // ── Flush: no prism of a station passes through another ──────────────────────────────
        // NO PRISM MAY INTERPENETRATE ANOTHER (Docs/ECOSYSTEM.md §49.10), held here at authoring
        // time by geometry rather than by any pass at load. Three things used to cross, measured
        // by Tools/Build/audit_prism_clipping.py (6,015 pairs at intensity 1, all in the plug):
        //
        //   1. The three rakes lay in ONE plane, so every bar of one rake cut straight through
        //      every bar of the other two where their lines crossed. No scale fixes a crossing,
        //      so the rakes are LAYERED along the axis, one cross-section apart (RakeLayer): the
        //      weave a pilot sees down the axis is identical, the bars of two rakes now lie face
        //      to face, and every bar keeps its full 3 x 3 section and its length.
        //   2. The bars of the two innermost lines ran in to the eye THROUGH the collar's
        //      blocks. They now start on the circle that just contains the collar
        //      (CollarClearRadius), so the eye's rim between 18 and 26.3 is the collar's alone -
        //      the Plain aim point the collar's remarks already describe, with Danger beyond it.
        //   3. Every line ran out to the port rim and into the dish's innermost ring, whose
        //      plates sit ON the rim. The bars now stop at that ring's inner reach
        //      (DishInnerReach).
        //
        // Neither trim changes a COUNT: a run is still cut into as many bars as the eye-to-rim
        // run always took, so the prism count, and with it Tools/Build/breakwater_arena.py, holds
        // at every intensity; only the bar LENGTHS shrink, and the model prices the new ones.

        /// <summary>Axial offset of rake <paramref name="rake"/> in the layered weave: one
        /// <see cref="BarCross"/> per rake, centred on the port plane (-3 / 0 / +3), so two rakes'
        /// bars meet face to face instead of passing through each other.</summary>
        static float RakeLayer(int rake) => (rake - (RakeAngles.Length - 1) * 0.5f) * BarCross;

        // ── The keystone collar ─────────────────────────────────────────────────────────────

        const int CollarCount = 12;
        const float CollarCube = 8f;

        /// <summary>
        /// DERIVED, never authored: the collar sits exactly half a block outside the eye, so its
        /// blocks' inner faces ARE the eye's rim (22 - 4 = 18 = <see cref="BreakwaterCourseSettings.EyeRadius"/>).
        /// The model's literal 22 is this same arithmetic; deriving it here means narrowing the
        /// eye can never leave a collar floating off the rim it is supposed to draw.
        /// </summary>
        public const float CollarRadius = BreakwaterCourseSettings.EyeRadius + CollarCube * 0.5f;

        /// <summary>
        /// The radius of the smallest circle in the port plane that contains every collar block:
        /// a block's outer corners stand at (CollarRadius + half a block) out and half a block
        /// round, so sqrt(26^2 + 4^2) = 26.31. A plug bar that starts on this circle cannot touch
        /// a block at any of the twelve bearings.
        /// </summary>
        static readonly float CollarClearRadius = Mathf.Sqrt(
            (CollarRadius + CollarCube * 0.5f) * (CollarRadius + CollarCube * 0.5f) +
            CollarCube * 0.5f * (CollarCube * 0.5f));

        // ── The dish ────────────────────────────────────────────────────────────────────────

        /// <summary>Dish rim radius as a multiple of the port radius. Public because the shoal
        /// scatter must keep its clusters clear of the dish and has to read the same number - two
        /// copies of it is one arena where the rubble grows through the horn.</summary>
        public const float DishRatio = 1.75f;

        /// <summary>Radial spacing of the dish's rings AND the plate pitch along each ring, so
        /// the plates tile roughly square and a ring's plate count falls straight out of its
        /// circumference.</summary>
        const float DishPitch = 14f;

        /// <summary>Half-angle of the cone, measured from its AXIS (the usual convention) - so
        /// the shell makes 22 degrees with the flow and 68 with the port plane. See
        /// <see cref="EmitDish"/> for the axial profile this implies.</summary>
        public const float DishHalfAngleDegrees = 22f;

        public const float DishPlateWidth = 7f;
        public const float DishPlateThickness = 1.5f;

        /// <summary>Per-plate scale jitter, +/- this fraction. Enough that the horn reads as
        /// wreckage rather than as a machined funnel; small enough that the thinnest axis stays
        /// clear of the prism clamp with room to spare.</summary>
        const float DishJitter = 0.18f;

        // ── The clamp the whole thing has to live inside ─────────────────────────────────────

        /// <summary>
        /// <c>PrismScaleAnimator</c> clamps every axis into [0.5, 100] INSIDE the setter, with no
        /// log and no return value, and the environment lay path writes <c>TargetScale</c>
        /// directly rather than through <c>AdmitTargetScale</c> - so an out-of-band axis is
        /// silently swallowed and every offline measurement of it is wrong (CLAUDE.md, "An
        /// AUTHORED prism size widens its clamp"). Stated here so the two bounds below can be
        /// DERIVED against it rather than eyeballed.
        /// </summary>
        public const float PrismMinScale = 0.5f;
        public const float PrismMaxScale = 100f;

        /// <summary>
        /// The thinnest axis this builder can ever emit: the dish plate's 1.5 thickness at the
        /// bottom of its jitter, 1.5 * 0.82 = 1.23. The bound is REACHED, not approached -
        /// <see cref="Hash01"/> returns exactly 0 for some (ring, plate) pair on some station -
        /// so it is asserted by construction rather than by sampling, and it clears
        /// <see cref="PrismMinScale"/> by 2.46x.
        /// </summary>
        public const float SmallestEmittedAxis = DishPlateThickness * (1f - DishJitter);

        /// <summary>
        /// The largest axis, taking the longer of the plate's jittered width (7 * 1.18 = 8.26)
        /// and a full-length bar (62). Both are far under <see cref="PrismMaxScale"/>, which is
        /// what the run-splitting in <see cref="EmitBarRun"/> exists to guarantee.
        /// </summary>
        public const float LargestEmittedAxis = MaxBarLength;

        /// <summary>
        /// Lay one station. <paramref name="emit"/> is the spawnable's <c>Emit</c> with the domain
        /// already bound to <see cref="Domains.Blue"/> - the arena is Blue everywhere so that
        /// <c>IsFriendlyEnvironmentPrism</c> (which is domain-only) reads it as hostile to every
        /// pilot: every rocket pays ammunition on every door, and the Sparrow's Charge-5 upgrade
        /// - which spares own-domain mass, and which the comeback system hands to whoever is
        /// LOSING - cannot render a station unopenable by the domain that happens to share its
        /// colour. Binding the domain at the call site rather than passing it through here is
        /// what makes that impossible to get wrong one part at a time.
        ///
        /// <para>Parts are emitted outside-in, in the order the pilot meets them. Nothing depends
        /// on the order - the lay is a set - but a reveal that builds toward the pilot reads as
        /// the station arriving rather than as prisms appearing.</para>
        /// </summary>
        public static void Build(in BreakwaterStation station,
                                 Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            // ONE degeneracy guard, at the top. A course axis is a jittered flow bisector and can
            // point anywhere, including at world up where LookRotation's default up-reference is
            // degenerate - the same guard RaceGateRing.Build carries, for the same reason.
            // Past this point the basis is orthonormal by construction, so every LookRotation
            // below is handed two perpendicular unit vectors and cannot fail; guarding each of
            // them individually would only hide that fact.
            Vector3 axis = station.Axis.sqrMagnitude > 1e-6f
                ? station.Axis.normalized
                : Vector3.forward;

            Vector3 u = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.ProjectOnPlane(Vector3.right, axis);
            u.Normalize();
            Vector3 v = Vector3.Cross(axis, u);   // unit: axis and u are perpendicular unit vectors

            EmitDish(station.Position, axis, u, v, station.PortRadius, emit);
            EmitCollar(station.Position, axis, u, v, emit);
            EmitPlug(station.Position, axis, u, v, station.PortRadius, emit);
        }

        /// <summary>The dish's rim radius for a port. One expression, two readers (here and the
        /// shoal clearance), so the horn and the rubble cannot overlap by drift.</summary>
        public static float DishRadius(float portRadius) => DishRatio * portRadius;

        /// <summary>
        /// How close to the axis the dish's innermost ring reaches. Its plates are centred ON the
        /// port rim, and a plate's half-width down the slope (sin a of it) plus its half-thickness
        /// across the shell (cos a of it) bring its inner edge in toward the axis; at the top of the
        /// jitter that is 1.18 x (3.5 sin 22 + 0.75 cos 22) = 2.37 units inside the rim. Going
        /// round the ring only moves a point AWAY from the axis, so nothing of the ring is closer.
        /// The plug's bars end inside this radius.
        /// </summary>
        static float DishInnerReach(float portRadius)
        {
            float a = DishHalfAngleDegrees * Mathf.Deg2Rad;
            return portRadius - (1f + DishJitter) *
                   (DishPlateWidth * 0.5f * Mathf.Sin(a) + DishPlateThickness * 0.5f * Mathf.Cos(a));
        }

        // ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The weave. Three rakes at 0/60/120 degrees within the port plane, each a family of
        /// parallel lines at perpendicular offsets +/-(k + 0.5) * pitch. The half-pitch offset is
        /// load-bearing: at k = 0 the nearest line stands 6 units off centre, so no line of any
        /// rake passes through the eye and the thread exists at every bearing without a special
        /// case carved for it.
        ///
        /// <para>Each line is clipped to the annulus [eye, port]. A line whose BAR BODY comes
        /// closer to centre than the eye radius is SPLIT into two runs, one either side of the
        /// hole - which is what actually cuts the eye out of the weave - and a line outside it is
        /// one run across the full chord. A run is then cut into equal bars no longer than
        /// <see cref="MaxBarLength"/>: equal rather than "as many full-length bars as fit plus a
        /// remainder", because a stub prism at the rim costs the same collider as a full bar and
        /// reads as damage rather than as structure.</para>
        ///
        /// <para><b>THE CLIP IS AGAINST THE BAR'S NEAR EDGE, NOT ITS CENTRELINE, and that is a
        /// correctness fix rather than a refinement.</b> A bar is <see cref="BarCross"/> wide, so
        /// a line whose centre stands exactly <see cref="BreakwaterCourseSettings.EyeRadius"/> off
        /// centre still puts half a cross-section of prism inside the hole. Testing <c>d</c> alone
        /// made the k = 1 line - at <c>(1 + 0.5) * 12 = 18.0</c>, identically the eye radius - an
        /// UNSPLIT full chord, so six bar bodies straddled 16.5..19.5 and formed a hexagon of
        /// inradius 16.5 at every station, at every intensity. The keystone collar's inner faces
        /// sit at exactly 18, so the station advertised a mouth 1.5 units wider than it had.
        /// Clipping the near edge (<c>d - BarCross / 2</c>) and taking the eye's half-chord THERE
        /// puts the nearest corner of the nearest bar at exactly the eye radius - which is what
        /// makes the documented 1.46x hull clearance true rather than 1.34x. Since the flush pass
        /// (see "Flush" above) that eye-to-rim run sets only how many bars a run is cut into: the
        /// bars themselves are laid from the collar's clear circle to the dish's inner reach, in
        /// their rake's own layer.</para>
        /// </summary>
        static void EmitPlug(Vector3 centre, Vector3 axis, Vector3 u, Vector3 v, float port,
                             Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            const float eye = BreakwaterCourseSettings.EyeRadius;
            float eyeSq = eye * eye;
            float portSq = port * port;
            float collarSq = CollarClearRadius * CollarClearRadius;
            float reach = DishInnerReach(port);
            float reachSq = reach * reach;

            for (int a = 0; a < RakeAngles.Length; a++)
            {
                float rad = RakeAngles[a] * Mathf.Deg2Rad;
                float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

                Vector3 dir = c * u + s * v;        // along the bars
                Vector3 perp = -s * u + c * v;      // across them, still in the port plane

                // The bars lie in a plane parallel to the port plane, so local Y (the axis) is
                // perpendicular to local Z (the bar) by construction; each rake's plane is
                // RakeLayer off the port plane (see "Flush" above).
                Quaternion rot = Quaternion.LookRotation(dir, axis);
                Vector3 layer = axis * RakeLayer(a);

                for (int k = 0; (k + 0.5f) * RakePitch < port - RakeEdgeMargin; k++)
                {
                    float d = (k + 0.5f) * RakePitch;
                    float near = Mathf.Max(0f, d - BarCross * 0.5f);  // the bar's inner FACE
                    float far = d + BarCross * 0.5f;                   // ...and its outer face
                    float half = Mathf.Sqrt(portSq - d * d);          // half-chord at the rim

                    // Where the laid bars END: the bar's outer corner - at (perp = far, along =
                    // end) - stops on the dish's inner reach. Never below zero; no shipped port
                    // puts a line's outer face outside that reach (breakwater_arena.py asserts it).
                    float end = Mathf.Sqrt(Mathf.Max(0f, reachSq - far * far));

                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 lineOrigin = centre + perp * (sign * d) + layer;

                        if (near < eye)
                        {
                            // The COUNT is still the eye-to-rim run's: the half-chord measured at
                            // the NEAR FACE, so the run is the one whose nearest corner - at (perp
                            // = near, along = inner) - would land on the eye.
                            float inner = Mathf.Sqrt(eyeSq - near * near);
                            int bars = BarsFor(half - inner);

                            // The LAID run starts on the collar's clear circle (its nearest corner
                            // lands there) and stops on the dish's inner reach.
                            float start = Mathf.Sqrt(collarSq - near * near);
                            float laid = Mathf.Max(0f, end - start);
                            EmitBarRun(lineOrigin, dir, rot, start, laid, bars, emit);
                            EmitBarRun(lineOrigin, dir, rot, -end, laid, bars, emit);
                        }
                        else
                        {
                            EmitBarRun(lineOrigin, dir, rot, -end, 2f * end, BarsFor(2f * half), emit);
                        }
                    }
                }
            }
        }

        /// <summary>How many equal bars a run of <paramref name="length"/> is cut into: as few as
        /// keep each one no longer than <see cref="MaxBarLength"/>. Taken from the full eye-to-rim
        /// run, never the trimmed laid one, so the flush trims change no count.</summary>
        static int BarsFor(float length) => Mathf.Max(1, Mathf.CeilToInt(length / MaxBarLength));

        /// <summary>
        /// Cut one run - the interval [<paramref name="start"/>, start + <paramref name="length"/>]
        /// measured along <paramref name="dir"/> from <paramref name="origin"/> - into
        /// <paramref name="bars"/> equal bars laid end to end (face to face, so flush), and emit
        /// them.
        ///
        /// <para>The ceiling division in <see cref="BarsFor"/> is the whole reason the prism clamp
        /// never bites this arena: every bar is shorter than <see cref="MaxBarLength"/>, against a
        /// clamp at 100 that would have swallowed the difference in silence.</para>
        /// </summary>
        static void EmitBarRun(Vector3 origin, Vector3 dir, Quaternion rot, float start,
                               float length, int bars, Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            if (length <= 0f) return;

            float barLength = length / bars;
            Vector3 scale = new Vector3(BarCross, BarCross, barLength);

            for (int i = 0; i < bars; i++)
            {
                float t = start + (i + 0.5f) * barLength;
                emit(origin + dir * t, rot, scale, PrismKind.Danger);
            }
        }

        /// <summary>
        /// The keystone collar: twelve cubes on a ring at <see cref="CollarRadius"/>, 30 degrees
        /// apart, in the port plane.
        ///
        /// <para><b>It is the aim point, and it is sized to be unmissable.</b> Twelve 8-unit
        /// blocks on a 22-unit ring cover 12 * 8 / (2 * pi * 22) = 69% of the rim's circumference,
        /// so a rocket aimed at the eye and a little off axis still clips a block and detonates
        /// where the pilot meant it to rather than sailing through and arming on nothing.</para>
        ///
        /// <para><b>Deliberately <see cref="PrismKind.Plain"/>, not Danger.</b> The collar is what
        /// a pilot brushes when they very nearly thread the eye, and clipping the rim of the hole
        /// you were aiming at should be an ordinary prism hit - while a wild miss lands in the
        /// weave, which is Danger and hurts. Making the collar dangerous would punish the good
        /// attempt and the bad one identically and delete the difference between them.</para>
        ///
        /// <para>Each block is posed with local Z along the flow and local Y radial, so its
        /// half-extent falls on the radius and its inner face lands exactly on the eye's rim -
        /// the rim the pilot is threading is a real flat surface, not an implied circle.</para>
        /// </summary>
        static void EmitCollar(Vector3 centre, Vector3 axis, Vector3 u, Vector3 v,
                               Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            Vector3 scale = new Vector3(CollarCube, CollarCube, CollarCube);

            for (int i = 0; i < CollarCount; i++)
            {
                float phi = i * (Mathf.PI * 2f / CollarCount);
                Vector3 radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * v;
                emit(centre + radial * CollarRadius,
                     Quaternion.LookRotation(axis, radial), scale, PrismKind.Plain);
            }
        }

        /// <summary>
        /// The horn: concentric rings of plates on a cone shell whose rim is pinned to the port
        /// and whose mouth opens back toward the incoming leg.
        ///
        /// <para><b>The axial profile, derived.</b> The half-angle is measured from the cone's
        /// AXIS, so the shell makes 22 degrees with the flow. On a cone, radius grows linearly
        /// with distance from the apex (r = t * tan a), so two rings differ in axial position by
        /// dt = dr / tan(a). The rim is pinned at r = PortRadius in the port plane and the mouth
        /// opens toward the pilot, i.e. along -Axis, so a ring of radius r sits at
        /// <c>offset = -Axis * (r - PortRadius) / tan(22 deg)</c>, which is 2.475 * (r - PortRadius).
        /// The sign is worth stating twice, because it is the one thing here that inverts
        /// silently: a dish built along +Axis is a shell the pilot never sees, hiding behind the
        /// plug and adding nothing to the approach but prisms and colliders.</para>
        ///
        /// <para><b>Plates lie TANGENT to the shell.</b> A point on the surface is
        /// <c>apex + g*t</c> with generator <c>g = -cos(a)*Axis + sin(a)*radial</c>, so the
        /// outward normal is <c>n = sin(a)*Axis + cos(a)*radial</c> (n.g is identically zero).
        /// Posing local Z along n puts the plate's 1.5-unit thickness across the shell and its
        /// two 7-unit faces along the slope and around the ring. The normal's SIGN is immaterial
        /// for a symmetric slab; outward is chosen so a reader can predict the pose.</para>
        ///
        /// <para>A ring's plate count is <c>round(2 pi r / pitch)</c> - the same pitch as the
        /// radial spacing, so plates tile roughly square at every radius and the ring count and
        /// plate count both fall out of the port radius with nothing to author. Note
        /// <c>Mathf.RoundToInt</c> is round-half-to-EVEN, matching the model's <c>round()</c>;
        /// a hand-rolled <c>(int)(x + 0.5f)</c> would break the mirror at a half.</para>
        /// </summary>
        static void EmitDish(Vector3 centre, Vector3 axis, Vector3 u, Vector3 v, float port,
                             Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            float halfAngle = DishHalfAngleDegrees * Mathf.Deg2Rad;
            float sinA = Mathf.Sin(halfAngle);
            float cosA = Mathf.Cos(halfAngle);
            float depthPerUnitRadius = cosA / sinA;      // 1 / tan(a), the axial profile above
            float outer = DishRadius(port);

            // Indexed rather than accumulated so the ring radii carry no float drift; the epsilon
            // matches the model's inclusive bound, and no shipped port lands a ring on it.
            for (int ring = 0; ; ring++)
            {
                float r = port + ring * DishPitch;
                if (r > outer + 1e-4f) break;

                Vector3 ringCentre = centre - axis * ((r - port) * depthPerUnitRadius);

                // Unreachable below r ~ 1.1 units and therefore below every shipped port, so it
                // cannot put this builder and the model on different arithmetic; it is here so a
                // future narrowing of the ladder cannot divide by zero on the azimuth step.
                int plates = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * r / DishPitch));
                float step = Mathf.PI * 2f / plates;

                for (int p = 0; p < plates; p++)
                {
                    float phi = p * step;
                    Vector3 radial = Mathf.Cos(phi) * u + Mathf.Sin(phi) * v;
                    Vector3 generator = -cosA * axis + sinA * radial;
                    Vector3 normal = sinA * axis + cosA * radial;

                    // ONE factor for all three axes: a uniform scale keeps the plate's aspect -
                    // its identity as a thin slab - exact at every draw, where per-axis jitter
                    // would turn some plates into splinters and others into blocks.
                    //
                    // The jitter is mean-zero per AXIS but k^3 is NOT mean-zero, so the dish is
                    // ~3.2% heavier than its nominal volume. That is small against the cell's
                    // 120,000-unit Restless headroom and it is still not estimated:
                    // Tools/Build/breakwater_arena.py mirrors Hash01 bit for bit and sums the real
                    // draws, so the authored PhaseThresholds describe this arena exactly (verified
                    // by compiling this file and diffing every emitted scale against the model -
                    // 0.0000% on all four intensities). Pricing the dish at nominal would have
                    // been the easy call and would have made every threshold an approximation.
                    float k = 1f + (2f * Hash01(ring, p) - 1f) * DishJitter;

                    emit(ringCentre + radial * r,
                         Quaternion.LookRotation(normal, generator),
                         new Vector3(DishPlateWidth * k, DishPlateWidth * k, DishPlateThickness * k),
                         PrismKind.Plain);
                }
            }
        }

        /// <summary>
        /// Order-independent hash of a (ring, plate) pair into [0, 1).
        ///
        /// <para>The dish's variety has to come from somewhere, and it cannot come from a draw:
        /// see the class remarks. It is also deliberately free of any STATION index, so all
        /// fourteen stations jitter identically - which is what lets
        /// <c>Tools/Build/breakwater_arena.py</c>, which has no notion of a station index,
        /// reproduce the emitted scales exactly, and what lets a peer rebuild a station from a
        /// pose alone.</para>
        ///
        /// <para>The same integer-mix family and 24-bit mantissa trick as
        /// <c>CellEnvironmentSpawnableBase.Hash01</c>, duplicated rather than shared because that
        /// one is a protected instance-side helper on a MonoBehaviour base - reaching it would
        /// mean either widening that surface or making this builder a component, and a pure
        /// static that owns its own determinism is worth eight lines. The salt is not decoration:
        /// without it (0, 0) is a fixed point of a multiply-xor mix, and ring 0 plate 0 is a real
        /// plate on every station.</para>
        /// </summary>
        static float Hash01(int ring, int plate)
        {
            unchecked
            {
                uint h = ((uint)ring * 0x9E3779B9u) ^ ((uint)plate * 0x85EBCA6Bu) ^ 0x7F4A7C15u;
                h ^= h >> 15; h *= 0x2545F491u;
                h ^= h >> 13; h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }
    }
}
