using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The plan for one microscene on the freestyle conveyor: an authored-feeling arrangement of
    /// prism spawn points (local space, +z = flight direction), elemental-crystal pickup points,
    /// and a count of lifeforms to release into the host <see cref="Cell"/> at the scene site.
    /// Plans are produced by <see cref="MicroscenePatterns"/> from an instance-local
    /// <see cref="System.Random"/>, so generation is deterministic per seed and safe to run
    /// incrementally (never touches the global UnityEngine.Random).
    /// </summary>
    public sealed class MicroscenePlan
    {
        public string RecipeName;
        public readonly List<SpawnPoint> PrismPoints = new();
        public readonly List<Vector3> CrystalPoints = new();
        public int FloraCount;
        public int FaunaCount;
    }

    /// <summary>
    /// Pure generators for the conveyor's microscene recipes — each one a small flyable set piece
    /// tuned for a Squirrel run. Every recipe re-rolls its own parameters (radii, counts, twists,
    /// bends, phases) on EVERY plan, so the same recipe never lands the same way twice, and every
    /// recipe emits exactly <c>prismBudget</c> prism points so a recycled scene can re-pose its
    /// fixed stock of prisms into any recipe without creating or destroying mass (the conveyor is
    /// a closed system: same prisms, new arrangement).
    ///
    /// Prism sizing follows the shipped structures (HexRace ribbon 10×1×3, ring gates ~1.8×1.8×7.5,
    /// helix strands 1×1×5): the LONG axis runs along the structure's own path, so sparse counts
    /// still read as hoops / strands / walls rather than dotted specks.
    /// </summary>
    public static class MicroscenePatterns
    {
        public const int RecipeCount = 16;

        static readonly string[] Names =
        {
            "Gate Run", "Helix Weave", "Tunnel", "Slalom", "Starburst", "Orchard", "Meadow", "Menagerie",
            "Polygon Gates", "Serpent Ribbon", "Colonnade", "Orbitals", "Canyon", "Lattice", "Comet Tail", "Spiral Ramp",
        };

        public static string RecipeName(int recipe) => Names[Mathf.Abs(recipe) % RecipeCount];

        /// <summary>Recipes that release lifeforms into the host cell (skipped when lifeform scenes are disabled).</summary>
        public static bool IsLifeformRecipe(int recipe)
        {
            int r = Mathf.Abs(recipe) % RecipeCount;
            return r == 6 || r == 7; // Meadow, Menagerie
        }

        /// <summary>
        /// Build the plan for one microscene. <paramref name="radius"/> bounds the lateral extent;
        /// the scene runs roughly 2.2 × radius along +z so it reads as a place you fly THROUGH.
        /// </summary>
        public static MicroscenePlan Plan(int recipe, System.Random rng, int prismBudget, float radius, int maxCrystals)
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
            }

            FitToBudget(plan, rng, prismBudget, radius);
            ClampCrystals(plan, rng, maxCrystals);
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

                AddHoop(plan, new Vector3(wander.x, wander.y, z), tilt, gateRadius, perGate, rng);
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
            plan.FloraCount = 1 + rng.Next(2);
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
            plan.FaunaCount = 1 + rng.Next(2);
        }

        // ── New eight ────────────────────────────────────────────────────────

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
                AddHoop(plan, Vector3.zero, tilt, ringRadius, perRing, rng);
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

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// A prism hoop: long axes chained around the circumference (the shipped ring-gate look)
        /// so the gate reads as a continuous hoop rather than dotted tiles.
        /// </summary>
        static void AddHoop(MicroscenePlan plan, Vector3 center, Quaternion tilt, float ringRadius, int count, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector3 radial = tilt * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 tangent = tilt * new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                var rot = Quaternion.LookRotation(tangent, radial);
                plan.PrismPoints.Add(new SpawnPoint(center + radial * ringRadius, rot, StrandScale(rng)));
            }
        }

        /// <summary>Elongated strand segment (~1.7×1.7×6.5) — long axis is local +z (helix/ring/spoke).</summary>
        static Vector3 StrandScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.25f) * bias;
            return new Vector3(1.7f * j, 1.7f * j, 6.5f * j);
        }

        /// <summary>Broad wall plate (~5.5×5.5×1.2) for fins and ground panels.</summary>
        static Vector3 PlateScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.85f, 1.3f) * bias;
            return new Vector3(5.5f * j, 5.5f * j, 1.2f);
        }

        /// <summary>Tall trunk segment — long axis is local +y.</summary>
        static Vector3 TrunkScale(System.Random rng)
        {
            float j = Range(rng, 0.85f, 1.2f);
            return new Vector3(1.8f * j, 6.5f * j, 1.8f * j);
        }

        /// <summary>Nominal-ish chunk (4×4×1 ≈ the 16-volume leaf) with organic jitter — scatter/canopy.</summary>
        static Vector3 ChunkScale(System.Random rng, float bias = 1f)
        {
            float j = Range(rng, 0.8f, 1.35f) * bias;
            return new Vector3(4f * j, 4f * j, 1f);
        }

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

        static float Range(System.Random rng, float min, float max) => (float)(rng.NextDouble() * (max - min) + min);

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        static int RangeInt(System.Random rng, int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);

        static Vector3 OnUnitSphere(System.Random rng)
        {
            // Polar pick — good enough distribution for scenery.
            float z = Range(rng, -1f, 1f);
            float a = Range(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }

        static Vector3 InsideUnitSphere(System.Random rng) =>
            OnUnitSphere(rng) * Mathf.Pow(Range(rng, 0f, 1f), 1f / 3f);
    }
}
