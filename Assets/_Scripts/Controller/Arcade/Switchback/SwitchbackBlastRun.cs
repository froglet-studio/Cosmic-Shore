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
        Fortress = 4,   // I4: the GAUNTLET - a zig-zag of keys and plugged tunnels to sealed doors, in the densest field
    }

    /// <summary>How a <see cref="BlastRunMover"/> moves: back and forth along its axis, or round it.</summary>
    public enum BlastRunMotion : byte
    {
        Slide = 1,
        Spin = 2,
    }

    /// <summary>The tunnel an I4 gauntlet stage lays round its segment (key to the next key, or to the door), each
    /// one plugged inside by walls of cubes the key's blast clears (SWITCHBACK.md "The I4 gauntlet").</summary>
    public enum BlastRunTunnel : byte
    {
        None = 0,
        Pipe = 1,       // an octagonal tube of wall tiles, every other section turned half a facet
        Hoops = 2,      // a row of twelve-tile rings across the segment, open between them
        Helix = 3,      // blocks wound round the segment like a corkscrew
        Cage = 4,       // six rails along the segment
        Duct = 5,       // a square tube of slabs
    }

    /// <summary>
    /// One KEY of a BLOCKED gate (SWITCHBACK.md "Blast Run"): the gate's ring is sealed by a Breakwater door (keystone
    /// collar plus plug, <see cref="BreakwaterStationBuilder.BuildDoor"/>) with only the
    /// <see cref="BreakwaterCourseSettings.EyeRadius"/> eye open (at I4 the eye is sealed too), and its inbound leg is
    /// filled with a <see cref="World"/> block field. The <see cref="Key"/> crystal sits <see cref="KeyLead"/> before the
    /// gate: collecting it fires the Dolphin's forward crystal blast down the nose, at <see cref="Target"/>.
    ///
    /// <para>Below I4 a gate has ONE station: its key on the leg's line, aimed at the door. An I4 gate is a GAUNTLET of
    /// <see cref="Stages"/> stations in a row, upstream first (<see cref="Stage"/> 0): each key sits off the line in a
    /// zig-zag and is aimed at the NEXT key (the last at the door), and its segment holds a plugged <see cref="Tunnel"/>.
    /// Every station of a gate shares its <see cref="Gate"/>, <see cref="Door"/>, <see cref="Port"/> and
    /// <see cref="DoubleDoor"/>; only the last (<see cref="Final"/>) owns the doors' prisms and the gate's block field.</para>
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
        public readonly int WorldPrisms;    // the station's own prisms after the doors: its tunnel, then (the last) the field
        public readonly Vector3 Target;     // where this key's blast is aimed: the door, or (a gauntlet's earlier key) the next key
        public readonly int Stage;          // 0 = the gate's first key, upstream
        public readonly int Stages;         // keys on this gate's leg (1 below I4)
        public readonly BlastRunTunnel Tunnel;
        public readonly int TunnelPrisms;   // the first TunnelPrisms of the WorldPrisms are the tunnel's walls and plugs

        /// <summary>The last key of its gate: it owns the doors and the gate's field.</summary>
        public bool Final => Stage == Stages - 1;

        public BlastRunStation(int gate, Vector3 key, float keyLead, Vector3 door, Vector3 flowAxis, float port,
                               bool doubleDoor, BlastRunWorld world, int worldPrisms)
            : this(gate, key, keyLead, door, flowAxis, port, doubleDoor, world, worldPrisms, door, 0, 1, BlastRunTunnel.None, 0) { }

        public BlastRunStation(int gate, Vector3 key, float keyLead, Vector3 door, Vector3 flowAxis, float port,
                               bool doubleDoor, BlastRunWorld world, int worldPrisms, Vector3 target, int stage, int stages,
                               BlastRunTunnel tunnel, int tunnelPrisms)
        {
            Gate = gate; Key = key; KeyLead = keyLead; Door = door; FlowAxis = flowAxis; Port = port;
            DoubleDoor = doubleDoor; World = world; WorldPrisms = worldPrisms;
            Target = target; Stage = stage; Stages = Mathf.Max(stages, 1); Tunnel = tunnel; TunnelPrisms = tunnelPrisms;
        }

        /// <summary>The same station with its laid prism counts.</summary>
        public BlastRunStation WithCounts(int worldPrisms, int tunnelPrisms) =>
            new(Gate, Key, KeyLead, Door, FlowAxis, Port, DoubleDoor, World, worldPrisms, Target, Stage, Stages, Tunnel, tunnelPrisms);
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
    ///
    /// <para><b>The I4 gauntlet</b> (Yash, 2026-11-03: "I4 is too empty ... they cannot just simply fly into the rings,
    /// they have to use both drifting and blasting multiple times to pass the rings"). There is no bore: the field fills
    /// the leg's line, and both doors' eyes are SEALED (<see cref="EmitSeal"/>: nine plain cubes; the Danger heart they had until 2026-11-13
    /// made every missed blast two dead stops a gate, Docs/ElementalLoop/DOLPHIN_TIME_LADDER.md). The way through is a
    /// zig-zag of <see cref="GauntletStages"/> keys (<see cref="GauntletKeys"/>), each off the line and aimed at the next,
    /// the last on the line aimed at the door; every segment between them is a plugged tunnel (<see cref="BlastRunTunnel"/>).
    /// Each key's segment turns from the one before it, so a blast down the cleared lane needs the nose swung onto the
    /// next segment as the key is taken - a drift - at every key. The field keeps off the route (<see cref="RouteClearance"/>),
    /// and every route segment is lit by a ray (<see cref="RaySegments"/>, drawn by <c>SwitchbackPathRays</c>).</para>
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
        /// <summary>The row of the per-intensity tables: I1-I4, and a fifth for I4 WITHOUT the gauntlet - the Fortress as it
        /// was before 2026-11-03, which a mode reusing Switchback's course keeps (Scarab Rally,
        /// <c>SwitchbackController.BlastRunGauntlet</c>). Every table's one-argument form is the gauntlet's.</summary>
        static int R(int intensity, bool gauntlet) => intensity >= 4 && !gauntlet ? 4 : I(intensity);
        /// <summary>Cells either side of the line (the field is 2n x 2n cells across).</summary>
        public static int CellsHalf(int intensity) => CellsHalf(intensity, true);
        public static int CellsHalf(int intensity, bool gauntlet) => new[] { 3, 3, 4, 5, 4 }[R(intensity, gauntlet)];
        /// <summary>Slices along the leg, at most (the leg between the mouth gap and the key gap may hold fewer).</summary>
        public static int CellsDeep(int intensity) => CellsDeep(intensity, true);
        public static int CellsDeep(int intensity, bool gauntlet) => new[] { 5, 6, 7, 12, 8 }[R(intensity, gauntlet)];
        /// <summary>The open bore round the line: no object's box, and no mover's sweep, comes inside it. I4 has none:
        /// its way through is the gauntlet (<see cref="HasGauntlet"/>), and the field fills the line.</summary>
        public static float BoreRadius(int intensity) => BoreRadius(intensity, true);
        public static float BoreRadius(int intensity, bool gauntlet) => new[] { 30f, 26f, 22f, 0f, 18f }[R(intensity, gauntlet)];
        /// <summary>u past the bigger of a leg's two mouths that the sight line keeps clear (<see cref="SightBore"/>): half a
        /// cell, so the next ring's whole rim, and a little water round it, shows down the leg.</summary>
        public const float SightMargin = 12f;
        /// <summary>The bore a leg's field actually keeps clear. Every row keeps <see cref="BoreRadius(int, bool)"/>; I4
        /// WITHOUT the gauntlet (Scarab Rally) also keeps the SIGHT LINE from the ring you leave to the next one: the bigger
        /// of the two mouths plus <see cref="SightMargin"/>, so no field prism hides the next ring's lime rim from where
        /// you leave a ring (Garrett, 2026-11-14: "respect sight lines from one ring to another"). Its 18 u bore was
        /// narrower than its 22 u mouths, so the field covered the rim on every blocked leg.</summary>
        public static float SightBore(int intensity, bool gauntlet, float mouthRadius, float previousMouthRadius)
        {
            float bore = BoreRadius(intensity, gauntlet);
            if (R(intensity, gauntlet) != 4) return bore;
            return Mathf.Max(bore, Mathf.Max(mouthRadius, previousMouthRadius) + SightMargin);
        }
        /// <summary>The chance a free cell starts an object (bigger objects then claim more cells).</summary>
        public static float Fill(int intensity) => Fill(intensity, true);
        public static float Fill(int intensity, bool gauntlet) => new[] { 0.30f, 0.40f, 0.50f, 0.70f, 0.60f }[R(intensity, gauntlet)];
        /// <summary>The share of small cubes and pillars laid as danger prisms.</summary>
        public static float DangerShare(int intensity) => new[] { 0.06f, 0.08f, 0.10f, 0.12f }[I(intensity)];

        // ── the I4 gauntlet (SWITCHBACK.md "The I4 gauntlet") ──────────────
        /// <summary>I4: keys in a zig-zag, plugged tunnels between them, sealed eyes, no bore.</summary>
        public static bool HasGauntlet(int intensity) => intensity >= 4;
        /// <summary>The gauntlet where the caller asked for it (<paramref name="gauntlet"/>), at I4.</summary>
        public static bool HasGauntlet(int intensity, bool gauntlet) => gauntlet && intensity >= 4;
        /// <summary>u before the gate: a gauntlet's last key, on the leg's line, aimed at the door.</summary>
        public const float GauntletFinalLead = 120f;
        /// <summary>u: an earlier key's step off the line (alternating sides; the second-to-last key steps half as far).</summary>
        public const float GauntletZigZag = 40f;
        /// <summary>A first key at least this far out makes three keys; nearer, two.</summary>
        public const float GauntletThreeKeyLead = 300f;
        /// <summary>u from a tunnel's axis to the inner faces of its walls: the hull's flight tube.</summary>
        public const float TunnelRadius = 15f;
        public const float TunnelWall = 3f;
        /// <summary>u along the segment a tunnel's walls start past their key, and stop short of the next.</summary>
        public const float TunnelKeyGap = 22f;
        public const float TunnelSection = 18f;         // a wall section's length along the segment...
        public const float TunnelSectionGap = 2f;       // ...and the gap to the next
        /// <summary>A plug wall across the tunnel: a plus of five cubes (the middle one a Danger heart on every
        /// <see cref="PlugDangerEvery"/>th wall), every other wall turned 45 deg so no straight line threads two.</summary>
        public const float PlugCube = 6.5f;
        public const float PlugPitch = 7.5f;
        public const float PlugStart = 34f;             // u past the key to the first plug wall
        public const float PlugSpacing = 22f;
        public const int PlugDangerEvery = 2;
        /// <summary>u a tunnel piece keeps off its own gate's keys (the generic key clearance is 30).</summary>
        public const float GauntletKeyClearance = 14f;
        /// <summary>u the gate field (and every other gate's tunnel) keeps off a gauntlet segment's axis.</summary>
        public const float RouteClearance = TunnelRadius + TunnelWall + 6f;
        /// <summary>The eye seal: a 3 x 3 of cubes in each door's eye, its middle a Danger heart (corner reach 16.3 u,
        /// inside the 18 u eye every door bar keeps out of).</summary>
        public const float SealCube = 7f;
        public const float SealPitch = 8f;
        public const int SealPrisms = 9;

        /// <summary>Keys on a gauntlet leg whose first key is <paramref name="lead"/> before the gate.</summary>
        public static int GauntletStages(float lead) => lead >= GauntletThreeKeyLead ? 3 : 2;

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
        /// <summary>Whether a blocked gate hangs its door(s). Every row does except I4 WITHOUT the gauntlet (Scarab Rally): a
        /// door is a plug across the port with an 18 u eye, so it hid the 22 u lime ring behind it from down the leg, and its
        /// eye only opens to the Dolphin's blast (Garrett, 2026-11-14: keep the sight line from one ring to the next). The
        /// door's keepout stays, so the field still leaves the mouth clear.</summary>
        public static bool LaysDoors(int intensity, bool gauntlet) => R(intensity, gauntlet) != 4;

        // ── budgets (the 100 fps target; SWITCHBACK.md "Blast Run - budgets") ──
        /// <summary>Whole-course prism ceilings, doors and moving prisms included (Breakwater lays about 4600 at I1). I4
        /// is 1.5x its 3400 of before the gauntlet (Yash, 2026-11-03: "increase object counts by 50 percent"); the
        /// headless I4 soak measured +1,500 static prisms within noise (Docs/perf/SWITCHBACK_PRISM_BUDGET.md).</summary>
        public static int TotalCap(int intensity) => TotalCap(intensity, true);
        public static int TotalCap(int intensity, bool gauntlet) => new[] { 1800, 2400, 3000, 5100, 3400 }[R(intensity, gauntlet)];
        /// <summary>One station's own prisms (its tunnel; the last of a gate also its field), moving prisms included.</summary>
        public static int StationCap(int intensity) => StationCap(intensity, true);
        public static int StationCap(int intensity, bool gauntlet) => new[] { 150, 170, 190, 315, 210 }[R(intensity, gauntlet)];
        /// <summary>At most this many laid prisms within <see cref="ColliderRadius"/> of any laid prism - the colliders
        /// <c>PrismColliderLodManager</c> keeps live round a pilot there. Breakwater's measured worst is 568 / 464 / 322 /
        /// 276 (I1-I4) of a 1,500 band; I1-I3 stay at or under 70 % of its I1 worst. I4 is 1.5x its 400 of before the
        /// gauntlet, about Breakwater I1's shipped worst: the one budget the real-game soak has to confirm.</summary>
        public static int ColliderCap(int intensity) => ColliderCap(intensity, true);
        public static int ColliderCap(int intensity, bool gauntlet) => new[] { 300, 340, 370, 600, 400 }[R(intensity, gauntlet)];
        public const float ColliderRadius = 200f;       // PrismColliderLodManager.lodRadiusMeters
        /// <summary>Prisms written every frame (pose + <c>Prism.NotifyPositionChanged</c>) across the whole course.</summary>
        public static int MovingPrismCap(int intensity) => MovingPrismCap(intensity, true);
        public static int MovingPrismCap(int intensity, bool gauntlet) => new[] { 0, 40, 100, 210, 140 }[R(intensity, gauntlet)];
        public static int MovingGroupCap(int intensity) => MovingGroupCap(intensity, true);
        public static int MovingGroupCap(int intensity, bool gauntlet) => new[] { 0, 10, 20, 42, 28 }[R(intensity, gauntlet)];
        /// <summary>SpawnableSwitchbackFeatures' lay pool: over every TotalCap.</summary>
        public const int LayCapacity = 6144;

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
            public int Gate;        // the gauntlet gate a key or route belongs to; -1 = none (doors, keys below I4)
            public Keepout(Vector3 a, Vector3 b, float r, int gate = -1) { A = a; B = b; Radius = r; Gate = gate; }
        }

        /// <summary>Where the keys go, in station order - <see cref="Layout"/>'s <c>Stations[i].Key</c>.</summary>
        public static List<Vector3> KeyPositions(IReadOnlyList<RaceGate> course, int intensity,
                                                 Vector3 centre = default, float clearRadius = 0f, bool gauntlet = true)
        {
            var keys = new List<Vector3>();
            var layout = Layout(course, intensity, centre, clearRadius, gauntlet);
            for (int i = 0; i < layout.Stations.Count; i++) keys.Add(layout.Stations[i].Key);
            return keys;
        }

        /// <summary>Blast Run's layout of <paramref name="course"/>: stations (keys, doors), then each station's tunnel (I4)
        /// and block field (movers first, then the static objects) - each filtered by everything before it.
        /// <paramref name="gauntlet"/> false keeps I4 as it was before the gauntlet (a mode that reuses Switchback's course
        /// without the Dolphin's key blast: Scarab Rally); it changes nothing at I1-I3.</summary>
        public static SwitchbackFeatureLayout Layout(IReadOnlyList<RaceGate> course, int intensity,
                                                     Vector3 centre = default, float clearRadius = 0f, bool gauntlet = true)
        {
            var layout = new SwitchbackFeatureLayout();
            if (course == null || course.Count < 3) return layout;
            intensity = Mathf.Clamp(intensity, 1, 4);
            float port = PortFor(intensity);
            var keepouts = new List<Keepout>(64);
            var routes = new List<Keepout>(64);
            bool profile = gauntlet;
            gauntlet = HasGauntlet(intensity, profile);
            var gkeys = new List<Vector3>(3);

            // 1. Stations: key(s), door(s).
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

                if (LaysDoors(intensity, profile))
                {
                    EmitDoor(layout, door, axis, port, gauntlet);
                    if (dbl) EmitDoor(layout, door2, axis, port, gauntlet);
                }
                keepouts.Add(new Keepout(door, door, port + 6f));
                if (dbl) keepouts.Add(new Keepout(door2, door2, port + 6f));
                if (!gauntlet)
                {
                    keepouts.Add(new Keepout(key, key, WorldKeyClearance));
                    layout.Stations.Add(new BlastRunStation(g, key, lead, door, axis, port, dbl, WorldFor(intensity), 0));
                    dirs.Add(dir);
                    continue;
                }

                // The gauntlet: a zig-zag of keys, each aimed at the next, the last at the door.
                int n = GauntletKeys(course, centre, clearRadius, g, dir, lead, gkeys);
                if (n == 0) { gkeys.Add(key); n = 1; }
                int salt = GateSalt(b, g);
                for (int j = 0; j < n; j++)
                {
                    Vector3 kj = gkeys[j];
                    Vector3 target = j + 1 < n ? gkeys[j + 1] : door;
                    keepouts.Add(new Keepout(kj, kj, WorldKeyClearance, g));
                    routes.Add(new Keepout(kj, target, RouteClearance, g));
                    var tunnel = (BlastRunTunnel)(1 + Mathf.Min(4, Mathf.FloorToInt(Hash01(salt, 43, j, 0) * 5f)));
                    layout.Stations.Add(new BlastRunStation(g, kj, Vector3.Dot(b - kj, dir), door, axis, port, dbl,
                                                            WorldFor(intensity), 0, target, j, n, tunnel, 0));
                    dirs.Add(dir);
                }
            }

            // 2. Tunnels and fields, station by station, under the budgets.
            var density = new Density(ColliderRadius, ColliderCap(intensity, profile));
            for (int i = 0; i < layout.Prisms.Count; i++) density.Seed(layout.Prisms[i].Position);
            var ctx = new FieldContext
            {
                Course = course, Centre = centre, ClearRadius = clearRadius, Intensity = intensity, Profile = profile,
                Layout = layout, Keepouts = keepouts, Routes = routes, Density = density,
            };
            int stations = layout.Stations.Count;
            int doorPrisms = layout.Prisms.Count;
            int staticBudget = Mathf.Max(0, TotalCap(intensity, profile) - doorPrisms - MovingPrismCap(intensity, profile));
            for (int s = 0; s < stations; s++)
            {
                var st = layout.Stations[s];
                int before = layout.Prisms.Count;
                ctx.Blobs.Add(new List<Vector4>());
                ctx.Spheres.Add(Vector4.zero);
                // Even shares of the course budgets, carried forward: a station that could not use its share leaves
                // it to the next.
                int groupShare = MovingGroupCap(intensity, profile) * (s + 1) / stations - ctx.MovingGroups;
                int movingShare = MovingPrismCap(intensity, profile) * (s + 1) / stations - ctx.MovingPrisms;
                int staticShare = staticBudget * (s + 1) / stations - ctx.StaticPrisms;
                int tunnel = st.Tunnel != BlastRunTunnel.None
                    ? EmitTunnel(ctx, s, st, Mathf.Min(StationCap(intensity, profile), staticShare))
                    : 0;
                if (st.Final)
                {
                    // The gate's block field: from the mouth gap to short of its FIRST key, round every route segment.
                    float firstLead = layout.Stations[s - st.Stage].KeyLead;
                    EmitField(ctx, s, st, dirs[s], groupShare, movingShare, staticShare - tunnel, tunnel, firstLead);
                }
                layout.Stations[s] = st.WithCounts(layout.Prisms.Count - before, tunnel);
            }
            return layout;
        }

        static int GateSalt(Vector3 gate, int s) =>
            unchecked(Mathf.RoundToInt(gate.x) * 73856093 ^ Mathf.RoundToInt(gate.y) * 19349663 ^
                      Mathf.RoundToInt(gate.z) * 83492791 ^ (s + 1) * 2654435);

        /// <summary>The lateral step of key <paramref name="j"/> of <paramref name="n"/> off the leg's line, in units of
        /// <see cref="GauntletZigZag"/> along the gate's chosen side: two keys +1.5 then on the line; three +1, -1, then
        /// on the line. Every segment so turns from the one before it, which is what makes each blast want a drift.</summary>
        public static float GauntletOffset(int j, int n)
        {
            if (j >= n - 1) return 0f;
            if (n == 2) return 1.5f;
            return j % 2 == 0 ? 1f : -1f;
        }

        /// <summary>
        /// A gauntlet's keys for gate <paramref name="g"/> (leg direction <paramref name="dir"/>, first key
        /// <paramref name="lead"/> before the gate), upstream first, into <paramref name="keys"/>: the last on the line
        /// <see cref="GauntletFinalLead"/> out, the others stepped off it (<see cref="GauntletOffset"/>) to a hashed side,
        /// turned a quarter at a time until every key keeps <see cref="KeyClearance"/> off every other leg and out of every
        /// mouth. 0 when no side works (the caller falls back to one key on the line).
        /// </summary>
        public static int GauntletKeys(IReadOnlyList<RaceGate> course, Vector3 centre, float clearRadius, int g, Vector3 dir,
                                       float lead, List<Vector3> keys)
        {
            keys.Clear();
            Vector3 b = course[g].Position;
            int n = GauntletStages(lead);
            Frame(dir, out Vector3 s1, out Vector3 s2);
            float phi = Hash01(GateSalt(b, g), 41, g, 0) * 2f * Mathf.PI;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float ang = phi + attempt * 0.5f * Mathf.PI;
                Vector3 side = s1 * Mathf.Cos(ang) + s2 * Mathf.Sin(ang);
                keys.Clear();
                bool ok = true;
                for (int j = 0; j < n && ok; j++)
                {
                    float z = Mathf.Lerp(lead, GauntletFinalLead, j / (float)(n - 1));
                    Vector3 k = b - dir * z + side * (GauntletOffset(j, n) * GauntletZigZag);
                    ok = PointClear(course, centre, clearRadius, k, 0f, KeyClearance, g - 1, -1);
                    keys.Add(k);
                }
                if (ok) return n;
            }
            keys.Clear();
            return 0;
        }

        /// <summary>
        /// The rays that light an I4 gauntlet's way (<c>SwitchbackPathRays</c> draws them), as segment pairs into
        /// <paramref name="into"/>: a lead-in down the leg to each gate's first key, every key to what its blast is aimed
        /// at (the next key, or the door), and the door through the ring. Empty below I4.
        /// </summary>
        public static void RaySegments(SwitchbackFeatureLayout layout, IReadOnlyList<RaceGate> course, List<Vector3> into)
        {
            into.Clear();
            if (layout == null || course == null) return;
            for (int s = 0; s < layout.Stations.Count; s++)
            {
                var st = layout.Stations[s];
                if (st.Tunnel == BlastRunTunnel.None || st.Gate < 1 || st.Gate >= course.Count) continue;
                if (st.Stage == 0)
                {
                    var prev = course[st.Gate - 1];
                    Vector3 legDir = (course[st.Gate].Position - prev.Position).normalized;
                    float room = Vector3.Distance(st.Key, prev.Position) - prev.Radius - SwitchbackFeatures.MouthClearance;
                    float leadIn = Mathf.Min(RayLeadIn, room);
                    if (leadIn > 1f) { into.Add(st.Key - legDir * leadIn); into.Add(st.Key); }
                }
                into.Add(st.Key);
                into.Add(st.Target);
                if (st.Final)
                {
                    into.Add(st.Door);
                    into.Add(course[st.Gate].Position + st.FlowAxis * (DoorSetback + RayExit));
                }
            }
        }

        /// <summary>u of ray down the leg before a gauntlet's first key, and past the second door.</summary>
        public const float RayLeadIn = 90f;
        public const float RayExit = 20f;

        static bool Crowded(List<BlastRunStation> stations, Vector3 door)
        {
            for (int i = 0; i < stations.Count; i++)
                if ((stations[i].Door - door).sqrMagnitude < StationSpacing * StationSpacing) return true;
            return false;
        }

        static void EmitDoor(SwitchbackFeatureLayout layout, Vector3 position, Vector3 axis, float port, bool sealed_)
        {
            EmitDoorPrisms(position, axis, port, sealed_,
                (p, r, sc, kind) => layout.Prisms.Add(new SwitchbackFeaturePrism(p, r, sc, kind)));
        }

        /// <summary>One door: Breakwater's door, and with <paramref name="sealed_"/> (the I4 gauntlet) its eye's seal.</summary>
        public static void EmitDoorPrisms(Vector3 position, Vector3 axis, float port, bool sealed_,
                                          System.Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            BreakwaterStationBuilder.BuildDoor(new BreakwaterStation(position, axis, port), emit);
            if (sealed_) EmitSeal(position, axis, emit);
        }

        /// <summary>The prisms one door lays at <paramref name="intensity"/> (seal included where the gauntlet is).</summary>
        public static int DoorPrisms(int intensity, bool gauntlet = true)
        {
            if (!LaysDoors(intensity, gauntlet)) return 0;
            int n = 0;
            EmitDoorPrisms(Vector3.zero, Vector3.forward, PortFor(intensity), HasGauntlet(intensity, gauntlet), (p, r, s, k) => n++);
            return n;
        }

        /// <summary>
        /// The I4 eye SEAL: a 3 x 3 of <see cref="SealCube"/> plain cubes filling the door's 18 u eye, square to the door
        /// (<see cref="DoorBasis"/>). Nothing threads a sealed eye: a hull rams it (a plain hit's slow per cube) or a key's
        /// blast clears it. Its middle was a Danger heart until 2026-11-13 (elemental loop step 2: two dead stops a gate for
        /// every missed blast were most of I4's cliff over I3, Docs/ElementalLoop/DOLPHIN_TIME_LADDER.md).
        /// </summary>
        public static void EmitSeal(Vector3 position, Vector3 axis, System.Action<Vector3, Quaternion, Vector3, PrismKind> emit)
        {
            Vector3 ax = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            DoorBasis(ax, out Vector3 u, out Vector3 v);
            var rot = Quaternion.LookRotation(ax, u);
            var size = new Vector3(SealCube, SealCube, SealCube);
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                emit(position + u * (i * SealPitch) + v * (j * SealPitch), rot, size, PrismKind.Plain);
        }

        // ── fields ───────────────────────────────────────────────────────

        enum Shape : byte
        {
            SmallCube, Block, Pillar, Slab, SmallSphere, BigCube, BigSphere, BigSlab, LongPillar, Beam,
            Frame, Jack, Hoop, Ziggurat,    // I4 (the gauntlet's field): new shapes, appended so I1-I3 pick exactly as before
        }
        const int ShapeCount = 14;

        /// <summary>Weighted shape menus per intensity: I1 is mostly small and single-cell, each step up adds bigger,
        /// wider and longer objects (big spheres from I2, beams across the field from I3, a beam lattice at I4). I4 adds
        /// frames (a hollow cube's twelve edges), jacks (a cube with six arms), hoops (a ring of tiles facing down the leg)
        /// and ziggurats (four stacked slabs).</summary>
        static readonly int[][] ShapeWeights =
        {
            //          SmallCube Block Pillar Slab SmallSphere BigCube BigSphere BigSlab LongPillar Beam Frame Jack Hoop Ziggurat
            new[] {     5,        2,    3,     2,   2,          1,      0,        0,      1,         0,   0,    0,   0,   0 },
            new[] {     3,        2,    2,     2,   2,          2,      1,        2,      1,         0,   0,    0,   0,   0 },
            new[] {     2,        2,    2,     2,   2,          2,      2,        2,      2,         3,   0,    0,   0,   0 },
            new[] {     2,        2,    2,     2,   1,          2,      2,        2,      2,         4,   2,    2,   2,   2 },
            new[] {     2,        2,    2,     2,   1,          2,      2,        3,      2,         5,   0,    0,   0,   0 },   // I4 without the gauntlet
        };

        struct LBox
        {
            public Vector3 C, Size;     // local: x along s1, y along s2, z upstream
            public PrismKind Kind;
            public float Turn;          // degrees about the leg (local z): a hoop's tiles; 0 for everything else
            public LBox(Vector3 c, Vector3 size, PrismKind kind, float turn = 0f) { C = c; Size = size; Kind = kind; Turn = turn; }
        }

        sealed class FieldContext
        {
            public IReadOnlyList<RaceGate> Course;
            public Vector3 Centre;
            public float ClearRadius;
            public int Intensity;
            public bool Profile;                // false: I4 without the gauntlet (R)
            public SwitchbackFeatureLayout Layout;
            public List<Keepout> Keepouts;
            public List<Keepout> Routes;        // I4: every gauntlet segment, kept clear of the fields and other gates' tunnels
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

        /// <summary>The smallest sphere (x, y, z, radius) holding both; an empty one (radius 0) is no sphere.</summary>
        static Vector4 Union(Vector4 a, Vector4 b)
        {
            if (a.w <= 0f) return b;
            if (b.w <= 0f) return a;
            Vector3 ca = new(a.x, a.y, a.z), cb = new(b.x, b.y, b.z);
            float d = Vector3.Distance(ca, cb);
            if (d + b.w <= a.w) return a;
            if (d + a.w <= b.w) return b;
            float r = 0.5f * (d + a.w + b.w);
            Vector3 c = d > 1e-4f ? ca + (cb - ca) * ((r - a.w) / d) : ca;
            return new Vector4(c.x, c.y, c.z, r);
        }

        /// <summary>Station <paramref name="s"/>'s block field on its gate's leg, from the mouth gap to
        /// <see cref="WorldKeyGap"/> short of the gate's first key (<paramref name="keyLead"/> out), counting
        /// <paramref name="laid0"/> prisms the station already laid (its tunnel) against <see cref="StationCap"/>.</summary>
        static void EmitField(FieldContext ctx, int s, BlastRunStation st, Vector3 dir,
                              int groupShare, int movingShare, int staticShare, int laid0, float keyLead)
        {
            var course = ctx.Course;
            int it = ctx.Intensity;
            float z0 = WorldNear(it, course[st.Gate].Radius);
            float z1 = keyLead - WorldKeyGap;
            int nz = Mathf.Min(CellsDeep(it, ctx.Profile), Mathf.FloorToInt((z1 - z0) / CellPitch));
            if (nz < MinCellsDeep) return;

            Frame(dir, out Vector3 s1, out Vector3 s2);
            Vector3 gate = course[st.Gate].Position;
            var f = new Field
            {
                Station = s, OwnLeg = st.Gate - 1, N = CellsHalf(it, ctx.Profile), Nz = nz, Z0 = z0,
                Bore = SightBore(it, ctx.Profile, course[st.Gate].Radius, course[st.Gate - 1].Radius),
                Origin = gate, S1 = s1, S2 = s2, Dir = dir, Rot = Quaternion.LookRotation(dir, s2),
                Salt = GateSalt(gate, s),
            };
            f.Occ = new bool[f.Side * f.Side * nz];
            float half = f.N * CellPitch, depth = nz * CellPitch;
            Vector3 mid = f.W(new Vector3(0f, 0f, z0 + depth * 0.5f));
            float reach = Mathf.Sqrt(2f * half * half + depth * depth * 0.25f) + 30f;
            ctx.Spheres[s] = Union(ctx.Spheres[s], new Vector4(mid.x, mid.y, mid.z, reach));

            int stationCap = StationCap(it, ctx.Profile);
            int laid = laid0;

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
            float fill = Fill(it, ctx.Profile);
            int[] weights = ShapeWeights[R(it, ctx.Profile)];
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
                case Shape.Frame:
                {
                    // A hollow cube's twelve edges, 4 u thick, 1 u apart where they meet: the x edges run the full side,
                    // the y and z edges stop short of them.
                    sx = sy = sz = 2;
                    var c = m + new Vector3(P, P, P);
                    const float half = P - M, t = FrameEdge, g = 1f;
                    float o = half - t * 0.5f, inner = 2f * (half - t - g);
                    for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        boxes.Add(new LBox(c + new Vector3(0f, a * o, b * o), new Vector3(2f * half, t, t), PrismKind.Plain));
                        boxes.Add(new LBox(c + new Vector3(a * o, 0f, b * o), new Vector3(t, inner, t), PrismKind.Plain));
                        boxes.Add(new LBox(c + new Vector3(a * o, b * o, 0f), new Vector3(t, t, inner), PrismKind.Plain));
                    }
                    break;
                }
                case Shape.Jack:
                {
                    // A cube with six arms, 1 u off its faces.
                    sx = sy = sz = 2;
                    var c = m + new Vector3(P, P, P);
                    const float core = 8f, arm = P - M - core * 0.5f - 1f, w = 5f;
                    boxes.Add(new LBox(c, new Vector3(core, core, core), PrismKind.Plain));
                    float o = core * 0.5f + 1f + arm * 0.5f;
                    for (int k = -1; k <= 1; k += 2)
                    {
                        boxes.Add(new LBox(c + new Vector3(k * o, 0f, 0f), new Vector3(arm, w, w), thin));
                        boxes.Add(new LBox(c + new Vector3(0f, k * o, 0f), new Vector3(w, arm, w), thin));
                        boxes.Add(new LBox(c + new Vector3(0f, 0f, k * o), new Vector3(w, w, arm), thin));
                    }
                    break;
                }
                case Shape.Hoop:
                {
                    // A ring of HoopTiles tiles facing down the leg, 2 x 2 cells across and one deep: a hoop to fly through.
                    sx = sy = 2;
                    var c = m + new Vector3(P, P, P * 0.5f);
                    float w = 2f * HoopRadius * Mathf.Tan(Mathf.PI / HoopTiles) - 1f;
                    float turn0 = 360f / HoopTiles * h3;
                    for (int k = 0; k < HoopTiles; k++)
                    {
                        float th = turn0 + k * 360f / HoopTiles;
                        float rad = th * Mathf.Deg2Rad;
                        float rc = HoopRadius + FrameEdge * 0.5f;
                        boxes.Add(new LBox(c + new Vector3(Mathf.Cos(rad) * rc, Mathf.Sin(rad) * rc, 0f),
                                           new Vector3(w, FrameEdge, HoopDepth), PrismKind.Plain, th - 90f));
                    }
                    break;
                }
                case Shape.Ziggurat:
                {
                    // Four stacked square slabs, each 12 u narrower, 8 u of air between them.
                    sx = sy = sz = 2;
                    var c = m + new Vector3(P, P, P);
                    for (int k = 0; k < 4; k++)
                    {
                        float side = 2f * (P - M) - k * 12f;
                        boxes.Add(new LBox(c + new Vector3(0f, -(P - M) + 2f + k * 12f, 0f), new Vector3(side, 4f, side),
                                           PrismKind.Plain));
                    }
                    break;
                }
            }
        }

        const float FrameEdge = 4f;
        const float HoopRadius = 16f;   // the hoop's inside, axis to the tiles' inner faces
        const int HoopTiles = 12;
        const float HoopDepth = 6f;

        /// <summary>A local box's half extents across its turn about the leg (an axis-aligned box: its own).</summary>
        static Vector3 HalfExtents(in LBox b)
        {
            Vector3 h = b.Size * 0.5f;
            if (b.Turn == 0f) return h;
            float r = b.Turn * Mathf.Deg2Rad, c = Mathf.Abs(Mathf.Cos(r)), s = Mathf.Abs(Mathf.Sin(r));
            return new Vector3(c * h.x + s * h.y, s * h.x + c * h.y, h.z);
        }

        static void Bounds(List<LBox> boxes, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < boxes.Count; i++)
            {
                Vector3 h = HalfExtents(boxes[i]);
                min = Vector3.Min(min, boxes[i].C - h);
                max = Vector3.Max(max, boxes[i].C + h);
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
            if (RouteBlocked(ctx, -1)) return false;
            return ctx.Density.CanAdd(f.W((min + max) * 0.5f), (max - min).magnitude * 0.5f, k);
        }

        /// <summary>Whether any of the current samples (<c>ctx.Samples</c>) comes inside a gauntlet route segment of a
        /// gate other than <paramref name="ownGate"/> (-1: any gate's).</summary>
        static bool RouteBlocked(FieldContext ctx, int ownGate)
        {
            var routes = ctx.Routes;
            if (routes == null || routes.Count == 0) return false;
            for (int i = 0; i < ctx.Samples.Count; i++)
            {
                Vector4 sm = ctx.Samples[i];
                var p = new Vector3(sm.x, sm.y, sm.z);
                for (int r = 0; r < routes.Count; r++)
                    if (routes[r].Gate != ownGate &&
                        SwitchbackFeatures.DistanceToSegment(p, routes[r].A, routes[r].B) < routes[r].Radius + sm.w)
                        return true;
            }
            return false;
        }

        // ── the gauntlet's tunnels ───────────────────────────────────────

        /// <summary>
        /// Station <paramref name="s"/>'s tunnel round its segment (key to <see cref="BlastRunStation.Target"/>): walls of
        /// its <see cref="BlastRunTunnel"/> kind outside <see cref="TunnelRadius"/>, and plug walls across the inside every
        /// <see cref="PlugSpacing"/> from <see cref="PlugStart"/> past the key. Each piece is admitted on its own (a piece
        /// that would crowd a mouth, another leg, a door, a key, another station or the collider budget is left out), and
        /// at most <paramref name="budget"/> are laid. Returns the prisms laid.
        /// </summary>
        static int EmitTunnel(FieldContext ctx, int s, in BlastRunStation st, int budget)
        {
            Vector3 a = st.Key, seg = st.Target - st.Key;
            float len = seg.magnitude;
            if (len < 2f * TunnelKeyGap + TunnelSection || budget <= 0) return 0;
            Vector3 ax = seg / len;
            Vector3 e1 = RaceCourseGeometry.Perpendicular(ax), e2 = Vector3.Cross(ax, e1);
            Vector3 mid = a + seg * 0.5f;
            ctx.Spheres[s] = Union(ctx.Spheres[s], new Vector4(mid.x, mid.y, mid.z, len * 0.5f + RouteClearance + 30f));
            int salt = GateSalt(ctx.Course[st.Gate].Position, s);
            float twist = Hash01(salt, 51, 0, 0) * 360f;
            // The last segment runs into the door: its keepout (and the gate's mouth) stops the walls; the others stop
            // short of the next key.
            float end = st.Final ? len : len - TunnelKeyGap;
            int laid = 0;
            const float T = TunnelWall;
            switch (st.Tunnel)
            {
                case BlastRunTunnel.Pipe:
                case BlastRunTunnel.Duct:
                {
                    bool pipe = st.Tunnel == BlastRunTunnel.Pipe;
                    int tiles = pipe ? 8 : 4;
                    float w = pipe ? 2f * TunnelRadius * Mathf.Tan(Mathf.PI / 8f) - 1f : 2f * TunnelRadius - 2f;
                    for (int k = 0; ; k++)
                    {
                        float d0 = TunnelKeyGap + k * (TunnelSection + TunnelSectionGap);
                        if (d0 + TunnelSection > end) break;
                        float turn = twist + (k % 2) * (180f / tiles);
                        for (int i = 0; i < tiles; i++)
                            laid += TunnelTile(ctx, s, st.Gate, a, ax, e1, e2, d0 + TunnelSection * 0.5f, turn + i * 360f / tiles,
                                               TunnelRadius + T * 0.5f, new Vector3(w, T, TunnelSection), PrismKind.Plain, budget - laid);
                    }
                    break;
                }
                case BlastRunTunnel.Hoops:
                {
                    const int tiles = 8;
                    float w = 2f * TunnelRadius * Mathf.Tan(Mathf.PI / tiles) - 1f;
                    for (int k = 0; ; k++)
                    {
                        float d = TunnelKeyGap + 3f + k * 20f;
                        if (d + 3f > end) break;
                        float turn = twist + (k % 2) * (180f / tiles);
                        for (int i = 0; i < tiles; i++)
                            laid += TunnelTile(ctx, s, st.Gate, a, ax, e1, e2, d, turn + i * 360f / tiles,
                                               TunnelRadius + T * 0.5f, new Vector3(w, T, 6f), PrismKind.Plain, budget - laid);
                    }
                    break;
                }
                case BlastRunTunnel.Helix:
                {
                    // One block every 30 deg round, 2.4 u further along: a turn every 28.8 u.
                    for (int k = 0; ; k++)
                    {
                        float d = TunnelKeyGap + 3f + k * 2.4f;
                        if (d + 3f > end) break;
                        laid += TunnelTile(ctx, s, st.Gate, a, ax, e1, e2, d, twist + k * 30f, TunnelRadius + 3f,
                                           new Vector3(6f, 6f, 5f), PrismKind.Plain, budget - laid);
                    }
                    break;
                }
                case BlastRunTunnel.Cage:
                {
                    // Six rails along the segment, cut in pieces no longer than 90 u.
                    float span = end - TunnelKeyGap;
                    int pieces = Mathf.Max(1, Mathf.CeilToInt(span / 90f));
                    float piece = span / pieces;
                    for (int p = 0; p < pieces; p++)
                    for (int i = 0; i < 6; i++)
                        laid += TunnelTile(ctx, s, st.Gate, a, ax, e1, e2, TunnelKeyGap + (p + 0.5f) * piece, twist + i * 60f,
                                           TunnelRadius + 2f, new Vector3(4f, 4f, piece - 2f), PrismKind.Plain, budget - laid);
                    break;
                }
            }

            // The plugs: a plus of five cubes across the inside, every other wall turned 45 deg; a Danger heart on every
            // PlugDangerEvery-th wall.
            for (int k = 0; ; k++)
            {
                float d = PlugStart + k * PlugSpacing;
                if (d > end - 8f) break;
                float turn = twist + (k % 2) * 45f;
                var heart = k % PlugDangerEvery == PlugDangerEvery - 1 ? PrismKind.Danger : PrismKind.Plain;
                var cube = new Vector3(PlugCube, PlugCube, PlugCube);
                laid += TunnelTile(ctx, s, st.Gate, a, ax, e1, e2, d, turn, 0f, cube, heart, budget - laid);
                for (int i = 0; i < 4; i++)
                    laid += TunnelTile(ctx, s, st.Gate, a, ax, e1, e2, d, turn + i * 90f, PlugPitch, cube, PrismKind.Plain, budget - laid);
            }
            ctx.StaticPrisms += laid;
            return laid;
        }

        /// <summary>One tunnel piece: a box <paramref name="size"/> (x round, y out, z along) at <paramref name="along"/> u
        /// down the segment from <paramref name="a"/>, <paramref name="radius"/> u out at <paramref name="turnDegrees"/>
        /// round it, admitted on its own. 1 if laid.</summary>
        static int TunnelTile(FieldContext ctx, int s, int gate, Vector3 a, Vector3 ax, Vector3 e1, Vector3 e2, float along,
                              float turnDegrees, float radius, Vector3 size, PrismKind kind, int left)
        {
            if (left <= 0) return 0;
            float r = turnDegrees * Mathf.Deg2Rad;
            Vector3 radial = e1 * Mathf.Cos(r) + e2 * Mathf.Sin(r);
            Vector3 c = a + ax * along + radial * radius;
            var rot = Quaternion.LookRotation(ax, radial);
            if (!PieceClear(ctx, s, gate, c, rot, size)) return 0;
            if (!ctx.Density.CanAdd(c, size.magnitude * 0.5f, 1)) return 0;
            ctx.Blobs[s].AddRange(ctx.Samples);
            Lay(ctx, c, rot, size, kind);
            return 1;
        }

        /// <summary><see cref="VolumeClear"/> for a world-space box (centre, rotation, size) of gate <paramref name="gate"/>'s
        /// tunnel: its own keys keep only <see cref="GauntletKeyClearance"/>, its own route is where it belongs, every other
        /// gate's route is kept clear.</summary>
        static bool PieceClear(FieldContext ctx, int s, int gate, Vector3 c, Quaternion rot, Vector3 size)
        {
            ctx.Samples.Clear();
            Vector3 h = size * 0.5f;
            int axis = h.x >= h.y && h.x >= h.z ? 0 : (h.y >= h.z ? 1 : 2);
            float hl = h[axis];
            float hb = axis == 0 ? h.y : h.x, hc = axis == 2 ? h.y : h.z;
            float rc = Mathf.Sqrt(hb * hb + hc * hc);
            int n = Mathf.Max(1, Mathf.CeilToInt(2f * hl / Mathf.Max(2f * rc, 12f)));
            float step = 2f * hl / n;
            float r = Mathf.Sqrt(rc * rc + step * step * 0.25f);
            Vector3 unit = Vector3.zero;
            unit[axis] = 1f;
            Vector3 along = rot * unit;
            Vector4 me = ctx.Spheres[s];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = c + along * (-hl + step * (i + 0.5f));
                if (!PointClear(ctx.Course, ctx.Centre, ctx.ClearRadius, p, r, WorldLineClearance, gate - 1, -1)) return false;
                var keepouts = ctx.Keepouts;
                for (int k = 0; k < keepouts.Count; k++)
                {
                    float kr = keepouts[k].Gate == gate ? GauntletKeyClearance : keepouts[k].Radius;
                    if (SwitchbackFeatures.DistanceToSegment(p, keepouts[k].A, keepouts[k].B) < kr + r) return false;
                }
                for (int o = 0; o < ctx.Blobs.Count; o++)
                {
                    if (o == s) continue;
                    Vector4 sp = ctx.Spheres[o];
                    if (sp.w <= 0f) continue;
                    if ((new Vector3(sp.x, sp.y, sp.z) - new Vector3(me.x, me.y, me.z)).magnitude > sp.w + me.w) continue;
                    var blobs = ctx.Blobs[o];
                    for (int b = 0; b < blobs.Count; b++)
                    {
                        float rr = blobs[b].w + r + 2f;
                        if ((new Vector3(blobs[b].x, blobs[b].y, blobs[b].z) - p).sqrMagnitude < rr * rr) return false;
                    }
                }
                ctx.Samples.Add(new Vector4(p.x, p.y, p.z, r));
            }
            return !RouteBlocked(ctx, gate);
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
            for (int i = 0; i < boxes.Count; i++)
            {
                var rot = boxes[i].Turn == 0f ? f.Rot : f.Rot * Quaternion.AngleAxis(boxes[i].Turn, Vector3.forward);
                Lay(ctx, f.W(boxes[i].C), rot, boxes[i].Size, boxes[i].Kind);
            }
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
