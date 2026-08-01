using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure, engine-light geometry primitives that emit <see cref="SpawnPoint"/>s (position /
    /// rotation / scale) into a caller-owned list from an instance-local <see cref="System.Random"/>.
    /// The shared "prism vocabulary" behind BOTH the freestyle microscene recipes
    /// (<c>MicroscenePatterns</c>) and - available for adoption - the environment
    /// <c>Generators/</c>. No <see cref="UnityEngine.Random"/>, no MonoBehaviour, no cache; safe to
    /// call incrementally and deterministic per seed (unit-tested via the recipe layer).
    ///
    /// Sizing note (kept identical to the shipped structures so sparse counts still read as hoops /
    /// strands / walls, not dotted specks): the LONG axis runs along the piece's own path.
    /// </summary>
    public static class PrismGeometry
    {
        // ── Scalars ──────────────────────────────────────────────────────────

        public static float Range(System.Random rng, float min, float max) => (float)(rng.NextDouble() * (max - min) + min);

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public static int RangeInt(System.Random rng, int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);

        public static Vector3 OnUnitSphere(System.Random rng)
        {
            // Polar pick - good enough distribution for scenery.
            float z = Range(rng, -1f, 1f);
            float a = Range(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }

        public static Vector3 InsideUnitSphere(System.Random rng) =>
            OnUnitSphere(rng) * Mathf.Pow(Range(rng, 0f, 1f), 1f / 3f);

        // ── Scale palette ────────────────────────────────────────────────────
        // A richer set than the original four so a scene's mass reads at many sizes. Every family
        // jitters each axis INDEPENDENTLY (not one uniform factor) so no two prisms share exact
        // proportions - the base grain of 3-dimensional scale diversity. Recipes pick the family
        // that suits their read; the painter's per-scene moods (uniform scale, long-axis stretch,
        // per-structure taper) then reshape whole scenes for grand vs. delicate vs. elongated reads.

        /// <summary>Elongated strand (~1.7×1.7×6.5) - long axis is local +z (helix / ring / spoke).</summary>
        public static Vector3 StrandScale(System.Random rng, float bias = 1f) => new(
            1.7f * Range(rng, 0.8f, 1.3f) * bias,
            1.7f * Range(rng, 0.8f, 1.3f) * bias,
            6.5f * Range(rng, 0.8f, 1.35f) * bias);

        /// <summary>Broad wall plate (~5.5×5.5×1.2) for fins and ground panels.</summary>
        public static Vector3 PlateScale(System.Random rng, float bias = 1f) => new(
            5.5f * Range(rng, 0.8f, 1.35f) * bias,
            5.5f * Range(rng, 0.8f, 1.35f) * bias,
            1.2f * Range(rng, 0.7f, 1.5f));

        /// <summary>Tall trunk segment - long axis is local +y.</summary>
        public static Vector3 TrunkScale(System.Random rng, float bias = 1f) => new(
            1.8f * Range(rng, 0.8f, 1.25f) * bias,
            6.5f * Range(rng, 0.8f, 1.3f) * bias,
            1.8f * Range(rng, 0.8f, 1.25f) * bias);

        /// <summary>Nominal-ish chunk (4×4×1 ≈ the 16-volume leaf) with organic jitter - scatter/canopy.</summary>
        public static Vector3 ChunkScale(System.Random rng, float bias = 1f) => new(
            4f * Range(rng, 0.75f, 1.4f) * bias,
            4f * Range(rng, 0.75f, 1.4f) * bias,
            1f * Range(rng, 0.7f, 1.8f));

        /// <summary>Tiny long shard (~0.8×0.8×3) - delicate filaments, sparse fills, comet spray.</summary>
        public static Vector3 ShardScale(System.Random rng, float bias = 1f) => new(
            0.8f * Range(rng, 0.75f, 1.35f) * bias,
            0.8f * Range(rng, 0.75f, 1.35f) * bias,
            3f * Range(rng, 0.75f, 1.4f) * bias);

        /// <summary>Very long thin rail (~1.2×1.2×11) - long avenues and lattice spans.</summary>
        public static Vector3 RailScale(System.Random rng, float bias = 1f) => new(
            1.2f * Range(rng, 0.8f, 1.3f) * bias,
            1.2f * Range(rng, 0.8f, 1.3f) * bias,
            11f * Range(rng, 0.8f, 1.3f) * bias);

        /// <summary>Big flat slab (~9×9×1.6) - grand walls, floors, canyon faces.</summary>
        public static Vector3 SlabScale(System.Random rng, float bias = 1f) => new(
            9f * Range(rng, 0.8f, 1.3f) * bias,
            9f * Range(rng, 0.8f, 1.3f) * bias,
            1.6f * Range(rng, 0.7f, 1.5f));

        /// <summary>Tall pillar (~2.6×9×2.6) - colonnades and megaliths, long axis +y.</summary>
        public static Vector3 PillarScale(System.Random rng, float bias = 1f) => new(
            2.6f * Range(rng, 0.8f, 1.25f) * bias,
            9f * Range(rng, 0.8f, 1.25f) * bias,
            2.6f * Range(rng, 0.8f, 1.25f) * bias);

        /// <summary>Bulky boulder (~6.5×6.5×5) - landmark chunks, asteroid cores.</summary>
        public static Vector3 BoulderScale(System.Random rng, float bias = 1f) => new(
            6.5f * Range(rng, 0.75f, 1.35f) * bias,
            6.5f * Range(rng, 0.75f, 1.35f) * bias,
            5f * Range(rng, 0.75f, 1.35f) * bias);

        /// <summary>Tiny cube mote (~1.3³) - dust, sparse speckle, delicate fills.</summary>
        public static Vector3 MoteScale(System.Random rng, float bias = 1f) => new(
            1.3f * Range(rng, 0.7f, 1.45f) * bias,
            1.3f * Range(rng, 0.7f, 1.45f) * bias,
            1.3f * Range(rng, 0.7f, 1.45f) * bias);

        /// <summary>Long beam (~1.6×1.6×14) - spans, girders, long gates.</summary>
        public static Vector3 BeamScale(System.Random rng, float bias = 1f) => new(
            1.6f * Range(rng, 0.85f, 1.25f) * bias,
            1.6f * Range(rng, 0.85f, 1.25f) * bias,
            14f * Range(rng, 0.85f, 1.25f) * bias);

        /// <summary>Wide thin pane (~7×7×0.8) - windows, sails, delicate shell shingles.</summary>
        public static Vector3 PaneScale(System.Random rng, float bias = 1f) => new(
            7f * Range(rng, 0.8f, 1.3f) * bias,
            7f * Range(rng, 0.8f, 1.3f) * bias,
            0.8f * Range(rng, 0.75f, 1.5f));

        // ── Primitives (append SpawnPoints into a caller list) ───────────────

        /// <summary>
        /// A prism hoop: long axes chained around the circumference (the shipped ring-gate look) so
        /// the gate reads as a continuous hoop rather than dotted tiles.
        /// </summary>
        public static void AddHoop(List<SpawnPoint> into, Vector3 center, Quaternion tilt, float ringRadius, int count, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector3 radial = tilt * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 tangent = tilt * new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                var rot = Quaternion.LookRotation(tangent, radial);
                into.Add(new SpawnPoint(center + radial * ringRadius, rot, StrandScale(rng)));
            }
        }

        /// <summary>
        /// A single arch (half-hoop) standing in the flight plane - a gate you fly UNDER. Long axes
        /// chain along the arc; the base sits at <paramref name="center"/> − up·radius.
        /// </summary>
        public static void AddArch(List<SpawnPoint> into, Vector3 center, float radius, int count, float spanDeg, System.Random rng)
        {
            float span = spanDeg * Mathf.Deg2Rad;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float a = Mathf.Lerp(-span * 0.5f, span * 0.5f, t) + Mathf.PI * 0.5f;
                var pos = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius - radius, 0f);
                var rot = i == 0 ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up) : SpawnPoint.LookRotation(prev, pos, Vector3.up);
                into.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                prev = pos;
            }
        }

        /// <summary>
        /// One arm of a converging vortex: a strand spiralling inward to a shared point at +z,
        /// leaving the convergence itself OPEN (a sweet spot to thread, skimming hard). Callers loop
        /// arms (offsetting <paramref name="armAngle"/>) and tag each as its own substructure.
        /// </summary>
        public static void AddVortexArm(List<SpawnPoint> into, float armAngle, int perArm, float startRadius, float length, float turns, System.Random rng)
        {
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < perArm; i++)
            {
                float t = perArm > 1 ? i / (float)(perArm - 1) : 0f;
                float r = Mathf.Lerp(startRadius, 3f, t);        // converge toward the axis…
                float angle = armAngle + t * turns * Mathf.PI * 2f;
                float z = Mathf.Lerp(-length * 0.5f, length * 0.35f, t); // …stopping short so the mouth stays open
                var pos = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z);
                var rot = i == 0 ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up) : SpawnPoint.LookRotation(prev, pos, Vector3.up);
                into.Add(new SpawnPoint(pos, rot, ShardScale(rng, 1.2f)));
                prev = pos;
            }
        }

        /// <summary>
        /// Two parallel plate walls with a rideable slot between and periodic GAPS in each wall to
        /// roll and slip through - the flat-plate "slot" read the Squirrel loves.
        /// </summary>
        public static void AddCorridor(List<SpawnPoint> into, float halfGap, float wallHeight, float length, int steps, float gapEvery, System.Random rng)
        {
            for (int i = 0; i < steps; i++)
            {
                float t = steps > 1 ? i / (float)(steps - 1) : 0.5f;
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                // Punch a gap every few steps so there's always a way to slip sideways.
                bool gap = gapEvery > 0 && (i % Mathf.Max(2, Mathf.RoundToInt(gapEvery)) == 0);
                if (gap) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    var pos = new Vector3(side * halfGap, Range(rng, -0.15f, 0.15f) * wallHeight, z);
                    var rot = Quaternion.Euler(0f, 90f, Range(rng, -6f, 6f)); // plates face the slot
                    into.Add(new SpawnPoint(pos, rot, SlabScale(rng, 0.6f + wallHeight * 0.02f)));
                }
            }
        }

        /// <summary>A 3D cubic lattice of motes with gaps to pick a line through.</summary>
        public static void AddGrid3D(List<SpawnPoint> into, int nx, int ny, int nz, float spacing, float fill, System.Random rng)
        {
            Vector3 origin = new(-(nx - 1) * spacing * 0.5f, -(ny - 1) * spacing * 0.5f, -(nz - 1) * spacing * 0.5f);
            for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                    for (int z = 0; z < nz; z++)
                    {
                        if (rng.NextDouble() > fill) continue; // gaps
                        var pos = origin + new Vector3(x, y, z) * spacing;
                        var rot = Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, -8f, 8f), Range(rng, -8f, 8f));
                        into.Add(new SpawnPoint(pos, rot, MoteScale(rng, 1.4f)));
                    }
        }

        /// <summary>A big torus ring standing across the flight path - fly through the doughnut hole.</summary>
        public static void AddTorusRing(List<SpawnPoint> into, Vector3 center, Quaternion tilt, float ringRadius, float tubeRadius, int count, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Vector3 ringDir = tilt * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float b = Range(rng, 0f, Mathf.PI * 2f);
                Vector3 tubeOff = tilt * new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b)) * tubeRadius;
                Vector3 tangent = tilt * new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                into.Add(new SpawnPoint(center + ringDir * ringRadius + tubeOff, SpawnPoint.LookRotation(tangent, ringDir), ShardScale(rng, 1.3f)));
            }
        }

        /// <summary>One vertical pillar column (a stack of pillar segments) centred on y=0 so a tall
        /// column stays inside the scene envelope rather than towering out of it. Callers loop
        /// columns and tag each as its own substructure (t runs base → top).</summary>
        public static void AddPillarColumn(List<SpawnPoint> into, Vector3 baseXZ, int perColumn, float segment, System.Random rng)
        {
            float halfHeight = (perColumn - 1) * segment * 0.5f;
            for (int h = 0; h < perColumn; h++)
            {
                var pos = baseXZ + Vector3.up * (h * segment - halfHeight);
                var rot = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                into.Add(new SpawnPoint(pos, rot, PillarScale(rng)));
            }
        }

        /// <summary>One radial blade fanning off the axis - callers loop blades (offsetting
        /// <paramref name="baseAngle"/>) to build a turbine, tagging each blade as its own
        /// substructure (t runs hub → tip, so taper thins the blade tips naturally).</summary>
        public static void AddFanBlade(List<SpawnPoint> into, float baseAngle, int perBlade, float radius, float twist, System.Random rng)
        {
            for (int i = 0; i < perBlade; i++)
            {
                float t = perBlade > 1 ? i / (float)(perBlade - 1) : 0.5f;
                float angle = baseAngle + t * twist;
                float r = Mathf.Lerp(6f, radius, t);
                var pos = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, Range(rng, -4f, 4f));
                var rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f), Vector3.forward);
                into.Add(new SpawnPoint(pos, rot, RailScale(rng, 0.7f)));
            }
        }

        /// <summary>A loose asteroid field of boulders and motes to slalom.</summary>
        public static void AddScatter(List<SpawnPoint> into, int count, float radius, float length, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(Range(rng, -0.85f, 0.85f) * radius, Range(rng, -0.7f, 0.7f) * radius, Range(rng, -0.5f, 0.5f) * length);
                var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                into.Add(new SpawnPoint(pos, rot, rng.NextDouble() < 0.25 ? BoulderScale(rng) : ChunkScale(rng, 1.1f)));
            }
        }

        /// <summary>An undulating sheet of plates - a rolling floor to skim along.</summary>
        public static void AddWaveSheet(List<SpawnPoint> into, int nx, int nz, float radius, float length, float amp, System.Random rng)
        {
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            float baseY = -radius * 0.4f;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    float x = (ix / (float)Mathf.Max(1, nx - 1) - 0.5f) * radius * 1.7f;
                    float z = (iz / (float)Mathf.Max(1, nz - 1) - 0.5f) * length;
                    float y = baseY + Mathf.Sin(phase + x * 0.05f + z * 0.06f) * amp;
                    var rot = Quaternion.Euler(90f + Range(rng, -12f, 12f), Range(rng, 0f, 360f), 0f);
                    into.Add(new SpawnPoint(new Vector3(x, y, z), rot, PlateScale(rng, 1.1f)));
                }
        }

        // ── Superstructure-oriented primitives ───────────────────────────────
        // Each of these derives every prism's orientation from the CONSTRUCTION's own frame - the
        // curve's tangent/normal, the surface's normal - so sparse prisms read as continuous curves,
        // banked decks, shells, and twisted bands rather than jittered tiles.

        /// <summary>How a swept path dresses its spine with prisms.</summary>
        public enum SweepMode
        {
            /// <summary>Long strands chained along the tangent - a flowing cable to chase.</summary>
            Strand = 0,
            /// <summary>Plates lying flat on the path, banking into turns - a rideable road deck.</summary>
            Deck = 1,
            /// <summary>Plates standing on edge along the path - a keel/fence to slalom against.</summary>
            Fin = 2,
        }

        /// <summary>
        /// Sweep prisms along an arbitrary 3D <paramref name="spine"/> (t ∈ [0,1]) with a
        /// parallel-transported frame, so orientation follows the curve smoothly with no up-vector
        /// flips. <paramref name="bankStrength"/> rolls the frame into turns (Deck mode banks like a
        /// velodrome). Emission is in path order, so structure-t runs entry → exit.
        /// </summary>
        public static void AddSweptPath(List<SpawnPoint> into, Func<float, Vector3> spine, int steps,
            SweepMode mode, float bankStrength, System.Random rng)
        {
            steps = Mathf.Max(2, steps);
            const float dt = 0.002f;

            Vector3 TangentAt(float t)
            {
                Vector3 d = spine(Mathf.Min(1f, t + dt)) - spine(Mathf.Max(0f, t - dt));
                return d.sqrMagnitude < 1e-8f ? Vector3.forward : d.normalized;
            }

            Vector3 tan = TangentAt(0f);
            Vector3 seedUp = Mathf.Abs(Vector3.Dot(tan, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 normal = Vector3.Cross(Vector3.Cross(tan, seedUp), tan).normalized;
            Vector3 prevTan = tan;

            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                Vector3 pos = spine(t);
                tan = TangentAt(t);

                // Parallel transport: strip the tangential component so the frame turns WITH the
                // curve instead of snapping to a world up (the source of pole flips).
                normal -= Vector3.Dot(normal, tan) * tan;
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(tan, seedUp);
                normal = normal.normalized;

                // Bank into the turn (emission-only roll - never fed back into the transported
                // frame, so it doesn't accumulate over the sweep).
                Vector3 banked = normal;
                if (bankStrength > 0f)
                {
                    Vector3 binormal = Vector3.Cross(tan, normal);
                    float bankDeg = Mathf.Clamp(Vector3.Dot(tan - prevTan, binormal) * steps * bankStrength, -55f, 55f);
                    banked = Quaternion.AngleAxis(bankDeg, tan) * normal;
                }

                Quaternion rot;
                Vector3 scale;
                switch (mode)
                {
                    case SweepMode.Deck:
                        rot = SpawnPoint.LookRotation(banked, tan);           // thin axis = surface normal
                        scale = PlateScale(rng, 1.15f);
                        break;
                    case SweepMode.Fin:
                        rot = SpawnPoint.LookRotation(Vector3.Cross(tan, banked), banked); // thin axis across the path
                        scale = PlateScale(rng);
                        break;
                    default:
                        rot = SpawnPoint.LookRotation(tan, banked);
                        scale = StrandScale(rng);
                        break;
                }
                into.Add(new SpawnPoint(pos, rot, scale));
                prevTan = tan;
            }
        }

        /// <summary>
        /// A spherical-cap shell of tangent panes - a dome/bowl surface to skim around. Panes land
        /// on a Fibonacci spiral from apex to rim (structure-t runs apex → rim, so gradients and
        /// taper follow the shell outward); each pane's thin axis is the sphere normal, so the cap
        /// reads as one continuous curved surface. The cap's apex points along
        /// <paramref name="orient"/> · +z.
        /// </summary>
        public static void AddShellPatch(List<SpawnPoint> into, Vector3 center, Quaternion orient,
            float sphereRadius, float capDeg, int count, System.Random rng)
        {
            float capRad = capDeg * Mathf.Deg2Rad;
            const float golden = 2.39996323f; // golden angle, radians
            for (int i = 0; i < count; i++)
            {
                float u = (i + 0.5f) / count;
                float polar = capRad * Mathf.Sqrt(u); // equal-area-ish spread over the cap
                float az = i * golden;
                Vector3 local = new(
                    Mathf.Sin(polar) * Mathf.Cos(az),
                    Mathf.Sin(polar) * Mathf.Sin(az),
                    Mathf.Cos(polar));
                Vector3 dir = orient * local;
                Vector3 azimuthal = orient * new Vector3(-Mathf.Sin(az), Mathf.Cos(az), 0f);
                var rot = SpawnPoint.LookRotation(dir, azimuthal); // thin axis along the normal
                into.Add(new SpawnPoint(center + dir * sphereRadius, rot, PaneScale(rng, 1.05f)));
            }
        }

        /// <summary>
        /// A curved wall segment: panes shingled on a cylinder around <paramref name="orient"/> · +z,
        /// spanning <paramref name="arcDeg"/> centred on <paramref name="orient"/> · +x. Emitted
        /// row-major along the length, so structure-t runs entry → exit.
        /// </summary>
        public static void AddCylinderShell(List<SpawnPoint> into, Vector3 center, Quaternion orient,
            float radius, float arcDeg, float length, int rows, int cols, System.Random rng)
        {
            rows = Mathf.Max(1, rows);
            cols = Mathf.Max(1, cols);
            float halfArc = arcDeg * 0.5f * Mathf.Deg2Rad;
            Vector3 axis = orient * Vector3.forward;

            for (int r = 0; r < rows; r++)
            {
                float z = rows > 1 ? Mathf.Lerp(-length * 0.5f, length * 0.5f, r / (float)(rows - 1)) : 0f;
                for (int c = 0; c < cols; c++)
                {
                    float a = cols > 1 ? Mathf.Lerp(-halfArc, halfArc, c / (float)(cols - 1)) : 0f;
                    a += Range(rng, -0.03f, 0.03f);
                    Vector3 radial = orient * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    var rot = SpawnPoint.LookRotation(radial, axis); // thin axis along the radial
                    into.Add(new SpawnPoint(center + radial * radius + axis * z, rot, PaneScale(rng)));
                }
            }
        }

        /// <summary>
        /// One segment of a (p,q) torus-knot strand - a self-weaving loop to chase. Covers loop
        /// parameter [<paramref name="t0"/>, <paramref name="t1"/>) of the full knot, so consecutive
        /// segments tile the loop as separate substructures. Extent: |x|,|y| ≤ major+minor,
        /// |z| ≤ minor × <paramref name="zAmp"/> (relative to <paramref name="orient"/>).
        /// </summary>
        public static void AddTorusKnotSegment(List<SpawnPoint> into, Quaternion orient, int p, int q,
            float majorRadius, float minorRadius, float zAmp, float t0, float t1, int count, System.Random rng)
        {
            Vector3 Knot(float t)
            {
                float phi = t * Mathf.PI * 2f;
                return orient * new Vector3(
                    (majorRadius + minorRadius * Mathf.Cos(q * phi)) * Mathf.Cos(p * phi),
                    (majorRadius + minorRadius * Mathf.Cos(q * phi)) * Mathf.Sin(p * phi),
                    minorRadius * Mathf.Sin(q * phi) * zAmp);
            }

            Vector3 prev = Knot(t0 - 0.01f);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = Knot(Mathf.Lerp(t0, t1, i / (float)count));
                var rot = SpawnPoint.LookRotation(prev, pos, Vector3.up);
                into.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                prev = pos;
            }
        }

        /// <summary>
        /// One arc of a twisted band ring: plates chained around a circle whose surface normal rolls
        /// by <paramref name="halfTwists"/> × 180° over the full loop (1 = a true Möbius band). The
        /// orientation IS the construction - the same ring of plates reads as a wholly different
        /// surface as the twist winds. Covers loop parameter [<paramref name="t0"/>,
        /// <paramref name="t1"/>), so consecutive arcs tile the ring as separate substructures.
        /// </summary>
        public static void AddMobiusArc(List<SpawnPoint> into, Vector3 center, Quaternion tilt,
            float ringRadius, int count, float halfTwists, float t0, float t1, System.Random rng)
        {
            Vector3 axisZ = tilt * Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(t0, t1, i / (float)count) * Mathf.PI * 2f;
                Vector3 radial = tilt * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 tangent = tilt * new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                float roll = a * halfTwists * 0.5f;
                Vector3 normal = Mathf.Cos(roll) * axisZ + Mathf.Sin(roll) * radial;
                var rot = SpawnPoint.LookRotation(normal, tangent); // thin axis rolls with the band
                into.Add(new SpawnPoint(center + radial * ringRadius, rot, PlateScale(rng, 1.1f)));
            }
        }

        /// <summary>
        /// An angular k-gon gate (triangle / diamond / pentagon…) in the plane ⊥
        /// <paramref name="orient"/> · +z - strands chained along each side.
        /// </summary>
        public static void AddPolygonGate(List<SpawnPoint> into, Vector3 center, Quaternion orient,
            int sides, float gateRadius, int perSide, float rollDeg, System.Random rng)
        {
            for (int s = 0; s < sides; s++)
            {
                float a0 = (s / (float)sides) * Mathf.PI * 2f + rollDeg * Mathf.Deg2Rad;
                float a1 = ((s + 1) / (float)sides) * Mathf.PI * 2f + rollDeg * Mathf.Deg2Rad;
                Vector3 c0 = orient * new Vector3(Mathf.Cos(a0) * gateRadius, Mathf.Sin(a0) * gateRadius, 0f);
                Vector3 c1 = orient * new Vector3(Mathf.Cos(a1) * gateRadius, Mathf.Sin(a1) * gateRadius, 0f);

                for (int i = 0; i < perSide; i++)
                {
                    float t = (i + 0.5f) / perSide;
                    Vector3 pos = center + Vector3.Lerp(c0, c1, t);
                    var rot = SpawnPoint.LookRotation(c1 - c0, (c0 + c1).normalized);
                    into.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                }
            }
        }

        /// <summary>
        /// One petal: a strand arc that starts at <paramref name="center"/> heading along
        /// <paramref name="orient"/> · +z and curls outward toward <paramref name="axisAngle"/>
        /// (radians around +z). Max outward reach = petalRadius × (1 − cos(curlDeg)) - callers size
        /// petalRadius to keep the corolla inside the scene. Structure-t runs root → tip.
        /// </summary>
        public static void AddPetalArc(List<SpawnPoint> into, Vector3 center, Quaternion orient,
            float axisAngle, float petalRadius, float curlDeg, int count, System.Random rng)
        {
            Vector3 outward = orient * new Vector3(Mathf.Cos(axisAngle), Mathf.Sin(axisAngle), 0f);
            Vector3 fwd = orient * Vector3.forward;
            float curl = curlDeg * Mathf.Deg2Rad;

            Vector3 prev = center;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float ang = t * curl;
                Vector3 pos = center
                              + outward * (petalRadius * (1f - Mathf.Cos(ang)))
                              + fwd * (petalRadius * Mathf.Sin(ang) * 0.9f);
                var rot = i == 0 ? SpawnPoint.LookRotation(fwd, outward) : SpawnPoint.LookRotation(prev, pos, outward);
                into.Add(new SpawnPoint(pos, rot, StrandScale(rng, 0.95f)));
                prev = pos;
            }
        }

        /// <summary>
        /// A corkscrew of rideable plates around the flight axis - each tread's surface faces the
        /// axis (the inside of a rifled barrel), so the spiral reads as one continuous banked
        /// surface to carve along. Structure-t runs entry → exit.
        /// </summary>
        public static void AddTerraceTreads(List<SpawnPoint> into, float startRadius, float endRadius,
            float turns, float length, int count, System.Random rng)
        {
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float ang = phase + t * turns * Mathf.PI * 2f;
                float r = Mathf.Lerp(startRadius, endRadius, t);
                Vector3 radial = new(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 climb = (new Vector3(-Mathf.Sin(ang), Mathf.Cos(ang), 0f) + Vector3.forward * 0.35f).normalized;
                var pos = radial * r + Vector3.forward * Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                var rot = SpawnPoint.LookRotation(-radial, climb); // thin axis faces the axis
                into.Add(new SpawnPoint(pos, rot, PlateScale(rng, 1.2f)));
            }
        }
    }
}
