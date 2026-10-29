using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The closed-form world a Blast Run station grows on its inbound leg, one per intensity.</summary>
    public enum BlastRunWorld
    {
        Garden = 1,     // I1: stalks grow in from the wall, hollow blooms on their tips
        Maze = 2,       // I2: baffles across the leg, one hole each, the hole walking round the leg
        Membrane = 3,   // I3: a voxel gyroid sheet - two interwoven labyrinths with pores between them
        MengerCage = 4, // I4: level-1 Menger sponges straddling the line - a cage with one tunnel each way
    }

    /// <summary>
    /// One BLOCKED gate (SWITCHBACK.md "Blast Run"): its ring is sealed by a Breakwater door (keystone collar plus
    /// plug, <see cref="BreakwaterStationBuilder.BuildDoor"/>) with only the <see cref="BreakwaterCourseSettings.EyeRadius"/>
    /// eye open, and its inbound leg is filled with <see cref="World"/>. The <see cref="Key"/> crystal sits on the
    /// leg's line <see cref="KeyLead"/> before the gate: collecting it fires the Dolphin's forward crystal blast down
    /// the leg, through the world and the door.
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
        public readonly int WorldPrisms;

        public BlastRunStation(int gate, Vector3 key, float keyLead, Vector3 door, Vector3 flowAxis, float port,
                               bool doubleDoor, BlastRunWorld world, int worldPrisms)
        {
            Gate = gate; Key = key; KeyLead = keyLead; Door = door; FlowAxis = flowAxis; Port = port;
            DoubleDoor = doubleDoor; World = world; WorldPrisms = worldPrisms;
        }
    }

    /// <summary>
    /// A hunter pack's den (Blast Run, I4): <see cref="Members"/> bodies of <see cref="PerMember"/> danger prisms laid
    /// around <see cref="Den"/>, member m's prisms at <c>FirstPrism + m * PerMember</c>. The server moves the pack
    /// (<c>SwitchbackBlastRunHunters</c>); every member moves rigidly by (pack position - den).
    /// </summary>
    public readonly struct BlastRunPack
    {
        public readonly int Station;
        public readonly Vector3 Den;
        public readonly int FirstPrism;
        public readonly int Members;
        public readonly int PerMember;

        public BlastRunPack(int station, Vector3 den, int firstPrism, int members, int perMember)
        {
            Station = station; Den = den; FirstPrism = firstPrism; Members = members; PerMember = perMember;
        }
    }

    /// <summary>
    /// BLAST RUN (<see cref="SwitchbackFunType.BlastRun"/>, SWITCHBACK.md "Blast Run"; Yash 2026-10-28: "Dolphins need
    /// to use their shooting abilities to clear paths for their rings ... like sparrow's time game, just in dolphin
    /// way"). A pure function of the course, like <see cref="SwitchbackLiveFeatures"/>, so every peer lays the same
    /// prisms and the EditMode tests (<c>SwitchbackBlastRunTests</c>) and <c>Tools/Build/audit_prism_clipping.py</c> hold
    /// it without a scene.
    ///
    /// <para><b>The loop.</b> Some gates are BLOCKED (<see cref="IsBlocked"/>): a Breakwater door hangs
    /// <see cref="DoorSetback"/> in front of the ring and seals all of it but the 18 u eye, and the leg in is full of
    /// world. On that leg's line, <see cref="KeyLeadFor"/> before the gate, floats a KEY crystal (laid by
    /// <see cref="CrystalManager.SetCourseKeys"/>; it respawns in place). The Dolphin has no gun - its weapon IS the
    /// crystal: collecting one fires the forward conic blast (400-2080 u long, ~320 u core) along the gape, so a key
    /// taken while facing the gate blows the world and the door away in one go. Miss it and you thread the eye, or
    /// thread the world's pores, or fly round.</para>
    ///
    /// <para><b>The ladder.</b> I1 Garden (6 stations, every third gate), I2 Maze plus sweepers (9, every second),
    /// I3 Membrane plus sweepers and snap-trap jaws (all 18), I4 Menger cage, sweepers, jaws, a door each side of the
    /// ring, and three hunter packs that chase the leader. Every count is under Breakwater's (see the tests).</para>
    ///
    /// <para><b>Clearances.</b> Everything keeps <see cref="SwitchbackFeatures.MouthClearance"/> outside every ring,
    /// out of the nucleus, and off every OTHER leg's line; world prisms also keep off keys, doors, jaws, sweepers and
    /// dens, and off other stations' world prisms (bounding spheres, so no two prisms ever interpenetrate).</para>
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

        // ── worlds ───────────────────────────────────────────────────────
        public const float WorldMouthGap = 50f;         // u past the ring's radius before the world starts
        public const float WorldKeyGap = 40f;           // u the world stops short of the key
        public const float WorldLineClearance = 40f;    // u world prisms keep off every other leg's line
        public const float WorldKeyClearance = 30f;     // u world prisms keep off every key

        // ── sweepers (I2+) and jaws (I3+) ────────────────────────────────
        public const float SweeperBeforeKey = 70f;      // u upstream of the key
        public const int SweeperPrisms = 3;
        public const float JawSetback = 36f;            // u upstream of the ring
        public const float JawSetbackI4 = 52f;
        public const int JawPrisms = 5;                 // per jaw, two jaws per station
        public const float JawPitch = 12f;              // u between a jaw's prisms, across the eye
        public static readonly Vector3 JawPrismScale = new(4f, 10f, 6f);
        public const float JawOpen = 26f;               // u from the axis to a jaw's centre, wide open
        public const float JawClosed = 4f;              // ...and shut: 2 u faces, a 4 u gap - nothing gets through
        public const float JawPeriod = 2.4f;            // s: snap shut, open, faster than the sweepers' 4 s
        public static float JawOmega => 2f * Mathf.PI / JawPeriod;
        public static float JawAmplitude => (JawOpen - JawClosed) * 0.5f;
        /// <summary>A jaw prism's bounding sphere (half the 4 x 10 x 6 diagonal, 6.2) plus 2 u.</summary>
        const float JawKeepout = 8.2f;

        // ── hunters (I4) ─────────────────────────────────────────────────
        public const int PackEvery = 4;                 // a den on every fourth blocked station...
        public const int MaxPacks = 3;                  // ...three at most
        public const int PackOffset = 3;
        public const int PackMembers = 3;
        public const int PackBodyPrisms = 7;            // a jack: a centre cube and one on each frame axis
        public const float PackBodyCube = 5f;
        public const float PackBodyArm = 6f;            // u from a body's centre to its arm cubes
        public const float PackMemberSpacing = 18f;     // u between members, abreast
        public const float DenOffset = 110f;            // u off the leg's midpoint
        public const float DenClearance = 60f;          // u a den keeps off every other leg's line
        public const float PackSpeed = 45f;             // u/s - under the Dolphin's cruise: it intercepts, it cannot chase
        public const float PackLeash = 260f;            // u from the den the pack will roam
        public const float PackSense = 400f;            // u from the den at which it notices the leader
        public const float PackMemberDeadShare = 0.4f;  // a member with no more than this share of its body left is down

        /// <summary>Per-station caps on world prisms: Breakwater's densest 200 u neighbourhood is 568/464/322/276.</summary>
        public static int WorldCap(int intensity) => new[] { 280, 180, 100, 60 }[Mathf.Clamp(intensity, 1, 4) - 1];
        /// <summary>Whole-course prism ceilings, asserted by the tests (Breakwater lays about 4600 at I1).</summary>
        public static int TotalCap(int intensity) => new[] { 4600, 4000, 3500, 3000 }[Mathf.Clamp(intensity, 1, 4) - 1];
        /// <summary>SpawnableSwitchbackFeatures' lay pool: over every TotalCap.</summary>
        public const int LayCapacity = 4096;

        public static BlastRunWorld WorldFor(int intensity) => (BlastRunWorld)Mathf.Clamp(intensity, 1, 4);
        public static bool HasSweepers(int intensity) => intensity >= 2;
        public static bool HasJaws(int intensity) => intensity >= 3;
        public static bool HasDoubleDoor(int intensity) => intensity >= 4;
        public static bool HasHunters(int intensity) => intensity >= 4;

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

        public static float WorldNear(int intensity, float ringRadius)
        {
            float near = ringRadius + WorldMouthGap;
            if (HasJaws(intensity)) near = Mathf.Max(near, JawSetbackFor(intensity) + 24f);
            return near;
        }

        public static float JawSetbackFor(int intensity) => intensity >= 4 ? JawSetbackI4 : JawSetback;

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

        /// <summary>Blast Run's layout of <paramref name="course"/>: stations (keys, doors), jaws and sweepers (moving
        /// clusters), hunter dens and packs, then the worlds - in that order, each filtered by everything before it.</summary>
        public static SwitchbackFeatureLayout Layout(IReadOnlyList<RaceGate> course, int intensity,
                                                     Vector3 centre = default, float clearRadius = 0f)
        {
            var layout = new SwitchbackFeatureLayout();
            if (course == null || course.Count < 3) return layout;
            intensity = Mathf.Clamp(intensity, 1, 4);
            float port = PortFor(intensity);
            var keepouts = new List<Keepout>(64);

            // 1. Stations: key, door(s).
            var plans = new List<(int gate, Vector3 dir, float leg, float lead)>();
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
                if (HasJaws(intensity) &&
                    !DoorClear(course, centre, clearRadius, b - axis * JawSetbackFor(intensity), JawOpen + JawPitch * 2f + 8f, g))
                    continue;

                EmitDoor(layout, door, axis, port);
                if (dbl) EmitDoor(layout, door2, axis, port);
                keepouts.Add(new Keepout(door, door, port + 6f));
                if (dbl) keepouts.Add(new Keepout(door2, door2, port + 6f));
                keepouts.Add(new Keepout(key, key, WorldKeyClearance));
                layout.Stations.Add(new BlastRunStation(g, key, lead, door, axis, port, dbl, WorldFor(intensity), 0));
                plans.Add((g, dir, leg, lead));
            }

            // 2. Jaws (I3+): two opposed jaws across the eye, snapping shut.
            if (HasJaws(intensity))
                for (int s = 0; s < layout.Stations.Count; s++)
                {
                    var st = layout.Stations[s];
                    Vector3 gate = course[st.Gate].Position;
                    Vector3 axis = st.FlowAxis;
                    DoorBasis(axis, out Vector3 u, out Vector3 v);
                    Vector3 c = gate - axis * JawSetbackFor(intensity);
                    float phase = -Mathf.PI * 0.5f + s * 2.3f;   // staggered station to station
                    float laid = JawOpen - JawAmplitude * (1f + Mathf.Sin(phase));
                    Quaternion rot = Quaternion.LookRotation(axis, v);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int first = layout.Prisms.Count;
                        for (int j = 0; j < JawPrisms; j++)
                        {
                            float across = (j - (JawPrisms - 1) * 0.5f) * JawPitch;
                            layout.Prisms.Add(new SwitchbackFeaturePrism(c + u * (side * laid) + v * across, rot,
                                                                         JawPrismScale, PrismKind.Danger));
                        }
                        // Closing moves the jaw TOWARD the axis: its sweep side is -side x u.
                        layout.MovingClusters.Add(new SwitchbackMovingCluster(first, JawPrisms, u * -side,
                                                                              JawAmplitude, phase, JawOmega));
                    }
                    // The jaws' swept slab: one capsule per prism row, from wide open on one side to the other.
                    for (int j = 0; j < JawPrisms; j++)
                    {
                        Vector3 row = c + v * ((j - (JawPrisms - 1) * 0.5f) * JawPitch);
                        keepouts.Add(new Keepout(row - u * JawOpen, row + u * JawOpen, JawKeepout));
                    }
                }

            // 3. Sweepers (I2+): the shipped I4 danger cluster, sweeping across the line before the key.
            if (HasSweepers(intensity))
                for (int s = 0; s < layout.Stations.Count; s++)
                {
                    var st = layout.Stations[s];
                    var plan = plans[s];
                    float z = st.KeyLead + SweeperBeforeKey;
                    float amp = SwitchbackLiveFeatures.MovingAmplitude;
                    if (z > plan.leg - (course[st.Gate - 1].Radius + SwitchbackFeatures.MouthClearance + amp)) continue;
                    Vector3 gate = course[st.Gate].Position;
                    Vector3 dir = plan.dir;
                    Vector3 side = RaceCourseGeometry.Perpendicular(dir) * (s % 2 == 0 ? 1f : -1f);
                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.Cross(side, dir));
                    float phase = st.Gate * 1.7f;
                    float laid = amp * Mathf.Sin(phase);
                    var cluster = new List<SwitchbackFeaturePrism>(SweeperPrisms);
                    bool clear = true;
                    Vector3 lo = default, hi = default;
                    for (int j = 0; j < SweeperPrisms && clear; j++)
                    {
                        float d = z + (j - (SweeperPrisms - 1) * 0.5f) * SwitchbackFeatures.DangerSpacing;
                        Vector3 onLine = gate - dir * d;
                        clear = PointClear(course, centre, clearRadius, onLine + side * amp, 0f, amp * SwitchbackFeatures.LineClearanceShare, st.Gate - 1, -1)
                             && PointClear(course, centre, clearRadius, onLine - side * amp, 0f, amp * SwitchbackFeatures.LineClearanceShare, st.Gate - 1, -1);
                        if (j == 0) lo = onLine; else hi = onLine;
                        cluster.Add(new SwitchbackFeaturePrism(onLine + side * laid, rot,
                                                               SwitchbackFeatures.DangerPrismScale, PrismKind.Danger));
                    }
                    if (!clear) continue;
                    // ...and its whole sweep keeps off every door, jaw and key laid so far.
                    float sr = SwitchbackFeatures.DangerPrismScale.x * 0.8660254f;
                    if (SweepBlocked(keepouts, lo, hi, side * amp, sr)) continue;
                    layout.MovingClusters.Add(new SwitchbackMovingCluster(layout.Prisms.Count, cluster.Count, side, amp, phase));
                    layout.Prisms.AddRange(cluster);
                    float r = SwitchbackFeatures.DangerPrismScale.x * 0.8660254f + 4f;
                    keepouts.Add(new Keepout(lo + side * amp, lo - side * amp, r));
                    keepouts.Add(new Keepout(hi + side * amp, hi - side * amp, r));
                    keepouts.Add(new Keepout(lo + side * amp, hi + side * amp, r));
                    keepouts.Add(new Keepout(lo - side * amp, hi - side * amp, r));
                }

            // 4. Hunter dens (I4).
            if (HasHunters(intensity))
            {
                int lastDen = PackOffset - PackEvery;
                for (int s = PackOffset; s < layout.Stations.Count && layout.Packs.Count < MaxPacks; s++)
                {
                    if (s - lastDen < PackEvery) continue;
                    var st = layout.Stations[s];
                    var plan = plans[s];
                    Vector3 gate = course[st.Gate].Position;
                    Frame(plan.dir, out Vector3 s1, out Vector3 s2);
                    float bodyR = PackMemberSpacing + PackBodyArm + PackBodyCube;
                    // Either flank of the leg's midpoint; the first that is clear of the course and the other features.
                    Vector3 den = default;
                    bool found = false;
                    for (int side = 0; side < 2 && !found; side++)
                    {
                        if (side == 1) s2 = -s2;
                        den = gate - plan.dir * (plan.leg * 0.5f) + s2 * DenOffset;
                        found = PointClear(course, centre, clearRadius + bodyR, den, bodyR, DenClearance, -1, -1)
                                && !Blocked(keepouts, den, bodyR);
                    }
                    if (!found) continue;
                    lastDen = s;
                    int first = layout.Prisms.Count;
                    Quaternion rot = Quaternion.LookRotation(plan.dir, s2);
                    var scale = new Vector3(PackBodyCube, PackBodyCube, PackBodyCube);
                    for (int m = 0; m < PackMembers; m++)
                    {
                        Vector3 body = den + s1 * ((m - (PackMembers - 1) * 0.5f) * PackMemberSpacing);
                        layout.Prisms.Add(new SwitchbackFeaturePrism(body, rot, scale, PrismKind.Danger));
                        for (int axisIndex = 0; axisIndex < 3; axisIndex++)
                        {
                            Vector3 ax = axisIndex == 0 ? s1 : axisIndex == 1 ? s2 : plan.dir;
                            layout.Prisms.Add(new SwitchbackFeaturePrism(body + ax * PackBodyArm, rot, scale, PrismKind.Danger));
                            layout.Prisms.Add(new SwitchbackFeaturePrism(body - ax * PackBodyArm, rot, scale, PrismKind.Danger));
                        }
                    }
                    layout.Packs.Add(new BlastRunPack(s, den, first, PackMembers, PackBodyPrisms));
                    keepouts.Add(new Keepout(den, den, bodyR + 20f));
                }
            }

            // 5. Worlds.
            var accepted = new Dictionary<long, List<(int prism, int station)>>();
            for (int s = 0; s < layout.Stations.Count; s++)
            {
                var st = layout.Stations[s];
                var plan = plans[s];
                int before = layout.Prisms.Count;
                EmitWorld(layout, course, centre, clearRadius, intensity, st, plan.dir, keepouts, accepted, s);
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

        /// <summary>The leg frame: <paramref name="s1"/> = <see cref="RaceCourseGeometry.Perpendicular"/>, s2 = dir x s1.</summary>
        public static void Frame(Vector3 dir, out Vector3 s1, out Vector3 s2)
        {
            s1 = RaceCourseGeometry.Perpendicular(dir);
            s2 = Vector3.Cross(dir, s1);
        }

        /// <summary>Lattice pitch, cube size and radius of each world.</summary>
        public static void WorldShape(BlastRunWorld world, out float pitch, out float cube, out float radius)
        {
            switch (world)
            {
                case BlastRunWorld.Garden: pitch = 8f; cube = 6.4f; radius = 80f; break;
                case BlastRunWorld.Maze: pitch = 12f; cube = 10.4f; radius = 60f; break;
                case BlastRunWorld.Membrane: pitch = 14f; cube = 12f; radius = 54f; break;
                default: pitch = 24f; cube = 22f; radius = 36f; break;
            }
        }

        // Garden
        public const float GardenRowPitch = 44f;
        public const int GardenPlantsPerRow = 6;
        public const float GardenTip = 24f;           // u from the line to a bloom's centre
        public const float GardenStem = 4.5f;
        public const float GardenBloomInner = 6f, GardenBloomOuter = 12f, GardenBloomCore = 4f;
        // Maze
        public const float MazeBafflePitch = 40f;
        public const float MazeHoleRadius = 15f;
        public const float MazeHoleAt = 0.55f;        // of the radius
        // Membrane
        public const float MembranePeriod = 84f;
        public const float MembraneThickness = 0.6f;  // |gyroid| below this is sheet
        // shared
        public const float WorldDangerShare = 0.08f;  // hash-picked danger cells (plus a shape's own)

        static void EmitWorld(SwitchbackFeatureLayout layout, IReadOnlyList<RaceGate> course, Vector3 centre,
                              float clearRadius, int intensity, in BlastRunStation st, Vector3 dir,
                              List<Keepout> keepouts, Dictionary<long, List<(int prism, int station)>> accepted, int station)
        {
            WorldShape(st.World, out float P, out float cube, out float Rw);
            float z0 = WorldNear(intensity, course[st.Gate].Radius);
            float z1 = st.KeyLead - WorldKeyGap;
            if (z1 - z0 < P) return;
            Vector3 gate = course[st.Gate].Position;
            Frame(dir, out Vector3 s1, out Vector3 s2);
            Quaternion rot = Quaternion.LookRotation(dir, s2);
            var scale = new Vector3(cube, cube, cube);
            float h = cube * 0.8660254f;
            int cap = WorldCap(intensity);
            int count = 0;
            int n = Mathf.CeilToInt(Rw / P);

            if (st.World == BlastRunWorld.MengerCage)
            {
                // Level-1 sponges of 3x3x3 cells centred on the line, back to back while they fit. A sponge is laid
                // whole or not at all (a half cage is just debris); one that does not fit slides a cell upstream.
                float side = 3f * P;
                var cells = new List<(Vector3 p, bool danger)>(20);
                for (float zc = z0 + side * 0.5f; zc + side * 0.5f <= z1 && count + 20 <= cap;)
                {
                    cells.Clear();
                    bool fits = true;
                    for (int i = 0; i < 3 && fits; i++)
                    for (int j = 0; j < 3 && fits; j++)
                    for (int k = 0; k < 3 && fits; k++)
                    {
                        int centres = (i == 1 ? 1 : 0) + (j == 1 ? 1 : 0) + (k == 1 ? 1 : 0);
                        if (centres >= 2) continue;   // the body centre and the six face centres: the tunnels
                        bool corner = (i == 0 && j == 0 && k == 0) || (i == 2 && j == 2 && k == 2);
                        Vector3 p = gate - dir * (zc + (k - 1) * P) + s1 * ((i - 1) * P) + s2 * ((j - 1) * P);
                        if (!WorldPrismFree(layout, course, centre, clearRadius, st.Gate, station, p, h, keepouts, accepted))
                            fits = false;
                        else
                            cells.Add((p, corner));
                    }
                    if (!fits) { zc += P; continue; }
                    foreach (var c in cells)
                    {
                        Accept(layout, accepted, c.p, station);
                        layout.Prisms.Add(new SwitchbackFeaturePrism(c.p, rot, scale, c.danger ? PrismKind.Danger : PrismKind.Plain));
                    }
                    count += cells.Count;
                    zc += side + P;
                }
                return;
            }

            int kz0 = Mathf.CeilToInt(z0 / P), kz1 = Mathf.FloorToInt(z1 / P);
            for (int kz = kz0; kz <= kz1 && count < cap; kz++)
            {
                float z = kz * P;
                for (int ix = -n; ix <= n && count < cap; ix++)
                for (int iy = -n; iy <= n && count < cap; iy++)
                {
                    float x = ix * P, y = iy * P;
                    if (x * x + y * y > Rw * Rw) continue;
                    if (!InWorld(st.World, x, y, z, z0, z1, Rw, P, station, out bool danger)) continue;
                    if (!danger && Hash01(station, ix, iy, kz) < WorldDangerShare) danger = true;
                    Vector3 p = gate - dir * z + s1 * x + s2 * y;
                    if (!TryWorldPrism(layout, course, centre, clearRadius, st.Gate, station, p, h, keepouts, accepted)) continue;
                    layout.Prisms.Add(new SwitchbackFeaturePrism(p, rot, scale, danger ? PrismKind.Danger : PrismKind.Plain));
                    count++;
                }
            }
        }

        /// <summary>The worlds' closed forms in the leg frame: x, y across, z upstream from the gate.</summary>
        public static bool InWorld(BlastRunWorld world, float x, float y, float z, float z0, float z1, float Rw,
                                   float P, int station, out bool danger)
        {
            danger = false;
            switch (world)
            {
                case BlastRunWorld.Garden:
                {
                    float best = float.MaxValue;
                    for (int m = 0; ; m++)
                    {
                        float zm = z0 + 18f + GardenRowPitch * m;
                        if (zm > z1 - 14f) break;
                        if (Mathf.Abs(z - zm) > GardenBloomOuter + GardenStem) continue;
                        for (int j = 0; j < GardenPlantsPerRow; j++)
                        {
                            float th = (j * 60f + m * 30f + station * 17f) * Mathf.Deg2Rad;
                            float cx = Mathf.Cos(th), cy = Mathf.Sin(th);
                            // bloom
                            float bx = x - cx * GardenTip, by = y - cy * GardenTip, bz = z - zm;
                            float bd = Mathf.Sqrt(bx * bx + by * by + bz * bz);
                            if (bd <= GardenBloomCore) { danger = true; return true; }
                            if (bd >= GardenBloomInner && bd <= GardenBloomOuter) best = 0f;
                            // stalk: from the wall in to the bloom's outer edge, along the radial
                            float along = x * cx + y * cy;
                            if (along >= GardenTip + GardenBloomOuter && along <= Rw)
                            {
                                float px = x - cx * along, py = y - cy * along;
                                float sd = Mathf.Sqrt(px * px + py * py + bz * bz);
                                if (sd <= GardenStem) best = 0f;
                            }
                        }
                    }
                    return best == 0f;
                }
                case BlastRunWorld.Maze:
                {
                    // baffles on lattice planes, one hole each
                    int kz = Mathf.RoundToInt(z / P);
                    int first = Mathf.CeilToInt((z0 + P) / P);
                    int every = Mathf.Max(1, Mathf.RoundToInt(MazeBafflePitch / P));
                    if ((kz - first) % every != 0 || kz < first) return false;
                    int b = (kz - first) / every;
                    float th = (b * 137.5f + station * 53f) * Mathf.Deg2Rad;
                    float hx = Mathf.Cos(th) * Rw * MazeHoleAt, hy = Mathf.Sin(th) * Rw * MazeHoleAt;
                    float dx = x - hx, dy = y - hy;
                    return dx * dx + dy * dy > MazeHoleRadius * MazeHoleRadius;
                }
                case BlastRunWorld.Membrane:
                {
                    float w = 2f * Mathf.PI / MembranePeriod;
                    float X = x * w, Y = y * w, Z = z * w + station * 0.9f;
                    float gyr = Mathf.Sin(X) * Mathf.Cos(Y) + Mathf.Sin(Y) * Mathf.Cos(Z) + Mathf.Sin(Z) * Mathf.Cos(X);
                    return Mathf.Abs(gyr) < MembraneThickness;
                }
                default:
                    return false;
            }
        }

        static bool WorldPrismFree(SwitchbackFeatureLayout layout, IReadOnlyList<RaceGate> course, Vector3 centre,
                                  float clearRadius, int gate, int station, Vector3 p, float h, List<Keepout> keepouts,
                                  Dictionary<long, List<(int prism, int station)>> accepted)
        {
            if (!PointClear(course, centre, clearRadius, p, h, WorldLineClearance, gate - 1, -1)) return false;
            if (Blocked(keepouts, p, h)) return false;
            // Other stations' world prisms, by bounding sphere. A station's own lattice never overlaps itself
            // (every pitch exceeds its cube), so only another station's prisms are tested.
            int cx = Mathf.FloorToInt(p.x / AcceptCell), cy = Mathf.FloorToInt(p.y / AcceptCell), cz = Mathf.FloorToInt(p.z / AcceptCell);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!accepted.TryGetValue(CellKey(cx + dx, cy + dy, cz + dz), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].station == station) continue;
                    var o = layout.Prisms[list[i].prism];
                    float r = o.Scale.x * 0.8660254f + h + 0.5f;
                    if ((o.Position - p).sqrMagnitude < r * r) return false;
                }
            }
            return true;
        }

        /// <summary>Records the prism about to be added at <paramref name="p"/> in the accept grid.</summary>
        static void Accept(SwitchbackFeatureLayout layout, Dictionary<long, List<(int prism, int station)>> accepted,
                           Vector3 p, int station)
        {
            long key = CellKey(Mathf.FloorToInt(p.x / AcceptCell), Mathf.FloorToInt(p.y / AcceptCell), Mathf.FloorToInt(p.z / AcceptCell));
            if (!accepted.TryGetValue(key, out var mine)) accepted[key] = mine = new List<(int, int)>(8);
            mine.Add((layout.Prisms.Count, station));
        }

        static bool TryWorldPrism(SwitchbackFeatureLayout layout, IReadOnlyList<RaceGate> course, Vector3 centre,
                                  float clearRadius, int gate, int station, Vector3 p, float h, List<Keepout> keepouts,
                                  Dictionary<long, List<(int prism, int station)>> accepted)
        {
            if (!WorldPrismFree(layout, course, centre, clearRadius, gate, station, p, h, keepouts, accepted)) return false;
            Accept(layout, accepted, p, station);
            return true;
        }

        /// <summary>The accept grid's cell: at least the largest pair of bounding spheres (2 x 22 x 0.866 = 38.1).</summary>
        const float AcceptCell = 40f;

        static long CellKey(int x, int y, int z) =>
            ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);

        static bool SweepBlocked(List<Keepout> keepouts, Vector3 lo, Vector3 hi, Vector3 reach, float h)
        {
            // Sample the swept rectangle densely enough that a 7 u sphere cannot slip between samples.
            for (int a = 0; a <= 4; a++)
            for (int b = 0; b <= 8; b++)
            {
                Vector3 p = Vector3.Lerp(lo, hi, a / 4f) + reach * (b / 4f - 1f);
                if (Blocked(keepouts, p, h)) return true;
            }
            return false;
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
