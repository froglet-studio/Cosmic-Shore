using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The block field a Blast Run station grows on its inbound leg, one per intensity.</summary>
    public enum BlastRunWorld
    {
        Scatter = 1,    // I1: a loose field of small and big cubes, pillars, slabs and the odd sphere - nothing moves
        Blocks = 2,     // I2: denser and bigger (big cubes, big spheres, wall panels) plus a sliding block group
        Lattice = 3,    // I3: denser again, beams across the field, sliders and a spinning rotor
        Fortress = 4,   // I4: the densest field and a beam lattice; rotors and opposed slider pairs; two doors
    }

    /// <summary>How a <see cref="BlastRunMover"/> moves: back and forth along its axis, or round it.</summary>
    public enum BlastRunMotion : byte
    {
        Slide = 1,
        Spin = 2,
    }

    /// <summary>
    /// One BLOCKED gate (SWITCHBACK.md "Blast Run"): its ring is sealed by a Breakwater door (keystone collar plus
    /// plug, <see cref="BreakwaterStationBuilder.BuildDoor"/>) with only the <see cref="BreakwaterCourseSettings.EyeRadius"/>
    /// eye open, and its inbound leg is filled with a <see cref="World"/> block field. The <see cref="Key"/> crystal sits
    /// on the leg's line <see cref="KeyLead"/> before the gate: collecting it fires the Dolphin's forward crystal blast
    /// down the leg, through the field and the door.
    /// </summary>
    public readonly struct BlastRunStation
    {
        public readonly int Gate;           // the blocked gate (course index)
        public readonly Vector3 Key;
        public readonly float KeyLead;      // u from the key to the gate along the leg
        public readonly Vector3 Door;       // the (first) door's centre
        public readonly Vector3 FlowAxis;   // unit: the gate's axis, pointing the way the leg flows through it
        public readonly float Port;
        public readonly bool DoubleDoor;    // I4: a second door the same distance DOWNSTREAM of the ring
        public readonly BlastRunWorld World;
        public readonly int WorldPrisms;    // the field's prisms, moving ones included

        public BlastRunStation(int gate, Vector3 key, float keyLead, Vector3 door, Vector3 flowAxis, float port,
                               bool doubleDoor, BlastRunWorld world, int worldPrisms)
        {
            Gate = gate; Key = key; KeyLead = keyLead; Door = door; FlowAxis = flowAxis; Port = port;
            DoubleDoor = doubleDoor; World = world; WorldPrisms = worldPrisms;
        }
    }

    /// <summary>
    /// A rigid group of Blast Run prisms (<c>FirstPrism .. FirstPrism + Count - 1</c>) that moves as one, as a pure
    /// function of the shared clock: a SLIDE along <see cref="Axis"/> (<c>Amplitude * (sin(wt + phase) - sin phase)</c>
    /// from the laid pose) or a SPIN about the line through <see cref="Pivot"/> along <see cref="Axis"/> (<c>w t</c>
    /// radians from the laid pose). Nothing about it is replicated or reacts to anyone: every peer evaluates
    /// <see cref="PoseAt"/> off <c>NetworkManager.ServerTime</c> and gets the same pose - zero per-prism traffic.
    /// </summary>
    public readonly struct BlastRunMover
    {
        public readonly int Station;
        public readonly int FirstPrism;
        public readonly int Count;
        public readonly BlastRunMotion Motion;
        public readonly Vector3 Pivot;      // spin: a point on the axis; slide: the track's centre
        public readonly Vector3 Axis;       // unit
        public readonly float Amplitude;    // slide: u either side of the track's centre
        public readonly float Omega;        // rad/s
        public readonly float Phase;        // rad: where on its cycle the group was laid

        public BlastRunMover(int station, int firstPrism, int count, BlastRunMotion motion, Vector3 pivot, Vector3 axis,
                             float amplitude, float omega, float phase)
        {
            Station = station; FirstPrism = firstPrism; Count = count; Motion = motion; Pivot = pivot; Axis = axis;
            Amplitude = amplitude; Omega = omega; Phase = phase;
        }

        /// <summary>The group's transform at <paramref name="seconds"/> RELATIVE to its laid pose: rotate about
        /// <see cref="Pivot"/> by <paramref name="spin"/>, then translate by <paramref name="offset"/>.</summary>
        public void PoseAt(float seconds, out Quaternion spin, out Vector3 offset)
        {
            if (Motion == BlastRunMotion.Spin)
            {
                float a = Mathf.Repeat(Omega * seconds, 2f * Mathf.PI);
                spin = Quaternion.AngleAxis(a * Mathf.Rad2Deg, Axis);
                offset = Vector3.zero;
            }
            else
            {
                spin = Quaternion.identity;
                offset = Axis * (Amplitude * (Mathf.Sin(Omega * seconds + Phase) - Mathf.Sin(Phase)));
            }
        }

        /// <summary>Where a prism laid at <paramref name="laidPosition"/> / <paramref name="laidRotation"/> stands
        /// under the pose <paramref name="spin"/> / <paramref name="offset"/> (<see cref="PoseAt"/>).</summary>
        public void Apply(in Quaternion spin, in Vector3 offset, Vector3 laidPosition, Quaternion laidRotation,
                          out Vector3 position, out Quaternion rotation)
        {
            if (Motion == BlastRunMotion.Spin)
            {
                position = Pivot + spin * (laidPosition - Pivot) + offset;
                rotation = spin * laidRotation;
            }
            else
            {
                position = laidPosition + offset;
                rotation = laidRotation;
            }
        }
    }

    /// <summary>
    /// BLAST RUN (Switchback's only game at I1-I4 since 2026-10-29; SWITCHBACK.md "Blast Run"; Yash 2026-10-28: "Dolphins need
    /// to use their shooting abilities to clear paths for their rings"; 2026-10-29: "3D grids of prisms with movable
    /// area that are challenging, cubes, cuboids, spheres, big and small objects ... easy to sync in multiplayer ...
    /// we target 100fps"). A pure function of the course, so every peer lays
    /// the same prisms and the EditMode tests (<c>SwitchbackBlastRunTests</c>) and
    /// <c>Tools/Build/audit_prism_clipping.py</c> hold it without a scene.
    ///
    /// <para><b>The loop.</b> Some gates are BLOCKED (<see cref="IsBlocked"/>): a Breakwater door hangs
    /// <see cref="DoorSetback"/> in front of the ring and seals all of it but the 18 u eye, and the leg in holds a
    /// BLOCK FIELD. On that leg's line, <see cref="KeyLeadFor"/> before the gate, floats a KEY crystal (laid by
    /// <see cref="CrystalManager.SetCourseKeys"/>; it respawns in place). Collecting it fires the Dolphin's forward
    /// conic blast along the gape, so a key taken while facing the gate blows the field and the door away in one go.
    /// Miss it and you thread the bore and the eye, or slalom the field.</para>
    ///
    /// <para><b>The field.</b> A deterministic 3D grid of <see cref="CellPitch"/> cells on the leg's own frame (x, y
    /// across, z upstream from the gate), <see cref="CellsHalf"/> cells either side of the line and up to
    /// <see cref="CellsDeep"/> deep. Each object claims whole cells and sits <see cref="CellMargin"/> inside them, so a
    /// station never clips itself: small cubes, 22 u blocks, pillars and slabs (one cell); small voxel spheres (19 cubes,
    /// one cell); big cubes and big voxel spheres (26 cubes; 2 x 2 x 2 cells); wall panels (2 x 2 x 1); long pillars
    /// (1 x 1 x 3); and beams (3-4 cells across) from I3. A straight BORE of <see cref="BoreRadius"/> (at least the
    /// eye) stays open down the line - the line of last resort, and the AI's lane.</para>
    ///
    /// <para><b>Moving areas.</b> From I2 some cells are reserved for <see cref="BlastRunMover"/>s: sliders (a 2 x 2
    /// of cubes running a 3-cell track), rotors (I3+: a hub and four danger arms spinning in a 2 x 2-cell disc) and
    /// opposed slider pairs (I4). Every mover is a closed-form function of the shared clock, so a moving section
    /// costs no network traffic and two peers can never disagree about it; its whole sweep is reserved, so it never
    /// touches a static block.</para>
    ///
    /// <para><b>Budgets</b> (the 100 fps target; SWITCHBACK.md "Blast Run - budgets"): <see cref="TotalCap"/> prisms
    /// per course, <see cref="StationCap"/> per station, at most <see cref="ColliderCap"/> prisms within the 200 u
    /// collider LOD radius of any prism, <see cref="MovingPrismCap"/> moving prisms in <see cref="MovingGroupCap"/>
    /// groups - all enforced here while laying, and asserted by the tests.</para>
    ///
    /// <para><b>Clearances.</b> Everything keeps <see cref="SwitchbackFeatures.MouthClearance"/> outside every ring,
    /// out of the nucleus, and off every OTHER leg's line; field objects also keep off keys and doors, and off other
    /// stations' fields (bounding samples, so no two prisms ever interpenetrate).</para>
    /// </summary>
    public static class SwitchbackBlastRun
    {
        // ── stations ─────────────────────────────────────────────────────
        public const float DoorSetback = 8f;            // u upstream of the ring plane (and downstream, for I4's second door)
        public const float KeyLeadShare = 0.62f;        // of the leg...
        public const float KeyLeadMin = 230f;           // ...clamped: inside the blast's 400 u minimum length...
        public const float KeyLeadMax = 400f;           // ...and never past it
        public const float KeyMinLead = 160f;           // a leg too short to hold this lead is not blocked
        public const float KeyClearance = 30f;          // u a key keeps off every other leg's line
        public const float DoorLineClearance = 20f;     // u past the port a door keeps off every other leg's line
        public const float StationSpacing = 280f;       // u between two stations' doors, where the course folds back on itself

        // ── fields ───────────────────────────────────────────────────────
        public const float WorldMouthGap = 50f;         // u past the ring's radius before the field starts
        public const float WorldKeyGap = 40f;           // u the field stops short of the key
        public const float WorldLineClearance = 40f;    // u field prisms keep off every other leg's line
        public const float WorldKeyClearance = 30f;     // u field prisms keep off every key
        public const float CellPitch = 24f;
        public const float CellMargin = 1f;             // u every object keeps inside its cells: 2 u between neighbours
        public const int MinCellsDeep = 2;              // a leg too short for this many slices gets no field

        static int I(int intensity) => Mathf.Clamp(intensity, 1, 4) - 1;
        /// <summary>Cells either side of the line (the field is 2n x 2n cells across).</summary>
        public static int CellsHalf(int intensity) => new[] { 3, 3, 4, 4 }[I(intensity)];
        /// <summary>Slices along the leg, at most (the leg between the mouth gap and the key gap may hold fewer).</summary>
        public static int CellsDeep(int intensity) => new[] { 5, 6, 7, 8 }[I(intensity)];
        /// <summary>The open bore round the line: no object's box, and no mover's sweep, comes inside it.</summary>
        public static float BoreRadius(int intensity) => new[] { 30f, 26f, 22f, 18f }[I(intensity)];
        /// <summary>The chance a free cell starts an object (bigger objects then claim more cells).</summary>
        public static float Fill(int intensity) => new[] { 0.30f, 0.40f, 0.50f, 0.60f }[I(intensity)];
        /// <summary>The share of small cubes and pillars laid as danger prisms.</summary>
        public static float DangerShare(int intensity) => new[] { 0.06f, 0.08f, 0.10f, 0.12f }[I(intensity)];

        // ── movers ───────────────────────────────────────────────────────
        public const int SliderPrisms = 4;              // a 2 x 2 of cubes...
        public const float SliderCube = 10f;            // ...this big, 1 u apart, square to the track
        public const int SliderTrackCells = 3;
        public const float SliderAmplitude = 24f;       // one cell either side of the track's centre
        public const int RotorPrisms = 5;               // a hub and four arms
        public const float RotorHub = 6f;
        public static readonly Vector3 RotorArmScale = new(16f, 4f, 4f);   // radial x tangential x along the axis
        public const float RotorArmCentre = 13f;        // arms 5..21 u out: clear of the hub's 4.24 u corner; swept 21.1
        public static float SlidePeriod(int intensity) => new[] { 0f, 4f, 3.2f, 2.6f }[I(intensity)];
        public static float SpinDegreesPerSecond(int intensity) => new[] { 0f, 0f, 90f, 135f }[I(intensity)];
        public static bool HasSliders(int intensity) => intensity >= 2;
        public static bool HasRotors(int intensity) => intensity >= 3;
        public static bool HasSliderPairs(int intensity) => intensity >= 4;
        public static bool HasBeams(int intensity) => intensity >= 3;
        public static bool HasDoubleDoor(int intensity) => intensity >= 4;

        // ── budgets (the 100 fps target; SWITCHBACK.md "Blast Run - budgets") ──
        /// <summary>Whole-course prism ceilings, doors and moving prisms included (Breakwater lays about 4600 at I1).</summary>
        public static int TotalCap(int intensity) => new[] { 1800, 2400, 3000, 3400 }[I(intensity)];
        /// <summary>One station's field, moving prisms included.</summary>
        public static int StationCap(int intensity) => new[] { 150, 170, 190, 210 }[I(intensity)];
        /// <summary>At most this many laid prisms within <see cref="ColliderRadius"/> of any laid prism - the colliders
        /// <c>PrismColliderLodManager</c> keeps live round a pilot there. Breakwater's measured worst is 568 / 464 / 322 /
        /// 276 (I1-I4) of a 1,500 band; these stay at or under 70 % of its I1 worst.</summary>
        public static int ColliderCap(int intensity) => new[] { 300, 340, 370, 400 }[I(intensity)];
        public const float ColliderRadius = 200f;       // PrismColliderLodManager.lodRadiusMeters
        /// <summary>Prisms written every frame (pose + <c>Prism.NotifyPositionChanged</c>) across the whole course.</summary>
        public static int MovingPrismCap(int intensity) => new[] { 0, 40, 100, 140 }[I(intensity)];
        public static int MovingGroupCap(int intensity) => new[] { 0, 10, 20, 28 }[I(intensity)];
        /// <summary>SpawnableSwitchbackFeatures' lay pool: over every TotalCap.</summary>
        public const int LayCapacity = 4096;

        /// <summary>
        /// The launch panel's FIXED SEED for an intensity: 45_501-45_504, the same numbers the retired fun-type
        /// picker gave Blast Run (45_000 + 5 x 100 + intensity), so the fixed courses testers know are unchanged.
        /// Never 0 (0 means "random" to the course seed pickers).
        /// </summary>
        public static int FixedSeed(int intensity) => 45_500 + Mathf.Clamp(intensity, 1, 4);

        public static BlastRunWorld WorldFor(int intensity) => (BlastRunWorld)(I(intensity) + 1);

        /// <summary>Whether gate <paramref name="gate"/> is meant to be blocked: never gate 0 (the start) or 1 (its leg
        /// is left clear, as in every fun type); then every third gate at I1, every second at I2, all at I3-I4.</summary>
        public static bool IsBlocked(int gate, int intensity)
        {
            if (gate < 2) return false;
            switch (Mathf.Clamp(intensity, 1, 4))
            {
                case 1: return gate % 3 == 2;
                case 2: return gate % 2 == 0;
                default: return true;
            }
        }

        /// <summary>The door's port: Breakwater's own per intensity, so the plug always reaches past the ring.</summary>
        public static float PortFor(int intensity) => BreakwaterCourseSettings.PortRadiusForIntensity(intensity);

        /// <summary>u from the key to the gate on a leg of <paramref name="legLength"/> whose start gate has radius
        /// <paramref name="previousRadius"/>; below <see cref="KeyMinLead"/> the station is dropped.</summary>
        public static float KeyLeadFor(float legLength, float previousRadius)
        {
            float lead = Mathf.Clamp(KeyLeadShare * legLength, KeyLeadMin, KeyLeadMax);
            return Mathf.Min(lead, legLength - (previousRadius + SwitchbackFeatures.MouthClearance));
        }

        /// <summary>u upstream of the gate where the field starts.</summary>
        public static float WorldNear(int intensity, float ringRadius) => ringRadius + WorldMouthGap;

        /// <summary>The flow-oriented axis of gate <paramref name="g"/> for a leg arriving along <paramref name="legDir"/>.</summary>
        public static Vector3 FlowAxis(in RaceGate gate, Vector3 legDir)
        {
            Vector3 a = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : legDir;
            return Vector3.Dot(a, legDir) < 0f ? -a : a;
        }

        /// <summary>The door frame, exactly <see cref="BreakwaterStationBuilder"/>'s.</summary>
        public static void DoorBasis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            u = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.ProjectOnPlane(Vector3.right, axis);
            u.Normalize();
            v = Vector3.Cross(axis, u);
        }

        // ── layout ───────────────────────────────────────────────────────

        struct Keepout
        {
            public Vector3 A, B;
            public float Radius;
            public Keepout(Vector3 a, Vector3 b, float r) { A = a; B = b; Radius = r; }
        }

        /// <summary>Where the keys go, in station order - <see cref="Layout"/>'s <c>Stations[i].Key</c>.</summary>
        public static List<Vector3> KeyPositions(IReadOnlyList<RaceGate> course, int intensity,
                                                 Vector3 centre = default, float clearRadius = 0f)
        {
            var keys = new List<Vector3>();
            var layout = Layout(course, intensity, centre, clearRadius);
            for (int i = 0; i < layout.Stations.Count; i++) keys.Add(layout.Stations[i].Key);
            return keys;
        }

        /// <summary>Blast Run's layout of <paramref name="course"/>: stations (keys, doors), then each station's block
        /// field (movers first, then the static objects) - each filtered by everything before it.</summary>
        public static SwitchbackFeatureLayout Layout(IReadOnlyList<RaceGate> course, int intensity,
                                                     Vector3 centre = default, float clearRadius = 0f)
        {
            var layout = new SwitchbackFeatureLayout();
            if (course == null || course.Count < 3) return layout;
            intensity = Mathf.Clamp(intensity, 1, 4);
            float port = PortFor(intensity);
            var keepouts = new List<Keepout>(64);

            // 1. Stations: key, door(s).
            var dirs = new List<Vector3>();
            for (int g = 2; g < course.Count; g++)
            {
                if (!IsBlocked(g, intensity)) continue;
                Vector3 a = course[g - 1].Position, b = course[g].Position;
                Vector3 legV = b - a;
                float leg = legV.magnitude;
                if (leg < 1f) continue;
                Vector3 dir = legV / leg;
                float lead = KeyLeadFor(leg, course[g - 1].Radius);
                if (lead < KeyMinLead) continue;
                Vector3 key = b - dir * lead;
                if (!PointClear(course, centre, clearRadius, key, 0f, KeyClearance, g - 1, -1)) continue;

                Vector3 axis = FlowAxis(course[g], dir);
                Vector3 door = b - axis * DoorSetback;
                Vector3 door2 = b + axis * DoorSetback;
                if (!DoorClear(course, centre, clearRadius, door, port, g)) continue;
                if (Crowded(layout.Stations, door)) continue;
                bool dbl = HasDoubleDoor(intensity);
                if (dbl && !DoorClear(course, centre, clearRadius, door2, port, g)) continue;

                EmitDoor(layout, door, axis, port);
                if (dbl) EmitDoor(layout, door2, axis, port);
                keepouts.Add(new Keepout(door, door, port + 6f));
                if (dbl) keepouts.Add(new Keepout(door2, door2, port + 6f));
                keepouts.Add(new Keepout(key, key, WorldKeyClearance));
                layout.Stations.Add(new BlastRunStation(g, key, lead, door, axis, port, dbl, WorldFor(intensity), 0));
                dirs.Add(dir);
            }

            // 2. Fields, station by station, under the budgets.
            var density = new Density(ColliderRadius, ColliderCap(intensity));
            for (int i = 0; i < layout.Prisms.Count; i++) density.Seed(layout.Prisms[i].Position);
            var ctx = new FieldContext
            {
                Course = course, Centre = centre, ClearRadius = clearRadius, Intensity = intensity, Layout = layout,
                Keepouts = keepouts, Density = density,
            };
            int stations = layout.Stations.Count;
            int doorPrisms = layout.Prisms.Count;
            int staticBudget = Mathf.Max(0, TotalCap(intensity) - doorPrisms - MovingPrismCap(intensity));
            for (int s = 0; s < stations; s++)
            {
                var st = layout.Stations[s];
                int before = layout.Prisms.Count;
                // Even shares of the course budgets, carried forward: a station that could not use its share leaves
                // it to the next.
                int groupShare = MovingGroupCap(intensity) * (s + 1) / stations - ctx.MovingGroups;
                int movingShare = MovingPrismCap(intensity) * (s + 1) / stations - ctx.MovingPrisms;
                int staticShare = staticBudget * (s + 1) / stations - ctx.StaticPrisms;
                EmitField(ctx, s, st, dirs[s], groupShare, movingShare, staticShare);
                layout.Stations[s] = new BlastRunStation(st.Gate, st.Key, st.KeyLead, st.Door, st.FlowAxis, st.Port,
                                                         st.DoubleDoor, st.World, layout.Prisms.Count - before);
            }
            return layout;
        }

        static bool Crowded(List<BlastRunStation> stations, Vector3 door)
        {
            for (int i = 0; i < stations.Count; i++)
                if ((stations[i].Door - door).sqrMagnitude < StationSpacing * StationSpacing) return true;
            return false;
        }

        static void EmitDoor(SwitchbackFeatureLayout layout, Vector3 position, Vector3 axis, float port)
        {
            BreakwaterStationBuilder.BuildDoor(new BreakwaterStation(position, axis, port),
                (p, r, sc, kind) => layout.Prisms.Add(new SwitchbackFeaturePrism(p, r, sc, kind)));
        }

        // ── fields ───────────────────────────────────────────────────────

        enum Shape : byte { SmallCube, Block, Pillar, Slab, SmallSphere, BigCube, BigSphere, BigSlab, LongPillar, Beam }
        const int ShapeCount = 10;

        /// <summary>Weighted shape menus per intensity: I1 is mostly small and single-cell, each step up adds bigger,
        /// wider and longer objects (big spheres from I2, beams across the field from I3, a beam lattice at I4).</summary>
        static readonly int[][] ShapeWeights =
        {
            //          SmallCube Block Pillar Slab SmallSphere BigCube BigSphere BigSlab LongPillar Beam
            new[] {     5,        2,    3,     2,   2,          1,      0,        0,      1,         0 },
            new[] {     3,        2,    2,     2,   2,          2,      1,        2,      1,         0 },
            new[] {     2,        2,    2,     2,   2,          2,      2,        2,      2,         3 },
            new[] {     2,        2,    2,     2,   1,          2,      2,        3,      2,         5 },
        };

        struct LBox
        {
            public Vector3 C, Size;     // local: x along s1, y along s2, z upstream
            public PrismKind Kind;
            public LBox(Vector3 c, Vector3 size, PrismKind kind) { C = c; Size = size; Kind = kind; }
        }

        sealed class FieldContext
        {
            public IReadOnlyList<RaceGate> Course;
            public Vector3 Centre;
            public float ClearRadius;
            public int Intensity;
            public SwitchbackFeatureLayout Layout;
            public List<Keepout> Keepouts;
            public Density Density;
            public int MovingGroups, MovingPrisms, StaticPrisms;
            public readonly List<List<Vector4>> Blobs = new();      // per station: (point, radius)
            public readonly List<Vector4> Spheres = new();          // per station: its field's bounding sphere
            public readonly List<Vector4> Samples = new();
            public readonly List<LBox> Boxes = new();
            public readonly List<Vector3> Points = new();
        }

        /// <summary>One station's cell grid in the leg's frame (z upstream from the gate).</summary>
        sealed class Field
        {
            public int Station, OwnLeg, N, Nz, Salt;
            public float Z0, Bore;
            public Vector3 Origin, S1, S2, Dir;
            public Quaternion Rot;
            public bool[] Occ;
            public int Side => 2 * N;
            public Vector3 W(Vector3 l) => Origin + S1 * l.x + S2 * l.y - Dir * l.z;
            public int Idx(int ix, int iy, int iz) => (iz * Side + (iy + N)) * Side + (ix + N);
            public Vector3 CellMin(int ix, int iy, int iz) => new(ix * CellPitch, iy * CellPitch, Z0 + iz * CellPitch);

            public bool Free(int ix, int iy, int iz, int sx, int sy, int sz)
            {
                if (ix < -N || iy < -N || iz < 0 || ix + sx > N || iy + sy > N || iz + sz > Nz) return false;
                for (int z = iz; z < iz + sz; z++)
                for (int y = iy; y < iy + sy; y++)
                for (int x = ix; x < ix + sx; x++)
                    if (Occ[Idx(x, y, z)]) return false;
                return true;
            }

            public void Claim(int ix, int iy, int iz, int sx, int sy, int sz)
            {
                for (int z = iz; z < iz + sz; z++)
                for (int y = iy; y < iy + sy; y++)
                for (int x = ix; x < ix + sx; x++)
                    Occ[Idx(x, y, z)] = true;
            }
        }

        /// <summary>
        /// Counts, for every laid prism, the laid prisms within <see cref="ColliderRadius"/> of it (itself included) -
        /// the colliders <c>PrismColliderLodManager</c> would keep live round a pilot there - and refuses an addition
        /// that could push any count past the cap. Conservative: an object of k prisms within e of c is refused when
        /// (prisms within r + e of c) + k, or any of those prisms' counts + k, would pass the cap.
        /// </summary>
        sealed class Density
        {
            readonly float _r;
            readonly int _cap;
            readonly List<Vector3> _p = new();
            readonly List<int> _n = new();
            readonly Dictionary<long, List<int>> _grid = new();
            readonly List<int> _near = new();

            public Density(float radius, int cap) { _r = radius; _cap = cap; }

            static long Key(int x, int y, int z) =>
                ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);

            void Near(Vector3 c, float radius, List<int> into)
            {
                into.Clear();
                int x0 = Mathf.FloorToInt((c.x - radius) / _r), x1 = Mathf.FloorToInt((c.x + radius) / _r);
                int y0 = Mathf.FloorToInt((c.y - radius) / _r), y1 = Mathf.FloorToInt((c.y + radius) / _r);
                int z0 = Mathf.FloorToInt((c.z - radius) / _r), z1 = Mathf.FloorToInt((c.z + radius) / _r);
                float r2 = radius * radius;
                for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    if (_grid.TryGetValue(Key(x, y, z), out var list))
                        for (int i = 0; i < list.Count; i++)
                            if ((_p[list[i]] - c).sqrMagnitude <= r2) into.Add(list[i]);
            }

            public bool CanAdd(Vector3 c, float e, int k)
            {
                Near(c, _r + e, _near);
                if (_near.Count + k > _cap) return false;
                for (int i = 0; i < _near.Count; i++)
                    if (_n[_near[i]] + k > _cap) return false;
                return true;
            }

            public void Seed(Vector3 p)
            {
                Near(p, _r, _near);
                int self = _p.Count;
                for (int i = 0; i < _near.Count; i++) _n[_near[i]]++;
                _p.Add(p);
                _n.Add(_near.Count + 1);
                long key = Key(Mathf.FloorToInt(p.x / _r), Mathf.FloorToInt(p.y / _r), Mathf.FloorToInt(p.z / _r));
                if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = new List<int>();
                list.Add(self);
            }
        }

        static void EmitField(FieldContext ctx, int s, BlastRunStation st, Vector3 dir,
                              int groupShare, int movingShare, int staticShare)
        {
            var course = ctx.Course;
            int it = ctx.Intensity;
            float z0 = WorldNear(it, course[st.Gate].Radius);
            float z1 = st.KeyLead - WorldKeyGap;
            int nz = Mathf.Min(CellsDeep(it), Mathf.FloorToInt((z1 - z0) / CellPitch));
            ctx.Blobs.Add(new List<Vector4>());
            ctx.Spheres.Add(Vector4.zero);
            if (nz < MinCellsDeep) return;

            Frame(dir, out Vector3 s1, out Vector3 s2);
            Vector3 gate = course[st.Gate].Position;
            var f = new Field
            {
                Station = s, OwnLeg = st.Gate - 1, N = CellsHalf(it), Nz = nz, Z0 = z0, Bore = BoreRadius(it),
                Origin = gate, S1 = s1, S2 = s2, Dir = dir, Rot = Quaternion.LookRotation(dir, s2),
                Salt = unchecked(Mathf.RoundToInt(gate.x) * 73856093 ^ Mathf.RoundToInt(gate.y) * 19349663 ^
                                 Mathf.RoundToInt(gate.z) * 83492791 ^ (s + 1) * 2654435),
            };
            f.Occ = new bool[f.Side * f.Side * nz];
            float half = f.N * CellPitch, depth = nz * CellPitch;
            Vector3 mid = f.W(new Vector3(0f, 0f, z0 + depth * 0.5f));
            float reach = Mathf.Sqrt(2f * half * half + depth * depth * 0.25f) + 30f;
            ctx.Spheres[s] = new Vector4(mid.x, mid.y, mid.z, reach);

            int stationCap = StationCap(it);
            int laid = 0;

            // 1. Movers (I2+), first, so the field fills round their sweeps.
            if (HasSliders(it))
            {
                int groups = 0, moving = 0;
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    int groupsLeft = groupShare - groups, movingLeft = movingShare - moving;
                    if (groupsLeft <= 0 || movingLeft < SliderPrisms) break;
                    float pick = Hash01(f.Salt, 11, attempt, 0);
                    BlastRunMotion motion; bool pair = false;
                    if (HasSliderPairs(it)) { motion = pick < 2f / 3f ? BlastRunMotion.Spin : BlastRunMotion.Slide; pair = motion == BlastRunMotion.Slide; }
                    else if (HasRotors(it)) motion = pick < 0.5f ? BlastRunMotion.Spin : BlastRunMotion.Slide;
                    else motion = BlastRunMotion.Slide;
                    if (pair && (groupsLeft < 2 || movingLeft < 2 * SliderPrisms)) { pair = false; }
                    if (motion == BlastRunMotion.Spin && movingLeft < RotorPrisms) continue;

                    int ix = -f.N + Mathf.FloorToInt(Hash01(f.Salt, 12, attempt, 0) * f.Side);
                    int iy = -f.N + Mathf.FloorToInt(Hash01(f.Salt, 13, attempt, 0) * f.Side);
                    int iz = Mathf.FloorToInt(Hash01(f.Salt, 14, attempt, 0) * nz);
                    bool alongX = Hash01(f.Salt, 15, attempt, 0) < 0.5f;
                    float phase = Hash01(f.Salt, 16, attempt, 0) * 2f * Mathf.PI;
                    int added = motion == BlastRunMotion.Spin
                        ? TryRotor(ctx, f, ix, iy, iz, phase, Hash01(f.Salt, 17, attempt, 0) < 0.5f ? 1f : -1f, stationCap - laid)
                        : TrySliders(ctx, f, ix, iy, iz, alongX, phase, pair ? 2 : 1, stationCap - laid);
                    if (added == 0) continue;
                    laid += added;
                    moving += added;
                    groups += motion == BlastRunMotion.Spin ? 1 : (pair ? 2 : 1);
                }
                ctx.MovingGroups += groups;
                ctx.MovingPrisms += moving;
            }

            // 2. Static objects: cells in hashed order, so a budget cut thins the field evenly instead of lopping
            // off its far end.
            int cells = f.Occ.Length;
            var order = new int[cells];
            var keys = new float[cells];
            for (int i = 0; i < cells; i++) { order[i] = i; keys[i] = Hash01(f.Salt, 21, i, 0); }
            System.Array.Sort(keys, order);
            int staticLaid = 0;
            float fill = Fill(it);
            int[] weights = ShapeWeights[I(it)];
            int weightSum = 0;
            for (int w = 0; w < ShapeCount; w++) weightSum += weights[w];
            for (int o = 0; o < cells; o++)
            {
                int left = Mathf.Min(stationCap - laid, staticShare - staticLaid);
                if (left <= 0) break;
                int idx = order[o];
                if (f.Occ[idx]) continue;
                if (Hash01(f.Salt, 22, idx, 0) >= fill) continue;
                int ix = idx % f.Side - f.N, iy = idx / f.Side % f.Side - f.N, iz = idx / (f.Side * f.Side);

                float pick = Hash01(f.Salt, 23, idx, 0) * weightSum;
                Shape shape = Shape.SmallCube;
                for (int w = 0, acc = 0; w < ShapeCount; w++)
                {
                    acc += weights[w];
                    if (pick < acc) { shape = (Shape)w; break; }
                }
                int added = TryStatic(ctx, f, shape, ix, iy, iz, idx, left, false);
                if (added == 0 && shape != Shape.SmallCube) added = TryStatic(ctx, f, Shape.SmallCube, ix, iy, iz, idx, left, false);
                if (added == 0) added = TryStatic(ctx, f, Shape.SmallCube, ix, iy, iz, idx, left, true);
                laid += added;
                staticLaid += added;
            }
            ctx.StaticPrisms += staticLaid;
        }

        /// <summary>A shape's cells (sx, sy, sz) and local boxes, anchored at the min corner <paramref name="m"/> of its region.</summary>
        static void BuildShape(Shape shape, Vector3 m, float h1, float h2, float h3, float dangerShare,
                               List<LBox> boxes, out int sx, out int sy, out int sz)
        {
            boxes.Clear();
            const float P = CellPitch, M = CellMargin;
            sx = sy = sz = 1;
            Vector3 c1 = m + new Vector3(P, P, P) * 0.5f;
            PrismKind thin = h2 < dangerShare ? PrismKind.Danger : PrismKind.Plain;
            switch (shape)
            {
                case Shape.SmallCube:
                {
                    float size = 8f + 6f * h1;
                    float j = P * 0.5f - M - size * 0.5f;
                    var c = c1 + new Vector3((h2 * 2f - 1f) * j, (h3 * 2f - 1f) * j, ((h1 * 7f) % 1f * 2f - 1f) * j);
                    boxes.Add(new LBox(c, new Vector3(size, size, size), thin));
                    break;
                }
                case Shape.Block:
                    boxes.Add(new LBox(c1, new Vector3(P - 2f * M, P - 2f * M, P - 2f * M), PrismKind.Plain));
                    break;
                case Shape.Pillar:
                {
                    int axis = Mathf.Min(2, Mathf.FloorToInt(h1 * 3f));
                    var size = new Vector3(6f, 6f, 6f);
                    size[axis] = P - 2f * M;
                    boxes.Add(new LBox(c1, size, thin));
                    break;
                }
                case Shape.Slab:
                {
                    int axis = Mathf.Min(2, Mathf.FloorToInt(h1 * 3f));
                    var size = new Vector3(P - 2f * M, P - 2f * M, P - 2f * M);
                    size[axis] = 4f;
                    boxes.Add(new LBox(c1, size, PrismKind.Plain));
                    break;
                }
                case Shape.SmallSphere:
                    for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    for (int k = -1; k <= 1; k++)
                        if (i * i + j * j + k * k <= 2)
                            boxes.Add(new LBox(c1 + new Vector3(i, j, k) * 7f, new Vector3(6.2f, 6.2f, 6.2f), PrismKind.Plain));
                    break;
                case Shape.BigCube:
                    sx = sy = sz = 2;
                    boxes.Add(new LBox(m + new Vector3(P, P, P), new Vector3(2f * P - 2f * M, 2f * P - 2f * M, 2f * P - 2f * M),
                                       PrismKind.Plain));
                    break;
                case Shape.BigSphere:
                    sx = sy = sz = 2;
                    for (int i = -2; i <= 2; i++)
                    for (int j = -2; j <= 2; j++)
                    for (int k = -2; k <= 2; k++)
                    {
                        int r2 = i * i + j * j + k * k;
                        if (r2 >= 2 && r2 <= 4)
                            boxes.Add(new LBox(m + new Vector3(P, P, P) + new Vector3(i, j, k) * 9.4f,
                                               new Vector3(8.4f, 8.4f, 8.4f), PrismKind.Plain));
                    }
                    break;
                case Shape.BigSlab:
                    sx = sy = 2;
                    boxes.Add(new LBox(m + new Vector3(P, P, P * 0.5f), new Vector3(2f * P - 2f * M, 2f * P - 2f * M, 4f),
                                       PrismKind.Plain));
                    break;
                case Shape.LongPillar:
                    sz = 3;
                    boxes.Add(new LBox(m + new Vector3(P * 0.5f, P * 0.5f, P * 1.5f), new Vector3(6f, 6f, 3f * P - 2f * M), thin));
                    break;
                case Shape.Beam:
                {
                    int len = h1 < 0.5f ? 3 : 4;
                    bool alongX = h3 < 0.5f;
                    if (alongX) sx = len; else sy = len;
                    var size = alongX ? new Vector3(len * P - 2f * M, 6f, 6f) : new Vector3(6f, len * P - 2f * M, 6f);
                    var c = m + new Vector3(sx * P, sy * P, P) * 0.5f;
                    boxes.Add(new LBox(c, size, PrismKind.Plain));
                    break;
                }
            }
        }

        static void Bounds(List<LBox> boxes, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < boxes.Count; i++)
            {
                min = Vector3.Min(min, boxes[i].C - boxes[i].Size * 0.5f);
                max = Vector3.Max(max, boxes[i].C + boxes[i].Size * 0.5f);
            }
        }

        /// <summary>Whether a local box stays outside the bore: its distance from the line, across the leg, is at
        /// least the bore's radius.</summary>
        public static bool OutsideBore(Vector3 min, Vector3 max, float bore)
        {
            float dx = Mathf.Max(0f, Mathf.Max(min.x, -max.x));
            float dy = Mathf.Max(0f, Mathf.Max(min.y, -max.y));
            return dx * dx + dy * dy >= bore * bore - 1e-3f;
        }

        /// <summary>Cover the local box [min, max] with spheres along its longest axis into ctx.Samples (world), and
        /// check each against the course, the keys and doors, and other stations' fields.</summary>
        static bool VolumeClear(FieldContext ctx, Field f, Vector3 min, Vector3 max)
        {
            ctx.Samples.Clear();
            Vector3 h = (max - min) * 0.5f, c = (max + min) * 0.5f;
            int axis = h.x >= h.y && h.x >= h.z ? 0 : (h.y >= h.z ? 1 : 2);
            float hl = h[axis];
            float hb = axis == 0 ? h.y : h.x, hc = axis == 2 ? h.y : h.z;
            float rc = Mathf.Sqrt(hb * hb + hc * hc);
            int n = Mathf.Max(1, Mathf.CeilToInt(2f * hl / Mathf.Max(2f * rc, 12f)));
            float step = 2f * hl / n;
            float r = Mathf.Sqrt(rc * rc + step * step * 0.25f);
            for (int i = 0; i < n; i++)
            {
                Vector3 l = c;
                l[axis] = c[axis] - hl + step * (i + 0.5f);
                Vector3 p = f.W(l);
                if (!PointClear(ctx.Course, ctx.Centre, ctx.ClearRadius, p, r, WorldLineClearance, f.OwnLeg, -1)) return false;
                if (Blocked(ctx.Keepouts, p, r)) return false;
                Vector4 me = ctx.Spheres[f.Station];
                for (int s = 0; s < ctx.Blobs.Count; s++)
                {
                    if (s == f.Station) continue;
                    Vector4 sp = ctx.Spheres[s];
                    if (sp.w <= 0f) continue;
                    if ((new Vector3(sp.x, sp.y, sp.z) - new Vector3(me.x, me.y, me.z)).magnitude > sp.w + me.w) continue;
                    var blobs = ctx.Blobs[s];
                    for (int b = 0; b < blobs.Count; b++)
                    {
                        float rr = blobs[b].w + r + 2f;
                        if ((new Vector3(blobs[b].x, blobs[b].y, blobs[b].z) - p).sqrMagnitude < rr * rr) return false;
                    }
                }
                ctx.Samples.Add(new Vector4(p.x, p.y, p.z, r));
            }
            return true;
        }

        static bool Admit(FieldContext ctx, Field f, Vector3 min, Vector3 max, int k)
        {
            if (!OutsideBore(min, max, f.Bore)) return false;
            if (!VolumeClear(ctx, f, min, max)) return false;
            return ctx.Density.CanAdd(f.W((min + max) * 0.5f), (max - min).magnitude * 0.5f, k);
        }

        static void Commit(FieldContext ctx, Field f)
        {
            ctx.Blobs[f.Station].AddRange(ctx.Samples);
        }

        static void Lay(FieldContext ctx, Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind)
        {
            ctx.Layout.Prisms.Add(new SwitchbackFeaturePrism(position, rotation, scale, kind));
            ctx.Density.Seed(position);
        }

        static int TryStatic(FieldContext ctx, Field f, Shape shape, int ix, int iy, int iz, int idx, int left, bool corner)
        {
            float h1 = Hash01(f.Salt, 24, idx, 0), h2 = Hash01(f.Salt, 25, idx, 0), h3 = Hash01(f.Salt, 26, idx, 0);
            Vector3 m = f.CellMin(ix, iy, iz);
            var boxes = ctx.Boxes;
            int sx, sy, sz;
            if (corner)
            {
                // The fallback for a cell the bore grazes: a 10 u cube in the cell's corner farthest from the line.
                sx = sy = sz = 1;
                boxes.Clear();
                const float half = 5f;
                float cx = m.x + CellPitch * 0.5f >= 0f ? m.x + CellPitch - CellMargin - half : m.x + CellMargin + half;
                float cy = m.y + CellPitch * 0.5f >= 0f ? m.y + CellPitch - CellMargin - half : m.y + CellMargin + half;
                boxes.Add(new LBox(new Vector3(cx, cy, m.z + CellPitch * 0.5f), new Vector3(10f, 10f, 10f), PrismKind.Plain));
            }
            else BuildShape(shape, m, h1, h2, h3, DangerShare(ctx.Intensity), boxes, out sx, out sy, out sz);
            if (boxes.Count > left) return 0;
            if (!f.Free(ix, iy, iz, sx, sy, sz)) return 0;
            Bounds(boxes, out Vector3 min, out Vector3 max);
            if (!Admit(ctx, f, min, max, boxes.Count)) return 0;
            f.Claim(ix, iy, iz, sx, sy, sz);
            Commit(ctx, f);
            for (int i = 0; i < boxes.Count; i++) Lay(ctx, f.W(boxes[i].C), f.Rot, boxes[i].Size, boxes[i].Kind);
            return boxes.Count;
        }

        /// <summary>One slider (or, for <paramref name="tracks"/> = 2, an opposed pair in adjacent tracks): a 2 x 2 of
        /// cubes running a <see cref="SliderTrackCells"/>-cell track across the leg.</summary>
        static int TrySliders(FieldContext ctx, Field f, int ix, int iy, int iz, bool alongX, float phase, int tracks, int left)
        {
            int k = tracks * SliderPrisms;
            if (k > left) return 0;
            int sx = alongX ? SliderTrackCells : 1, sy = alongX ? tracks : SliderTrackCells;
            if (!alongX) sx = tracks;
            if (!f.Free(ix, iy, iz, sx, sy, 1)) return 0;
            Vector3 m = f.CellMin(ix, iy, iz);
            const float P = CellPitch, hc = SliderCube * 0.5f;
            // The whole region's sweep (the cubes reach SliderAmplitude + hc either side of a track's centre).
            Vector3 min = m + new Vector3(CellMargin, CellMargin, CellMargin);
            Vector3 max = m + new Vector3(sx * P, sy * P, P) - new Vector3(CellMargin, CellMargin, CellMargin);
            if (!Admit(ctx, f, min, max, k)) return 0;
            f.Claim(ix, iy, iz, sx, sy, 1);
            Commit(ctx, f);
            Vector3 axisL = alongX ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 1f, 0f);
            Vector3 acrossL = alongX ? new Vector3(0f, 1f, 0f) : new Vector3(1f, 0f, 0f);
            Vector3 axisW = alongX ? f.S1 : f.S2;
            float omega = 2f * Mathf.PI / SlidePeriod(ctx.Intensity);
            for (int t = 0; t < tracks; t++)
            {
                float ph = phase + t * Mathf.PI;
                Vector3 centre = m + axisL * (SliderTrackCells * P * 0.5f) + acrossL * ((t + 0.5f) * P) + new Vector3(0f, 0f, P * 0.5f);
                Vector3 laid = centre + axisL * (SliderAmplitude * Mathf.Sin(ph));
                int first = ctx.Layout.Prisms.Count;
                for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                    Lay(ctx, f.W(laid + acrossL * (a * 5.5f) + new Vector3(0f, 0f, b * 5.5f)), f.Rot,
                        new Vector3(SliderCube, SliderCube, SliderCube), PrismKind.Plain);
                ctx.Layout.Movers.Add(new BlastRunMover(f.Station, first, SliderPrisms, BlastRunMotion.Slide, f.W(centre),
                                                        axisW, SliderAmplitude, omega, ph));
            }
            return k;
        }

        /// <summary>A rotor: a hub and four danger arms spinning about the leg's direction in a 2 x 2-cell disc.</summary>
        static int TryRotor(FieldContext ctx, Field f, int ix, int iy, int iz, float phase, float sign, int left)
        {
            if (RotorPrisms > left) return 0;
            if (!f.Free(ix, iy, iz, 2, 2, 1)) return 0;
            Vector3 m = f.CellMin(ix, iy, iz);
            const float P = CellPitch;
            Vector3 pivotL = m + new Vector3(P, P, P * 0.5f);
            float sweep = RotorSweepRadius;
            Vector3 min = pivotL - new Vector3(sweep, sweep, RotorArmScale.z * 0.5f);
            Vector3 max = pivotL + new Vector3(sweep, sweep, RotorArmScale.z * 0.5f);
            if (!Admit(ctx, f, min, max, RotorPrisms)) return 0;
            f.Claim(ix, iy, iz, 2, 2, 1);
            Commit(ctx, f);
            Vector3 pivot = f.W(pivotL);
            int first = ctx.Layout.Prisms.Count;
            Lay(ctx, pivot, f.Rot, new Vector3(RotorHub, RotorHub, RotorHub), PrismKind.Plain);
            for (int a = 0; a < 4; a++)
            {
                float th = phase + a * Mathf.PI * 0.5f;
                Quaternion spin = Quaternion.AngleAxis(th * Mathf.Rad2Deg, f.Dir);
                Lay(ctx, pivot + spin * (f.S1 * RotorArmCentre), spin * f.Rot, RotorArmScale, PrismKind.Danger);
            }
            float omega = sign * SpinDegreesPerSecond(ctx.Intensity) * Mathf.Deg2Rad;
            ctx.Layout.Movers.Add(new BlastRunMover(f.Station, first, RotorPrisms, BlastRunMotion.Spin, pivot, f.Dir,
                                                    0f, omega, phase));
            return RotorPrisms;
        }

        /// <summary>The farthest a rotor arm's corner reaches from its axis.</summary>
        public static float RotorSweepRadius =>
            Mathf.Sqrt((RotorArmCentre + RotorArmScale.x * 0.5f) * (RotorArmCentre + RotorArmScale.x * 0.5f) +
                       RotorArmScale.y * RotorArmScale.y * 0.25f);

        /// <summary>The leg frame: <paramref name="s1"/> = <see cref="RaceCourseGeometry.Perpendicular"/>, s2 = dir x s1.</summary>
        public static void Frame(Vector3 dir, out Vector3 s1, out Vector3 s2)
        {
            s1 = RaceCourseGeometry.Perpendicular(dir);
            s2 = Vector3.Cross(dir, s1);
        }

        static bool Blocked(List<Keepout> keepouts, Vector3 p, float h)
        {
            for (int i = 0; i < keepouts.Count; i++)
                if (SwitchbackFeatures.DistanceToSegment(p, keepouts[i].A, keepouts[i].B) < keepouts[i].Radius + h)
                    return true;
            return false;
        }

        /// <summary>Out of the nucleus, outside every ring's mouth, off every leg's line but <paramref name="ownLeg"/>
        /// and <paramref name="ownLeg2"/>.</summary>
        static bool PointClear(IReadOnlyList<RaceGate> course, Vector3 centre, float clearRadius, Vector3 p, float h,
                               float lineClearance, int ownLeg, int ownLeg2)
        {
            float cr = clearRadius + h;
            if (clearRadius > 0f && (p - centre).sqrMagnitude < cr * cr) return false;
            for (int g = 0; g < course.Count; g++)
                if ((p - course[g].Position).magnitude < course[g].Radius + SwitchbackFeatures.MouthClearance + h) return false;
            for (int k = 0; k + 1 < course.Count; k++)
                if (k != ownLeg && k != ownLeg2 &&
                    SwitchbackFeatures.DistanceToSegment(p, course[k].Position, course[k + 1].Position) < lineClearance + h)
                    return false;
            return true;
        }

        /// <summary>A door (or jaw) disc of <paramref name="radius"/> at <paramref name="p"/> for gate <paramref name="g"/>:
        /// out of the nucleus, clear of every other ring, and off every leg but the two through its own gate.</summary>
        static bool DoorClear(IReadOnlyList<RaceGate> course, Vector3 centre, float clearRadius, Vector3 p, float radius, int g)
        {
            float cr = clearRadius + radius;
            if (clearRadius > 0f && (p - centre).sqrMagnitude < cr * cr) return false;
            for (int o = 0; o < course.Count; o++)
                if (o != g && (p - course[o].Position).magnitude < course[o].Radius + SwitchbackFeatures.MouthClearance + radius)
                    return false;
            for (int k = 0; k + 1 < course.Count; k++)
                if (k != g - 1 && k != g &&
                    SwitchbackFeatures.DistanceToSegment(p, course[k].Position, course[k + 1].Position) < radius + DoorLineClearance)
                    return false;
            return true;
        }

        /// <summary>A stable hash in [0, 1) of four integers - the same in C# and in the Python mirror.</summary>
        public static float Hash01(int a, int b, int c, int d)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h = (h ^ (uint)d) * 16777619u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }
    }
}
