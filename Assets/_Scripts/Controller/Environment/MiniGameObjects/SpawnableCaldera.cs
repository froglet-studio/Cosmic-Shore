using System;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Caldera" - the fire-and-stone cell environment and the rotation's one danger-led biome.
    /// A THING IN SPACE, not a landscape: there is no ground plane and no world up anywhere in
    /// this file. FOUR floating volcanic massifs hang at the vertices of a roughly regular
    /// tetrahedron around the cell NUCLEUS, each aimed inward - broad shield base outward,
    /// crater mouth facing the core - so the cell's only "down" is the radial pull toward the
    /// nucleus, and every family is authored in a per-massif radial frame.
    ///
    /// Each massif drains inward: spillways run down its flanks INTO its vent, the vent drips a
    /// molten curtain across the gap, and the four curtains land on a shared magma crust riding
    /// the nucleus shell - impact basins joined by great-circle magma rivers. Six obsidian
    /// knife-arcs span the tetrahedron's six edges and tie the massifs together; basalt column
    /// collars stand off each outer face (the weave-through slalom terrain); an ember shell
    /// drifts in the voids between.
    ///
    /// The four are deliberately different creatures - silhouette (shingled / terraced / fluted /
    /// shattered), activity (erupting / degassing / collapsed / cooled), stone palette, girth,
    /// reach, roll, and chirality all vary, so no two read as the same mountain. Basalt blue +
    /// fire ruby + sulfur/ember gold.
    ///
    /// <b>Every structure is laid FLUSH</b> (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10): a chain prism is as long as the room its neighbours on the line leave it, a ring
    /// plate runs only as far as its ring neighbours leave it, and where two families cross, the
    /// skin yields to the structure on the one axis that keeps the most of it - from geometry
    /// computed once per generation from indices, so the prism count, the draw stream and the
    /// positions are the authored ones (see "Flush fitting" at the bottom of the file, and its
    /// note on what is NOT flush yet: the scattered families).
    ///
    /// <b>The crust stays OUTSIDE the nucleus.</b> <see cref="NucleusR"/> is the cell's
    /// node-control radius; nothing here is laid inside it. That keeps the territorial claim
    /// unclaimed at boot (an authored environment inside the nucleus hands DominantDomain to
    /// whichever colour it happens to favour - the shipped landscape put 89% of its mass in
    /// there) and keeps true-danger mass out of the fauna sanctuary. See Docs/ECOSYSTEM.md
    /// §13 + §18.
    /// </summary>
    public class SpawnableCaldera : CellEnvironmentSpawnableBase
    {
        // ── Shell geometry (every radius is distance from the cell centre) ──
        //
        // NucleusR is Nucleus.prefab's world radius (localScale 400 x the Node mesh's ~0.98 unit
        // radius) - the same figure Cell.RefreshNucleusControlRadius derives from the renderer
        // bounds. Everything below is laid outside it, crust included.
        const float NucleusR = 392f;
        const float CrustClearance = 16f;      // margin the crust keeps off the control zone
        const float FallDrop = 40f;            // vent mouth -> crust: the height of every curtain
        const float CrustR = NucleusR + CrustClearance; // the cell's only "floor"
        const float VentR = CrustR + FallDrop; // crater mouth: each massif's innermost point
        const float ArcR = 860f;               // apex of the six tetrahedron-edge obsidian arcs
        const float MassifLength = 304f;       // vent -> outer rim, before each massif's Reach

        /// <summary>Flank sampling spacing AND plate footprint both scale by this. Using ONE factor
        /// for both holds surface COVERAGE exactly constant (count x footprint / area = 1) while a
        /// doubled massif pays ~1.9x the prisms for 4x the area instead of 4x. Raise it for a
        /// coarser, holier mountain; lower it for a denser one.</summary>
        const float PlateDetail = 1.45f;

        /// <summary>A flank plate: broad axes scale with <see cref="PlateDetail"/>, thickness never
        /// does (a bigger mountain has bigger slabs, not thicker ones).</summary>
        static Vector3 Plate(float x, float y, float thickness) =>
            new(x * PlateDetail, y * PlateDetail, thickness);

        protected override int DefaultSeed => 53;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableCaldera), 5);

        /// <summary>Massif silhouette - the four are built by genuinely different rules, not one
        /// cone with different numbers.</summary>
        enum FlankStyle { Shingled = 0, Terraced = 1, Fluted = 2, Shattered = 3 }

        /// <summary>How alive the mouth is - sets the danger budget and what the vent does.</summary>
        enum VentState { Erupting = 0, Degassing = 1, Collapsed = 2, Cooled = 3 }

        readonly struct Spec
        {
            public readonly float Girth;      // cross-section radius at the outer rim
            public readonly float Reach;      // multiplier on the vent->rim length
            public readonly float Roll;       // rotation of the massif's own tangent basis (radians)
            public readonly float Chirality;  // +/-1: which way its spirals and shingle offsets wind
            public readonly FlankStyle Flank;
            public readonly VentState Vent;
            public readonly Domains Stone;    // dominant structural colour
            public readonly Domains Trim;     // capping / accent colour

            public Spec(float girth, float reach, float roll, float chirality,
                FlankStyle flank, VentState vent, Domains stone, Domains trim)
            {
                Girth = girth; Reach = reach; Roll = roll; Chirality = chirality;
                Flank = flank; Vent = vent; Stone = stone; Trim = trim;
            }
        }

        static readonly Spec[] Specs =
        {
            // The forge proper: a shingled basalt cone in full eruption - thickest curtain,
            // widest impact basin, the cell's signature.
            new(184f, 1.12f, 0.0f, 1f, FlankStyle.Shingled, VentState.Erupting, Domains.Blue, Domains.Ruby),
            // The sulfur works: a stepped ziggurat venting gas, not lava - treads crusted gold,
            // fumarole chimneys ringing the mouth under shielded caps.
            new(152f, 0.90f, 1.10f, -1f, FlankStyle.Terraced, VentState.Degassing, Domains.Gold, Domains.Blue),
            // The collapsed caldera: organ-pipe fluting down a broad mouth that fell in on
            // itself, ringed by secondary vents each dripping its own thin fall.
            new(172f, 1.00f, 2.30f, 1f, FlankStyle.Fluted, VentState.Collapsed, Domains.Blue, Domains.Gold),
            // The dead one: a shattered obsidian massif, glass tongue frozen mid-pour, a
            // super-shielded heart where the melt set. Almost no danger - the safe approach.
            new(140f, 0.84f, 3.40f, -1f, FlankStyle.Shattered, VentState.Cooled, Domains.Ruby, Domains.Blue),
        };

        /// <summary>Tetrahedron vertex directions (normalised in <see cref="BuildFrames"/>).</summary>
        static readonly Vector3[] TetraAxes =
        {
            new(1f, 1f, 1f), new(1f, -1f, -1f), new(-1f, 1f, -1f), new(-1f, -1f, 1f),
        };

        /// <summary>A massif's radial working frame: <c>Ax</c> is the outward radial (its axis,
        /// pointing away from the nucleus), <c>U</c>/<c>V</c> span the plane across it.</summary>
        readonly struct Frame
        {
            public readonly Vector3 Ax, U, V;
            public readonly float Length, Rho0, Rho1;

            public Frame(Vector3 ax, Vector3 u, Vector3 v, float length, float rho0, float rho1)
            {
                Ax = ax; U = u; V = v; Length = length; Rho0 = rho0; Rho1 = rho1;
            }

            /// <summary>Point <paramref name="radial"/> out along the axis, offset
            /// <paramref name="rho"/> across it at angle <paramref name="theta"/>.</summary>
            public Vector3 At(float radial, float rho, float theta) =>
                Ax * radial + (U * Mathf.Cos(theta) + V * Mathf.Sin(theta)) * rho;

            /// <summary>Unit vector pointing away from the axis at <paramref name="theta"/>.</summary>
            public Vector3 Out(float theta) => U * Mathf.Cos(theta) + V * Mathf.Sin(theta);

            /// <summary>Unit vector running around the axis at <paramref name="theta"/>.</summary>
            public Vector3 Around(float theta) => V * Mathf.Cos(theta) - U * Mathf.Sin(theta);

            /// <summary>Outward surface normal of the cone flank. It tilts toward the vent with the
            /// slope, so shingled plates lie ON the slope instead of standing off it.</summary>
            public Vector3 Normal(float theta) =>
                (Out(theta) * Length - Ax * (Rho1 - Rho0)).normalized;

            /// <summary>Radial distance of the outer rim.</summary>
            public float RimR => VentR + Length;
        }

        Frame[] _frames;

        protected override void BuildEnvironment()
        {
            BuildFrames();
            BuildCaches();

            for (int k = 0; k < Specs.Length; k++)
            {
                ref readonly Spec s = ref Specs[k];
                Frame f = _frames[k];
                BuildFlank(s, f, k);
                BuildCraterRim(s, f, k);
                BuildSpillways(s, f, k);
                BuildVent(s, f, k);
                BuildFumaroles(s, f, k);
                BuildColumnCollar(s, f, k);
                BuildEmberPlumes(s, f, k);
            }

            BuildCoreCrust();
            BuildTetraArcs();
            BuildEmberShell();
            _m = null;
            _river = _basin = _arc = null;
            _runA0 = _runSide = _runPerp = null;
            _runSpan = null;
        }



        // =====================================================================
        //  Frames - "roughly" tetrahedral
        // =====================================================================

        void BuildFrames()
        {
            _frames = new Frame[Specs.Length];
            for (int k = 0; k < Specs.Length; k++)
            {
                ref readonly Spec s = ref Specs[k];

                // ROUGHLY tetrahedral: each axis is nudged a few degrees off true so the four read
                // as siblings that grew where they landed, not as a machined lattice.
                var jitter = new Vector3(Hash01(k * 31 + _noiseSeed) - 0.5f,
                    Hash01(k * 57 + _noiseSeed) - 0.5f, Hash01(k * 91 + _noiseSeed) - 0.5f);
                var ax = (TetraAxes[k].normalized + jitter * 0.13f).normalized;

                // Any stable perpendicular, then rolled by the massif's own angle so their flutes,
                // spillways, and collars never line up with each other.
                var reference = Mathf.Abs(ax.y) > 0.9f ? Vector3.forward : Vector3.up;
                var u0 = Vector3.Cross(reference, ax).normalized;
                var v0 = Vector3.Cross(ax, u0);
                var u = u0 * Mathf.Cos(s.Roll) + v0 * Mathf.Sin(s.Roll);
                var v = Vector3.Cross(ax, u);

                float mouth = s.Vent == VentState.Collapsed ? 100f : 44f; // a collapsed mouth is wide
                _frames[k] = new Frame(ax, u, v, MassifLength * s.Reach, mouth, s.Girth);
            }
        }

        /// <summary>Where a point sits in massif <paramref name="k"/>'s frame: its fraction
        /// <paramref name="t"/> of the way from the vent to the rim, its bearing
        /// <paramref name="a"/> around the axis and its distance <paramref name="rho"/> off it.</summary>
        void Locate(int k, Vector3 p, out float t, out float a, out float rho)
        {
            Frame f = _frames[k];
            float axial = Vector3.Dot(p, f.Ax);
            float u = Vector3.Dot(p, f.U), v = Vector3.Dot(p, f.V);
            t = (axial - VentR) / f.Length;
            a = Mathf.Atan2(v, u);
            rho = Mathf.Sqrt(u * u + v * v);
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;

        // =====================================================================
        //  Fitted geometry, computed once
        // =====================================================================
        //
        // Every prism another family looks at - a ring plate's neighbours, the furniture a flank
        // plate yields to, the falls a crust plate yields to - has its pose and fitted (bound)
        // size worked out ONCE per generation, from its indices, into these arrays. A fit then
        // reads its neighbours by index instead of re-deriving them, so it costs a few box tests
        // per prism. They live only while BuildEnvironment runs.

        readonly struct Box
        {
            public readonly Vector3 P;
            public readonly Quaternion R;
            public readonly Pose A;     // R's axes, worked out once
            public readonly Vector3 S;
            public Box(Vector3 p, Quaternion r, Vector3 s) { P = p; R = r; A = new Pose(r); S = s; }
        }

        sealed class Massif
        {
            public Box[] Ring;          // flank plates: shingle rings / tread lanes / groove rings / shards
            public bool[] Laid;
            public bool[] Done;         // filled on first use: each plate's pose is worked out once
            public int[] RingAt;        // start of each ring (tread lane = step * 2 + lane)
            public Box[] Pipe;          // fluted: [fl * PipePrisms + i]
            public Box[] Teeth;
            public Box[] Spill;         // [lane * SpillwayPrisms + i]
            public float[] SpillA;
            public Vector3 Ax;          // the massif's axis and rim, for the cheap where-am-I tests
            public float RimR;
            public float[] FumAxial, FumReachAxial;
            public Vector3[] FumSeat, FumN;
            public float[] FumA;
            public int[] FumH;
            public Vector3[] CollarSeat;
            public float[] CollarA;
            public int[] CollarCols;
            public Box[] Column;        // [cl * 12 + co]: a whole column, courses and cap, as one box
            public Vector3[] ColumnOff;
            public int[] ColumnH;
            public Vector2[] ColumnFoot;
            public Box[][] Fall;        // [which][strand * FallPrisms + i], null where there is none
            public float[] FallReach;   // how far each fall strays off its mouth's axis
            public Vector3[] FallMid;   // the middle of each fall's drop (its bounding sphere)
            public Box[] Stack;         // collapsed: [sv * 9 + h]
        }

        Massif[] _m;
        Box[] _river, _basin, _arc;
        Vector3[] _runA0, _runSide, _runPerp;
        float[] _runSpan;

        // One chain's poses at a time, each worked out once, then fitted from each other.
        readonly Vector3[] _chainP = new Vector3[128];
        readonly Quaternion[] _chainR = new Quaternion[128];
        readonly Pose[] _chainA = new Pose[128];

        void ChainPose(int i, Vector3 p, Quaternion r) { _chainP[i] = p; _chainR[i] = r; _chainA[i] = new Pose(r); }

        /// <summary>Chain prism <paramref name="i"/> of <paramref name="n"/> (poses in the
        /// scratch): as long as its two neighbours leave it room for, never under the floor.</summary>
        float ChainLength(int i, int n, Vector3 size)
        {
            float len = size.z;
            if (i > 0) len = Mathf.Min(len, Room(_chainP[i - 1], _chainA[i - 1], size, _chainP[i], _chainA[i], size));
            if (i < n - 1) len = Mathf.Min(len, Room(_chainP[i], _chainA[i], size, _chainP[i + 1], _chainA[i + 1], size));
            return Mathf.Max(len, MinAxis);
        }

        static int Run(int i, int j) => i == 0 ? j - 1 : i == 1 ? j + 1 : 5;

        void BuildCaches()
        {
            _arc = new Box[6 * ArcSteps];
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                    for (int step = 0; step < ArcSteps; step++)
                        _arc[Run(i, j) * ArcSteps + step] = new Box(ComputeArcPos(i, j, step, out var r), r, ArcSize);

            _m = new Massif[Specs.Length];
            for (int k = 0; k < Specs.Length; k++)
            {
                ref readonly Spec s = ref Specs[k];
                var m = _m[k] = new Massif();

                int teeth = ToothCount(k);
                m.Teeth = new Box[teeth];
                for (int i = 0; i < teeth; i++) m.Teeth[i] = new Box(ComputeToothPos(k, i, out var r), r, ToothSize);

                int lanes = Lanes(k);
                m.Spill = new Box[lanes * SpillwayPrisms];
                m.SpillA = new float[lanes * SpillwayPrisms];
                for (int lane = 0; lane < lanes; lane++)
                    for (int i = 0; i < SpillwayPrisms; i++)
                    {
                        var p = ComputeSpillPos(k, lane, i, out var r, out float a);
                        // Where two lanes wander across each other, the later one's runnel is laid
                        // over the earlier one's - moved by the least that clears it (no cut can
                        // clear a centre laid inside the other runnel).
                        Pose ax = r;
                        for (int other = 0; other < lane; other++)
                            for (int j = Mathf.Max(0, i - 1); j <= Mathf.Min(SpillwayPrisms - 1, i + 1); j++)
                            {
                                ref readonly Box o = ref m.Spill[other * SpillwayPrisms + j];
                                if ((o.P - p).sqrMagnitude > Sq(SpillSize.magnitude)) continue;
                                Clear(SpillSize, p, ax, o.P, o.A, o.S, out bool fits);
                                if (!fits) p += Escape(SpillSize, p, ax, o.P, o.A, o.S, Vector3.zero);
                            }
                        m.Spill[lane * SpillwayPrisms + i] = new Box(p, r, SpillSize);
                        m.SpillA[lane * SpillwayPrisms + i] = a;
                    }

                int clusters = FumaroleClusters(k);
                m.FumSeat = new Vector3[clusters]; m.FumN = new Vector3[clusters];
                m.FumA = new float[clusters]; m.FumH = new int[clusters];
                m.Ax = _frames[k].Ax;
                m.RimR = _frames[k].RimR;
                m.FumAxial = new float[clusters]; m.FumReachAxial = new float[clusters];
                for (int c = 0; c < clusters; c++)
                {
                    m.FumSeat[c] = ComputeFumaroleSeat(k, c, out m.FumA[c], out m.FumN[c], out m.FumH[c]);
                    // The chimney's span along the massif axis (it stands along the slope normal).
                    float tall = m.FumH[c] * 2.5f + 3.1f;
                    float lean = Vector3.Dot(m.FumN[c], m.Ax);
                    m.FumAxial[c] = Vector3.Dot(m.FumSeat[c], m.Ax) + 0.5f * tall * lean;
                    m.FumReachAxial[c] = 0.5f * tall * Mathf.Abs(lean) + 6f;
                }

                m.CollarSeat = new Vector3[CollarClusters]; m.CollarA = new float[CollarClusters];
                m.CollarCols = new int[CollarClusters];
                m.Column = new Box[CollarClusters * 12]; m.ColumnOff = new Vector3[CollarClusters * 12];
                m.ColumnH = new int[CollarClusters * 12]; m.ColumnFoot = new Vector2[CollarClusters * 12];
                for (int cl = 0; cl < CollarClusters; cl++)
                {
                    ComputeCollarCluster(k, cl, out m.CollarSeat[cl], out m.CollarA[cl], out m.CollarCols[cl]);
                    for (int co = 0; co < m.CollarCols[cl]; co++)
                        m.ColumnOff[cl * 12 + co] = ComputeCollarOffset(k, cl, co, m.CollarCols[cl], m.CollarA[cl], out m.ColumnH[cl * 12 + co]);
                }
                for (int cl = 0; cl < CollarClusters; cl++)
                {
                    var crot = SpawnPoint.LookRotation(_frames[k].Ax, _frames[k].Around(m.CollarA[cl]));
                    Pose cax = crot;
                    for (int co = 0; co < m.CollarCols[cl]; co++)
                    {
                        var foot = ComputeCollarFootprint(k, cl, co);
                        // Two neighbouring bundles can stand close enough to touch: a column of
                        // the later bundle narrows (never shortens) against the earlier bundle's.
                        float bottom0 = -0.5f * CollarColumn.z * (1f + CollarJit);
                        float top0 = m.ColumnH[cl * 12 + co] * CollarPitch + 0.5f * CollarCapSize.z;
                        var at = m.CollarSeat[cl] + m.ColumnOff[cl * 12 + co] + _frames[k].Ax * (0.5f * (bottom0 + top0));
                        var whole = new Vector3(foot.x, foot.y, top0 - bottom0);
                        for (int dc = 1; dc <= 2; dc++)
                        {
                            int other = cl == CollarClusters - 1 && dc == 2 ? 0 : cl - (dc == 1 ? 1 : 99);
                            if (other < 0) continue;
                            for (int oc = 0; oc < m.CollarCols[other]; oc++)
                            {
                                ref readonly Box ob = ref m.Column[other * 12 + oc];
                                if (other > cl && ob.S == default) continue;
                                whole = Clear(whole, at, cax, ob.P, ob.A, ob.S, out _, true, 2);
                            }
                        }
                        foot = m.ColumnFoot[cl * 12 + co] = new Vector2(whole.x, whole.y);
                        float bottom = -0.5f * CollarColumn.z * (1f + CollarJit);
                        float top = m.ColumnH[cl * 12 + co] * CollarPitch + 0.5f * CollarCapSize.z;
                        m.Column[cl * 12 + co] = new Box(m.CollarSeat[cl] + m.ColumnOff[cl * 12 + co] + _frames[k].Ax * (0.5f * (bottom + top)),
                            crot, new Vector3(foot.x, foot.y, top - bottom));
                    }
                }

                m.Fall = new Box[SubVents + 1][];
                m.FallReach = new float[SubVents + 1];
                m.FallMid = new Vector3[SubVents + 1];
                for (int which = 0; which <= SubVents; which++)
                {
                    if (!FallSpec(k, which, out var mouth, out int salt, out int strands, out float spread, out _)) continue;
                    var boxes = m.Fall[which] = new Box[strands * FallPrisms];
                    m.FallReach[which] = spread + FallSize.magnitude;
                    m.FallMid[which] = mouth - _frames[k].Ax * (0.5f * (VentR - CrustR + 2f));
                    for (int strand = 0; strand < strands; strand++)
                    {
                        float a0 = 2f * Mathf.PI * strand / strands + salt * 0.7f;
                        var prev = mouth;
                        for (int i = 0; i < FallPrisms; i++)
                        {
                            var fp = FallPoint(k, mouth, salt, strands, spread, strand, i);
                            ChainPose(i, fp, SpawnPoint.LookRotation(fp - prev, _frames[k].Out(a0 + (i + 1f) / FallPrisms * 2.6f)));
                            prev = fp;
                        }
                        // A curtain drop is as long as the room the strand leaves it: the curtain
                        // steps ~1.3 while its drops are 3.2 long, so they shorten to touch.
                        for (int i = 0; i < FallPrisms; i++)
                            boxes[strand * FallPrisms + i] = new Box(_chainP[i], _chainR[i],
                                new Vector3(FallSize.x, FallSize.y, ChainLength(i, FallPrisms, FallSize)));
                    }
                }
                if (s.Vent == VentState.Collapsed)
                {
                    m.Stack = new Box[SubVents * 9];
                    for (int sv = 0; sv < SubVents; sv++)
                        for (int h = 0; h < 9; h++)
                            m.Stack[sv * 9 + h] = new Box(StackPos(k, sv, h, out var r), r, ComputeStackFit(k, sv, h));
                }

                // The flank plates' poses are worked out on first use (RingBox): a ring as it is laid,
                // and a plate another family asks about when it asks.
                switch (s.Flank)
                {
                    case FlankStyle.Shingled:
                        Rings(m, ShingleRings);
                        for (int L = 0; L < ShingleRings; L++) m.RingAt[L + 1] = m.RingAt[L] + ShingleCount(k, L);
                        Fill(m, ShingleRings);
                        break;
                    case FlankStyle.Terraced:
                        Rings(m, TerraceSteps * 2);
                        for (int id = 0; id < TerraceSteps * 2; id++) m.RingAt[id + 1] = m.RingAt[id] + TreadCount(k, id / 2, id % 2);
                        Fill(m, TerraceSteps * 2);
                        break;
                    case FlankStyle.Fluted:
                        Rings(m, GrooveRings);
                        for (int L = 0; L < GrooveRings; L++) m.RingAt[L + 1] = m.RingAt[L] + GrooveCount(k, L);
                        Fill(m, GrooveRings);
                        m.Pipe = new Box[Flutes * PipePrisms];
                        for (int fl = 0; fl < Flutes; fl++)
                        {
                            var prev = PipePoint(k, fl, -1);
                            for (int i = 0; i < PipePrisms; i++)
                            {
                                var pp = PipePoint(k, fl, i);
                                float a = 2f * Mathf.PI * fl / Flutes + 0.22f * (i / (PipePrisms - 1f)) * s.Chirality;
                                var pr = SpawnPoint.LookRotation(pp - prev, _frames[k].Normal(a));
                                prev = pp;
                                PlaceAmongFurniture(k, PipeSize, ref pp, pr);
                                ChainPose(i, pp, pr);
                            }
                            for (int i = 1; i < PipePrisms; i++)
                                m.Pipe[fl * PipePrisms + i] = new Box(_chainP[i], _chainR[i],
                                    new Vector3(PipeSize.x, PipeSize.y, ChainLength(i, PipePrisms, PipeSize)));
                            // The first course faces OUT of the mouth (the rest run up the flute),
                            // so a length budget does not describe it: it is cut to touch the second.
                            ref readonly Box second = ref m.Pipe[fl * PipePrisms + 1];
                            m.Pipe[fl * PipePrisms] = new Box(_chainP[0], _chainR[0],
                                Clear(PipeSize, _chainP[0], _chainA[0], second.P, second.A, second.S));
                        }
                        break;
                    default:
                        Rings(m, 1);
                        m.RingAt[1] = Shards;
                        Fill(m, 1);
                        break;
                }
            }

            _runA0 = new Vector3[6]; _runSide = new Vector3[6]; _runPerp = new Vector3[6]; _runSpan = new float[6];
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                {
                    int run = Run(i, j);
                    _runA0[run] = _frames[i].Ax;
                    _runSide[run] = Vector3.Cross(_frames[i].Ax, _frames[j].Ax).normalized;
                    _runPerp[run] = Vector3.Cross(_runSide[run], _frames[i].Ax);
                    _runSpan[run] = Vector3.Angle(_frames[i].Ax, _frames[j].Ax) * Mathf.Deg2Rad;
                }
            _river = new Box[6 * RiverSteps * 3];
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                    for (int step = 0; step < RiverSteps; step++)
                        for (int b = -1; b <= 1; b++)
                            _river[(Run(i, j) * RiverSteps + step) * 3 + b + 1] =
                                new Box(RiverPos(i, j, step, b, out var r), r, b == 0 ? RiverSize : BankSize);

            _basin = new Box[_frames.Length * 170];
            for (int k = 0; k < _frames.Length; k++)
            {
                Frame f = _frames[k];
                for (int i = 0; i < 170; i++)
                {
                    float u = Hash01(i * 13 + k * 601 + _noiseSeed);
                    float a = i * GoldenAngle;
                    float ang = 0.30f * Mathf.Sqrt(u); // angular radius of the basin on the shell
                    var dir = (f.Ax * Mathf.Cos(ang) + f.Out(a) * Mathf.Sin(ang)).normalized;
                    _basin[k * 170 + i] = new Box(dir * (CrustR + 1.5f * Mathf.Sin(a * 5f)), SpawnPoint.LookRotation(dir, f.Around(a)), BasinSize);
                }
            }
        }

        static void Rings(Massif m, int rings) => m.RingAt = new int[rings + 1];

        static void Fill(Massif m, int rings)
        {
            m.Ring = new Box[m.RingAt[rings]];
            m.Laid = new bool[m.RingAt[rings]];
            m.Done = new bool[m.RingAt[rings]];
        }

        ref readonly Box RingBox(int k, int ring, int i, out bool laid)
        {
            var m = _m[k];
            int n = m.RingAt[ring + 1] - m.RingAt[ring];
            i = Wrap(i, n);
            int at = m.RingAt[ring] + i;
            if (!m.Done[at])
            {
                m.Done[at] = true;
                Vector3 p;
                Quaternion r;
                switch (Specs[k].Flank)
                {
                    case FlankStyle.Shingled: p = ComputeShinglePos(k, ring, i, out r, out m.Laid[at]); break;
                    case FlankStyle.Terraced: p = ComputeTreadPos(k, ring / 2, ring % 2, i, out r, out m.Laid[at]); break;
                    case FlankStyle.Fluted: p = ComputeGroovePos(k, ring, i, out r, out m.Laid[at]); break;
                    default: p = ComputeShardPos(k, i, out r, out m.Laid[at]); break;
                }
                // A blown-through hole lays nothing, so nothing needs its pose.
                if (m.Laid[at])
                {
                    Pose ax = r;
                    var bound = FlankBound(k);
                    PlaceAmongFurniture(k, bound, ref p, ax);
                    m.Ring[at] = new Box(p, r, default);
                }
            }
            laid = m.Laid[at];
            return ref m.Ring[at];
        }

        /// <summary>Massif <paramref name="k"/>'s flank plate at the jitter's largest.</summary>
        static Vector3 FlankBound(int k) => Specs[k].Flank switch
        {
            FlankStyle.Shingled => ShinglePlate * (1f + ShingleJit),
            FlankStyle.Terraced => TreadPlate * (1f + TreadJit),
            FlankStyle.Fluted => GroovePlate * (1f + GrooveJit),
            _ => ShardPlate * (1f + ShardJit),
        };
        // =====================================================================
        //  The massif bodies - four different mountains
        // =====================================================================
        //
        // Every flank plate is fitted where it is laid: to the room its ring neighbours leave it,
        // and then - yielding to the massif's furniture - to the crater teeth, the spillways, the
        // fumaroles, the column collar and the knife-arcs it would otherwise pass through
        // (ClearOfFurniture). A plate is the one that yields because it is the one that a cut costs
        // least: the furniture is the readable structure, the plates are the skin under it.

        void BuildFlank(in Spec s, in Frame f, int k)
        {
            switch (s.Flank)
            {
                case FlankStyle.Shingled: BuildShingledFlank(s, f, k); break;
                case FlankStyle.Terraced: BuildTerracedFlank(s, f, k); break;
                case FlankStyle.Fluted: BuildFlutedFlank(s, f, k); break;
                default: BuildShatteredFlank(s, f, k); break;
            }
        }

        const int ShingleRings = 40;
        static readonly Vector3 ShinglePlate = Plate(4.2f, 2.6f, 1.4f);
        const float ShingleJit = 0.25f;

        int ShingleCount(int k, int L)
        {
            Frame f = _frames[k];
            float rho = Mathf.Lerp(f.Rho0, f.Rho1, L / (ShingleRings - 1f));
            return Mathf.Max(6, (int)(2f * Mathf.PI * rho / (3.1f * PlateDetail)));
        }
        Vector3 ComputeShinglePos(int k, int L, int i, out Quaternion rot, out bool laid)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float t = L / (ShingleRings - 1f);
            float radial = VentR + f.Length * t;
            float rho = Mathf.Lerp(f.Rho0, f.Rho1, t);
            int n = ShingleCount(k, L);
            float a = 2f * Mathf.PI * i / n + L * 0.13f * s.Chirality;
            laid = N01(a * 2.6f, L * 0.7f + k * 5f, 3.3f, 2) >= 0.09f; // else a blown-through vent
            if (!laid) { rot = Quaternion.identity; return default; }
            float wob = 9f * N01(Mathf.Cos(a) * 4f, L * 0.5f + k * 3f, Mathf.Sin(a) * 4f, 1);
            rot = SpawnPoint.LookRotation(f.Normal(a), f.Around(a));
            return f.At(radial + 2f * Mathf.Sin(a * 5f + L), rho + wob, a);
        }

        /// <summary>Overlapping basalt plates in rings - the classic cone, shingled so the thin
        /// axis is the slope normal. A plate runs around the ring only as far as its two ring
        /// neighbours leave it room (at their widest), and up the slope only until it touches the
        /// ring below.</summary>
        void BuildShingledFlank(in Spec s, in Frame f, int k)
        {
            var bound = ShinglePlate * (1f + ShingleJit);
            for (int L = 0; L < ShingleRings; L++)
            {
                int n = ShingleCount(k, L);
                for (int i = 0; i < n; i++)
                {
                    ref readonly Box b = ref RingBox(k, L, i, out bool laid);
                    if (!laid) continue;
                    var pos = b.P;
                    var size = Jit(ShinglePlate, ShingleJit);
                    ref readonly Box prev = ref RingBox(k, L, i - 1, out bool pl);
                    ref readonly Box next = ref RingBox(k, L, i + 1, out bool nl);
                    if (pl) size.y = Mathf.Min(size.y, Room(prev.P, prev.A, bound, pos, b.A, bound, 1));
                    if (nl) size.y = Mathf.Min(size.y, Room(pos, b.A, bound, next.P, next.A, bound, 1));
                    if (L > 0)
                    {
                        // The ring below, at the three bearings nearest this plate.
                        int nb = ShingleCount(k, L - 1);
                        float ab = 2f * Mathf.PI * i / n + L * 0.13f * s.Chirality - (L - 1) * 0.13f * s.Chirality;
                        int j0 = Mathf.RoundToInt(ab / (2f * Mathf.PI) * nb);
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            ref readonly Box below = ref RingBox(k, L - 1, j0 + dj, out bool bl);
                            if (bl) size = Clear(size, pos, b.A, below.P, below.A, bound);
                        }
                    }
                    ClearOfFurniture(k, ref size, ref pos, b.A);
                    Emit(pos, b.R, size, s.Stone);
                }
            }
        }

        const int TerraceSteps = 8;
        static readonly Vector3 TreadPlate = Plate(4.4f, 3.4f, 1.2f);
        static readonly Vector3 RiserPlate = Plate(3.2f, 2.4f, 1.3f);
        const float TreadJit = 0.2f, RiserJit = 0.15f;

        int TreadCount(int k, int step, int lane)
        {
            Frame f = _frames[k];
            float rhoIn = Mathf.Lerp(f.Rho0, f.Rho1, step / (float)TerraceSteps);
            float rhoOut = Mathf.Lerp(f.Rho0, f.Rho1, (step + 1f) / TerraceSteps);
            float rho = Mathf.Lerp(rhoIn, rhoOut, 0.34f + lane * 0.42f);
            return Mathf.Max(8, (int)(2f * Mathf.PI * rho / (3.0f * PlateDetail)));
        }
        Vector3 ComputeTreadPos(int k, int step, int lane, int i, out Quaternion rot, out bool laid)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float t0 = step / (float)TerraceSteps, t1 = (step + 1f) / TerraceSteps;
            float radialIn = VentR + f.Length * t0;
            float rho = Mathf.Lerp(Mathf.Lerp(f.Rho0, f.Rho1, t0), Mathf.Lerp(f.Rho0, f.Rho1, t1), 0.34f + lane * 0.42f);
            int n = TreadCount(k, step, lane);
            float a = 2f * Mathf.PI * i / n + step * 0.2f * s.Chirality;
            laid = N01(a * 3.1f, step * 1.3f + k * 4f, lane * 2.7f, 4) >= 0.12f;
            if (!laid) { rot = Quaternion.identity; return default; }
            rot = SpawnPoint.LookRotation(-f.Ax, f.Around(a));
            return f.At(radialIn, rho, a);
        }

        int RiserCount(int k, int step)
        {
            Frame f = _frames[k];
            float rhoOut = Mathf.Lerp(f.Rho0, f.Rho1, (step + 1f) / TerraceSteps);
            return Mathf.Max(8, (int)(2f * Mathf.PI * rhoOut / (3.7f * PlateDetail)));
        }

        Vector3 ComputeRiserPos(int k, int step, int i, int h, out Quaternion rot)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float rise = f.Length / TerraceSteps;
            float radialIn = VentR + f.Length * (step / (float)TerraceSteps);
            float rhoOut = Mathf.Lerp(f.Rho0, f.Rho1, (step + 1f) / TerraceSteps);
            float a = 2f * Mathf.PI * i / RiserCount(k, step) + step * 0.2f * s.Chirality;
            rot = SpawnPoint.LookRotation(f.Out(a), f.Ax);
            return f.At(radialIn + (h + 0.5f) * rise / 3f, rhoOut, a);
        }

        /// <summary>A stepped ziggurat: flat treads crusted with sulfur, stone risers between. A
        /// tread runs around its lane only as far as its lane neighbours leave it, and the outer
        /// lane's plates stop where they meet the inner lane's.</summary>
        void BuildTerracedFlank(in Spec s, in Frame f, int k)
        {
            var bound = TreadPlate * (1f + TreadJit);
            for (int step = 0; step < TerraceSteps; step++)
            {
                // Tread: a flat annulus facing the core, two plates deep.
                for (int lane = 0; lane < 2; lane++)
                {
                    int n = TreadCount(k, step, lane);
                    for (int i = 0; i < n; i++)
                    {
                        int ring = step * 2 + lane;
                        ref readonly Box b = ref RingBox(k, ring, i, out bool laid);
                        if (!laid) continue;
                        var pos = b.P;
                        var size = Jit(TreadPlate, TreadJit);
                        ref readonly Box prev = ref RingBox(k, ring, i - 1, out bool pl);
                        ref readonly Box next = ref RingBox(k, ring, i + 1, out bool nl);
                        if (pl) size.y = Mathf.Min(size.y, Room(prev.P, prev.A, bound, pos, b.A, bound, 1));
                        if (nl) size.y = Mathf.Min(size.y, Room(pos, b.A, bound, next.P, next.A, bound, 1));
                        if (lane == 1)
                        {
                            int n0 = TreadCount(k, step, 0);
                            int j0 = Mathf.RoundToInt((float)i / n * n0);
                            for (int dj = -2; dj <= 2; dj++)
                            {
                                ref readonly Box inner = ref RingBox(k, step * 2, j0 + dj, out bool il);
                                if (il) size = Clear(size, pos, b.A, inner.P, inner.A, bound);
                            }
                        }
                        ClearOfFurniture(k, ref size, ref pos, b.A);
                        Emit(pos, b.R, size, lane == 0 ? s.Stone : s.Trim);
                    }
                }

                // Riser: the wall climbing out to the next tread.
                int rn = RiserCount(k, step);
                for (int i = 0; i < rn; i++)
                {
                    for (int h = 0; h < 3; h++)
                    {
                        var pos = ComputeRiserPos(k, step, i, h, out var rot);
                        var size = Jit(RiserPlate, RiserJit);
                        Pose rax = rot;
                        PlaceAmongFurniture(k, size, ref pos, rax);
                        ClearOfFurniture(k, ref size, ref pos, rax);
                        Emit(pos, rot, size, s.Stone);
                    }
                }
            }
        }

        const int GrooveRings = 30;
        const int Flutes = 24;
        const int PipePrisms = 72;
        static readonly Vector3 GroovePlate = Plate(4.6f, 3f, 1.1f);
        static readonly Vector3 PipeSize = Plate(2.4f, 3.6f, 3.4f);
        const float GrooveJit = 0.2f;

        int GrooveCount(int k, int L)
        {
            Frame f = _frames[k];
            float rho = Mathf.Lerp(f.Rho0, f.Rho1, L / (GrooveRings - 1f)) - 1.5f;
            return Mathf.Max(6, (int)(2f * Mathf.PI * rho / (3.6f * PlateDetail)));
        }
        Vector3 ComputeGroovePos(int k, int L, int i, out Quaternion rot, out bool laid)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float t = L / (GrooveRings - 1f);
            float rho = Mathf.Lerp(f.Rho0, f.Rho1, t) - 1.5f;
            float a = 2f * Mathf.PI * i / GrooveCount(k, L) + L * 0.09f * s.Chirality;
            laid = N01(a * 2.2f, L * 0.9f + k * 6f, 2.2f, 5) >= 0.16f;
            if (!laid) { rot = Quaternion.identity; return default; }
            rot = SpawnPoint.LookRotation(f.Normal(a), f.Around(a));
            return f.At(VentR + f.Length * t, rho, a);
        }

        Vector3 PipePoint(int k, int fl, int i)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float a0 = 2f * Mathf.PI * fl / Flutes;
            if (i < 0) return f.At(VentR, f.Rho0, a0);
            float t = i / (PipePrisms - 1f);
            float a = a0 + 0.22f * t * s.Chirality; // flutes sweep as they climb out
            return f.At(VentR + f.Length * t, Mathf.Lerp(f.Rho0, f.Rho1, t) + 5.5f, a);
        }

        Quaternion PipeRot(int k, int fl, int i)
        {
            ref readonly Spec s = ref Specs[k];
            float a = 2f * Mathf.PI * fl / Flutes + 0.22f * (i / (PipePrisms - 1f)) * s.Chirality;
            return SpawnPoint.LookRotation(PipePoint(k, fl, i) - PipePoint(k, fl, i - 1), _frames[k].Normal(a));
        }

        Vector3 ComputePipeFit(int k, int fl, int i)
        {
            var p = PipePoint(k, fl, i);
            var r = PipeRot(k, fl, i);
            if (i == 0) return Clear(PipeSize, p, r, PipePoint(k, fl, 1), PipeRot(k, fl, 1), ComputePipeFit(k, fl, 1));
            var size = PipeSize;
            size.z = Mathf.Min(size.z, Room(PipePoint(k, fl, i - 1), PipeRot(k, fl, i - 1), PipeSize, p, r, PipeSize));
            if (i < PipePrisms - 1) size.z = Mathf.Min(size.z, Room(p, r, PipeSize, PipePoint(k, fl, i + 1), PipeRot(k, fl, i + 1), PipeSize));
            return Floor(size);
        }

        /// <summary>Organ-pipe fluting: a recessed groove-floor shell with heavy chains standing
        /// proud of it, running vent->rim ALONG the flutes (long axis on the line of flight).</summary>
        void BuildFlutedFlank(in Spec s, in Frame f, int k)
        {
            // Groove floor - the shell the pipes stand out of.
            var bound = GroovePlate * (1f + GrooveJit);
            for (int L = 0; L < GrooveRings; L++)
            {
                int n = GrooveCount(k, L);
                for (int i = 0; i < n; i++)
                {
                    ref readonly Box b = ref RingBox(k, L, i, out bool laid);
                    if (!laid) continue;
                    var pos = b.P;
                    var size = Jit(GroovePlate, GrooveJit);
                    ref readonly Box prev = ref RingBox(k, L, i - 1, out bool pl);
                    ref readonly Box next = ref RingBox(k, L, i + 1, out bool nl);
                    if (pl) size.y = Mathf.Min(size.y, Room(prev.P, prev.A, bound, pos, b.A, bound, 1));
                    if (nl) size.y = Mathf.Min(size.y, Room(pos, b.A, bound, next.P, next.A, bound, 1));
                    if (L > 0)
                    {
                        int nb = GrooveCount(k, L - 1);
                        int j0 = Mathf.RoundToInt((float)i / n * nb);
                        for (int dj = -2; dj <= 2; dj++)
                        {
                            ref readonly Box below = ref RingBox(k, L - 1, j0 + dj, out bool bl);
                            if (bl) size = Clear(size, pos, b.A, below.P, below.A, bound);
                        }
                    }
                    ClearOfFurniture(k, ref size, ref pos, b.A);
                    Emit(pos, b.R, size, s.Stone);
                }
            }

            for (int fl = 0; fl < Flutes; fl++)
            {
                for (int i = 0; i < PipePrisms; i++)
                {
                    ref readonly Box b = ref _m[k].Pipe[fl * PipePrisms + i];
                    var p = b.P;
                    var size = b.S;
                    ClearOfFurniture(k, ref size, ref p, b.A);
                    Emit(p, b.R, size, fl % 4 == 0 ? s.Trim : s.Stone);
                }
            }
        }

        const int Shards = 4560;
        static readonly Vector3 ShardPlate = Plate(6.4f, 4.8f, 1.2f);
        const float ShardJit = 0.35f;
        Vector3 ComputeShardPos(int k, int i, out Quaternion rot, out bool laid)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float t = (i + 0.5f) / Shards;
            float a = i * GoldenAngle * s.Chirality;
            laid = N01(Mathf.Cos(a) * 3f, t * 11f, Mathf.Sin(a) * 3f + k, 6) >= 0.24f;
            if (!laid) { rot = Quaternion.identity; return default; }
            float rho = Mathf.Lerp(f.Rho0, f.Rho1, t) + 13f * (Hash01(i * 7 + k * 101) - 0.5f);
            // Each shard is knocked off true by its own noise - a fracture field, not a shell.
            var tilt = (f.Normal(a) + f.Around(a) * (Hash01(i * 13 + k) - 0.5f) * 0.9f
                                    + f.Ax * (Hash01(i * 29 + k) - 0.5f) * 0.7f).normalized;
            rot = SpawnPoint.LookRotation(tilt, f.Around(a));
            return f.At(VentR + f.Length * t, rho, a);
        }

        /// <summary>A massif broken into big tilted glass plates with real holes in it -
        /// phyllotaxis over the cone surface instead of rings, so nothing reads as a course. The
        /// shards still lap EACH OTHER (see the note at the end of the file); they yield to the
        /// furniture.</summary>
        void BuildShatteredFlank(in Spec s, in Frame f, int k)
        {
            for (int i = 0; i < Shards; i++)
            {
                ref readonly Box b = ref RingBox(k, 0, i, out bool laid);
                if (!laid) continue;
                var pos = b.P;
                var size = Jit(ShardPlate, ShardJit);
                ClearOfFurniture(k, ref size, ref pos, b.A);
                Emit(pos, b.R, size, i % 5 == 0 ? s.Trim : s.Stone);
            }
        }

        /// <summary>Fit a box against massif <paramref name="k"/>'s flank plates near it (the
        /// rings, lanes or flutes at its fraction and bearing, at their widest) - what a vent
        /// disc and an ember ask.</summary>
        void ClearOfFlank(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            Locate(k, pos, out float t, out float a, out float rho);
            float reach = 0.5f * size.magnitude;
            if (t < -(reach + 30f) / f.Length || t > 1f + (reach + 30f) / f.Length) return;
            // Every plate lies within ~16 of the cone surface (a shingle's wobble, a shard's
            // offset, a pipe standing proud): a box further off it meets none.
            if (Mathf.Abs(rho - Mathf.Lerp(f.Rho0, f.Rho1, Mathf.Clamp01(t))) > reach + 22f) return;
            switch (s.Flank)
            {
                case FlankStyle.Shingled:
                {
                    var bound = ShinglePlate * (1f + ShingleJit);
                    int l0 = Mathf.RoundToInt(t * (ShingleRings - 1f));
                    for (int L = Mathf.Max(0, l0 - 1); L <= Mathf.Min(ShingleRings - 1, l0 + 1); L++)
                    {
                        int n = ShingleCount(k, L);
                        int j0 = Mathf.RoundToInt((a - L * 0.13f * s.Chirality) / (2f * Mathf.PI) * n);
                        for (int dj = -2; dj <= 2; dj++)
                        {
                            ref readonly Box b = ref RingBox(k, L, j0 + dj, out bool laid);
                            if (laid) Meet(ref size, ref pos, rot, b.P, b.A, bound);
                        }
                    }
                    break;
                }
                case FlankStyle.Terraced:
                {
                    var tb = TreadPlate * (1f + TreadJit);
                    var rb = RiserPlate * (1f + RiserJit);
                    int s0 = Mathf.FloorToInt(t * TerraceSteps);
                    for (int step = Mathf.Max(0, s0 - 1); step <= Mathf.Min(TerraceSteps - 1, s0 + 1); step++)
                    {
                        for (int lane = 0; lane < 2; lane++)
                        {
                            int n = TreadCount(k, step, lane);
                            int j0 = Mathf.RoundToInt((a - step * 0.2f * s.Chirality) / (2f * Mathf.PI) * n);
                            for (int dj = -2; dj <= 2; dj++)
                            {
                                ref readonly Box b = ref RingBox(k, step * 2 + lane, j0 + dj, out bool laid);
                                if (laid) Meet(ref size, ref pos, rot, b.P, b.A, tb);
                            }
                        }
                        int rn = RiserCount(k, step);
                        int r0 = Mathf.RoundToInt((a - step * 0.2f * s.Chirality) / (2f * Mathf.PI) * rn);
                        for (int dj = -2; dj <= 2; dj++)
                            for (int h = 0; h < 3; h++)
                            {
                                var rp = ComputeRiserPos(k, step, Wrap(r0 + dj, rn), h, out var rr);
                                Meet(ref size, ref pos, rot, rp, rr, rb);
                            }
                    }
                    break;
                }
                case FlankStyle.Fluted:
                {
                    var gb = GroovePlate * (1f + GrooveJit);
                    int l0 = Mathf.RoundToInt(t * (GrooveRings - 1f));
                    for (int L = Mathf.Max(0, l0 - 1); L <= Mathf.Min(GrooveRings - 1, l0 + 1); L++)
                    {
                        int n = GrooveCount(k, L);
                        int j0 = Mathf.RoundToInt((a - L * 0.09f * s.Chirality) / (2f * Mathf.PI) * n);
                        for (int dj = -2; dj <= 2; dj++)
                        {
                            ref readonly Box b = ref RingBox(k, L, j0 + dj, out bool laid);
                            if (laid) Meet(ref size, ref pos, rot, b.P, b.A, gb);
                        }
                    }
                    float tc = Mathf.Clamp01(t);
                    int f0 = Mathf.RoundToInt((a - 0.22f * tc * s.Chirality) / (2f * Mathf.PI) * Flutes);
                    int i0 = Mathf.RoundToInt(tc * (PipePrisms - 1f));
                    for (int df = -1; df <= 1; df++)
                        for (int i = Mathf.Max(0, i0 - 2); i <= Mathf.Min(PipePrisms - 1, i0 + 2); i++)
                        {
                            var b = _m[k].Pipe[Wrap(f0 + df, Flutes) * PipePrisms + i];
                            Meet(ref size, ref pos, rot, in b);
                        }
                    break;
                }
                default:
                {
                    // The shards are a SCATTER (see the note at the end of the file): no index names
                    // the shard nearest a point, and finding it would be a search - a box near the
                    // shattered flank is not fitted to it.
                    break;
                }
            }
        }

        // =====================================================================
        //  The mouth
        // =====================================================================

        static readonly Vector3 ToothSize = new(3f, 3f, 7.2f);

        int ToothCount(int k) => Specs[k].Vent == VentState.Collapsed ? 112 : 68;

        Vector3 ToothPos(int k, int i, out Quaternion rot)
        {
            rot = _m[k].Teeth[i].R;
            return _m[k].Teeth[i].P;
        }

        Vector3 ComputeToothPos(int k, int i, out Quaternion rot)
        {
            Frame f = _frames[k];
            float a = 2f * Mathf.PI * i / ToothCount(k);
            float lean = 6f + 6f * Mathf.Sin(a * 6f + k);
            rot = SpawnPoint.LookRotation(-f.Ax + f.Out(a) * 0.35f, f.Around(a));
            return f.At(VentR - lean, f.Rho0 + 2f, a);
        }

        /// <summary>Teeth around the crater lip, leaning inward over the drop.</summary>
        void BuildCraterRim(in Spec s, in Frame f, int k)
        {
            int n = ToothCount(k);
            for (int i = 0; i < n; i++)
                Emit(ToothPos(k, i, out var rot), rot, ToothSize, i % 3 == 0 ? s.Trim : s.Stone);
        }

        void ClearOfRim(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            Frame f = _frames[k];
            float reach = 0.5f * (size.magnitude + ToothSize.magnitude);
            float axial = Vector3.Dot(pos, f.Ax);
            if (axial < VentR - 12f - reach || axial > VentR + reach) return;
            Locate(k, pos, out _, out float a, out float rho);
            if (Mathf.Abs(rho - f.Rho0 - 2f) > reach) return;
            int n = ToothCount(k);
            int i0 = Mathf.RoundToInt(a / (2f * Mathf.PI) * n);
            for (int di = -2; di <= 2; di++)
                Meet(ref size, ref pos, rot, in _m[k].Teeth[Wrap(i0 + di, n)]);
        }

        const int SpillwayPrisms = 64;
        static readonly Vector3 SpillSize = new(3.4f, 1.4f, 3.4f);

        int Lanes(int k) => Specs[k].Vent == VentState.Cooled ? 5 : 9;

        Vector3 SpillPoint(int k, int lane, int i, out float a)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float a0 = 2f * Mathf.PI * lane / Lanes(k) + k * 0.4f;
            if (i < 0) { a = a0; return f.At(f.RimR + 7f, f.Rho1 + 7f, a0); }
            float t = 1f - i / (SpillwayPrisms - 1f); // rim -> vent
            a = a0 + 0.5f * Mathf.Sin(t * 4.1f + lane) * s.Chirality;
            return f.At(VentR + f.Length * t, Mathf.Lerp(f.Rho0, f.Rho1, t) + 7f, a);
        }

        Vector3 SpillPos(int k, int lane, int i, out Quaternion rot)
        {
            rot = _m[k].Spill[lane * SpillwayPrisms + i].R;
            return _m[k].Spill[lane * SpillwayPrisms + i].P;
        }

        Vector3 ComputeSpillPos(int k, int lane, int i, out Quaternion rot, out float a)
        {
            var p = SpillPoint(k, lane, i, out a);
            rot = SpawnPoint.LookRotation(p - SpillPoint(k, lane, i - 1, out _), _frames[k].Normal(a));
            return p;
        }

        /// <summary>Runnels draining the flanks INWARD into the vent. The radial pull is the only
        /// gravity in this cell, so every flow on a massif runs toward the core - this is the
        /// family that sells it. Where two lanes wander together near the mouth, the later lane
        /// yields.</summary>
        void BuildSpillways(in Spec s, in Frame f, int k)
        {
            bool molten = s.Vent is VentState.Erupting or VentState.Collapsed;
            int lanes = Lanes(k);
            for (int lane = 0; lane < lanes; lane++)
            {
                for (int i = 0; i < SpillwayPrisms; i++)
                {
                    float t = 1f - i / (SpillwayPrisms - 1f);
                    var p = SpillPos(k, lane, i, out var rot);
                    var size = SpillSize;
                    Pose ax = rot;
                    for (int other = 0; other < lane; other++)
                    {
                        if ((_m[k].Spill[other * SpillwayPrisms + i].P - p).sqrMagnitude > Sq(SpillSize.magnitude + 7f)) continue;
                        for (int j = Mathf.Max(0, i - 1); j <= Mathf.Min(SpillwayPrisms - 1, i + 1); j++)
                        {
                            ref readonly Box o = ref _m[k].Spill[other * SpillwayPrisms + j];
                            size = Clear(size, p, ax, o.P, o.A, o.S);
                        }
                    }
                    // Only the stretch nearest the mouth still glows - a runnel cools as it
                    // climbs away from the vent, so the danger is telegraphed by where it is.
                    Emit(p, rot, size, Domains.Ruby, molten && t < 0.45f ? PrismKind.Danger : PrismKind.Plain);
                }
            }
        }

        void ClearOfSpillways(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            Frame f = _frames[k];
            var m = _m[k];
            float reach = 0.5f * (size.magnitude + SpillSize.magnitude);
            float t = (Vector3.Dot(pos, f.Ax) - VentR) / f.Length;
            if (t < -reach / f.Length || t > 1f + (reach + 10f) / f.Length) return;
            int i0 = Mathf.RoundToInt((1f - Mathf.Clamp01(t)) * (SpillwayPrisms - 1f));
            // A lane is in reach only if its prism at this fraction is, give or take a step.
            float near = Sq(reach + 0.5f * f.Length / (SpillwayPrisms - 1f) + 10f);
            int lanes = Lanes(k);
            for (int lane = 0; lane < lanes; lane++)
            {
                if ((m.Spill[lane * SpillwayPrisms + i0].P - pos).sqrMagnitude > near) continue;
                for (int i = Mathf.Max(0, i0 - 2); i <= Mathf.Min(SpillwayPrisms - 1, i0 + 2); i++)
                    Meet(ref size, ref pos, rot, in m.Spill[lane * SpillwayPrisms + i]);
            }
        }

        /// <summary>What the mouth is actually doing - the four vents are four different events.</summary>
        void BuildVent(in Spec s, in Frame f, int k)
        {
            switch (s.Vent)
            {
                case VentState.Erupting: BuildEruptingVent(s, f, k); break;
                case VentState.Degassing: BuildDegassingVent(s, f, k); break;
                case VentState.Collapsed: BuildCollapsedVent(s, f, k); break;
                default: BuildCooledVent(s, f, k); break;
            }
        }

        /// <summary>A vent-mouth scatter plate (the molten disc, the collapsed floor, the glass
        /// tongue) yields to everything structured around it: the falls, the crater teeth and
        /// the flank. The plates still lap EACH OTHER - see the note at the end of the file.</summary>
        void ClearOfMouth(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            for (int pass = 0; pass < 2; pass++)   // a move can land in a second obstacle
            {
                ClearOfFalls(k, ref size, ref pos, rot);
                ClearOfRim(k, ref size, ref pos, rot);
                ClearOfFlank(k, ref size, ref pos, rot);
            }
        }

        /// <summary>Full pour: a molten disc across the mouth and a braided curtain falling the
        /// whole way to the crust.</summary>
        void BuildEruptingVent(in Spec s, in Frame f, int k)
        {
            for (int i = 0; i < 285; i++)
            {
                float u = Hash01(i * 7 + _noiseSeed + k * 313);
                float a = i * GoldenAngle * s.Chirality;
                var pos = f.At(VentR - 2f, f.Rho0 * Mathf.Sqrt(u), a);
                var rot = SpawnPoint.LookRotation(f.Ax, f.Around(a));
                var size = new Vector3(4.4f, 4.4f, 0.9f);
                ClearOfMouth(k, ref size, ref pos, rot);
                Emit(pos, rot, size, Domains.Ruby, PrismKind.Danger);
            }
            BuildFall(k, 0);
        }

        static readonly Vector3 StrandSize = new(1.1f, 1.1f, 2.1f);
        const int StrandPrisms = 52;

        Vector3 StrandPoint(int k, int strand, int i)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            float a0 = 2f * Mathf.PI * strand / 4f;
            if (i < 0) return f.At(VentR + 8f, 6f, a0);
            float t = i / (StrandPrisms - 1f);
            return f.At(Mathf.Lerp(VentR, CrustR + 4f, t), 6f + t * 20f, a0 + t * 5.2f * s.Chirality);
        }
        /// <summary>No lava: a gas column of ember motes streaming inward, and a ring of tall
        /// chimneys under shielded caps around the lip. A mote is as long as the strand leaves it
        /// room for (it steps under 1 where the column starts).</summary>
        void BuildDegassingVent(in Spec s, in Frame f, int k)
        {
            for (int strand = 0; strand < 4; strand++)
            {
                var prev = StrandPoint(k, strand, -1);
                for (int i = 0; i < StrandPrisms; i++)
                {
                    var sp = StrandPoint(k, strand, i);
                    float a = 2f * Mathf.PI * strand / 4f + i / (StrandPrisms - 1f) * 5.2f * s.Chirality;
                    ChainPose(i, sp, SpawnPoint.LookRotation(sp - prev, f.Out(a)));
                    prev = sp;
                }
                for (int i = 0; i < StrandPrisms; i++)
                    Emit(_chainP[i], _chainR[i], new Vector3(StrandSize.x, StrandSize.y, ChainLength(i, StrandPrisms, StrandSize)),
                        i % 3 != 0 ? Domains.Gold : Domains.Ruby);
            }
            for (int i = 0; i < 10; i++)
            {
                float a = 2f * Mathf.PI * i / 10f;
                for (int h = 0; h < 12; h++)
                    Emit(f.At(VentR - h * 2.4f, f.Rho0 - 8f, a + h * 0.14f * s.Chirality),
                        SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                        new Vector3(2.2f, 2.2f, 2.6f), s.Stone);
                Emit(f.At(VentR - 31.8f, f.Rho0 - 8f, a + 1.68f * s.Chirality),
                    Quaternion.identity, new Vector3(2.4f, 2.4f, 2.4f), Domains.Ruby, PrismKind.Shielded);
            }
        }

        const int SubVents = 5;
        const float StackPitch = 2.2f;
        static readonly Vector3 StackSize = new(2.6f, 2.6f, 2.4f);

        Vector3 StackPos(int k, int sv, int h, out Quaternion rot)
        {
            Frame f = _frames[k];
            float a = 2f * Mathf.PI * sv / SubVents + 0.3f;
            rot = SpawnPoint.LookRotation(f.Ax, f.Around(a));
            return f.At(VentR + 2f - h * StackPitch, f.Rho0 * 0.62f, a);
        }

        /// <summary>The mouth fell in: a wide sunken danger floor ringed by secondary vents, each
        /// dripping its own short fall. A vent's courses are no taller than their pitch, and a
        /// course its fall spirals round is narrowed to let the fall by.</summary>
        void BuildCollapsedVent(in Spec s, in Frame f, int k)
        {
            for (int i = 0; i < 440; i++)
            {
                float u = Hash01(i * 11 + _noiseSeed + k * 971);
                float a = i * GoldenAngle;
                var pos = f.At(VentR + 24f - 44f * (1f - u), f.Rho0 * Mathf.Sqrt(u), a);
                var rot = SpawnPoint.LookRotation(f.Ax, f.Around(a));
                var size = new Vector3(4.6f, 4.6f, 0.9f);
                ClearOfStacks(k, ref size, ref pos, rot);
                ClearOfMouth(k, ref size, ref pos, rot);
                Emit(pos, rot, size, Domains.Ruby, u < 0.5f ? PrismKind.Danger : PrismKind.Plain);
            }
            for (int sv = 0; sv < SubVents; sv++)
            {
                for (int h = 0; h < 9; h++)
                    Emit(StackPos(k, sv, h, out var rot), rot, StackFit(k, sv, h), s.Trim);
                BuildFall(k, 1 + sv);
            }
        }

        /// <summary>A secondary vent's course: no taller than its pitch, and narrowed clear of the
        /// fall that spirals down around it.</summary>
        Vector3 StackFit(int k, int sv, int h) => _m[k].Stack[sv * 9 + h].S;

        Vector3 ComputeStackFit(int k, int sv, int h)
        {
            var pos = StackPos(k, sv, h, out var rot);
            var size = StackSize;
            size.z = Mathf.Min(size.z, StackPitch * Flush);
            FallSpec(k, 1 + sv, out var mouth, out _, out _, out _, out _);
            if (Vector3.Distance(pos, mouth) < 30f)
                ClearOfOneFall(k, 1 + sv, ref size, ref pos, rot);
            return size;
        }

        void ClearOfStacks(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            if (Specs[k].Vent != VentState.Collapsed) return;
            float reach = 0.5f * (size.magnitude + StackSize.magnitude);
            for (int sv = 0; sv < SubVents; sv++)
            {
                var top = StackPos(k, sv, 0, out _);
                if ((top - pos).sqrMagnitude > Sq(reach + 9f * StackPitch + 4f)) continue;
                for (int h = 0; h < 9; h++)
                {
                    var b = _m[k].Stack[sv * 9 + h];
                    Meet(ref size, ref pos, rot, in b);
                }
            }
        }

        /// <summary>Set solid: a glass tongue frozen mid-pour, no danger anywhere on it, and a
        /// super-shielded heart where the last of the melt stopped.</summary>
        void BuildCooledVent(in Spec s, in Frame f, int k)
        {
            BuildFall(k, 0);
            for (int i = 0; i < 170; i++)
            {
                float u = Hash01(i * 17 + _noiseSeed + k * 449);
                float a = i * GoldenAngle;
                var pos = f.At(VentR - 1f, f.Rho0 * Mathf.Sqrt(u), a);
                var rot = SpawnPoint.LookRotation(f.Ax, f.Around(a));
                var size = new Vector3(3.8f, 3.8f, 1f);
                ClearOfMouth(k, ref size, ref pos, rot);
                Emit(pos, rot, size, i % 7 == 0 ? s.Trim : s.Stone);
            }
            Emit(f.Ax * (VentR - 6f), Quaternion.identity, new Vector3(3.2f, 3.2f, 3.2f),
                Domains.Ruby, PrismKind.SuperShielded);
        }

        const int FallPrisms = 34;
        static readonly Vector3 FallSize = new(2.4f, 1.1f, 3.2f);

        /// <summary>Massif <paramref name="k"/>'s fall <paramref name="which"/> (0 = the mouth
        /// curtain, 1..5 = a collapsed vent's secondaries): false where there is none.</summary>
        bool FallSpec(int k, int which, out Vector3 mouth, out int salt, out int strands, out float spread, out PrismKind kind)
        {
            Frame f = _frames[k];
            var vent = Specs[k].Vent;
            mouth = f.Ax * VentR; salt = k; strands = 0; spread = 0f; kind = PrismKind.Plain;
            if (which == 0)
            {
                if (vent == VentState.Erupting) { strands = 5; spread = 15f; kind = PrismKind.Danger; return true; }
                if (vent == VentState.Cooled) { strands = 3; spread = 11f; return true; }
                return false;
            }
            if (vent != VentState.Collapsed || which > SubVents) return false;
            int sv = which - 1;
            float a = 2f * Mathf.PI * sv / SubVents + 0.3f;
            mouth = f.At(VentR - 4f, f.Rho0 * 0.62f, a);
            salt = k * 7 + sv; strands = 2; spread = 7f; kind = PrismKind.Danger;
            return true;
        }

        Vector3 FallPoint(int k, Vector3 mouth, int salt, int strands, float spread, int strand, int i)
        {
            if (i < 0) return mouth;
            Frame f = _frames[k];
            float drop = VentR - CrustR + 2f;
            float a0 = 2f * Mathf.PI * strand / strands + salt * 0.7f;
            float t = (i + 1f) / FallPrisms;
            float a = a0 + t * 2.6f;
            return mouth - f.Ax * (drop * t) + (f.U * Mathf.Cos(a) + f.V * Mathf.Sin(a)) * (spread * (0.25f + 0.75f * t));
        }
        /// <summary>A curtain falling from a vent mouth to the crust. It falls INWARD, spiralling
        /// as it goes - the drop is this cell's one unambiguous statement about which way is
        /// down.</summary>
        void BuildFall(int k, int which)
        {
            if (!FallSpec(k, which, out _, out _, out _, out _, out var kind)) return;
            foreach (var b in _m[k].Fall[which]) Emit(b.P, b.R, b.S, Domains.Ruby, kind);
        }

        void ClearOfOneFall(int k, int which, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            var boxes = _m[k].Fall[which];
            if (boxes == null) return;
            float drop = VentR - CrustR + 2f;
            float reach = 0.5f * (size.magnitude + FallSize.magnitude);
            if ((pos - _m[k].FallMid[which]).sqrMagnitude > Sq(reach + 0.5f * drop + _m[k].FallReach[which])) return;
            FallSpec(k, which, out var mouth, out _, out int strands, out _, out _);
            Frame f = _frames[k];
            var d = mouth - pos;
            float below = Vector3.Dot(d, f.Ax);
            if (below < -reach || below > drop + reach) return;
            if ((d - f.Ax * below).sqrMagnitude > Sq(_m[k].FallReach[which] + reach)) return;
            int i0 = Mathf.RoundToInt(below / drop * FallPrisms - 1f);
            for (int strand = 0; strand < strands; strand++)
                for (int i = Mathf.Max(0, i0 - 3); i <= Mathf.Min(FallPrisms - 1, i0 + 3); i++)
                {
                    var bx = boxes[strand * FallPrisms + i];
                    if ((bx.P - pos).sqrMagnitude > reach * reach) continue;
                    Meet(ref size, ref pos, rot, in bx);
                }
        }

        void ClearOfFalls(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            for (int which = 0; which <= SubVents; which++)
                ClearOfOneFall(k, which, ref size, ref pos, rot);
        }

        // =====================================================================
        //  Massif furniture
        // =====================================================================

        static readonly Vector3 FumaroleSize = new(1.4f, 1.5f, 1.4f);
        static readonly Vector3 FumaroleCap = new(2.2f, 2.2f, 2.2f);

        int FumaroleClusters(int k) => Specs[k].Vent == VentState.Cooled ? 4 : 7;

        Vector3 FumaroleSeat(int k, int c, out float a, out Vector3 n, out int height)
        {
            var m = _m[k];
            a = m.FumA[c]; n = m.FumN[c]; height = m.FumH[c];
            return m.FumSeat[c];
        }

        Vector3 ComputeFumaroleSeat(int k, int c, out float a, out Vector3 n, out int height)
        {
            Frame f = _frames[k];
            a = c * GoldenAngle * 1.7f + k * 0.9f;
            float t = 0.35f + 0.45f * Hash01(c * 29 + k * 53 + _noiseSeed);
            n = f.Normal(a);
            height = 9 + (int)(9f * Hash01(c * 5 + k * 17));
            return f.At(VentR + f.Length * t, Mathf.Lerp(f.Rho0, f.Rho1, t) + 3f, a);
        }

        Vector3 FumarolePos(int k, int c, int h, int j, out Quaternion rot)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            var seat = FumaroleSeat(k, c, out float a, out var n, out int height);
            float aa = 2f * Mathf.PI * j / 5f + h * 0.3f * s.Chirality;
            float r2 = 4f * (1f - h / (float)height * 0.5f);
            rot = SpawnPoint.LookRotation(n, f.Around(a));
            return seat + n * (h * 2.5f) + f.Around(a) * (r2 * Mathf.Cos(aa)) + f.Ax * (r2 * Mathf.Sin(aa));
        }

        /// <summary>Chimney clusters on the flanks under shielded caps - the landmark prisms that
        /// let you tell one face of a massif from another. A chimney stone a spillway runs
        /// through yields to it.</summary>
        void BuildFumaroles(in Spec s, in Frame f, int k)
        {
            int clusters = FumaroleClusters(k);
            for (int c = 0; c < clusters; c++)
            {
                var seat = FumaroleSeat(k, c, out _, out var n, out int height);
                for (int h = 0; h < height; h++)
                    for (int j = 0; j < 5; j++)
                    {
                        var pos = FumarolePos(k, c, h, j, out var rot);
                        var size = FumaroleSize;
                        Pose fax = rot;
                        ClearOfSpillways(k, ref size, ref pos, fax);
                        Emit(pos, rot, size, s.Stone);
                    }
                var cap = seat + n * (height * 2.5f + 2f);
                var capSize = FumaroleCap;
                ClearOfSpillways(k, ref capSize, ref cap, Quaternion.identity);
                Emit(cap, Quaternion.identity, capSize, Domains.Ruby, PrismKind.Shielded);
            }
        }

        void ClearOfFumaroles(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            float reach = 0.5f * size.magnitude + 4f;
            int clusters = FumaroleClusters(k);
            for (int c = 0; c < clusters; c++)
            {
                var seat = FumaroleSeat(k, c, out _, out var n, out int height);
                var d = pos - seat;
                if ((d - n * (height * 1.25f + 1f)).sqrMagnitude > Sq(reach + height * 1.25f + 6f)) continue;
                float along = Vector3.Dot(d, n);
                if (along < -reach || along > height * 2.5f + 3.1f + reach) continue;
                if ((d - n * along).sqrMagnitude > Sq(reach + 4f)) continue;
                int h0 = Mathf.RoundToInt(along / 2.5f);
                for (int h = Mathf.Max(0, h0 - 2); h <= Mathf.Min(height - 1, h0 + 2); h++)
                    for (int j = 0; j < 5; j++)
                        Meet(ref size, ref pos, rot, FumarolePos(k, c, h, j, out var r), r, FumaroleSize);
                Meet(ref size, ref pos, rot, seat + n * (height * 2.5f + 2f), Quaternion.identity, FumaroleCap);
            }
        }

        const int CollarClusters = 14;
        static readonly Vector3 CollarColumn = new(3f, 3f, 1.7f);
        static readonly Vector3 CollarCapSize = new(3.2f, 3.2f, 0.8f);
        const float CollarJit = 0.08f;
        const float CollarPitch = 1.9f;

        void CollarCluster(int k, int cl, out Vector3 seat, out float a, out int cols)
        {
            var m = _m[k];
            seat = m.CollarSeat[cl]; a = m.CollarA[cl]; cols = m.CollarCols[cl];
        }

        void ComputeCollarCluster(int k, int cl, out Vector3 seat, out float a, out int cols)
        {
            Frame f = _frames[k];
            a = 2f * Mathf.PI * cl / CollarClusters + k * 0.55f;
            float rho = f.Rho1 * (0.45f + 0.5f * Hash01(cl * 67 + k * 41 + _noiseSeed));
            seat = f.At(f.RimR - 4f, rho, a);
            cols = 7 + (int)(5f * Hash01(cl * 7 + k * 13));
        }

        /// <summary>A column's offset in its bundle and its height in courses.</summary>
        Vector3 CollarOffset(int k, int cl, int co, int cols, float a, out int height)
        {
            height = _m[k].ColumnH[cl * 12 + co];
            return _m[k].ColumnOff[cl * 12 + co];
        }

        Vector3 ComputeCollarOffset(int k, int cl, int co, int cols, float a, out int height)
        {
            Frame f = _frames[k];
            float ca = 2f * Mathf.PI * co / cols;
            // Inner ring ~9.5 across, outer ~19 - the two-ring gap idiom that keeps the bundle
            // flyable rather than solid.
            float ring = 9.5f * (1 + co % 2);
            height = 6 + (int)(9f * Hash01(cl * 13 + co + k * 3));
            return f.Around(a) * (ring * Mathf.Cos(ca)) + f.Out(a) * (ring * Mathf.Sin(ca) * 0.55f);
        }

        /// <summary>A column's footprint is capped where its bundle squeezes it against a
        /// neighbour: the columns all share one frame, so two of them only touch when each is no
        /// wider, on the axis they are furthest apart on, than that distance.</summary>
        Vector2 CollarFootprint(int k, int cl, int co) => _m[k].ColumnFoot[cl * 12 + co];

        Vector2 ComputeCollarFootprint(int k, int cl, int co)
        {
            Frame f = _frames[k];
            CollarCluster(k, cl, out _, out float a, out int cols);
            var off = CollarOffset(k, cl, co, cols, a, out _);
            var rot = SpawnPoint.LookRotation(f.Ax, f.Around(a));
            var right = rot * Vector3.right;
            var up = rot * Vector3.up;
            float wide = CollarColumn.x * (1f + CollarJit);
            var cap = new Vector2(wide, wide);
            for (int other = 0; other < cols; other++)
            {
                if (other == co) continue;
                var d = CollarOffset(k, cl, other, cols, a, out _) - off;
                float dx = Mathf.Abs(Vector3.Dot(d, right)), dy = Mathf.Abs(Vector3.Dot(d, up));
                if (dx >= wide || dy >= wide) continue;               // already apart
                if (dx >= dy) cap.x = Mathf.Min(cap.x, dx * Flush);
                else cap.y = Mathf.Min(cap.y, dy * Flush);
            }
            return cap;
        }

        /// <summary>Giant's-Causeway column bundles standing off the OUTER face - solid pipes on
        /// two rings whose gaps fit the vessel, so the back of every massif is weave-through
        /// slalom terrain instead of a wall.</summary>
        void BuildColumnCollar(in Spec s, in Frame f, int k)
        {
            for (int cl = 0; cl < CollarClusters; cl++)
            {
                CollarCluster(k, cl, out var seat, out float a, out int cols);
                var rot = SpawnPoint.LookRotation(f.Ax, f.Around(a));
                for (int co = 0; co < cols; co++)
                {
                    var off = CollarOffset(k, cl, co, cols, a, out int height);
                    var cap = CollarFootprint(k, cl, co);
                    for (int h = 0; h < height; h++)
                    {
                        var size = Jit(CollarColumn, CollarJit);
                        size.x = Mathf.Min(size.x, cap.x);
                        size.y = Mathf.Min(size.y, cap.y);
                        Emit(seat + off + f.Ax * (h * CollarPitch), rot, size, s.Stone);
                    }
                    Emit(seat + off + f.Ax * (height * CollarPitch), rot,
                        new Vector3(Mathf.Min(CollarCapSize.x, cap.x), Mathf.Min(CollarCapSize.y, cap.y), CollarCapSize.z), s.Trim);
                }
            }
        }

        /// <summary>Fit a box against massif <paramref name="k"/>'s column collar: each column is
        /// one box (its courses and cap), at its fitted footprint.</summary>
        void ClearOfCollar(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            Frame f = _frames[k];
            var m = _m[k];
            float reach = 0.5f * size.magnitude;
            float axial = Vector3.Dot(pos, f.Ax);
            if (axial < f.RimR - 4f - CollarPitch - reach || axial > f.RimR + 30f + reach) return;
            for (int cl = 0; cl < CollarClusters; cl++)
            {
                var d = pos - m.CollarSeat[cl];
                if ((d - f.Ax * Vector3.Dot(d, f.Ax)).sqrMagnitude > Sq(reach + 22f)) continue;
                for (int co = 0; co < m.CollarCols[cl]; co++)
                {
                    var b = m.Column[cl * 12 + co];
                    Meet(ref size, ref pos, rot, in b);
                }
            }
        }

        static readonly Vector3 EmberSize = new(1.1f, 1.1f, 1.9f);
        const int PlumePrisms = 110;

        Vector3 PlumePoint(int k, int pl, int i)
        {
            ref readonly Spec s = ref Specs[k];
            Frame f = _frames[k];
            int plumes = s.Vent == VentState.Cooled ? 1 : 2;
            float a0 = 2f * Mathf.PI * pl / plumes + k * 1.3f;
            var seat = f.At(f.RimR, f.Rho1 * 0.5f, a0);
            if (i < 0) return seat;
            float t = i / (PlumePrisms - 1f);
            float aa = a0 + t * 6.5f * Mathf.PI * s.Chirality;
            float r2 = 4f + t * 40f;
            return seat + f.Ax * (t * 104f) + f.Around(a0) * (r2 * Mathf.Cos(aa)) + f.Out(a0) * (r2 * Mathf.Sin(aa));
        }
        /// <summary>Ember spirals off the outer face - the fines the massif throws away from the
        /// core, and the only family that leaves the tetrahedron outward. An ember is as long as
        /// its spiral leaves it room for, and it threads the column collar.</summary>
        void BuildEmberPlumes(in Spec s, in Frame f, int k)
        {
            int plumes = s.Vent == VentState.Cooled ? 1 : 2;
            for (int pl = 0; pl < plumes; pl++)
            {
                var prev = PlumePoint(k, pl, -1);
                for (int i = 0; i < PlumePrisms; i++)
                {
                    var pp = PlumePoint(k, pl, i);
                    var pr = SpawnPoint.LookRotation(pp - prev, f.Ax);
                    prev = pp;
                    var eb = EmberSize;
                    _moveOnly = true;
                    ClearOfCollar(k, ref eb, ref pp, pr);
                    _moveOnly = false;
                    ChainPose(i, pp, pr);
                }
                for (int i = 0; i < PlumePrisms; i++)
                {
                    var p = _chainP[i];
                    var size = new Vector3(EmberSize.x, EmberSize.y, ChainLength(i, PlumePrisms, EmberSize));
                    ClearOfCollar(k, ref size, ref p, _chainA[i]);
                    Emit(p, _chainR[i], size, i % 3 != 0 ? Domains.Gold : Domains.Ruby);
                }
            }
        }

        /// <summary>What a flank plate yields to on its own massif: the crater teeth, the
        /// spillways, the fumaroles, the column collar - and the knife-arcs leaving its rim.</summary>
        void ClearOfFurniture(int k, ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            // Where along the massif the plate sits decides which furniture it can meet at all:
            // the teeth only at the mouth, the collar and the arcs only at the rim, a chimney only
            // within its own reach - so most plates test the spillways and nothing else.
            var m = _m[k];
            float axial = Vector3.Dot(pos, m.Ax);
            float reach = 0.5f * size.magnitude;
            if (axial < VentR + reach + 2f) ClearOfRim(k, ref size, ref pos, rot);
            ClearOfSpillways(k, ref size, ref pos, rot);
            for (int c = 0; c < m.FumSeat.Length; c++)
                if (Mathf.Abs(axial - m.FumAxial[c]) < m.FumReachAxial[c] + reach)
                {
                    ClearOfFumaroles(k, ref size, ref pos, rot);
                    break;
                }
            if (axial > m.RimR - 30f - reach)
            {
                ClearOfCollar(k, ref size, ref pos, rot);
                ClearOfArcs(ref size, ref pos, rot);
            }
        }

        // =====================================================================
        //  Shared structure
        // =====================================================================

        static readonly Vector3 BasinSize = new(3.4f, 3.4f, 0.9f);
        static readonly Vector3 RiverSize = new(2.6f, 0.8f, 3.4f);
        static readonly Vector3 BankSize = new(2.4f, 1f, 3.2f);
        static readonly Vector3 CrustPlate = new(4.6f, 4.2f, 1f);
        const float CrustJit = 0.3f;
        const int RiverSteps = 92;

        /// <summary>River run (i, j)'s step <paramref name="step"/>: its channel point (b = 0) or
        /// a bank (b = -1, +1).</summary>
        Vector3 RiverPos(int i, int j, int step, int b, out Quaternion rot) => RiverPos(i, j, step, b, out rot, out _);

        Vector3 RiverPos(int i, int j, int step, int b, out Quaternion rot, out Vector3 dir)
        {
            var a0 = _frames[i].Ax;
            var a1 = _frames[j].Ax;
            var side = Vector3.Cross(a0, a1).normalized;
            Vector3 Dir(int st)
            {
                float t = (st + 1f) / RiverSteps;
                // Slerp the run, then let it meander off the true edge like a river.
                return (Vector3.Slerp(a0, a1, t) + side * (0.10f * Mathf.Sin(t * 7.3f + i * 2f + j))).normalized;
            }
            dir = Dir(step);
            var p = dir * CrustR;
            var flow = p - (step == 0 ? a0 * CrustR : Dir(step - 1) * CrustR);
            if (b == 0)
            {
                rot = SpawnPoint.LookRotation(flow, dir);
                return p;
            }
            // Cooled banks either side - they chain along the CHANNEL's flow, not toward it.
            var bp = (dir + side * (b * 0.045f)).normalized * CrustR;
            rot = SpawnPoint.LookRotation(flow, bp.normalized);
            return bp;
        }

        /// <summary>Fit a box against the river runs (channel and banks) passing near it; with
        /// <paramref name="before"/> set, only the runs laid before run (i, j) - where three runs
        /// leave one basin, the later run yields.</summary>
        void ClearOfRivers(ref Vector3 size, ref Vector3 pos, in Pose rot, int beforeI = 99, int beforeJ = 99)
        {
            float reach = 0.5f * (size.magnitude + RiverSize.magnitude);
            var dir = pos.normalized;
            if (Mathf.Abs(pos.magnitude - CrustR) > reach + 2f) return;
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                {
                    if (i > beforeI || (i == beforeI && j >= beforeJ)) return;
                    int run = Run(i, j);
                    // The run is within ~0.15 rad of its great circle; skip a box far off it.
                    if (Mathf.Abs(Vector3.Dot(dir, _runSide[run])) > 0.16f + reach / CrustR) continue;
                    float span = _runSpan[run];
                    float along = Mathf.Atan2(Vector3.Dot(dir, _runPerp[run]), Vector3.Dot(dir, _runA0[run]));
                    int s0 = Mathf.RoundToInt(along / span * RiverSteps - 1f);
                    for (int st = Mathf.Max(0, s0 - 3); st <= Mathf.Min(RiverSteps - 1, s0 + 3); st++)
                        for (int b = 0; b < 3; b++)
                        {
                            var bx = _river[(Run(i, j) * RiverSteps + st) * 3 + b];
                            if ((bx.P - pos).sqrMagnitude > reach * reach) continue;
                            Meet(ref size, ref pos, rot, in bx);
                        }
                }
        }

        /// <summary>The magma crust riding the nucleus shell - this cell's only "floor", and a
        /// sphere rather than a plane. Four impact basins under the falls, joined by great-circle
        /// magma rivers along the tetrahedron's edges, with cooled plate crust between. Every
        /// prism here sits at <see cref="CrustR"/>, outside the node-control radius. Where a fall
        /// lands, the crust under it yields; where three rivers leave one basin, the later yields;
        /// a basin or crust plate a river runs over yields to the river.</summary>
        void BuildCoreCrust()
        {
            // Impact basins - where each curtain lands.
            for (int k = 0; k < _frames.Length; k++)
            {
                Frame f = _frames[k];
                for (int i = 0; i < 170; i++)
                {
                    float u = Hash01(i * 13 + k * 601 + _noiseSeed);
                    float a = i * GoldenAngle;
                    float ang = 0.30f * Mathf.Sqrt(u); // angular radius of the basin on the shell
                    var dir = (f.Ax * Mathf.Cos(ang) + f.Out(a) * Mathf.Sin(ang)).normalized;
                    var pos = dir * (CrustR + 1.5f * Mathf.Sin(a * 5f));
                    var rot = SpawnPoint.LookRotation(dir, f.Around(a));
                    var size = BasinSize;
                    ClearOfFalls(k, ref size, ref pos, rot);
                    ClearOfRivers(ref size, ref pos, rot);
                    // Molten at the centre of the splash, cooled toward the edge - the basin
                    // reads as hot without turning the whole shell into a hazard.
                    Emit(pos, rot, size, Domains.Ruby, u < 0.34f ? PrismKind.Danger : PrismKind.Plain);
                }
            }

            // Rivers: six great-circle runs between basins (the tetrahedron's edges projected onto
            // the shell) - the old meandering plain river, spherized.
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                {
                    for (int step = 0; step < RiverSteps; step++)
                    {
                        var p = RiverPos(i, j, step, 0, out var rot, out var dir);
                        // Molten channel, crusted over where the flow has slowed.
                        bool hot = N01(dir.x * 6f, dir.y * 6f, dir.z * 6f, 15) > 0.36f;
                        var size = RiverSize;
                        ClearOfRivers(ref size, ref p, rot, i, j);
                        ClearOfFalls(i, ref size, ref p, rot);
                        ClearOfFalls(j, ref size, ref p, rot);
                        Emit(p, rot, size, Domains.Ruby, hot ? PrismKind.Danger : PrismKind.Plain);

                        // Cooled banks either side - the telegraphed edge to bail onto (per-prism
                        // parity is invisible at flight speed; the pair of rails is not).
                        for (int b = -1; b <= 1; b += 2)
                        {
                            var bp = RiverPos(i, j, step, b, out var brot);
                            var bsize = BankSize;
                            ClearOfRivers(ref bsize, ref bp, brot, i, j);
                            ClearOfFalls(i, ref bsize, ref bp, brot);
                            ClearOfFalls(j, ref bsize, ref bp, brot);
                            Emit(bp, brot, bsize, b < 0 ? Domains.Gold : Domains.Blue);
                        }
                    }
                }

            // Cooled crust between the flows.
            int plates = Scaled(900);
            for (int i = 0; i < plates; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / plates;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float a = i * GoldenAngle;
                var dir = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                if (N01(dir.x * 4f, dir.y * 4f, dir.z * 4f, 14) < 0.34f) continue;
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                var pos = dir * (CrustR - 1f);
                var rot = SpawnPoint.LookRotation(dir, tangent);
                var size = Jit(CrustPlate, CrustJit);
                ClearOfRivers(ref size, ref pos, rot);
                for (int k = 0; k < _frames.Length; k++)
                    ClearOfFalls(k, ref size, ref pos, rot);
                Emit(pos, rot, size, i % 6 == 0 ? Domains.Gold : Domains.Blue);
            }
        }


        static readonly Vector3 ArcSize = new(0.9f, 1.6f, 3.8f);
        const int ArcSteps = 88;

        /// <summary>How far each knife-arc's last blade stops short of the massif it lands on.
        /// The arcs used to END on the rim apex, so the two or three arcs landing on one massif laid
        /// their last blades on ONE centre; each now stops a half-blade plus a blade-width back
        /// along its own line.</summary>
        const float ArcLanding = 0.5f * 3.8f + 1.6f;

        Vector3 ArcPos(int i, int j, int step, out Quaternion rot)
        {
            var b = _arc[Run(i, j) * ArcSteps + step];
            rot = b.R;
            return b.P;
        }

        Vector3 ComputeArcPos(int i, int j, int step, out Quaternion rot)
        {
            var a0 = _frames[i].Ax;
            var a1 = _frames[j].Ax;
            var side = Vector3.Cross(a0, a1).normalized;
            Vector3 P(int st)
            {
                if (st < 0) return a0 * _frames[i].RimR;
                float t = (st + 1f) / ArcSteps;
                float radial = Mathf.Lerp(_frames[i].RimR, _frames[j].RimR, t)
                               + (ArcR - _frames[i].RimR) * Mathf.Sin(t * Mathf.PI);
                return Vector3.Slerp(a0, a1, t) * radial;
            }
            var p = P(step);
            var prev = P(step - 1);
            rot = SpawnPoint.LookRotation(p - prev, side);
            if (step == ArcSteps - 1) p -= (p - prev).normalized * ArcLanding;
            return p;
        }

        /// <summary>Six obsidian knife-arcs on the tetrahedron's six edges, bowing outward between
        /// massif pairs. They make the symmetry legible from inside the cell and give the voids
        /// something to thread - the thin axis stays the blade normal down the whole span. A blade
        /// that crosses a column collar yields to it.</summary>
        void BuildTetraArcs()
        {
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                    for (int step = 0; step < ArcSteps; step++)
                    {
                        var p = ArcPos(i, j, step, out var rot);
                        var size = ArcSize;
                        ClearOfCollar(i, ref size, ref p, rot);
                        ClearOfCollar(j, ref size, ref p, rot);
                        Emit(p, rot, size, Domains.Blue);
                    }
        }

        /// <summary>Fit a box against the knife-arc blades near it (only a box near a rim asks).</summary>
        void ClearOfArcs(ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            float reach = 0.5f * (size.magnitude + ArcSize.magnitude);
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                {
                    // Only the blades nearest each end come down to a massif.
                    for (int end = 0; end < 2; end++)
                    {
                        int from = end == 0 ? 0 : ArcSteps - 20, to = end == 0 ? 19 : ArcSteps - 1;
                        for (int st = from; st <= to; st++)
                        {
                            ref readonly Box b = ref _arc[Run(i, j) * ArcSteps + st];
                            if ((b.P - pos).sqrMagnitude > reach * reach) continue;
                            Meet(ref size, ref pos, rot, in b);
                        }
                    }
                }
        }

        /// <summary>The ash the forge has thrown: a sparse ember shell filling the voids between
        /// the massifs. A shell, not a layer - there is no altitude here to stratify by. An ember
        /// that drifts into a massif's flank yields to it.</summary>
        void BuildEmberShell()
        {
            int n = Scaled(5200);
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 11) < 0.3f) continue;
                float y = 1f - 2f * (i + 0.5f) / n;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float a = i * GoldenAngle;
                var dir = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                var pos = dir * (470f + 410f * Hash01(i * 7 + _noiseSeed));
                var rot = Quaternion.Euler(0f, Hash01(i * 13) * 360f, Hash01(i * 3) * 360f);
                var size = new Vector3(3.2f, 0.8f, 2.4f);
                for (int k = 0; k < _frames.Length; k++)
                    if (Vector3.Dot(dir, _frames[k].Ax) > 0.5f) ClearOfFlank(k, ref size, ref pos, rot);
                Emit(pos, rot, size, i % 7 == 0 ? Domains.Gold : Domains.Blue);
            }
        }

        // =====================================================================
        //  Flush fitting (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10)
        // =====================================================================
        //
        // Every size above is FITTED at authoring time from geometry the generator already has: a
        // chain's prism is as long as the room its neighbours leave it, a ring's plate runs only as
        // far as its ring neighbours leave it, and where two families cross, the one named in the
        // family's summary yields on the single axis that keeps the most of it. Nothing searches
        // the lay list - every neighbour is recomputed from its own index - so the prism count,
        // positions and draw stream are the authored ones, except the knife-arc landings (moved,
        // above) and a prism whose centre was laid inside another (moved by the least that clears).
        //
        // NOT flush yet, and not fixable by a size: the SCATTERED families - the shattered flank's
        // shards (phyllotaxis sites, but every shard knocked off true by a hashed tilt and a hashed
        // offset of up to 6.5 off the cone), the vent-mouth plates (golden-angle bearings at
        // hashed radii: the molten disc, the collapsed floor, the glass tongue) and the impact
        // basins (the same) - put pairs of plates arbitrarily close together, so no per-plate size
        // short of a few percent of the authored one clears them among themselves. They yield to
        // every structure around them; among themselves they need a re-placement of their
        // positions or a search of their neighbours, which is a decision for the owner, not a fit.
        // For the same reason nothing is fitted AGAINST a scatter (no index names the shard or
        // basin plate nearest a point): a vent plate or ember touching a shard, and a crust plate
        // touching a basin plate, still clip. Three structural contacts are also left - two
        // chimney stones whose centres sit inside a flank plate that cannot step clear of the
        // whole chimney, and one collapsed-floor plate against a secondary vent.

        static float Sq(float x) => x * x;

        /// <summary>What the lay grows a fitted size to: the prism prefab's per-axis floor.</summary>
        static Vector3 Floor(Vector3 s) => new(Mathf.Max(s.x, MinAxis), Mathf.Max(s.y, MinAxis), Mathf.Max(s.z, MinAxis));

        /// <summary>A fitted size is this fraction of the exact contact size, so float noise never
        /// reads a touching pair as a clip.</summary>
        const float Flush = 0.995f;

        /// <summary>The prism prefab's per-axis scale floor: a fitted size under it is grown back
        /// to it by the lay, so a fit that needs less is not a fit.</summary>
        const float MinAxis = 0.5f;

        /// <summary>A rotation's three axes: what every box test reads, so they are worked out once
        /// per prism rather than once per test.</summary>
        readonly struct Pose
        {
            public readonly Vector3 X, Y, Z;
            public Pose(Quaternion r) { X = r * Vector3.right; Y = r * Vector3.up; Z = r * Vector3.forward; }
            public static implicit operator Pose(Quaternion r) => new(r);
            public Vector3 this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;
        }

        /// <summary>
        /// The budget two neighbouring prisms share along their local <paramref name="axis"/>
        /// (2 = z, a chain's length; 1 = y, a ring's tangential run): the largest
        /// (La + Lb) / 2 at which they still only touch, given each one's other two sizes.
        /// Either prism's axis may separate them, so it is the better of the two - each less the
        /// other prism's girth projected onto it, which is what a bend costs.
        /// </summary>
        static float Room(Vector3 pa, in Pose ra, Vector3 sa, Vector3 pb, in Pose rb, Vector3 sb, int axis = 2)
        {
            Vector3 d = pb - pa, za = ra[axis], zb = rb[axis];
            float onA = Mathf.Abs(Vector3.Dot(d, za)), onB = Mathf.Abs(Vector3.Dot(d, zb));
            for (int m = 0; m < 3; m++)
            {
                if (m == axis) continue;
                onA -= 0.5f * Comp(sb, m) * Mathf.Abs(Vector3.Dot(rb[m], za));
                onB -= 0.5f * Comp(sa, m) * Mathf.Abs(Vector3.Dot(ra[m], zb));
            }
            return Mathf.Max(onA, onB) * Flush;
        }

        static float Comp(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        static float Reach(Vector3 l, in Pose a, Vector3 h) =>
            Mathf.Abs(Vector3.Dot(a.X, l)) * h.x + Mathf.Abs(Vector3.Dot(a.Y, l)) * h.y + Mathf.Abs(Vector3.Dot(a.Z, l)) * h.z;

        /// <summary>The 15 separating axes of two boxes (the 6 faces, then the edge pairs that are
        /// not degenerate); returns how many.</summary>
        static int Axes(in Pose a, in Pose b, Span<Vector3> axes)
        {
            axes[0] = a.X; axes[1] = a.Y; axes[2] = a.Z; axes[3] = b.X; axes[4] = b.Y; axes[5] = b.Z;
            int n = 6;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var c = Vector3.Cross(a[i], b[j]);
                    float m = c.magnitude;
                    if (m > 1e-4f) axes[n++] = c / m;
                }
            return n;
        }

        /// <summary>
        /// Shrink box (<paramref name="size"/>, <paramref name="p"/>, <paramref name="a"/>) until
        /// it only touches the fixed box (<paramref name="os"/>, <paramref name="op"/>,
        /// <paramref name="b"/>): on the ONE axis, or uniformly, whichever keeps the most volume -
        /// or, when no one axis reaches, one axis to the floor and a second cut (separating-axis
        /// test, exact on the 15 axes). Already clear: unchanged. <paramref name="fits"/> is false
        /// when nothing at all fits (a centre laid inside the other box): that prism must move.
        /// </summary>
        static Vector3 Clear(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os) =>
            Clear(size, p, a, op, b, os, out _, true);

        static Vector3 Clear(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, out bool fits) =>
            Clear(size, p, a, op, b, os, out fits, true);

        static Vector3 Clear(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, out bool fits, bool pairs, int locked = -1)
        {
            fits = true;
            Vector3 d = op - p;
            // (|a| + |b|)^2 / 4 <= (|a|^2 + |b|^2) / 2: a bounding-sphere test with no root.
            if (d.sqrMagnitude >= 0.5f * (size.sqrMagnitude + os.sqrMagnitude)) return size;
            Vector3 h = 0.5f * size, oh = 0.5f * os;
            // Cheap first: one of the six face axes separates most pairs that get this far.
            if (Apart(d, a.X, a, h, b, oh) || Apart(d, a.Y, a, h, b, oh) || Apart(d, a.Z, a, h, b, oh) ||
                Apart(d, b.X, a, h, b, oh) || Apart(d, b.Y, a, h, b, oh) || Apart(d, b.Z, a, h, b, oh))
                return size;

            Span<Vector3> axes = stackalloc Vector3[15];
            int n = Axes(a, b, axes);
            float bestRatio = 0f, uniform = 0f;
            Span<float> best = stackalloc float[3] { 0f, 0f, 0f };
            for (int s = 0; s < n; s++)
            {
                var l = axes[s];
                float gap = Mathf.Abs(Vector3.Dot(d, l)) - Reach(l, b, oh);
                float own = Reach(l, a, h);
                if (gap >= own) return size;                       // already only touching
                if (gap <= 0f) continue;
                uniform = Mathf.Max(uniform, gap / own);
                for (int k = 0; k < 3; k++)
                {
                    float c = Mathf.Abs(Vector3.Dot(a[k], l));
                    if (c < 1e-4f) continue;
                    best[k] = Mathf.Max(best[k], (gap - (own - Comp(h, k) * c)) / c);
                }
            }

            // Pick the fit that keeps the most: one axis cut to its bound, or a uniform shrink.
            int pick = -1;
            for (int k = 0; k < 3; k++)
            {
                if (k == locked || 2f * best[k] * Flush < MinAxis) continue;
                float ratio = best[k] / Comp(h, k);
                if (ratio > bestRatio) { bestRatio = ratio; pick = k; }
            }
            float u = uniform * Flush;
            if (locked < 0 && u * u * u > bestRatio && Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * u >= MinAxis)
                return size * u;
            if (pick < 0)
            {
                // No one axis reaches: take one axis to the floor and cut a second, keeping the
                // pair that leaves the most.
                fits = false;
                if (!pairs) return size;
                Vector3 keep = size;
                float kept = 0f;
                for (int k = 0; k < 3; k++)
                {
                    if (k == locked) continue;
                    var floor = size;
                    if (k == 0) floor.x = MinAxis; else if (k == 1) floor.y = MinAxis; else floor.z = MinAxis;
                    var cut2 = Clear(floor, p, a, op, b, os, out bool ok, false, locked);
                    float vol = cut2.x * cut2.y * cut2.z;
                    if (ok && vol > kept) { kept = vol; keep = cut2; fits = true; }
                }
                return keep;
            }
            float cut = 2f * best[pick] * Flush;
            if (pick == 0) size.x = cut; else if (pick == 1) size.y = cut; else size.z = cut;
            return size;
        }

        static bool Apart(Vector3 d, Vector3 l, in Pose a, Vector3 h, in Pose b, Vector3 oh) =>
            Mathf.Abs(Vector3.Dot(d, l)) >= Reach(l, a, h) + Reach(l, b, oh);

        /// <summary>
        /// The smallest move that takes box A clear of box B at its full size: the
        /// separating-axis overlap along the axis where it is least, skipping any axis that runs
        /// within ~45 degrees of <paramref name="keep"/> (zero: none skipped).
        /// </summary>
        static Vector3 Escape(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, Vector3 keep)
        {
            Vector3 d = op - p, h = 0.5f * size, oh = 0.5f * os;
            Span<Vector3> axes = stackalloc Vector3[15];
            int n = Axes(a, b, axes);
            float least = float.MaxValue;
            Vector3 move = Vector3.zero;
            for (int s = 0; s < n; s++)
            {
                var l = axes[s];
                float overlap = Reach(l, a, h) + Reach(l, b, oh) - Mathf.Abs(Vector3.Dot(d, l));
                if (overlap <= 0f) return Vector3.zero;               // already clear
                if (keep != Vector3.zero && Mathf.Abs(Vector3.Dot(l, keep)) > 0.7f) continue;
                if (overlap < least)
                {
                    least = overlap;
                    move = l * (-(Vector3.Dot(d, l) >= 0f ? 1f : -1f) * (overlap / Flush + 1e-3f));
                }
            }
            return move;
        }

        /// <summary>One prism meeting one fixed obstacle: cut to touch it, or - when its centre lies
        /// so deep in the obstacle that no cut clears - moved by the least that does.</summary>
        void Meet(ref Vector3 size, ref Vector3 pos, in Pose rot, in Box b) => Meet(ref size, ref pos, rot, b.P, b.A, b.S);

        void Meet(ref Vector3 size, ref Vector3 pos, in Pose rot, Vector3 op, in Pose orot, Vector3 os)
        {
            var cut = Clear(size, pos, rot, op, orot, os, out bool fits);
            if (fits) { if (!_moveOnly) size = cut; }
            else { pos += Escape(size, pos, rot, op, orot, os, Vector3.zero); _moved = true; }
        }

        /// <summary>Set while a POSE is being placed: a meeting then only moves a prism whose centre
        /// no cut can clear, and leaves the cutting to where the prism is laid - so every family
        /// that reads the pose sees where the prism really is.</summary>
        bool _moveOnly, _moved;

        /// <summary>Place a pose against its massif's furniture: move only where no cut clears,
        /// and again only if the move landed in another obstacle.</summary>
        void PlaceAmongFurniture(int k, Vector3 bound, ref Vector3 pos, in Pose ax)
        {
            _moveOnly = true;
            for (int pass = 0; pass < 3; pass++)
            {
                _moved = false;
                ClearOfFurniture(k, ref bound, ref pos, ax);
                if (!_moved) break;
            }
            _moveOnly = false;
        }
    }
}
