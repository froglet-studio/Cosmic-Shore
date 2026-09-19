using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Garland" - the FAR-CAMERA cell: a flowering bough wound twice around the seed of the
    /// world, cut to ~5,000 prisms so it loads in a breath and reads as one composition from the
    /// menu camera's orbit rather than as a wall of detail nobody is close enough to see.
    ///
    /// <para>It is the Ourobor/Yggdra hybrid at a fifth of their weight, and the budget is the
    /// design. The freestyle seven each spend 34-41k prisms building places you FLY THROUGH; this
    /// one is composed for the one shot the home screen actually shows - <c>MenuCam_LavaLamp1</c>
    /// orbiting the cell centre at <see cref="CamR"/> with the autopilot vessel drifting through
    /// it. At that distance a nominal 2.5-unit prism is a pixel, so the whole file follows one
    /// rule: <b>spend prisms on LENGTH and SILHOUETTE, never on surface</b>. Every family is a
    /// curve laid ONE prism per step with that prism sized to close the gap, which buys a
    /// continuous, readable line for a twentieth of what filling the same shape as a sheet
    /// costs.</para>
    ///
    /// <para><b>Three depth layers</b>, because depth is what a distant static camera has instead
    /// of detail: the SHORE (bands + landfall patches hugging the nucleus, so the biggest object
    /// in frame reads as a world instead of a bare marker), the BOUGH and its VINE (the two
    /// counter-wound torus knots that are the subject, in the band the camera sits inside), and
    /// the CROWN and MOTES (thinning outward to the membrane, so the composition has a far edge).
    /// The FALLS spiral from the bough down onto the shore and are what ties the layers into one
    /// object rather than three shells.</para>
    ///
    /// <para><b>Zero danger, deliberately.</b> Yggdra's thorns are the environment being real for
    /// a pilot who chose to fly it; this cell's normal state is an AI flying the player's vessel
    /// behind a menu, where a danger prism reads as the ship being jerked about for no reason the
    /// player can see. Geode and Ourobor already hold that pole (Docs/ECOSYSTEM.md §18).</para>
    ///
    /// <para><b>Determinism without the shared stream.</b> Nothing here draws from
    /// <c>_r</c>/<c>RangeF</c>/<c>Jit</c> or from value noise - every wobble is
    /// <see cref="CellEnvironmentSpawnableBase.Hash01"/> of the emitting index. That is not
    /// fastidiousness: it makes the whole world a closed form, so the offline model in
    /// <c>Tools/Build/author_garland_cell.py</c> reproduces the count, the volume and the minimum
    /// radius EXACTLY rather than estimating them, and the PhaseThresholds this cell ships with
    /// are measured rather than guessed (the /ecology skill §7).</para>
    ///
    /// <para>Nothing is laid inside <see cref="NucleusR"/> - Docs/ECOSYSTEM.md §13/§18.1: the
    /// nucleus interior IS the territorial claim, so an authored environment sitting in it hands
    /// node control to whatever colour it happens to favour before anyone has flown a metre. The
    /// assertion is on each prism's NEAREST CORNER, not its lay point.</para>
    /// </summary>
    public class SpawnableGarland : CellEnvironmentSpawnableBase
    {
        // ── The cell this world is composed against (all distances from the cell centre) ──
        //
        // Load-bearing constants, not comments: every family is derived from them, so moving the
        // nucleus or the membrane moves the composition instead of silently breaking its clearance.
        /// <summary>Nucleus.prefab localScale 400 x the Node mesh's ~0.98u radius - the node-control
        /// radius Cell.RefreshNucleusControlRadius derives from the renderer bounds.</summary>
        public const float NucleusR = 392f;
        /// <summary>CapsuleMembrane.prefab radius - the playfield boundary.</summary>
        public const float MembraneR = 1200f;
        /// <summary>MenuCam_LavaLamp1's orbit radius. The camera sits INSIDE the bough band, which
        /// is why the bough is the subject and the crown is the backdrop.</summary>
        public const float CamR = 686f;

        // ── The shore: a crust on the seed. Its plates are laid TANGENT, but the clearance is
        // asserted on the half-DIAGONAL anyway - a bound that stays true if a later pass
        // re-orients them, where the tangent-only bound quietly would not. ──
        const float ShoreR = 430f;
        const float ShoreBandStep = 21f;
        const int ShoreBands = 3;

        // ── The bough: a (2,3) torus knot. Radius runs Major-Minor .. Major+Minor = 530 .. 870,
        // centred on the camera orbit so the near pass looms and the far pass is the backdrop. ──
        //
        // THE INNER RADIUS IS SET BY THE BLOSSOMS, NOT BY THE WOOD. A blossom sitting at the
        // knot's closest approach reaches BlossomRadius further in, and its petals are this
        // cell's nearest prisms to the nucleus - so the clearance is an INEQUALITY on the knot's
        // parameters rather than a property of where the seven blossoms happened to land
        // (Docs/ECOSYSTEM.md §18.1: the nucleus radius is load-bearing, not a comment). Asserted
        // over the emitted point cloud by Tools/Build/author_garland_cell.py, on each prism's
        // NEAREST CORNER:
        //     Major - Minor  >=  NucleusR + BlossomRadius + petal half-diagonal + NucleusMargin
        const int BoughP = 2, BoughQ = 3;
        const float BoughMajor = 700f, BoughMinor = 170f;
        const float BoughStep = 22f;

        /// <summary>Clearance every family keeps between its nearest prism corner and the
        /// node-control radius. Not a safety fudge - it is the room a grazing herbivore and a
        /// drifting vessel need to pass between the world and the seed without clipping either.</summary>
        const float NucleusMargin = 16f;

        // ── The vine: a (3,2) knot counter-wound INSIDE the bough (470 .. 640), thinner.
        // Its INNER radius is set by its blossoms, not by the wood: a petal reaching inward
        // from the knot's closest approach is this cell's nearest prism to the nucleus, so
        // Major-Minor >= NucleusR + VineBlossomRadius + a petal's half-diagonal + margin. ──
        const int VineP = 3, VineQ = 2;
        const float VineMajor = 555f, VineMinor = 85f;
        const float VineStep = 26f;

        // ── Populations ──
        const int Blossoms = 9;           // on the bough
        const int VineBlossoms = 5;       // on the vine, smaller
        const int BlossomPetals = 84;
        const int VineBlossomPetals = 46;
        const float BlossomRadius = 92f, VineBlossomRadius = 40f;
        const int Falls = 16;             // bough -> shore
        const int Crowns = 16;            // bough -> membrane
        const int Terraces = 5;           // inhabited platforms on the bough
        const int Skirts = 34;            // leaf tufts hanging off the bough
        const int Motes = 260;

        /// <summary>The knot axis - a general direction, so neither knot is ever edge-on or
        /// face-on to the whole of the camera's orbit (which sweeps about (1,1,0)).</summary>
        static readonly Vector3 KnotAxis = new(0.36f, 0.88f, 0.31f);

        protected override int DefaultSeed => 73;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableGarland), 1);
        protected override int LayCapacity => 6000;

        // Every knot sample the two knots produced, kept so the families that RIDE a knot
        // (blossoms, falls, crowns, terraces, skirts) index the same points the bough was laid
        // from - a second evaluation would drift and hang the furniture off the wood.
        List<KnotSample> _bough, _vine;

        protected override void BuildEnvironment()
        {
            _bough = SampleKnot(BoughP, BoughQ, BoughMajor, BoughMinor, BoughStep);
            _vine = SampleKnot(VineP, VineQ, VineMajor, VineMinor, VineStep);

            BuildBough();
            BuildVine();
            BuildBlossoms();
            BuildSkirts();
            BuildFalls();
            BuildShoreBands();
            BuildCrown();
            BuildTerraces();
            BuildMotes();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Knot sampling
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>One point on a knot: where it is, which way it runs, and which way is "out"
        /// (away from the torus's own centre circle - the surface normal a rider stands on).</summary>
        public readonly struct KnotSample
        {
            public readonly Vector3 Pos, Tangent, Outward;
            public KnotSample(Vector3 pos, Vector3 tangent, Vector3 outward)
            { Pos = pos; Tangent = tangent; Outward = outward; }
        }

        /// <summary>
        /// Sample a (p,q) torus knot at EVEN ARC SPACING. The count is
        /// <c>floor(totalLength / step + 0.5)</c> from a fixed 4096-substep integration rather
        /// than an accumulate-and-emit walk: a walk's count depends on where float error lands
        /// relative to the final step, which is exactly the kind of thing that makes an offline
        /// model disagree with the engine by one prism and no obvious reason.
        /// (<c>Mathf.RoundToInt</c> is banker's rounding - the /ecology skill's own warning.)
        /// </summary>
        List<KnotSample> SampleKnot(int p, int q, float major, float minor, float step)
        {
            const int Sub = 4096;
            var arc = new float[Sub + 1];
            arc[0] = 0f;
            Vector3 prev = KnotPoint(0f, p, q, major, minor);
            for (int i = 1; i <= Sub; i++)
            {
                Vector3 cur = KnotPoint(2f * Mathf.PI * i / Sub, p, q, major, minor);
                arc[i] = arc[i - 1] + (cur - prev).magnitude;
                prev = cur;
            }

            float total = arc[Sub];
            int n = Mathf.Max(8, Mathf.FloorToInt(total / step + 0.5f));

            var samples = new List<KnotSample>(n);
            int cursor = 0;
            for (int k = 0; k < n; k++)
            {
                float want = total * k / n;
                while (cursor < Sub && arc[cursor + 1] < want) cursor++;
                float span = arc[cursor + 1] - arc[cursor];
                float f = span > 1e-6f ? (want - arc[cursor]) / span : 0f;
                float t = 2f * Mathf.PI * (cursor + f) / Sub;

                Vector3 pos = KnotPoint(t, p, q, major, minor);
                // Central difference on the parameter: a closed form derivative buys nothing here
                // and would have to be re-derived per (p,q), where this is one expression.
                const float H = 1e-3f;
                Vector3 tangent = (KnotPoint(t + H, p, q, major, minor) - KnotPoint(t - H, p, q, major, minor)).normalized;
                // "Out" is away from the torus's CENTRE CIRCLE - the point on the major circle
                // directly under this sample - so it is the surface normal, not the radial from
                // the cell centre (which is what a rider on the far side would call down).
                Vector3 hub = HubPoint(t, p, major);
                Vector3 outward = (pos - hub).normalized;
                samples.Add(new KnotSample(pos, tangent, outward));
            }
            return samples;
        }

        static Vector3 KnotPoint(float t, int p, int q, float major, float minor)
        {
            float r = major + minor * Mathf.Cos(q * t);
            return ToWorld(r * Mathf.Cos(p * t), r * Mathf.Sin(p * t), minor * Mathf.Sin(q * t));
        }

        /// <summary>The point on the torus's centre circle beneath parameter t.</summary>
        static Vector3 HubPoint(float t, int p, float major) =>
            ToWorld(major * Mathf.Cos(p * t), major * Mathf.Sin(p * t), 0f);

        /// <summary>Canonical knot space (torus axis = +z) into the cell, through one fixed basis
        /// derived from <see cref="KnotAxis"/> - literal vectors so the offline model reproduces it
        /// without depending on Unity's Euler ordering.</summary>
        static Vector3 ToWorld(float x, float y, float z)
        {
            Vector3 w = KnotAxis.normalized;
            Vector3 u = Vector3.Cross(w, Vector3.forward).normalized;
            Vector3 v = Vector3.Cross(w, u);
            return u * x + v * y + w * z;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Families
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The bough: one chunky prism per arc step, long axis down the tangent and wide axis
        /// across the surface. At 22u spacing a 26u prism overlaps ~18%, which is what keeps the
        /// line continuous through the knot's tightest curvature instead of dotting.
        /// Every ninth prism is a gold growth ring - the only decoration the wood gets, because a
        /// second colour is legible at 700 units where a second SHAPE is not.
        /// </summary>
        void BuildBough()
        {
            for (int i = 0; i < _bough.Count; i++)
            {
                var s = _bough[i];
                Emit(s.Pos, SpawnPoint.LookRotation(s.Tangent, s.Outward),
                    HJit(new Vector3(16f, 7f, 26f), i * 13 + 5, 0.12f),
                    i % 9 == 0 ? Domains.Gold : Domains.Jade);
            }
        }

        /// <summary>
        /// The vine: the counter-wound (3,2) knot inside the bough. Half the girth and a longer
        /// step, so it reads as a younger runner rather than a second trunk - and because it sits
        /// at 420..620 it is the one structure the camera looks ACROSS at the nucleus through.
        /// </summary>
        void BuildVine()
        {
            for (int i = 0; i < _vine.Count; i++)
            {
                var s = _vine[i];
                Emit(s.Pos, SpawnPoint.LookRotation(s.Tangent, s.Outward),
                    HJit(new Vector3(8f, 4f, 30f), i * 7 + 19, 0.12f),
                    i % 11 == 0 ? Domains.Gold : Domains.Jade);
            }
        }

        /// <summary>
        /// Blossoms: golden-angle discs facing along the bough's own tangent, so the camera sees
        /// them as full faces on one side of the knot and as edges on the other - the single
        /// cheapest way to get a distant composition to change while the camera crawls.
        /// Each petal runs OUTWARD from the centre (placed at half its own length, the same
        /// attachment fix PhyllotacticFlora's whorls needed), so the disc reads as a head of
        /// petals rather than a ring of chips.
        /// The boss is the cell's landmark tier: five super-shielded prisms per blossom.
        /// </summary>
        void BuildBlossoms()
        {
            LayBlossoms(_bough, Blossoms, BlossomPetals, BlossomRadius, new Vector3(9f, 2.6f, 20f), 1000);
            LayBlossoms(_vine, VineBlossoms, VineBlossomPetals, VineBlossomRadius, new Vector3(6f, 2.2f, 13f), 2000);
        }

        void LayBlossoms(List<KnotSample> knot, int count, int petals, float radius, Vector3 petal, int saltBase)
        {
            for (int b = 0; b < count; b++)
            {
                var s = knot[b * knot.Count / count];
                Vector3 axis = s.Tangent;
                Vector3 uAxis = s.Outward;
                Vector3 vAxis = Vector3.Cross(axis, uAxis);

                for (int i = 0; i < petals; i++)
                {
                    int salt = saltBase + b * 131 + i;
                    float a = i * GoldenAngle;
                    // sqrt keeps the areal density even, so the head does not clot at its centre.
                    float rr = radius * Mathf.Sqrt((i + 0.5f) / petals);
                    Vector3 outward = uAxis * Mathf.Cos(a) + vAxis * Mathf.Sin(a);
                    Vector3 scale = HJit(petal, salt, 0.16f);
                    // Cupped: petals lift toward the blossom's face as they go out.
                    Vector3 dir = (outward + axis * (0.34f * rr / radius)).normalized;
                    Emit(s.Pos + dir * (rr + scale.z * 0.5f),
                        SpawnPoint.LookRotation(dir, axis), scale,
                        Hash01(salt * 3) < 0.16f ? Domains.Ruby : Domains.Gold);
                }

                for (int c = 0; c < 5; c++)
                {
                    float a = c * GoldenAngle;
                    Vector3 outward = uAxis * Mathf.Cos(a) + vAxis * Mathf.Sin(a);
                    Emit(s.Pos + outward * 7f, SpawnPoint.LookRotation(axis, outward),
                        new Vector3(7f, 7f, 7f), Domains.Gold, PrismKind.SuperShielded);
                }
            }
        }

        /// <summary>
        /// Skirts: small leaf tufts hanging off the bough between the blossoms. Nine leaves each,
        /// fanned on the side AWAY from the knot's own surface normal so they break the bough's
        /// silhouette rather than thickening it - the cheapest way to stop a long clean curve
        /// reading as a pipe.
        /// </summary>
        void BuildSkirts()
        {
            for (int k = 0; k < Skirts; k++)
            {
                var s = _bough[k * _bough.Count / Skirts];
                Vector3 hang = -s.Outward;
                Vector3 side = Vector3.Cross(s.Tangent, hang);
                for (int i = 0; i < 9; i++)
                {
                    int salt = 3000 + k * 97 + i;
                    float a = (i - 4) * 0.30f;
                    Vector3 dir = (hang * Mathf.Cos(a) + side * Mathf.Sin(a)).normalized;
                    Vector3 scale = HJit(new Vector3(6f, 2f, 15f), salt, 0.22f);
                    Emit(s.Pos + dir * (9f + scale.z * 0.5f),
                        SpawnPoint.LookRotation(dir, s.Tangent), scale, Domains.Jade);
                }
            }
        }

        /// <summary>
        /// Falls: root strands that leave the bough, spiral a third of a turn about the cell
        /// centre and land on the shore. They are the composition's depth cue - the only family
        /// that crosses the whole gap between the two things the camera can always see (the
        /// nucleus and the bough), so the eye reads the cell as ONE object with a near and a far
        /// side instead of as two concentric shells.
        /// The landfall gets a rosette of shore plates, which is why the shore needs almost no
        /// scatter of its own.
        /// </summary>
        void BuildFalls()
        {
            for (int f = 0; f < Falls; f++)
            {
                var s = _bough[f * _bough.Count / Falls];
                Vector3 start = s.Pos;
                float r0 = start.magnitude;
                Vector3 n0 = start / r0;
                // Spin axis: the component of the bough's tangent perpendicular to the radial, so
                // the strand peels off in the direction the wood was already running.
                Vector3 drift = (s.Tangent - n0 * Vector3.Dot(s.Tangent, n0)).normalized;
                float turn = 0.30f + 0.16f * Hash01(4000 + f * 17);
                float rEnd = ShoreR + 8f;

                int steps = 40;
                Vector3 prev = start;
                Vector3 land = start;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    // Radius eases in (t^1.4) so the strand leaves the bough softly and dives at
                    // the end - a linear fall reads as a wire.
                    float r = Mathf.Lerp(r0, rEnd, Mathf.Pow(t, 1.4f));
                    float ang = turn * 2f * Mathf.PI * t;
                    Vector3 dirOnSphere = (n0 * Mathf.Cos(ang) + drift * Mathf.Sin(ang)).normalized;
                    Vector3 p = dirOnSphere * r;
                    Vector3 step = p - prev;
                    if (step.sqrMagnitude > 1e-4f)
                    {
                        int salt = 5000 + f * 211 + i;
                        Emit(prev + step * 0.5f, SpawnPoint.LookRotation(step, n0),
                            HJit(new Vector3(4.5f, 4.5f, step.magnitude * 1.08f), salt, 0.14f),
                            i > steps - 6 ? Domains.Gold : Domains.Jade);
                    }
                    prev = p;
                    land = p;
                }

                LayShorePatch(land, 6000 + f * 53);
            }
        }

        /// <summary>A rosette of shore plates where a fall touches down - the world's only
        /// "ground", and it exists exactly where the eye is already being led.</summary>
        void LayShorePatch(Vector3 land, int salt)
        {
            Vector3 n = land.normalized;
            Vector3 u = Vector3.Cross(n, Vector3.up).sqrMagnitude < 1e-4f
                ? Vector3.Cross(n, Vector3.right).normalized
                : Vector3.Cross(n, Vector3.up).normalized;
            Vector3 v = Vector3.Cross(n, u);
            for (int i = 0; i < 7; i++)
            {
                float a = i * GoldenAngle;
                float rr = i == 0 ? 0f : 26f * Mathf.Sqrt(i / 6f);
                Vector3 p = n * ShoreR + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rr;
                Emit(p, SpawnPoint.LookRotation(u, n), HJit(new Vector3(24f, 3f, 24f), salt + i, 0.18f),
                    Hash01(salt + i * 5) < 0.25f ? Domains.Gold : Domains.Jade);
            }
        }

        /// <summary>
        /// Shore bands: three great circles on the seed at mutually tilted inclinations - the
        /// coastlines. A band is a chain of flat plates, so it costs one prism per 21 units of
        /// coastline and still reads as a continuous line at 700 units, where the same area filled
        /// as a surface would cost forty times as much and read as the same line.
        /// </summary>
        void BuildShoreBands()
        {
            for (int b = 0; b < ShoreBands; b++)
            {
                // Tilt each band by the golden angle about a fixed axis so the three never share
                // a pole - the bands cross, which is what makes the seed read as mapped.
                float tilt = b * GoldenAngle;
                Vector3 axis = new Vector3(Mathf.Cos(tilt), 0.42f, Mathf.Sin(tilt)).normalized;
                Vector3 u = Vector3.Cross(axis, Vector3.forward).normalized;
                Vector3 v = Vector3.Cross(axis, u);

                int n = Mathf.Max(8, Mathf.FloorToInt(2f * Mathf.PI * ShoreR / ShoreBandStep + 0.5f));
                for (int i = 0; i < n; i++)
                {
                    int salt = 7000 + b * 977 + i;
                    // A coastline is not a hoop: drop a fifth of the plates so the band breaks up.
                    if (Hash01(salt * 11) < 0.20f) continue;
                    float a = 2f * Mathf.PI * i / n;
                    Vector3 dir = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    Vector3 tangent = (v * Mathf.Cos(a) - u * Mathf.Sin(a)).normalized;
                    Emit(dir * ShoreR, SpawnPoint.LookRotation(tangent, dir),
                        HJit(new Vector3(22f, 3f, 26f), salt, 0.18f),
                        b == 1 ? Domains.Blue : Domains.Jade);
                }
            }
        }

        /// <summary>
        /// The crown: boughlets leaving the bough OUTWARD and thinning toward the membrane, each
        /// ending in a leaf tuft. This is the far edge of the composition - without it the cell
        /// stops at the bough and the outer two thirds of the frame is empty black.
        /// </summary>
        void BuildCrown()
        {
            for (int c = 0; c < Crowns; c++)
            {
                var s = _bough[(c * _bough.Count / Crowns + _bough.Count / (2 * Crowns)) % _bough.Count];
                Vector3 start = s.Pos;
                float r0 = start.magnitude;
                Vector3 n0 = start / r0;
                Vector3 drift = (s.Tangent - n0 * Vector3.Dot(s.Tangent, n0)).normalized;
                float rEnd = 900f + 150f * Hash01(8000 + c * 29);
                float turn = 0.10f + 0.10f * Hash01(8100 + c * 31);

                int steps = 20;
                Vector3 prev = start;
                Vector3 tipDir = n0;
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float r = Mathf.Lerp(r0, rEnd, t);
                    float ang = turn * 2f * Mathf.PI * t;
                    Vector3 dir = (n0 * Mathf.Cos(ang) + drift * Mathf.Sin(ang)).normalized;
                    Vector3 p = dir * r;
                    Vector3 step = p - prev;
                    int salt = 8200 + c * 173 + i;
                    // Taper: a branch that keeps its girth to the tip reads as scaffolding.
                    float taper = Mathf.Lerp(1f, 0.45f, t);
                    Emit(prev + step * 0.5f, SpawnPoint.LookRotation(step, n0),
                        HJit(new Vector3(7f * taper, 7f * taper, step.magnitude * 1.08f), salt, 0.14f),
                        Domains.Jade);
                    prev = p;
                    tipDir = dir;
                }

                // Leaf tuft: a fan on the tip, facing back along the branch so it catches the
                // camera rather than pointing out of frame.
                Vector3 tu = Vector3.Cross(tipDir, Vector3.up).sqrMagnitude < 1e-4f
                    ? Vector3.Cross(tipDir, Vector3.right).normalized
                    : Vector3.Cross(tipDir, Vector3.up).normalized;
                Vector3 tv = Vector3.Cross(tipDir, tu);
                for (int i = 0; i < 14; i++)
                {
                    int salt = 8400 + c * 191 + i;
                    float a = i * GoldenAngle;
                    Vector3 outward = (tu * Mathf.Cos(a) + tv * Mathf.Sin(a) + tipDir * 0.45f).normalized;
                    Vector3 scale = HJit(new Vector3(7f, 2.4f, 17f), salt, 0.2f);
                    Emit(prev + outward * (10f + scale.z * 0.5f),
                        SpawnPoint.LookRotation(outward, tipDir), scale,
                        Hash01(salt * 7) < 0.22f ? Domains.Gold : Domains.Jade);
                }
            }
        }

        /// <summary>
        /// Terraces: five small inhabited platforms riding the bough - Ourobor's countryside
        /// idea at a fiftieth of its prism cost. A lens deck of concentric plate rings, three
        /// slender masts, and one super-shielded keystone each. They exist to give the cell a
        /// human scale the eye can measure the rest of it against.
        /// </summary>
        void BuildTerraces()
        {
            for (int k = 0; k < Terraces; k++)
            {
                var s = _bough[(k * _bough.Count / Terraces + _bough.Count / (3 * Terraces)) % _bough.Count];
                Vector3 n = s.Outward;
                Vector3 u = s.Tangent;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 c = s.Pos + n * 14f;

                // Deck: four rings, 6/12/18/24 plates - the count rises with the circumference so
                // the plate pitch stays even and the deck reads as one surface.
                int[] ring = { 6, 12, 18, 24 };
                float[] radius = { 13f, 26f, 39f, 51f };
                for (int r = 0; r < ring.Length; r++)
                {
                    for (int i = 0; i < ring[r]; i++)
                    {
                        int salt = 9000 + k * 331 + r * 37 + i;
                        float a = 2f * Mathf.PI * i / ring[r] + r * 0.4f;
                        Vector3 outward = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                        Emit(c + outward * radius[r], SpawnPoint.LookRotation(outward, n),
                            HJit(new Vector3(16f, 3.5f, 15f), salt, 0.16f),
                            Hash01(salt * 3) < 0.3f ? Domains.Gold : Domains.Blue);
                    }
                }

                for (int m = 0; m < 3; m++)
                {
                    float a = m * 2.0944f + 0.6f;
                    Vector3 outward = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    Vector3 basePos = c + outward * 34f;
                    for (int i = 0; i < 9; i++)
                    {
                        int salt = 9500 + k * 419 + m * 53 + i;
                        float h = 9f + i * 13f;
                        float taper = Mathf.Lerp(1f, 0.5f, i / 8f);
                        Emit(basePos + n * h, SpawnPoint.LookRotation(n, outward),
                            HJit(new Vector3(5.5f * taper, 5.5f * taper, 13f), salt, 0.12f),
                            Domains.Blue);
                    }
                }

                Emit(c + n * 6f, SpawnPoint.LookRotation(n, u),
                    new Vector3(11f, 11f, 11f), Domains.Gold, PrismKind.SuperShielded);
            }
        }

        /// <summary>
        /// Motes: a thin halo between the crown and the membrane. One prism each, no structure -
        /// pure parallax, which is what tells a nearly-still camera that the world has a depth.
        /// Volume-uniform in radius (cbrt of the lerp) for the same reason planting bands are:
        /// a uniform-in-radius draw crowds the inner edge and leaves the outer band empty.
        /// </summary>
        void BuildMotes()
        {
            for (int i = 0; i < Motes; i++)
            {
                // Fibonacci sphere: the one distribution that is even and needs no rejection.
                float z = 1f - 2f * (i + 0.5f) / Motes;
                float rho = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float a = i * GoldenAngle;
                Vector3 dir = new(rho * Mathf.Cos(a), z, rho * Mathf.Sin(a));

                float u = Hash01(11000 + i * 61);
                const float Inner = 860f, Outer = 1150f;
                float r = Mathf.Pow(Mathf.Lerp(Inner * Inner * Inner, Outer * Outer * Outer, u), 1f / 3f);
                Vector3 p = dir * r;

                int salt = 11500 + i * 71;
                Emit(p, SpawnPoint.LookRotation(dir, Vector3.up),
                    HJit(new Vector3(6f, 6f, 6f), salt, 0.35f),
                    Hash01(salt * 13) < 0.18f ? Domains.Gold : Domains.Blue);
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <see cref="CellEnvironmentSpawnableBase.Jit"/>'s hash-keyed twin: one uniform jitter
        /// factor per prism, but keyed on the emitting INDEX instead of drawn from the shared
        /// System.Random stream. That is what lets the offline model reproduce this world's volume
        /// exactly (see the class doc), and it also means inserting a family no longer re-rolls
        /// every prism after it.
        /// Floored at 0.5 for the same reason Jit is: PrismScaleAnimator clamps per axis inside
        /// its setter with no log, so an axis under the prefab's minScale is silently a different
        /// prism (Docs/ECOSYSTEM.md §34.9).
        /// </summary>
        static Vector3 HJit(Vector3 s, int n, float amt)
        {
            float k = 1f + (Hash01(n) * 2f - 1f) * amt;
            return new Vector3(Mathf.Max(0.5f, s.x * k), Mathf.Max(0.5f, s.y * k), Mathf.Max(0.5f, s.z * k));
        }
    }
}
