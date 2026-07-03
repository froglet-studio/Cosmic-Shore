using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using static CosmicShore.Gameplay.PrismGeometry;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure generators for the conveyor's microscene recipes — each a small flyable set piece tuned
    /// for a Squirrel run. Every recipe re-rolls its own parameters (radii, counts, twists, bends,
    /// phases) on EVERY plan, so the same recipe never lands the same way twice, and every recipe is
    /// fitted to exactly <c>prismBudget</c> prism points so a recycled scene can re-pose its fixed
    /// stock of prisms into any recipe without creating or destroying mass (the conveyor is a closed
    /// system: same prisms, new arrangement).
    ///
    /// Geometry is produced through the shared <see cref="PrismGeometry"/> vocabulary (helices,
    /// hoops, tubes, arches, vortices, corridors, lattices, torus rings, fans, scatters, wave
    /// sheets…). The recipe knows ONLY shape; <see cref="Finalize"/> then themes each scene from a
    /// <see cref="MicroscenePalette"/> — per-prism domain (incl. neutral Blue), prism kind
    /// (plain / danger / shielded / supershielded), a scale mood, and the elemental/omni crystal
    /// mix — so most scenes read coherent with occasional spice, never chaotic confetti.
    ///
    /// Prism sizing follows the shipped structures (HexRace ribbon 10×1×3, ring gates ~1.8×1.8×7.5,
    /// helix strands 1×1×5): the LONG axis runs along the structure's own path, so sparse counts
    /// still read as hoops / strands / walls rather than dotted specks.
    /// </summary>
    public static class MicroscenePatterns
    {
        public const int RecipeCount = 28;

        static readonly string[] Names =
        {
            "Gate Run", "Helix Weave", "Tunnel", "Slalom", "Starburst", "Orchard", "Meadow", "Menagerie",
            "Polygon Gates", "Serpent Ribbon", "Colonnade", "Orbitals", "Canyon", "Lattice", "Comet Tail", "Spiral Ramp",
            "Archway", "Vortex", "Slot Corridor", "Cube Field", "Torus Gate", "Pillar Hall", "Turbine", "Asteroid Field",
            "Rolling Plains", "Grove", "Aviary", "Preserve",
        };

        public static string RecipeName(int recipe) => Names[Mathf.Abs(recipe) % RecipeCount];

        /// <summary>Recipes that release lifeforms into the host cell (skipped when lifeform scenes are disabled).</summary>
        public static bool IsLifeformRecipe(int recipe)
        {
            int r = Mathf.Abs(recipe) % RecipeCount;
            // Meadow, Menagerie, Rolling Plains, Grove, Aviary, Preserve.
            return r == 6 || r == 7 || r == 24 || r == 25 || r == 26 || r == 27;
        }

        /// <summary>
        /// Build the plan for one microscene. <paramref name="radius"/> bounds the lateral extent;
        /// the scene runs roughly 2.2 × radius along +z so it reads as a place you fly THROUGH.
        /// <paramref name="palette"/> drives theming (domain/kind/scale/crystal mix); null = defaults.
        /// </summary>
        public static MicroscenePlan Plan(int recipe, System.Random rng, int prismBudget, float radius, int maxCrystals,
            MicroscenePalette palette = null)
        {
            var plan = new MicroscenePlan { RecipeName = RecipeName(recipe) };
            float length = radius * 2.2f;

            switch (Mathf.Abs(recipe) % RecipeCount)
            {
                case 0: GateRun(plan, rng, prismBudget, radius, length); break;
                case 1: HelixWeave(plan, rng, prismBudget, radius, length); break;
                case 2: Tunnel(plan, rng, prismBudget, radius, length); break;
                case 3: Slalom(plan, rng, prismBudget, radius, length); break;
                case 4: Starburst(plan, rng, prismBudget, radius); break;
                case 5: Orchard(plan, rng, prismBudget, radius, length); break;
                case 6: Meadow(plan, rng, prismBudget, radius, length); break;
                case 7: Menagerie(plan, rng, prismBudget, radius, length); break;
                case 8: PolygonGates(plan, rng, prismBudget, radius, length); break;
                case 9: SerpentRibbon(plan, rng, prismBudget, radius, length); break;
                case 10: Colonnade(plan, rng, prismBudget, radius, length); break;
                case 11: Orbitals(plan, rng, prismBudget, radius); break;
                case 12: Canyon(plan, rng, prismBudget, radius, length); break;
                case 13: Lattice(plan, rng, prismBudget, radius, length); break;
                case 14: CometTail(plan, rng, prismBudget, radius, length); break;
                case 15: SpiralRamp(plan, rng, prismBudget, radius, length); break;
                case 16: Archway(plan, rng, prismBudget, radius, length); break;
                case 17: Vortex(plan, rng, prismBudget, radius, length); break;
                case 18: SlotCorridor(plan, rng, prismBudget, radius, length); break;
                case 19: CubeField(plan, rng, prismBudget, radius, length); break;
                case 20: TorusGate(plan, rng, prismBudget, radius, length); break;
                case 21: PillarHall(plan, rng, prismBudget, radius, length); break;
                case 22: Turbine(plan, rng, prismBudget, radius); break;
                case 23: AsteroidField(plan, rng, prismBudget, radius, length); break;
                case 24: RollingPlains(plan, rng, prismBudget, radius, length); break;
                case 25: Grove(plan, rng, prismBudget, radius, length); break;
                case 26: Aviary(plan, rng, prismBudget, radius, length); break;
                case 27: Preserve(plan, rng, prismBudget, radius, length); break;
            }

            FitToBudget(plan, rng, prismBudget, radius);
            ClampCrystals(plan, rng, maxCrystals);
            Finalize(plan, rng, palette);
            return plan;
        }

        // ── Original eight ───────────────────────────────────────────────────

        /// <summary>A corridor of tilted prism hoops to thread, drifting off-axis gate to gate.</summary>
        static void GateRun(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int gates = Mathf.Clamp(budget / RangeInt(rng, 8, 14), 3, 6);
            int perGate = budget / gates;
            float wanderStrength = Range(rng, 0.15f, 0.38f);
            Vector3 wander = Vector3.zero;

            for (int g = 0; g < gates; g++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, gates > 1 ? g / (float)(gates - 1) : 0.5f);
                wander += new Vector3(Range(rng, -wanderStrength, wanderStrength),
                                      Range(rng, -wanderStrength, wanderStrength) * 0.8f, 0f) * radius;
                wander = Vector3.ClampMagnitude(wander, radius * 0.55f);
                float gateRadius = Range(rng, 13f, 28f);
                Quaternion tilt = Quaternion.Euler(Range(rng, -22f, 22f), Range(rng, -22f, 22f), 0f);

                AddHoop(plan.PrismPoints, new Vector3(wander.x, wander.y, z), tilt, gateRadius, perGate, rng);
                if (g == gates - 1)
                    plan.CrystalPoints.Add(new Vector3(wander.x, wander.y, z + 24f));
            }
        }

        /// <summary>Two or three intertwined prism strands spiralling along the flight axis.</summary>
        static void HelixWeave(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int strands = RangeInt(rng, 2, 4);
            int perStrand = budget / strands;
            float helixRadius = radius * Range(rng, 0.25f, 0.5f);
            float turns = Range(rng, 1.0f, 3.0f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            float squash = Range(rng, 0.6f, 1f); // elliptical cross-section variance

            for (int s = 0; s < strands; s++)
            {
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < perStrand; i++)
                {
                    float t = perStrand > 1 ? i / (float)(perStrand - 1) : 0f;
                    float angle = phase + s * (Mathf.PI * 2f / strands) + t * turns * Mathf.PI * 2f;
                    var pos = new Vector3(
                        Mathf.Cos(angle) * helixRadius,
                        Mathf.Sin(angle) * helixRadius * squash,
                        Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
                    var rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up)
                        : SpawnPoint.LookRotation(prev, pos, Vector3.up);
                    plan.PrismPoints.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                    prev = pos;
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 20f));
        }

        /// <summary>A gently curving tube of longitudinal rails to ride through, crystal at the exit.</summary>
        static void Tunnel(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int rails = RangeInt(rng, 6, 11);
            int perRail = Mathf.Max(3, budget / rails);
            float tubeRadius = Range(rng, 19f, 30f);
            float tubeLength = length * Range(rng, 0.55f, 0.8f);
            float bendX = Range(rng, -0.3f, 0.3f) * radius;
            float bendY = Range(rng, -0.25f, 0.25f) * radius;
            float twist = Range(rng, -1.2f, 1.2f); // radians of roll over the tube
            float exitZ = 0f;

            for (int r = 0; r < rails; r++)
            {
                float baseAngle = r / (float)rails * Mathf.PI * 2f;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < perRail; i++)
                {
                    float t = perRail > 1 ? i / (float)(perRail - 1) : 0.5f;
                    float wave = Mathf.Sin(t * Mathf.PI); // bend peaks mid-tunnel, ends on-axis
                    float angle = baseAngle + twist * t;
                    var pos = new Vector3(
                        bendX * wave + Mathf.Cos(angle) * tubeRadius,
                        bendY * wave + Mathf.Sin(angle) * tubeRadius,
                        Mathf.Lerp(-tubeLength * 0.5f, tubeLength * 0.5f, t));
                    var rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up)
                        : SpawnPoint.LookRotation(prev, pos, Vector3.up);
                    plan.PrismPoints.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                    prev = pos;
                    exitZ = pos.z;
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, exitZ + 26f));
        }

        /// <summary>Alternating wall fins to slalom between.</summary>
        static void Slalom(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int fins = Mathf.Clamp(budget / RangeInt(rng, 6, 10), 4, 8);
            int perFin = budget / fins;
            int columns = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(perFin)));
            float plateBias = Range(rng, 0.85f, 1.25f);

            for (int f = 0; f < fins; f++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, fins > 1 ? f / (float)(fins - 1) : 0.5f);
                float side = (f % 2 == 0) ? 1f : -1f;
                float finX = side * radius * Range(rng, 0.15f, 0.42f);
                float baseY = Range(rng, -0.28f, 0.28f) * radius;
                var rot = Quaternion.Euler(0f, 90f + Range(rng, -14f, 14f), Range(rng, -12f, 12f));

                for (int i = 0; i < perFin; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    var pos = new Vector3(
                        finX + side * col * 6.5f,
                        baseY + (row - (perFin / columns) * 0.5f) * 6.5f,
                        z + Range(rng, -2f, 2f));
                    plan.PrismPoints.Add(new SpawnPoint(pos, rot, PlateScale(rng, plateBias)));
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 18f));
        }

        /// <summary>A radial prism sculpture to orbit and skim, crystal at the heart.</summary>
        static void Starburst(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            int spokes = Mathf.Clamp(budget / RangeInt(rng, 4, 7), 5, 14);
            int perSpoke = budget / spokes;
            float reach = Range(rng, 0.6f, 0.85f);

            for (int s = 0; s < spokes; s++)
            {
                Vector3 dir = OnUnitSphere(rng);
                var rot = SpawnPoint.LookRotation(dir, Vector3.up); // long axis along the spoke
                for (int i = 0; i < perSpoke; i++)
                {
                    float dist = Mathf.Lerp(12f, radius * reach, perSpoke > 1 ? i / (float)(perSpoke - 1) : 0.5f);
                    plan.PrismPoints.Add(new SpawnPoint(dir * dist, rot, StrandScale(rng)));
                }
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A grove of prism trees to weave between, crystals hidden in the canopy.</summary>
        static void Orchard(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int trees = Mathf.Clamp(budget / RangeInt(rng, 6, 11), 3, 7);
            int perTree = budget / trees;
            float segment = Range(rng, 5f, 7.5f);

            for (int t = 0; t < trees; t++)
            {
                var root = new Vector3(
                    Range(rng, -0.7f, 0.7f) * radius,
                    Range(rng, -0.55f, 0.2f) * radius,
                    Range(rng, -0.5f, 0.5f) * length);
                int trunk = Mathf.Max(2, perTree / 2);

                for (int i = 0; i < perTree; i++)
                {
                    if (i < trunk)
                    {
                        var pos = root + Vector3.up * (i * segment);
                        var rot = Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f));
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, TrunkScale(rng)));
                    }
                    else
                    {
                        var pos = root + Vector3.up * (trunk * segment) + InsideUnitSphere(rng) * 12f;
                        var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, ChunkScale(rng)));
                    }
                }

                if (t % 2 == 0)
                    plan.CrystalPoints.Add(root + Vector3.up * (trunk * segment + 16f));
            }
        }

        /// <summary>A sparse, open field — undulating ground plates, a crystal, flora seeded into the cell.</summary>
        static void Meadow(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float wavePhase = Range(rng, 0f, Mathf.PI * 2f);
            float waveAmp = Range(rng, 0.04f, 0.12f) * radius;

            for (int i = 0; i < budget; i++)
            {
                float x = Range(rng, -0.85f, 0.85f) * radius;
                float z = Range(rng, -0.5f, 0.5f) * length;
                float y = -radius * 0.45f
                          + Mathf.Sin(wavePhase + z * 0.06f + x * 0.04f) * waveAmp
                          + Mathf.Abs(Range(rng, 0f, 0.18f)) * radius;
                var rot = Quaternion.Euler(90f + Range(rng, -18f, 18f), Range(rng, 0f, 360f), 0f);
                plan.PrismPoints.Add(new SpawnPoint(new Vector3(x, y, z), rot, PlateScale(rng, 0.85f)));
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
            plan.FloraCount = 1 + rng.Next(3);
        }

        /// <summary>Loose prey clumps with wildlife released into the cell to hunt them.</summary>
        static void Menagerie(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int clumps = Mathf.Clamp(budget / RangeInt(rng, 8, 14), 2, 6);
            int perClump = budget / clumps;
            float clumpRadius = Range(rng, 10f, 18f);

            for (int c = 0; c < clumps; c++)
            {
                var center = new Vector3(
                    Range(rng, -0.6f, 0.6f) * radius,
                    Range(rng, -0.4f, 0.4f) * radius,
                    Range(rng, -0.45f, 0.45f) * length);
                for (int i = 0; i < perClump; i++)
                {
                    var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                    plan.PrismPoints.Add(new SpawnPoint(center + InsideUnitSphere(rng) * clumpRadius, rot, ChunkScale(rng)));
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 16f));
            plan.FaunaCount = 1 + rng.Next(3);
        }

        // ── The second eight ─────────────────────────────────────────────────

        /// <summary>Angular k-gon gates (triangles / diamonds / pentagons) rotating gate to gate.</summary>
        static void PolygonGates(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int sides = RangeInt(rng, 3, 6);
            int gates = Mathf.Clamp(budget / (sides * RangeInt(rng, 2, 4)), 3, 6);
            int perSide = Mathf.Max(2, budget / (gates * sides));
            float gateRadius = Range(rng, 16f, 26f);
            float spin = Range(rng, 8f, 40f); // degrees of roll gate-to-gate

            for (int g = 0; g < gates; g++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, gates > 1 ? g / (float)(gates - 1) : 0.5f);
                float roll = g * spin;
                var center = new Vector3(Range(rng, -0.2f, 0.2f) * radius, Range(rng, -0.2f, 0.2f) * radius, z);

                for (int s = 0; s < sides; s++)
                {
                    float a0 = (s / (float)sides) * Mathf.PI * 2f + roll * Mathf.Deg2Rad;
                    float a1 = ((s + 1) / (float)sides) * Mathf.PI * 2f + roll * Mathf.Deg2Rad;
                    Vector3 c0 = new(Mathf.Cos(a0) * gateRadius, Mathf.Sin(a0) * gateRadius, 0f);
                    Vector3 c1 = new(Mathf.Cos(a1) * gateRadius, Mathf.Sin(a1) * gateRadius, 0f);

                    for (int i = 0; i < perSide; i++)
                    {
                        float t = (i + 0.5f) / perSide;
                        Vector3 pos = center + Vector3.Lerp(c0, c1, t);
                        var rot = SpawnPoint.LookRotation(c1 - c0, (c0 + c1).normalized);
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                    }
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 22f));
        }

        /// <summary>A single sinuous ribbon wall to surf along — plates chained on a 3D sine path.</summary>
        static void SerpentRibbon(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float ampX = Range(rng, 0.2f, 0.5f) * radius;
            float ampY = Range(rng, 0.1f, 0.35f) * radius;
            float cyclesX = Range(rng, 1f, 2.5f);
            float cyclesY = Range(rng, 0.5f, 1.5f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            int width = RangeInt(rng, 1, 3); // ribbon 1-2 plates wide
            int steps = Mathf.Max(4, budget / width);

            Vector3 prev = Vector3.zero;
            for (int i = 0; i < steps; i++)
            {
                float t = steps > 1 ? i / (float)(steps - 1) : 0f;
                var spine = new Vector3(
                    Mathf.Sin(phase + t * cyclesX * Mathf.PI * 2f) * ampX,
                    Mathf.Sin(phase * 0.7f + t * cyclesY * Mathf.PI * 2f) * ampY,
                    Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
                Vector3 tangent = i == 0 ? Vector3.forward : (spine - prev).normalized;
                var rot = SpawnPoint.LookRotation(tangent, Vector3.up);

                for (int w = 0; w < width; w++)
                {
                    Vector3 offset = rot * Vector3.up * ((w - (width - 1) * 0.5f) * 6.5f);
                    plan.PrismPoints.Add(new SpawnPoint(spine + offset, rot, StrandScale(rng)));
                }
                prev = spine;
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 20f));
        }

        /// <summary>An avenue of vertical pillars to fly down, gently curving, crystal at the far end.</summary>
        static void Colonnade(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float avenueHalfWidth = Range(rng, 0.2f, 0.42f) * radius;
            float curve = Range(rng, -0.3f, 0.3f) * radius;
            int pillarHeight = RangeInt(rng, 3, 6);
            int pairs = Mathf.Max(2, budget / (pillarHeight * 2));
            float baseY = Range(rng, -0.5f, -0.2f) * radius;
            float segment = Range(rng, 5.5f, 7f);

            for (int p = 0; p < pairs; p++)
            {
                float t = pairs > 1 ? p / (float)(pairs - 1) : 0.5f;
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                float bend = Mathf.Sin(t * Mathf.PI) * curve;

                for (int side = -1; side <= 1; side += 2)
                {
                    float x = bend + side * avenueHalfWidth;
                    for (int h = 0; h < pillarHeight; h++)
                    {
                        var pos = new Vector3(x, baseY + h * segment, z);
                        var rot = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, TrunkScale(rng)));
                    }
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, baseY + pillarHeight * segment * 0.5f, length * 0.5f + 18f));
        }

        /// <summary>Concentric tilted rings around a heart crystal — a gyroscope to weave through.</summary>
        static void Orbitals(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            int rings = RangeInt(rng, 2, 5);
            int perRing = Mathf.Max(6, budget / rings);
            float reach = Range(rng, 0.5f, 0.85f);

            for (int r = 0; r < rings; r++)
            {
                float ringRadius = radius * reach * ((r + 1f) / rings);
                var tilt = Quaternion.Euler(Range(rng, 0f, 180f), Range(rng, 0f, 180f), Range(rng, 0f, 180f));
                AddHoop(plan.PrismPoints, Vector3.zero, tilt, ringRadius, perRing, rng);
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>Two winding parallel walls forming a slot canyon to thread at speed.</summary>
        static void Canyon(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float halfGap = Range(rng, 0.14f, 0.28f) * radius;
            float ampX = Range(rng, 0.15f, 0.35f) * radius;
            float cycles = Range(rng, 0.8f, 1.8f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            int wallHeight = RangeInt(rng, 2, 4);
            int steps = Mathf.Max(3, budget / (wallHeight * 2));
            float baseY = Range(rng, -0.25f, 0.05f) * radius;

            for (int i = 0; i < steps; i++)
            {
                float t = steps > 1 ? i / (float)(steps - 1) : 0.5f;
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                float bend = Mathf.Sin(phase + t * cycles * Mathf.PI * 2f) * ampX;

                for (int side = -1; side <= 1; side += 2)
                {
                    for (int h = 0; h < wallHeight; h++)
                    {
                        var pos = new Vector3(bend + side * halfGap, baseY + (h - wallHeight * 0.5f) * 6.5f, z);
                        var rot = Quaternion.Euler(0f, 90f, Range(rng, -8f, 8f)); // plates face the slot
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, PlateScale(rng)));
                    }
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, baseY, length * 0.5f + 18f));
        }

        /// <summary>Criss-crossing diagonal strands — a loose weave with gaps to pick a line through.</summary>
        static void Lattice(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int strands = RangeInt(rng, 4, 8);
            int perStrand = Mathf.Max(3, budget / strands);
            float spread = Range(rng, 0.5f, 0.8f) * radius;

            for (int s = 0; s < strands; s++)
            {
                // Each strand runs corner-to-corner through the volume in a random diagonal.
                var from = new Vector3(Range(rng, -spread, spread), Range(rng, -spread, spread), -length * 0.5f);
                var to = new Vector3(Range(rng, -spread, spread), Range(rng, -spread, spread), length * 0.5f);
                var rot = SpawnPoint.LookRotation(to - from, Vector3.up);

                for (int i = 0; i < perStrand; i++)
                {
                    float t = perStrand > 1 ? i / (float)(perStrand - 1) : 0.5f;
                    plan.PrismPoints.Add(new SpawnPoint(Vector3.Lerp(from, to, t), rot, StrandScale(rng)));
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
        }

        /// <summary>A widening debris cone converging on a crystal at the apex — fly up the tail.</summary>
        static void CometTail(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float baseRadius = Range(rng, 0.35f, 0.6f) * radius;
            float apexZ = length * 0.5f;
            float tailZ = -length * 0.55f;

            for (int i = 0; i < budget; i++)
            {
                float t = Mathf.Pow(Range(rng, 0f, 1f), 0.7f); // denser toward the apex
                float ringRadius = Mathf.Lerp(baseRadius, 4f, t);
                float angle = Range(rng, 0f, Mathf.PI * 2f);
                var pos = new Vector3(
                    Mathf.Cos(angle) * ringRadius * Range(rng, 0.5f, 1f),
                    Mathf.Sin(angle) * ringRadius * Range(rng, 0.5f, 1f),
                    Mathf.Lerp(tailZ, apexZ, t));
                var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                plan.PrismPoints.Add(new SpawnPoint(pos, rot, ChunkScale(rng, Mathf.Lerp(1.2f, 0.7f, t))));
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, apexZ + 12f));
        }

        /// <summary>A single strand unrolling outward around the axis — an expanding spiral ramp.</summary>
        static void SpiralRamp(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float turns = Range(rng, 1.5f, 3.5f);
            float startRadius = Range(rng, 4f, 10f);
            float endRadius = radius * Range(rng, 0.6f, 0.85f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            int direction = rng.Next(2) == 0 ? 1 : -1;

            Vector3 prev = Vector3.zero;
            for (int i = 0; i < budget; i++)
            {
                float t = budget > 1 ? i / (float)(budget - 1) : 0f;
                float angle = phase + direction * t * turns * Mathf.PI * 2f;
                float r = Mathf.Lerp(startRadius, endRadius, t);
                var pos = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r,
                    Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
                var rot = i == 0
                    ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up)
                    : SpawnPoint.LookRotation(prev, pos, Vector3.up);
                plan.PrismPoints.Add(new SpawnPoint(pos, rot, StrandScale(rng)));
                prev = pos;
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 20f));
        }

        // ── The new twelve (broader primitive vocabulary + more living scenes) ──

        /// <summary>A run of arches to fly UNDER, crystal past the last one.</summary>
        static void Archway(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int arches = Mathf.Clamp(budget / RangeInt(rng, 7, 12), 3, 6);
            int perArch = budget / arches;
            for (int a = 0; a < arches; a++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, arches > 1 ? a / (float)(arches - 1) : 0.5f);
                float r = Range(rng, 16f, 30f);
                AddArch(plan.PrismPoints, new Vector3(Range(rng, -0.2f, 0.2f) * radius, -radius * 0.1f, z), r, perArch,
                    Range(rng, 150f, 200f), rng);
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 18f));
        }

        /// <summary>Converging arms with an OPEN convergence mouth + an inviting crystal to skim into.</summary>
        static void Vortex(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int arms = RangeInt(rng, 3, 6);
            int perArm = Mathf.Max(3, budget / arms);
            AddVortex(plan.PrismPoints, arms, perArm, radius * Range(rng, 0.5f, 0.75f), length, Range(rng, 0.6f, 1.6f), rng);
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.4f)); // at the open mouth
        }

        /// <summary>Two parallel plate walls with gaps — a slot to roll and slip through.</summary>
        static void SlotCorridor(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float halfGap = Range(rng, 0.12f, 0.22f) * radius;
            float wallHeight = Range(rng, 0.3f, 0.6f) * radius;
            int steps = Mathf.Max(4, budget / 2);
            AddCorridor(plan.PrismPoints, halfGap, wallHeight, length, steps, Range(rng, 4f, 7f), rng);
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 16f));
        }

        /// <summary>A 3D cubic lattice with gaps to pick a line through, crystal at the core.</summary>
        static void CubeField(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(budget * 1.6f, 1f / 3f)), 2, 5);
            float spacing = Range(rng, 10f, 16f);
            int nz = Mathf.Max(n, Mathf.RoundToInt(length / spacing * 0.5f));
            AddGrid3D(plan.PrismPoints, n, n, nz, spacing, Range(rng, 0.55f, 0.8f), rng);
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>One to three big torus rings to fly through the doughnut hole of.</summary>
        static void TorusGate(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int rings = RangeInt(rng, 1, 3);
            int per = Mathf.Max(8, budget / Mathf.Max(1, rings));
            for (int r = 0; r < rings; r++)
            {
                float z = rings > 1 ? Mathf.Lerp(-length * 0.3f, length * 0.3f, r / (float)(rings - 1)) : 0f;
                var tilt = Quaternion.Euler(Range(rng, -20f, 20f), Range(rng, -20f, 20f), 0f);
                AddTorusRing(plan.PrismPoints, new Vector3(0f, 0f, z), tilt, radius * Range(rng, 0.5f, 0.7f),
                    Range(rng, 5f, 10f), per, rng);
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A hall of pillars to fly between, crystal past the far end.</summary>
        static void PillarHall(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int cols = Mathf.Clamp(budget / RangeInt(rng, 4, 7), 4, 10);
            int per = Mathf.Max(2, budget / cols);
            AddPillars(plan.PrismPoints, cols, per, radius * Range(rng, 0.5f, 0.8f), length, Range(rng, 6f, 8f), rng);
            plan.CrystalPoints.Add(new Vector3(0f, radius * 0.1f, length * 0.5f + 16f));
        }

        /// <summary>Radial blades fanning off the axis — a turbine to weave, crystal at the hub.</summary>
        static void Turbine(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            int blades = RangeInt(rng, 4, 9);
            int per = Mathf.Max(3, budget / blades);
            AddFan(plan.PrismPoints, blades, per, radius * Range(rng, 0.6f, 0.85f), Range(rng, 0.3f, 1.2f), rng);
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A loose asteroid field to slalom, crystal drifting in it.</summary>
        static void AsteroidField(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            AddScatter(plan.PrismPoints, budget, radius, length, rng);
            plan.CrystalPoints.Add(new Vector3(Range(rng, -0.2f, 0.2f) * radius, 0f, Range(rng, -0.2f, 0.2f) * length));
        }

        /// <summary>An open rolling floor to skim along — flora seeded into the cell.</summary>
        static void RollingPlains(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int nx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(budget)), 3, 8);
            int nz = Mathf.Max(2, budget / Mathf.Max(1, nx));
            AddWaveSheet(plan.PrismPoints, nx, nz, radius, length, Range(rng, 0.05f, 0.14f) * radius, rng);
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
            plan.FloraCount = 2 + rng.Next(3);
        }

        /// <summary>A grove of trees with flora seeded into the cell.</summary>
        static void Grove(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            Orchard(plan, rng, budget, radius, length); // reuse the tree geometry
            plan.FloraCount = 1 + rng.Next(2);
        }

        /// <summary>A prey field with wildlife released into the cell to hunt it.</summary>
        static void Aviary(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            AddScatter(plan.PrismPoints, budget, radius, length, rng);
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 16f));
            plan.FaunaCount = 2 + rng.Next(3);
        }

        /// <summary>An open preserve — a rolling floor with BOTH flora and fauna released into the cell.</summary>
        static void Preserve(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int nx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(budget)), 3, 7);
            AddWaveSheet(plan.PrismPoints, nx, Mathf.Max(2, budget / 6), radius, length, Range(rng, 0.05f, 0.12f) * radius, rng);
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
            plan.FloraCount = 1 + rng.Next(2);
            plan.FaunaCount = 1 + rng.Next(2);
        }

        // ── Budget fitting (geometry) ────────────────────────────────────────

        /// <summary>
        /// Recipes must emit exactly <paramref name="budget"/> prism points so the conveyor can
        /// re-pose its fixed prism stock into any plan. Trims overshoot; pads undershoot with
        /// ambient scatter.
        /// </summary>
        static void FitToBudget(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            while (plan.PrismPoints.Count > budget)
                plan.PrismPoints.RemoveAt(plan.PrismPoints.Count - 1);
            while (plan.PrismPoints.Count < budget)
            {
                var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                plan.PrismPoints.Add(new SpawnPoint(InsideUnitSphere(rng) * radius, rot, ChunkScale(rng, 0.9f)));
            }
        }

        static void ClampCrystals(MicroscenePlan plan, System.Random rng, int maxCrystals)
        {
            while (plan.CrystalPoints.Count > Mathf.Max(0, maxCrystals))
                plan.CrystalPoints.RemoveAt(rng.Next(plan.CrystalPoints.Count));
        }

        // ── Theming (domain + kind + scale mood + crystal mix) ───────────────

        enum DomainScheme { Mono = 0, Banded = 1, Accent = 2, NeutralVein = 3 }
        enum KindScheme { AllPlain = 0, DangerAccent = 1, ShieldAccent = 2, Landmark = 3 }

        static readonly Domains[] DefaultDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>
        /// Turn the recipe's pure geometry into themed <see cref="MicroscenePlan.Prisms"/> /
        /// <see cref="MicroscenePlan.Crystals"/>: a coherent per-scene domain scheme (incl. neutral
        /// Blue veins), a sparse capped prism-kind scheme (mostly plain), a per-scene scale mood, and
        /// a mostly-elemental/occasionally-omni crystal mix. Deterministic per rng.
        /// </summary>
        static void Finalize(MicroscenePlan plan, System.Random rng, MicroscenePalette pal)
        {
            pal ??= MicroscenePalette.Default;
            var domains = pal.PlayableDomains is { Length: > 0 } ? pal.PlayableDomains : DefaultDomains;

            float mood = rng.NextDouble() < pal.ScaleMoodChance ? Range(rng, pal.ScaleMoodMin, pal.ScaleMoodMax) : 1f;

            int n = plan.PrismPoints.Count;
            var domainOf = AssignDomains(n, rng, pal, domains);
            var kindOf = AssignKinds(n, rng, pal);

            plan.Prisms.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = plan.PrismPoints[i];
                var scaled = new SpawnPoint(p.Position, p.Rotation, p.Scale * mood);
                plan.Prisms.Add(new PrismLay(scaled, domainOf[i], kindOf[i]));
            }

            plan.Crystals.Clear();
            foreach (var pos in plan.CrystalPoints)
            {
                var kind = rng.NextDouble() < pal.OmniCrystalChance ? CrystalKind.Omni : CrystalKind.Elemental;
                plan.Crystals.Add(new CrystalDrop(pos, kind));
            }
        }

        static Domains[] AssignDomains(int count, System.Random rng, MicroscenePalette pal, Domains[] domains)
        {
            var result = new Domains[count];
            if (count == 0) return result;

            var scheme = (DomainScheme)WeightedIndex(rng, pal.MonoWeight, pal.BandedWeight, pal.AccentWeight, pal.NeutralVeinWeight);
            switch (scheme)
            {
                case DomainScheme.Banded:
                {
                    // Contiguous bands — structures tend to be contiguous runs in the point list,
                    // so a band ≈ a structure, keeping the colouring coherent rather than confetti.
                    int bands = Mathf.Clamp(domains.Length, 2, 3);
                    for (int i = 0; i < count; i++)
                    {
                        int band = Mathf.Min(bands - 1, i * bands / count);
                        result[i] = domains[band % domains.Length];
                    }
                    break;
                }
                case DomainScheme.Accent:
                {
                    var baseDomain = domains[rng.Next(domains.Length)];
                    var accent = PickOther(rng, domains, baseDomain);
                    for (int i = 0; i < count; i++)
                        result[i] = rng.NextDouble() < pal.AccentChance ? accent : baseDomain;
                    break;
                }
                case DomainScheme.NeutralVein:
                {
                    var baseDomain = domains[rng.Next(domains.Length)];
                    for (int i = 0; i < count; i++)
                        result[i] = rng.NextDouble() < pal.BlueVeinChance ? Domains.Blue : baseDomain;
                    break;
                }
                default: // Mono
                {
                    var only = domains[rng.Next(domains.Length)];
                    for (int i = 0; i < count; i++) result[i] = only;
                    break;
                }
            }
            return result;
        }

        static PrismKind[] AssignKinds(int count, System.Random rng, MicroscenePalette pal)
        {
            var kinds = new PrismKind[count]; // default Plain
            if (count == 0) return kinds;

            var scheme = (KindScheme)WeightedIndex(rng, pal.AllPlainWeight, pal.DangerAccentWeight, pal.ShieldAccentWeight, pal.LandmarkWeight);
            switch (scheme)
            {
                case KindScheme.DangerAccent:
                    Sprinkle(kinds, rng, PrismKind.Danger, Mathf.Min(pal.MaxDanger, Mathf.Max(1, count / 8)));
                    break;
                case KindScheme.ShieldAccent:
                    Sprinkle(kinds, rng, PrismKind.Shielded, Mathf.Min(pal.MaxShielded, Mathf.Max(1, count / 16)));
                    break;
                case KindScheme.Landmark:
                    Sprinkle(kinds, rng, PrismKind.SuperShielded, Mathf.Min(pal.MaxSuperShielded, 1));
                    Sprinkle(kinds, rng, PrismKind.Shielded, Mathf.Min(pal.MaxShielded, Mathf.Max(1, count / 20)));
                    break;
                // AllPlain: leave every prism plain.
            }
            return kinds;
        }

        static void Sprinkle(PrismKind[] kinds, System.Random rng, PrismKind kind, int n)
        {
            int placed = 0, guard = 0, cap = kinds.Length * 4;
            while (placed < n && guard++ < cap)
            {
                int idx = rng.Next(kinds.Length);
                if (kinds[idx] != PrismKind.Plain) continue;
                kinds[idx] = kind;
                placed++;
            }
        }

        static Domains PickOther(System.Random rng, Domains[] domains, Domains not)
        {
            if (domains.Length <= 1) return not;
            for (int guard = 0; guard < 8; guard++)
            {
                var pick = domains[rng.Next(domains.Length)];
                if (pick != not) return pick;
            }
            return not;
        }

        static int WeightedIndex(System.Random rng, float w0, float w1, float w2, float w3)
        {
            float total = Mathf.Max(0.0001f, w0 + w1 + w2 + w3);
            float roll = (float)(rng.NextDouble() * total);
            if ((roll -= w0) < 0f) return 0;
            if ((roll -= w1) < 0f) return 1;
            if ((roll -= w2) < 0f) return 2;
            return 3;
        }
    }
}
