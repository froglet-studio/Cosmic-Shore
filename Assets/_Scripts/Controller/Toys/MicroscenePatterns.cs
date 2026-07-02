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
    /// tuned for a Squirrel run (gates to thread, tunnels to ride, walls to slalom, orchards to
    /// weave, meadows and menageries that seed the living ecosystem). Every recipe emits exactly
    /// <c>prismBudget</c> prism points so a recycled scene can re-pose its fixed stock of prisms
    /// into any recipe (the conveyor is a closed system: same prisms, new arrangement).
    ///
    /// Prism sizing follows the shipped structures (HexRace ribbon 10×1×3, ring gates ~1.8×1.8×7.5,
    /// helix strands 1×1×5): the LONG axis runs along the structure's own path, so sparse counts
    /// still read as hoops / strands / walls rather than dotted specks.
    /// </summary>
    public static class MicroscenePatterns
    {
        public const int RecipeCount = 8;

        static readonly string[] Names =
        {
            "Gate Run", "Helix Weave", "Tunnel", "Slalom", "Starburst", "Orchard", "Meadow", "Menagerie",
        };

        public static string RecipeName(int recipe) => Names[Mathf.Abs(recipe) % RecipeCount];

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
            }

            FitToBudget(plan, rng, prismBudget, radius);
            ClampCrystals(plan, rng, maxCrystals);
            return plan;
        }

        // ── Recipes ──────────────────────────────────────────────────────────

        /// <summary>A corridor of tilted prism hoops to thread, drifting off-axis gate to gate.</summary>
        static void GateRun(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int gates = Mathf.Clamp(budget / 11, 3, 5);
            int perGate = budget / gates;
            Vector3 wander = Vector3.zero;

            for (int g = 0; g < gates; g++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, gates > 1 ? g / (float)(gates - 1) : 0.5f);
                wander += new Vector3(Range(rng, -0.28f, 0.28f), Range(rng, -0.22f, 0.22f), 0f) * radius;
                wander = Vector3.ClampMagnitude(wander, radius * 0.55f);
                float gateRadius = Range(rng, 16f, 23f);
                Quaternion tilt = Quaternion.Euler(Range(rng, -16f, 16f), Range(rng, -16f, 16f), 0f);

                AddHoop(plan, new Vector3(wander.x, wander.y, z), tilt, gateRadius, perGate, rng);
                if (g == gates - 1)
                    plan.CrystalPoints.Add(new Vector3(wander.x, wander.y, z + 24f));
            }
        }

        /// <summary>Two intertwined prism strands spiralling along the flight axis.</summary>
        static void HelixWeave(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int perStrand = budget / 2;
            float helixRadius = radius * Range(rng, 0.3f, 0.42f);
            float turns = Range(rng, 1.4f, 2.4f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);

            for (int s = 0; s < 2; s++)
            {
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < perStrand; i++)
                {
                    float t = perStrand > 1 ? i / (float)(perStrand - 1) : 0f;
                    float angle = phase + s * Mathf.PI + t * turns * Mathf.PI * 2f;
                    var pos = new Vector3(
                        Mathf.Cos(angle) * helixRadius,
                        Mathf.Sin(angle) * helixRadius,
                        Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
                    // Long axis chains along the strand so it reads as a continuous ribbon.
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
            int rails = 8;
            int perRail = Mathf.Max(3, budget / rails);
            float tubeRadius = Range(rng, 21f, 26f);
            float tubeLength = length * 0.7f; // shorter + denser so the rails read continuous
            float bendX = Range(rng, -0.3f, 0.3f) * radius;
            float bendY = Range(rng, -0.25f, 0.25f) * radius;
            float twist = Range(rng, -0.6f, 0.6f); // radians of roll over the tube
            Vector3 exit = Vector3.zero;

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
                    if (r == 0 && i == perRail - 1) exit = new Vector3(0f, 0f, pos.z); // tube ends on-axis
                }
            }
            plan.CrystalPoints.Add(exit + Vector3.forward * 26f);
        }

        /// <summary>Alternating wall fins to slalom between.</summary>
        static void Slalom(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int fins = Mathf.Clamp(budget / 8, 4, 7);
            int perFin = budget / fins;
            int columns = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(perFin)));

            for (int f = 0; f < fins; f++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, fins > 1 ? f / (float)(fins - 1) : 0.5f);
                float side = (f % 2 == 0) ? 1f : -1f;
                float finX = side * radius * Range(rng, 0.18f, 0.4f);
                float baseY = Range(rng, -0.25f, 0.25f) * radius;
                var rot = Quaternion.Euler(0f, 90f + Range(rng, -10f, 10f), 0f); // wall faces the corridor

                for (int i = 0; i < perFin; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    var pos = new Vector3(
                        finX + side * col * 6.5f,
                        baseY + (row - (perFin / columns) * 0.5f) * 6.5f,
                        z + Range(rng, -2f, 2f));
                    plan.PrismPoints.Add(new SpawnPoint(pos, rot, PlateScale(rng)));
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 18f));
        }

        /// <summary>A radial prism sculpture to orbit and skim, crystal at the heart.</summary>
        static void Starburst(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            int spokes = Mathf.Clamp(budget / 5, 6, 12);
            int perSpoke = budget / spokes;

            for (int s = 0; s < spokes; s++)
            {
                Vector3 dir = OnUnitSphere(rng);
                var rot = SpawnPoint.LookRotation(dir, Vector3.up); // long axis along the spoke
                for (int i = 0; i < perSpoke; i++)
                {
                    float dist = Mathf.Lerp(12f, radius * 0.85f, perSpoke > 1 ? i / (float)(perSpoke - 1) : 0.5f);
                    plan.PrismPoints.Add(new SpawnPoint(dir * dist, rot, StrandScale(rng)));
                }
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A grove of prism trees to weave between, crystals hidden in the canopy.</summary>
        static void Orchard(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int trees = Mathf.Clamp(budget / 8, 4, 6);
            int perTree = budget / trees;

            for (int t = 0; t < trees; t++)
            {
                var root = new Vector3(
                    Range(rng, -0.7f, 0.7f) * radius,
                    Range(rng, -0.55f, 0.25f) * radius,
                    Range(rng, -0.5f, 0.5f) * length);
                int trunk = Mathf.Max(2, perTree / 2);

                for (int i = 0; i < perTree; i++)
                {
                    if (i < trunk)
                    {
                        // Trunk: tall segments stacked nearly end-to-end.
                        var pos = root + Vector3.up * (i * 6f);
                        var rot = Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f));
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, TrunkScale(rng)));
                    }
                    else
                    {
                        // Canopy puff above the trunk.
                        var pos = root + Vector3.up * (trunk * 6f) + InsideUnitSphere(rng) * 12f;
                        var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, ChunkScale(rng)));
                    }
                }

                if (t % 2 == 0)
                    plan.CrystalPoints.Add(root + Vector3.up * (trunk * 6f + 16f));
            }
        }

        /// <summary>A sparse, open field — shallow ground plates, a crystal, and flora seeded into the cell.</summary>
        static void Meadow(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            for (int i = 0; i < budget; i++)
            {
                var pos = new Vector3(
                    Range(rng, -0.85f, 0.85f) * radius,
                    -radius * 0.45f + Mathf.Abs(Range(rng, 0f, 0.25f)) * radius,
                    Range(rng, -0.5f, 0.5f) * length);
                // Plates lying flat like ground panels.
                var rot = Quaternion.Euler(90f + Range(rng, -18f, 18f), Range(rng, 0f, 360f), 0f);
                plan.PrismPoints.Add(new SpawnPoint(pos, rot, PlateScale(rng, 0.85f)));
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
            plan.FloraCount = 1 + rng.Next(2);
        }

        /// <summary>Loose prey clumps with wildlife released into the cell to hunt them.</summary>
        static void Menagerie(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int clumps = Mathf.Clamp(budget / 10, 3, 5);
            int perClump = budget / clumps;

            for (int c = 0; c < clumps; c++)
            {
                var center = new Vector3(
                    Range(rng, -0.6f, 0.6f) * radius,
                    Range(rng, -0.4f, 0.4f) * radius,
                    Range(rng, -0.45f, 0.45f) * length);
                for (int i = 0; i < perClump; i++)
                {
                    var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                    plan.PrismPoints.Add(new SpawnPoint(center + InsideUnitSphere(rng) * 14f, rot, ChunkScale(rng)));
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 16f));
            plan.FaunaCount = 1 + rng.Next(2);
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

        /// <summary>Elongated strand segment (~1.6×1.6×6) — long axis is local +z (helix/ring/spoke).</summary>
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
