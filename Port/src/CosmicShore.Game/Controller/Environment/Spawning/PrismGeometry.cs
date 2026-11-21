using System;
using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure, engine-light geometry primitives that emit <see cref="SpawnPoint"/>s (position /
    /// rotation / scale) into a caller-owned list from an instance-local <see cref="System.Random"/>.
    /// The shared "prism vocabulary" behind BOTH the freestyle microscene recipes
    /// (<c>MicroscenePatterns</c>) and — available for adoption — the environment
    /// <c>Generators/</c>. No <see cref="CosmicShore.Engine.Random"/>, no MonoBehaviour, no cache; safe to
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
            // Polar pick — good enough distribution for scenery.
            float z = Range(rng, -1f, 1f);
            float a = Range(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }

        public static Vector3 InsideUnitSphere(System.Random rng) =>
            OnUnitSphere(rng) * Mathf.Pow(Range(rng, 0f, 1f), 1f / 3f);

        // ── Scale palette ────────────────────────────────────────────────────
        // A richer set than the original four so a scene's mass reads at many sizes. Recipes pick
        // the family that suits their read; the conveyor's per-scene "scale mood" then multiplies
        // the whole scene up/down for grand vs. delicate arrangements.

        /// <summary>Elongated strand (~1.7×1.7×6.5) — long axis is local +z (helix / ring / spoke).</summary>
        public static Vector3 StrandScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.25f) * bias;
            return new Vector3(1.7f * j, 1.7f * j, 6.5f * j);
        }

        /// <summary>Broad wall plate (~5.5×5.5×1.2) for fins and ground panels.</summary>
        public static Vector3 PlateScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.3f) * bias;
            return new Vector3(5.5f * j, 5.5f * j, 1.2f);
        }

        /// <summary>Tall trunk segment — long axis is local +y.</summary>
        public static Vector3 TrunkScale(System.Random rng)
        {
            float j = Range(rng, 0.85f, 1.2f);
            return new Vector3(1.8f * j, 6.5f * j, 1.8f * j);
        }

        /// <summary>Nominal-ish chunk (4×4×1 ≈ the 16-volume leaf) with organic jitter — scatter/canopy.</summary>
        public static Vector3 ChunkScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.8f, 1.35f) * bias;
            return new Vector3(4f * j, 4f * j, 1f);
        }

        /// <summary>Tiny long shard (~0.8×0.8×3) — delicate filaments, sparse fills, comet spray.</summary>
        public static Vector3 ShardScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.8f, 1.3f) * bias;
            return new Vector3(0.8f * j, 0.8f * j, 3f * j);
        }

        /// <summary>Very long thin rail (~1.2×1.2×11) — long avenues and lattice spans.</summary>
        public static Vector3 RailScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.25f) * bias;
            return new Vector3(1.2f * j, 1.2f * j, 11f * j);
        }

        /// <summary>Big flat slab (~9×9×1.6) — grand walls, floors, canyon faces.</summary>
        public static Vector3 SlabScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.25f) * bias;
            return new Vector3(9f * j, 9f * j, 1.6f);
        }

        /// <summary>Tall pillar (~2.6×9×2.6) — colonnades and megaliths, long axis +y.</summary>
        public static Vector3 PillarScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.2f) * bias;
            return new Vector3(2.6f * j, 9f * j, 2.6f * j);
        }

        /// <summary>Bulky boulder (~6.5×6.5×5) — landmark chunks, asteroid cores.</summary>
        public static Vector3 BoulderScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.8f, 1.3f) * bias;
            return new Vector3(6.5f * j, 6.5f * j, 5f * j);
        }

        /// <summary>Tiny cube mote (~1.3³) — dust, sparse speckle, delicate fills.</summary>
        public static Vector3 MoteScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.75f, 1.4f) * bias;
            return new Vector3(1.3f * j, 1.3f * j, 1.3f * j);
        }

        /// <summary>Long beam (~1.6×1.6×14) — spans, girders, long gates.</summary>
        public static Vector3 BeamScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.9f, 1.2f) * bias;
            return new Vector3(1.6f * j, 1.6f * j, 14f * j);
        }

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
        /// A single arch (half-hoop) standing in the flight plane — a gate you fly UNDER. Long axes
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
        /// A converging vortex: <paramref name="arms"/> strands spiralling inward to a shared point
        /// at +z, leaving the convergence itself OPEN (a sweet spot to thread, skimming hard).
        /// </summary>
        public static void AddVortex(List<SpawnPoint> into, int arms, int perArm, float startRadius, float length, float turns, System.Random rng)
        {
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            for (int s = 0; s < arms; s++)
            {
                float arm = phase + s * (Mathf.PI * 2f / arms);
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < perArm; i++)
                {
                    float t = perArm > 1 ? i / (float)(perArm - 1) : 0f;
                    float r = Mathf.Lerp(startRadius, 3f, t);        // converge toward the axis…
                    float angle = arm + t * turns * Mathf.PI * 2f;
                    float z = Mathf.Lerp(-length * 0.5f, length * 0.35f, t); // …stopping short so the mouth stays open
                    var pos = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z);
                    var rot = i == 0 ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up) : SpawnPoint.LookRotation(prev, pos, Vector3.up);
                    into.Add(new SpawnPoint(pos, rot, ShardScale(rng, 1.2f)));
                    prev = pos;
                }
            }
        }

        /// <summary>
        /// Two parallel plate walls with a rideable slot between and periodic GAPS in each wall to
        /// roll and slip through — the flat-plate "slot" read the Squirrel loves.
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

        /// <summary>A big torus ring standing across the flight path — fly through the doughnut hole.</summary>
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

        /// <summary>A field of vertical pillars to fly between — centred on the axis so a tall
        /// column stays inside the scene envelope rather than towering out of it.</summary>
        public static void AddPillars(List<SpawnPoint> into, int columns, int perColumn, float spread, float length, float segment, System.Random rng)
        {
            float halfHeight = (perColumn - 1) * segment * 0.5f;
            for (int c = 0; c < columns; c++)
            {
                var baseXZ = new Vector3(Range(rng, -spread, spread), 0f, Range(rng, -0.5f, 0.5f) * length);
                for (int h = 0; h < perColumn; h++)
                {
                    var pos = baseXZ + Vector3.up * (h * segment - halfHeight);
                    var rot = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                    into.Add(new SpawnPoint(pos, rot, PillarScale(rng)));
                }
            }
        }

        /// <summary>Radial blades fanning off the axis — a turbine to weave.</summary>
        public static void AddFan(List<SpawnPoint> into, int blades, int perBlade, float radius, float twist, System.Random rng)
        {
            for (int bld = 0; bld < blades; bld++)
            {
                float baseAngle = bld / (float)blades * Mathf.PI * 2f;
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

        /// <summary>An undulating sheet of plates — a rolling floor to skim along.</summary>
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
    }
}
