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
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableCaldera), 4);

        /// <summary>Massif silhouette - the four are built by genuinely different rules, not one
        /// cone with different numbers.</summary>
        enum FlankStyle { Shingled, Terraced, Fluted, Shattered }

        /// <summary>How alive the mouth is - sets the danger budget and what the vent does.</summary>
        enum VentState { Erupting, Degassing, Collapsed, Cooled }

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

        // =====================================================================
        //  The massif bodies - four different mountains
        // =====================================================================

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

        /// <summary>Overlapping basalt plates in rings - the classic cone, shingled so the thin
        /// axis is the slope normal.</summary>
        void BuildShingledFlank(in Spec s, in Frame f, int k)
        {
            const int rings = 40;
            for (int L = 0; L < rings; L++)
            {
                float t = L / (rings - 1f);
                float radial = VentR + f.Length * t;
                float rho = Mathf.Lerp(f.Rho0, f.Rho1, t);
                int n = Mathf.Max(6, (int)(2f * Mathf.PI * rho / (3.1f * PlateDetail)));
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n + L * 0.13f * s.Chirality;
                    if (N01(a * 2.6f, L * 0.7f + k * 5f, 3.3f, 2) < 0.09f) continue; // blown-through vents
                    float wob = 9f * N01(Mathf.Cos(a) * 4f, L * 0.5f + k * 3f, Mathf.Sin(a) * 4f, 1);
                    Emit(f.At(radial + 2f * Mathf.Sin(a * 5f + L), rho + wob, a),
                        SpawnPoint.LookRotation(f.Normal(a), f.Around(a)),
                        Jit(Plate(4.2f, 2.6f, 1.4f), 0.25f), s.Stone);
                }
            }
        }

        /// <summary>A stepped ziggurat: flat treads crusted with sulfur, stone risers between.</summary>
        void BuildTerracedFlank(in Spec s, in Frame f, int k)
        {
            const int steps = 8;
            float rise = f.Length / steps;
            for (int step = 0; step < steps; step++)
            {
                float t0 = step / (float)steps, t1 = (step + 1f) / steps;
                float radialIn = VentR + f.Length * t0;
                float rhoIn = Mathf.Lerp(f.Rho0, f.Rho1, t0);
                float rhoOut = Mathf.Lerp(f.Rho0, f.Rho1, t1);

                // Tread: a flat annulus facing the core, two plates deep.
                for (int lane = 0; lane < 2; lane++)
                {
                    float rho = Mathf.Lerp(rhoIn, rhoOut, 0.34f + lane * 0.42f);
                    int n = Mathf.Max(8, (int)(2f * Mathf.PI * rho / (3.0f * PlateDetail)));
                    for (int i = 0; i < n; i++)
                    {
                        float a = 2f * Mathf.PI * i / n + step * 0.2f * s.Chirality;
                        if (N01(a * 3.1f, step * 1.3f + k * 4f, lane * 2.7f, 4) < 0.12f) continue;
                        Emit(f.At(radialIn, rho, a),
                            SpawnPoint.LookRotation(-f.Ax, f.Around(a)),
                            Jit(Plate(4.4f, 3.4f, 1.2f), 0.2f), lane == 0 ? s.Stone : s.Trim);
                    }
                }

                // Riser: the wall climbing out to the next tread.
                int rn = Mathf.Max(8, (int)(2f * Mathf.PI * rhoOut / (3.7f * PlateDetail)));
                for (int i = 0; i < rn; i++)
                {
                    float a = 2f * Mathf.PI * i / rn + step * 0.2f * s.Chirality;
                    for (int h = 0; h < 3; h++)
                        Emit(f.At(radialIn + (h + 0.5f) * rise / 3f, rhoOut, a),
                            SpawnPoint.LookRotation(f.Out(a), f.Ax),
                            Jit(Plate(3.2f, 2.4f, 1.3f), 0.15f), s.Stone);
                }
            }
        }

        /// <summary>Organ-pipe fluting: a recessed groove-floor shell with heavy chains standing
        /// proud of it, running vent->rim ALONG the flutes (long axis on the line of flight).</summary>
        void BuildFlutedFlank(in Spec s, in Frame f, int k)
        {
            // Groove floor - the shell the pipes stand out of.
            const int rings = 30;
            for (int L = 0; L < rings; L++)
            {
                float t = L / (rings - 1f);
                float rho = Mathf.Lerp(f.Rho0, f.Rho1, t) - 1.5f;
                int n = Mathf.Max(6, (int)(2f * Mathf.PI * rho / (3.6f * PlateDetail)));
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n + L * 0.09f * s.Chirality;
                    if (N01(a * 2.2f, L * 0.9f + k * 6f, 2.2f, 5) < 0.16f) continue;
                    Emit(f.At(VentR + f.Length * t, rho, a),
                        SpawnPoint.LookRotation(f.Normal(a), f.Around(a)),
                        Jit(Plate(4.6f, 3f, 1.1f), 0.2f), s.Stone);
                }
            }

            const int flutes = 24;
            for (int fl = 0; fl < flutes; fl++)
            {
                float a0 = 2f * Mathf.PI * fl / flutes;
                Vector3 prev = f.At(VentR, f.Rho0, a0);
                for (int i = 0; i < 72; i++)
                {
                    float t = i / 71f;
                    float a = a0 + 0.22f * t * s.Chirality; // flutes sweep as they climb out
                    float rho = Mathf.Lerp(f.Rho0, f.Rho1, t) + 5.5f;
                    var p = f.At(VentR + f.Length * t, rho, a);
                    Emit(p, SpawnPoint.LookRotation(p - prev, f.Normal(a)),
                        Plate(2.4f, 3.6f, 3.4f), fl % 4 == 0 ? s.Trim : s.Stone);
                    prev = p;
                }
            }
        }

        /// <summary>A massif broken into big tilted glass plates with real holes in it -
        /// phyllotaxis over the cone surface instead of rings, so nothing reads as a course.</summary>
        void BuildShatteredFlank(in Spec s, in Frame f, int k)
        {
            const int plates = 4560;
            for (int i = 0; i < plates; i++)
            {
                float t = (i + 0.5f) / plates;
                float a = i * GoldenAngle * s.Chirality;
                if (N01(Mathf.Cos(a) * 3f, t * 11f, Mathf.Sin(a) * 3f + k, 6) < 0.24f) continue;

                float rho = Mathf.Lerp(f.Rho0, f.Rho1, t) + 13f * (Hash01(i * 7 + k * 101) - 0.5f);
                // Each shard is knocked off true by its own noise - a fracture field, not a shell.
                var tilt = (f.Normal(a) + f.Around(a) * (Hash01(i * 13 + k) - 0.5f) * 0.9f
                                        + f.Ax * (Hash01(i * 29 + k) - 0.5f) * 0.7f).normalized;
                Emit(f.At(VentR + f.Length * t, rho, a),
                    SpawnPoint.LookRotation(tilt, f.Around(a)),
                    Jit(Plate(6.4f, 4.8f, 1.2f), 0.35f), i % 5 == 0 ? s.Trim : s.Stone);
            }
        }

        // =====================================================================
        //  The mouth
        // =====================================================================

        /// <summary>Teeth around the crater lip, leaning inward over the drop.</summary>
        void BuildCraterRim(in Spec s, in Frame f, int k)
        {
            int n = s.Vent == VentState.Collapsed ? 112 : 68;
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                float lean = 6f + 6f * Mathf.Sin(a * 6f + k);
                Emit(f.At(VentR - lean, f.Rho0 + 2f, a),
                    SpawnPoint.LookRotation(-f.Ax + f.Out(a) * 0.35f, f.Around(a)),
                    new Vector3(3f, 3f, 7.2f), i % 3 == 0 ? s.Trim : s.Stone);
            }
        }

        /// <summary>Runnels draining the flanks INWARD into the vent. The radial pull is the only
        /// gravity in this cell, so every flow on a massif runs toward the core - this is the
        /// family that sells it.</summary>
        void BuildSpillways(in Spec s, in Frame f, int k)
        {
            bool molten = s.Vent is VentState.Erupting or VentState.Collapsed;
            int lanes = s.Vent == VentState.Cooled ? 5 : 9;
            for (int lane = 0; lane < lanes; lane++)
            {
                float a0 = 2f * Mathf.PI * lane / lanes + k * 0.4f;
                Vector3 prev = f.At(f.RimR + 7f, f.Rho1 + 7f, a0);
                for (int i = 0; i < 64; i++)
                {
                    float t = 1f - i / 63f; // rim -> vent
                    float a = a0 + 0.5f * Mathf.Sin(t * 4.1f + lane) * s.Chirality;
                    float rho = Mathf.Lerp(f.Rho0, f.Rho1, t) + 7f;
                    var p = f.At(VentR + f.Length * t, rho, a);
                    // Only the stretch nearest the mouth still glows - a runnel cools as it
                    // climbs away from the vent, so the danger is telegraphed by where it is.
                    Emit(p, SpawnPoint.LookRotation(p - prev, f.Normal(a)),
                        new Vector3(3.4f, 1.4f, 3.4f), Domains.Ruby,
                        molten && t < 0.45f ? PrismKind.Danger : PrismKind.Plain);
                    prev = p;
                }
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

        /// <summary>Full pour: a molten disc across the mouth and a braided curtain falling the
        /// whole way to the crust.</summary>
        void BuildEruptingVent(in Spec s, in Frame f, int k)
        {
            for (int i = 0; i < 285; i++)
            {
                float u = Hash01(i * 7 + _noiseSeed + k * 313);
                float a = i * GoldenAngle * s.Chirality;
                Emit(f.At(VentR - 2f, f.Rho0 * Mathf.Sqrt(u), a),
                    SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                    new Vector3(4.4f, 4.4f, 0.9f), Domains.Ruby, PrismKind.Danger);
            }
            BuildFall(f, k, f.Ax * VentR, 5, 15f, PrismKind.Danger);
        }

        /// <summary>No lava: a gas column of ember motes streaming inward, and a ring of tall
        /// chimneys under shielded caps around the lip.</summary>
        void BuildDegassingVent(in Spec s, in Frame f, int k)
        {
            for (int strand = 0; strand < 4; strand++)
            {
                float a0 = 2f * Mathf.PI * strand / 4f;
                Vector3 prev = f.At(VentR + 8f, 6f, a0);
                for (int i = 0; i < 52; i++)
                {
                    float t = i / 51f;
                    float a = a0 + t * 5.2f * s.Chirality;
                    var p = f.At(Mathf.Lerp(VentR, CrustR + 4f, t), 6f + t * 20f, a);
                    Emit(p, SpawnPoint.LookRotation(p - prev, f.Out(a)),
                        new Vector3(1.1f, 1.1f, 2.1f), i % 3 != 0 ? Domains.Gold : Domains.Ruby);
                    prev = p;
                }
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

        /// <summary>The mouth fell in: a wide sunken danger floor ringed by secondary vents, each
        /// dripping its own short fall.</summary>
        void BuildCollapsedVent(in Spec s, in Frame f, int k)
        {
            for (int i = 0; i < 440; i++)
            {
                float u = Hash01(i * 11 + _noiseSeed + k * 971);
                float a = i * GoldenAngle;
                Emit(f.At(VentR + 24f - 44f * (1f - u), f.Rho0 * Mathf.Sqrt(u), a),
                    SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                    new Vector3(4.6f, 4.6f, 0.9f), Domains.Ruby,
                    u < 0.5f ? PrismKind.Danger : PrismKind.Plain);
            }
            for (int sv = 0; sv < 5; sv++)
            {
                float a = 2f * Mathf.PI * sv / 5f + 0.3f;
                float rho = f.Rho0 * 0.62f;
                for (int h = 0; h < 9; h++)
                    Emit(f.At(VentR + 2f - h * 2.2f, rho, a),
                        SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                        new Vector3(2.6f, 2.6f, 2.4f), s.Trim);
                BuildFall(f, k * 7 + sv, f.At(VentR - 4f, rho, a), 2, 7f, PrismKind.Danger);
            }
        }

        /// <summary>Set solid: a glass tongue frozen mid-pour, no danger anywhere on it, and a
        /// super-shielded heart where the last of the melt stopped.</summary>
        void BuildCooledVent(in Spec s, in Frame f, int k)
        {
            BuildFall(f, k, f.Ax * VentR, 3, 11f, PrismKind.Plain);
            for (int i = 0; i < 170; i++)
            {
                float u = Hash01(i * 17 + _noiseSeed + k * 449);
                float a = i * GoldenAngle;
                Emit(f.At(VentR - 1f, f.Rho0 * Mathf.Sqrt(u), a),
                    SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                    new Vector3(3.8f, 3.8f, 1f), i % 7 == 0 ? s.Trim : s.Stone);
            }
            Emit(f.Ax * (VentR - 6f), Quaternion.identity, new Vector3(3.2f, 3.2f, 3.2f),
                Domains.Ruby, PrismKind.SuperShielded);
        }

        /// <summary>A curtain falling from a vent mouth to the crust. It falls INWARD, spiralling
        /// as it goes - the drop is this cell's one unambiguous statement about which way is
        /// down.</summary>
        void BuildFall(in Frame f, int salt, Vector3 mouth, int strands, float spread, PrismKind kind)
        {
            const int n = 34;
            float drop = VentR - CrustR + 2f;
            for (int strand = 0; strand < strands; strand++)
            {
                float a0 = 2f * Mathf.PI * strand / strands + salt * 0.7f;
                Vector3 prev = mouth;
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 1f) / n;
                    float a = a0 + t * 2.6f;
                    var p = mouth - f.Ax * (drop * t)
                            + (f.U * Mathf.Cos(a) + f.V * Mathf.Sin(a)) * (spread * (0.25f + 0.75f * t));
                    Emit(p, SpawnPoint.LookRotation(p - prev, f.Out(a)),
                        new Vector3(2.4f, 1.1f, 3.2f), Domains.Ruby, kind);
                    prev = p;
                }
            }
        }

        // =====================================================================
        //  Massif furniture
        // =====================================================================

        /// <summary>Chimney clusters on the flanks under shielded caps - the landmark prisms that
        /// let you tell one face of a massif from another.</summary>
        void BuildFumaroles(in Spec s, in Frame f, int k)
        {
            int clusters = s.Vent == VentState.Cooled ? 4 : 7;
            for (int c = 0; c < clusters; c++)
            {
                float a = c * GoldenAngle * 1.7f + k * 0.9f;
                float t = 0.35f + 0.45f * Hash01(c * 29 + k * 53 + _noiseSeed);
                var seat = f.At(VentR + f.Length * t, Mathf.Lerp(f.Rho0, f.Rho1, t) + 3f, a);
                var n = f.Normal(a);
                int height = 9 + (int)(9f * Hash01(c * 5 + k * 17));
                for (int h = 0; h < height; h++)
                    for (int j = 0; j < 5; j++)
                    {
                        float aa = 2f * Mathf.PI * j / 5f + h * 0.3f * s.Chirality;
                        float r2 = 4f * (1f - h / (float)height * 0.5f);
                        Emit(seat + n * (h * 2.5f) + f.Around(a) * (r2 * Mathf.Cos(aa)) + f.Ax * (r2 * Mathf.Sin(aa)),
                            SpawnPoint.LookRotation(n, f.Around(a)),
                            new Vector3(1.4f, 1.5f, 1.4f), s.Stone);
                    }
                Emit(seat + n * (height * 2.5f + 2f), Quaternion.identity,
                    new Vector3(2.2f, 2.2f, 2.2f), Domains.Ruby, PrismKind.Shielded);
            }
        }

        /// <summary>Giant's-Causeway column bundles standing off the OUTER face - solid pipes on
        /// two rings whose gaps fit the vessel, so the back of every massif is weave-through
        /// slalom terrain instead of a wall.</summary>
        void BuildColumnCollar(in Spec s, in Frame f, int k)
        {
            const int clusters = 14;
            for (int cl = 0; cl < clusters; cl++)
            {
                float a = 2f * Mathf.PI * cl / clusters + k * 0.55f;
                float rho = f.Rho1 * (0.45f + 0.5f * Hash01(cl * 67 + k * 41 + _noiseSeed));
                var seat = f.At(f.RimR - 4f, rho, a);
                int cols = 7 + (int)(5f * Hash01(cl * 7 + k * 13));
                for (int co = 0; co < cols; co++)
                {
                    float ca = 2f * Mathf.PI * co / cols;
                    // Inner ring ~9.5 across, outer ~19 - the two-ring gap idiom that keeps the
                    // bundle flyable rather than solid.
                    float ring = 9.5f * (1 + co % 2);
                    var off = f.Around(a) * (ring * Mathf.Cos(ca)) + f.Out(a) * (ring * Mathf.Sin(ca) * 0.55f);
                    int height = 6 + (int)(9f * Hash01(cl * 13 + co + k * 3));
                    for (int h = 0; h < height; h++)
                        Emit(seat + off + f.Ax * (h * 1.9f),
                            SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                            Jit(new Vector3(3f, 3f, 1.7f), 0.08f), s.Stone);
                    Emit(seat + off + f.Ax * (height * 1.9f),
                        SpawnPoint.LookRotation(f.Ax, f.Around(a)),
                        new Vector3(3.2f, 3.2f, 0.8f), s.Trim);
                }
            }
        }

        /// <summary>Ember spirals off the outer face - the fines the massif throws away from the
        /// core, and the only family that leaves the tetrahedron outward.</summary>
        void BuildEmberPlumes(in Spec s, in Frame f, int k)
        {
            int plumes = s.Vent == VentState.Cooled ? 1 : 2;
            for (int pl = 0; pl < plumes; pl++)
            {
                float a0 = 2f * Mathf.PI * pl / plumes + k * 1.3f;
                var seat = f.At(f.RimR, f.Rho1 * 0.5f, a0);
                Vector3 prev = seat;
                for (int i = 0; i < 110; i++)
                {
                    float t = i / 109f;
                    float aa = a0 + t * 6.5f * Mathf.PI * s.Chirality;
                    float r2 = 4f + t * 40f;
                    var p = seat + f.Ax * (t * 104f)
                            + f.Around(a0) * (r2 * Mathf.Cos(aa)) + f.Out(a0) * (r2 * Mathf.Sin(aa));
                    Emit(p, SpawnPoint.LookRotation(p - prev, f.Ax),
                        new Vector3(1.1f, 1.1f, 1.9f), i % 3 != 0 ? Domains.Gold : Domains.Ruby);
                    prev = p;
                }
            }
        }

        // =====================================================================
        //  Shared structure
        // =====================================================================

        /// <summary>The magma crust riding the nucleus shell - this cell's only "floor", and a
        /// sphere rather than a plane. Four impact basins under the falls, joined by great-circle
        /// magma rivers along the tetrahedron's edges, with cooled plate crust between. Every
        /// prism here sits at <see cref="CrustR"/>, outside the node-control radius.</summary>
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
                    // Molten at the centre of the splash, cooled toward the edge - the basin
                    // reads as hot without turning the whole shell into a hazard.
                    Emit(dir * (CrustR + 1.5f * Mathf.Sin(a * 5f)),
                        SpawnPoint.LookRotation(dir, f.Around(a)),
                        new Vector3(3.4f, 3.4f, 0.9f), Domains.Ruby,
                        u < 0.34f ? PrismKind.Danger : PrismKind.Plain);
                }
            }

            // Rivers: six great-circle runs between basins (the tetrahedron's edges projected onto
            // the shell) - the old meandering plain river, spherized.
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                {
                    var a0 = _frames[i].Ax;
                    var a1 = _frames[j].Ax;
                    var side = Vector3.Cross(a0, a1).normalized;
                    Vector3 prev = a0 * CrustR;
                    for (int step = 0; step < 92; step++)
                    {
                        float t = (step + 1f) / 92f;
                        // Slerp the run, then let it meander off the true edge like a river.
                        var dir = (Vector3.Slerp(a0, a1, t)
                                   + side * (0.10f * Mathf.Sin(t * 7.3f + i * 2f + j))).normalized;
                        var p = dir * CrustR;
                        // Molten channel, crusted over where the flow has slowed.
                        var flow = p - prev;
                        bool hot = N01(dir.x * 6f, dir.y * 6f, dir.z * 6f, 15) > 0.36f;
                        Emit(p, SpawnPoint.LookRotation(flow, dir),
                            new Vector3(2.6f, 0.8f, 3.4f), Domains.Ruby,
                            hot ? PrismKind.Danger : PrismKind.Plain);

                        // Cooled banks either side - the telegraphed edge to bail onto (per-prism
                        // parity is invisible at flight speed; the pair of rails is not). They
                        // chain along the CHANNEL's flow, not toward it, or each bank prism would
                        // point at the river instead of down it.
                        for (int b = -1; b <= 1; b += 2)
                        {
                            var bp = (dir + side * (b * 0.045f)).normalized * CrustR;
                            Emit(bp, SpawnPoint.LookRotation(flow, bp.normalized),
                                new Vector3(2.4f, 1f, 3.2f), b < 0 ? Domains.Gold : Domains.Blue);
                        }
                        prev = p;
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
                Emit(dir * (CrustR - 1f), SpawnPoint.LookRotation(dir, tangent),
                    Jit(new Vector3(4.6f, 4.2f, 1f), 0.3f), i % 6 == 0 ? Domains.Gold : Domains.Blue);
            }
        }

        /// <summary>Six obsidian knife-arcs on the tetrahedron's six edges, bowing outward between
        /// massif pairs. They make the symmetry legible from inside the cell and give the voids
        /// something to thread - the thin axis stays the blade normal down the whole span.</summary>
        void BuildTetraArcs()
        {
            for (int i = 0; i < _frames.Length; i++)
                for (int j = i + 1; j < _frames.Length; j++)
                {
                    var a0 = _frames[i].Ax;
                    var a1 = _frames[j].Ax;
                    var side = Vector3.Cross(a0, a1).normalized;
                    Vector3 prev = a0 * _frames[i].RimR;
                    for (int step = 0; step < 88; step++)
                    {
                        float t = (step + 1f) / 88f;
                        float radial = Mathf.Lerp(_frames[i].RimR, _frames[j].RimR, t)
                                       + (ArcR - _frames[i].RimR) * Mathf.Sin(t * Mathf.PI);
                        var p = Vector3.Slerp(a0, a1, t) * radial;
                        Emit(p, SpawnPoint.LookRotation(p - prev, side),
                            new Vector3(0.9f, 1.6f, 3.8f), Domains.Blue);
                        prev = p;
                    }
                }
        }

        /// <summary>The ash the forge has thrown: a sparse ember shell filling the voids between
        /// the massifs. A shell, not a layer - there is no altitude here to stratify by.</summary>
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
                Emit(dir * (470f + 410f * Hash01(i * 7 + _noiseSeed)),
                    Quaternion.Euler(0f, Hash01(i * 13) * 360f, Hash01(i * 3) * 360f),
                    new Vector3(3.2f, 0.8f, 2.4f), i % 7 == 0 ? Domains.Gold : Domains.Blue);
            }
        }
    }
}
