using System.Collections.Generic;
using UnityEngine;
using static CosmicShore.Gameplay.PrismGeometry;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pure generators for the conveyor's microscene recipes - each a small flyable set piece tuned
    /// for a Squirrel run. Every recipe re-rolls its own parameters (radii, counts, twists, bends,
    /// phases) on EVERY plan, so the same recipe never lands the same way twice, and every recipe is
    /// fitted to exactly <c>prismBudget</c> prism points so a recycled scene can re-pose its fixed
    /// stock of prisms into any recipe without creating or destroying mass (the conveyor is a closed
    /// system: same prisms, new arrangement).
    ///
    /// Geometry is produced through the shared <see cref="PrismGeometry"/> vocabulary (helices,
    /// hoops, tubes, arches, vortices, corridors, lattices, torus rings, fans, scatters, wave
    /// sheets, swept decks, shells, torus knots, Möbius bands, petals, terraces…). Recipes stamp
    /// STRUCTURAL METADATA as they emit - <see cref="MicroscenePlan.CloseStructure"/> after each
    /// gate / strand / tree / wall tags every point with its substructure id + t-along-path - and
    /// the "Medley" recipes compose spine × motif combinatorially, so the parametric space is far
    /// larger than the recipe count. The recipe knows ONLY shape; <see cref="MicroscenePainter"/>
    /// then paints each scene from a <see cref="MicroscenePalette"/> - structural domain schemes
    /// (alternating gates, gradients, pinwheels, stripes, mirrors, veins), structural prism kinds
    /// (danger gates/tips, armoured frames, keystone landmarks), scale moods (uniform / long-axis
    /// stretch / taper), and the elemental/omni crystal mix - so variety lands as deliberate
    /// construction features, never chaotic confetti.
    ///
    /// Prism sizing follows the shipped structures (HexRace ribbon 10×1×3, ring gates ~1.8×1.8×7.5,
    /// helix strands 1×1×5): the LONG axis runs along the structure's own path, so sparse counts
    /// still read as hoops / strands / walls rather than dotted specks. Every scale family jitters
    /// each axis independently, so no two prisms share exact proportions.
    /// </summary>
    public static class MicroscenePatterns
    {
        /// <summary>
        /// The classic recipes — hand-tuned in ABSOLUTE world units (gate radii 13-28, ribbon
        /// prisms 10×1×3) around <see cref="DesignRadius"/>, and sized by DIVIDING the budget
        /// (a gate run is always 3-6 gates however much mass it is handed). They are generated at
        /// their design scale and then scaled bodily to the scene, which preserves their
        /// proportions exactly at any belt size.
        /// </summary>
        public const int ClassicRecipeCount = 40;

        /// <summary>Classic + <see cref="MicroscenePatternsGrand"/>. Recipe indices at or above
        /// <see cref="ClassicRecipeCount"/> address the grand assemblies.</summary>
        public static int RecipeCount => ClassicRecipeCount + MicroscenePatternsGrand.Count;

        /// <summary>
        /// The scene radius the classic recipes were authored against. A scene laid at a different
        /// radius gets their geometry scaled by <c>sceneRadius / DesignRadius</c> — POSITIONS only,
        /// never prism scales: a grand scene should read as more architecture at the same grain,
        /// not as the same architecture built out of boulders (and per-prism volume feeds the host
        /// cell's phase ladder, which must not inflate just because the belt got bigger).
        /// </summary>
        public const float DesignRadius = 80f;

        static readonly string[] Names =
        {
            "Gate Run", "Helix Weave", "Tunnel", "Slalom", "Starburst", "Orchard", "Meadow", "Menagerie",
            "Polygon Gates", "Serpent Ribbon", "Colonnade", "Orbitals", "Canyon", "Lattice", "Comet Tail", "Spiral Ramp",
            "Archway", "Vortex", "Slot Corridor", "Cube Field", "Torus Gate", "Pillar Hall", "Turbine", "Asteroid Field",
            "Rolling Plains", "Grove", "Aviary", "Preserve",
            "Dome", "Grotto", "Torus Knot", "Mobius Rail", "Rosette", "Terrace Spiral", "Ribbon Chicane", "Split Tube",
            "Medley", "Medley II", "Medley III", "Medley IV",
        };

        public static string RecipeName(int recipe)
        {
            int r = Mathf.Abs(recipe) % RecipeCount;
            return r < ClassicRecipeCount ? Names[r] : MicroscenePatternsGrand.Name(r - ClassicRecipeCount);
        }

        /// <summary>Recipes that release lifeforms into the host cell (skipped when lifeform scenes are disabled).</summary>
        public static bool IsLifeformRecipe(int recipe)
        {
            int r = Mathf.Abs(recipe) % RecipeCount;
            // Meadow, Menagerie, Rolling Plains, Grove, Aviary, Preserve. The grand assemblies are
            // architecture and request none.
            return r == 6 || r == 7 || r == 24 || r == 25 || r == 26 || r == 27;
        }

        /// <summary>
        /// True for the monument-scale family (<see cref="MicroscenePatternsGrand"/>), which only
        /// reads as intended once a scene's prism budget is in the hundreds — below that the belt
        /// should stay on the classic recipes.
        /// </summary>
        public static bool IsGrandRecipe(int recipe) => Mathf.Abs(recipe) % RecipeCount >= ClassicRecipeCount;

        /// <summary>Scene prism budget at or above which the grand assemblies join the shuffle bag.</summary>
        public const int GrandBudgetThreshold = 400;

        /// <summary>
        /// Build the plan for one microscene. <paramref name="sceneRadius"/> bounds the lateral
        /// extent; the scene runs roughly 2.2 × that along +z so it reads as a place you fly
        /// THROUGH. Classic recipes are generated at <see cref="DesignRadius"/> and scaled to the
        /// scene; grand assemblies are authored at the scene's own scale.
        /// <paramref name="palette"/> drives theming (domain/kind/scale/crystal mix); null = defaults.
        /// </summary>
        public static MicroscenePlan Plan(int recipe, System.Random rng, int prismBudget, float sceneRadius, int maxCrystals,
            MicroscenePalette palette = null)
        {
            var plan = new MicroscenePlan { RecipeName = RecipeName(recipe) };
            int recipeIndex = Mathf.Abs(recipe) % RecipeCount;

            if (recipeIndex >= ClassicRecipeCount)
            {
                // Grand assemblies take the scene radius as their own basis and multiply their part
                // counts with the budget — no rescale pass.
                MicroscenePatternsGrand.Build(recipeIndex - ClassicRecipeCount, plan, rng, prismBudget, sceneRadius);
                plan.CloseStructure();
                FitToBudget(plan, rng, prismBudget, sceneRadius);
                ClampCrystals(plan, rng, maxCrystals);
                MicroscenePainter.Paint(plan, rng, palette);
                return plan;
            }

            // Classic recipes are generated at the radius they were authored against and scaled
            // bodily afterwards (see DesignRadius).
            float radius = DesignRadius;
            float length = radius * 2.2f;

            switch (recipeIndex)
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
                case 28: Dome(plan, rng, prismBudget, radius, length); break;
                case 29: Grotto(plan, rng, prismBudget, radius, length); break;
                case 30: TorusKnotChase(plan, rng, prismBudget, radius, length); break;
                case 31: MobiusRail(plan, rng, prismBudget, radius); break;
                case 32: Rosette(plan, rng, prismBudget, radius, length); break;
                case 33: TerraceSpiral(plan, rng, prismBudget, radius, length); break;
                case 34: RibbonChicane(plan, rng, prismBudget, radius, length); break;
                case 35: SplitTube(plan, rng, prismBudget, radius, length); break;
                case 36:
                case 37:
                case 38:
                case 39: Medley(plan, rng, prismBudget, radius, length); break;
            }

            plan.CloseStructure(); // sweep any untagged tail into a final substructure
            ScaleToScene(plan, sceneRadius / DesignRadius);
            FitToBudget(plan, rng, prismBudget, sceneRadius);
            ClampCrystals(plan, rng, maxCrystals);
            MicroscenePainter.Paint(plan, rng, palette);
            return plan;
        }

        /// <summary>
        /// Blow a design-scale plan up (or down) to the live scene size. POSITIONS only — see
        /// <see cref="DesignRadius"/> for why prism scales deliberately stay put. Rotations are
        /// scale-invariant, and the structural metadata is untouched, so the painter still themes
        /// the same architecture.
        /// </summary>
        static void ScaleToScene(MicroscenePlan plan, float k)
        {
            if (Mathf.Approximately(k, 1f)) return;

            var points = plan.PrismPoints;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                p.Position *= k;
                points[i] = p;
            }

            var crystals = plan.CrystalPoints;
            for (int i = 0; i < crystals.Count; i++)
                crystals[i] *= k;
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
                // End gates sit inset from the scene ends so a tilted wide hoop (up to ~31° combined
                // tilt × 28 radius ≈ 14.3 of z-reach) stays inside the advertised scene envelope.
                float z = Mathf.Lerp(-length * 0.5f + 15f, length * 0.5f - 15f, gates > 1 ? g / (float)(gates - 1) : 0.5f);
                wander += new Vector3(Range(rng, -wanderStrength, wanderStrength),
                                      Range(rng, -wanderStrength, wanderStrength) * 0.8f, 0f) * radius;
                wander = Vector3.ClampMagnitude(wander, radius * 0.55f);
                float gateRadius = Range(rng, 13f, 28f);
                Quaternion tilt = Quaternion.Euler(Range(rng, -22f, 22f), Range(rng, -22f, 22f), 0f);

                AddHoop(plan.PrismPoints, new Vector3(wander.x, wander.y, z), tilt, gateRadius, perGate, rng);
                plan.CloseStructure(); // each gate is one substructure
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
                plan.CloseStructure(); // each strand is one substructure
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
                plan.CloseStructure(); // each rail is one substructure
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, exitZ + 26f));
        }

        /// <summary>Alternating wall fins to slalom between.</summary>
        static void Slalom(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int fins = Mathf.Clamp(budget / RangeInt(rng, 6, 10), 4, 8);
            int perFin = budget / fins;
            int columns = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(perFin)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(perFin / (float)columns));
            float plateBias = Range(rng, 0.85f, 1.25f);

            // Each fin is a columns×rows grid stepping OUTWARD from finX, and `columns` grows as
            // √perFin - so a fixed step makes the fin's SIZE a function of the BUDGET and the grid
            // marches out of the scene (at the shipped 1500 it reached 1.34× the advertised extent).
            // Same trap the grand recipes' spire records: derive the step from a target, never from
            // the count. 6.5 stays the authored step and every budget that already fitted is
            // unchanged - only a fin too dense to fit compresses.
            float pitch = Mathf.Min(6.5f, Mathf.Min(
                (radius - radius * 0.42f) / Mathf.Max(1, columns - 1),   // widest finX rolled below
                (radius - radius * 0.28f) / Mathf.Max(1f, rows * 0.5f))); // largest |baseY| rolled below

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
                        finX + side * col * pitch,
                        baseY + (row - (perFin / columns) * 0.5f) * pitch,
                        z + Range(rng, -2f, 2f));
                    plan.PrismPoints.Add(new SpawnPoint(pos, rot, PlateScale(rng, plateBias)));
                }
                plan.CloseStructure(); // each fin is one substructure
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
                plan.CloseStructure(); // each spoke is one substructure (t runs core → tip)
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
                // Trunk height is otherwise budget-driven and radius-blind - cap it so root + trunk
                // + canopy ball stays inside the scene's vertical envelope at large budgets (the
                // spare points thicken the canopy instead).
                int trunk = Mathf.Max(2, perTree / 2);
                int maxTrunk = Mathf.Max(2, Mathf.FloorToInt((radius * 1.05f - root.y - 12f) / segment));
                trunk = Mathf.Min(trunk, maxTrunk);

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
                plan.CloseStructure(); // each tree is one substructure (t runs trunk → canopy)

                if (t % 2 == 0)
                    plan.CrystalPoints.Add(root + Vector3.up * (trunk * segment + 16f));
            }
        }

        /// <summary>A sparse, open field - undulating ground plates, a crystal, flora seeded into the cell.</summary>
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
                plan.CloseStructure(); // each clump is one substructure
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
                var center = new Vector3(Range(rng, -0.2f, 0.2f) * radius, Range(rng, -0.2f, 0.2f) * radius, z);
                AddPolygonGate(plan.PrismPoints, center, Quaternion.identity, sides, gateRadius, perSide, g * spin, rng);
                plan.CloseStructure(); // each gate is one substructure
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 22f));
        }

        /// <summary>A single sinuous ribbon wall to surf along - plates chained on a 3D sine path.</summary>
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
                    plan.CloseStructure(); // each pillar is one substructure (t runs base → top)
                }
            }
            plan.CrystalPoints.Add(new Vector3(0f, baseY + pillarHeight * segment * 0.5f, length * 0.5f + 18f));
        }

        /// <summary>Concentric tilted rings around a heart crystal - a gyroscope to weave through.</summary>
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
                plan.CloseStructure(); // each ring is one substructure
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

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < steps; i++)
                {
                    float t = steps > 1 ? i / (float)(steps - 1) : 0.5f;
                    float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                    float bend = Mathf.Sin(phase + t * cycles * Mathf.PI * 2f) * ampX;

                    for (int h = 0; h < wallHeight; h++)
                    {
                        var pos = new Vector3(bend + side * halfGap, baseY + (h - wallHeight * 0.5f) * 6.5f, z);
                        var rot = Quaternion.Euler(0f, 90f, Range(rng, -8f, 8f)); // plates face the slot
                        plan.PrismPoints.Add(new SpawnPoint(pos, rot, PlateScale(rng)));
                    }
                }
                plan.CloseStructure(); // each canyon wall is one substructure (t runs entry → exit)
            }
            plan.CrystalPoints.Add(new Vector3(0f, baseY, length * 0.5f + 18f));
        }

        /// <summary>Criss-crossing diagonal strands - a loose weave with gaps to pick a line through.</summary>
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
                plan.CloseStructure(); // each diagonal strand is one substructure
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
        }

        /// <summary>A widening debris cone converging on a crystal at the apex - fly up the tail.</summary>
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

        /// <summary>A single strand unrolling outward around the axis - an expanding spiral ramp.</summary>
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
                plan.CloseStructure(); // each arch is one substructure (t runs foot → foot)
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 18f));
        }

        /// <summary>Converging arms with an OPEN convergence mouth + an inviting crystal to skim into.</summary>
        static void Vortex(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int arms = RangeInt(rng, 3, 6);
            int perArm = Mathf.Max(3, budget / arms);
            float startRadius = radius * Range(rng, 0.5f, 0.75f);
            float turns = Range(rng, 0.6f, 1.6f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            for (int s = 0; s < arms; s++)
            {
                AddVortexArm(plan.PrismPoints, phase + s * (Mathf.PI * 2f / arms), perArm, startRadius, length, turns, rng);
                plan.CloseStructure(); // each arm is one substructure (t runs rim → convergence)
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.4f)); // at the open mouth
        }

        /// <summary>Two parallel plate walls with gaps - a slot to roll and slip through.</summary>
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
                plan.CloseStructure(); // each torus ring is one substructure
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A hall of pillars to fly between, crystal past the far end.</summary>
        static void PillarHall(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int cols = Mathf.Clamp(budget / RangeInt(rng, 4, 7), 4, 10);
            int per = Mathf.Max(2, budget / cols);
            float spread = radius * Range(rng, 0.5f, 0.8f);
            // AddPillarColumn centres the column on baseXZ and takes a per-SEGMENT length, so a
            // fixed segment makes the hall's HEIGHT scale with the budget and shoot out of the
            // scene - exactly what MicroscenePatternsGrand's spire records. Clamped rather than
            // re-rolled so the RNG stream (and every budget that already fitted) is unchanged.
            float segment = Mathf.Min(Range(rng, 6f, 8f),
                radius * 1.6f / Mathf.Max(1, per - 1));
            for (int c = 0; c < cols; c++)
            {
                var baseXZ = new Vector3(Range(rng, -spread, spread), 0f, Range(rng, -0.5f, 0.5f) * length);
                AddPillarColumn(plan.PrismPoints, baseXZ, per, segment, rng);
                plan.CloseStructure(); // each column is one substructure (t runs base → top)
            }
            plan.CrystalPoints.Add(new Vector3(0f, radius * 0.1f, length * 0.5f + 16f));
        }

        /// <summary>Radial blades fanning off the axis - a turbine to weave, crystal at the hub.</summary>
        static void Turbine(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            int blades = RangeInt(rng, 4, 9);
            int per = Mathf.Max(3, budget / blades);
            float reach = radius * Range(rng, 0.6f, 0.85f);
            float twist = Range(rng, 0.3f, 1.2f);
            for (int b = 0; b < blades; b++)
            {
                AddFanBlade(plan.PrismPoints, b / (float)blades * Mathf.PI * 2f, per, reach, twist, rng);
                plan.CloseStructure(); // each blade is one substructure (t runs hub → tip)
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A loose asteroid field to slalom, crystal drifting in it.</summary>
        static void AsteroidField(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            AddScatter(plan.PrismPoints, budget, radius, length, rng);
            plan.CrystalPoints.Add(new Vector3(Range(rng, -0.2f, 0.2f) * radius, 0f, Range(rng, -0.2f, 0.2f) * length));
        }

        /// <summary>An open rolling floor to skim along - flora seeded into the cell.</summary>
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

        /// <summary>An open preserve - a rolling floor with BOTH flora and fauna released into the cell.</summary>
        static void Preserve(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int nx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(budget)), 3, 7);
            AddWaveSheet(plan.PrismPoints, nx, Mathf.Max(2, budget / 6), radius, length, Range(rng, 0.05f, 0.12f) * radius, rng);
            plan.CrystalPoints.Add(new Vector3(0f, 0f, Range(rng, -0.2f, 0.2f) * length));
            plan.FloraCount = 1 + rng.Next(2);
            plan.FaunaCount = 1 + rng.Next(2);
        }

        // ── The third twelve (surfaces, curves & composed medleys) ──────────
        // These lean on the superstructure-oriented primitives: prisms take their orientation from
        // the construction's own frame (curve tangents, surface normals, twisting bands), so sparse
        // prisms read as continuous curved surfaces rather than jittered tiles.

        /// <summary>A shingled bowl below the flight line to skim across, debris drifting above it.</summary>
        static void Dome(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int shell = Mathf.Max(4, (budget * 4) / 5);
            float sphere = radius * Range(rng, 0.55f, 0.75f);
            // Apex pointing DOWN → the cap opens upward: a bowl under the flight path.
            var orient = Quaternion.Euler(90f + Range(rng, -10f, 10f), Range(rng, 0f, 360f), 0f);
            AddShellPatch(plan.PrismPoints, new Vector3(0f, -radius * 0.25f, 0f), orient, sphere,
                Range(rng, 55f, 75f), shell, rng);
            plan.CloseStructure(); // the shell (t runs apex → rim)

            for (int i = 0; i < budget - shell; i++) // drifting debris above the bowl
            {
                var pos = new Vector3(Range(rng, -0.5f, 0.5f) * radius, Range(rng, 0.1f, 0.5f) * radius,
                    Range(rng, -0.4f, 0.4f) * length);
                var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                plan.PrismPoints.Add(new SpawnPoint(pos, rot, ChunkScale(rng, 0.9f)));
            }
            plan.CloseStructure(); // the debris

            plan.CrystalPoints.Add(new Vector3(0f, -radius * 0.25f + sphere * Range(rng, 0.55f, 0.75f), 0f));
        }

        /// <summary>An overhead shingled vault to fly UNDER, held up by pillar columns.</summary>
        static void Grotto(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int pillars = RangeInt(rng, 2, 5);
            int perPillar = Mathf.Max(2, budget / (pillars * 4));
            int shell = Mathf.Max(4, budget - pillars * perPillar);

            float sphere = radius * Range(rng, 0.55f, 0.75f);
            // Apex pointing UP → the cap opens downward: a vault over the flight path.
            var orient = Quaternion.Euler(-90f + Range(rng, -10f, 10f), Range(rng, 0f, 360f), 0f);
            AddShellPatch(plan.PrismPoints, new Vector3(0f, radius * 0.25f, 0f), orient, sphere,
                Range(rng, 50f, 70f), shell, rng);
            plan.CloseStructure(); // the vault (t runs apex → rim)

            float ring = sphere * Range(rng, 0.6f, 0.85f);
            for (int p = 0; p < pillars; p++)
            {
                float a = p / (float)pillars * Mathf.PI * 2f + Range(rng, -0.3f, 0.3f);
                var baseXZ = new Vector3(Mathf.Cos(a) * ring, -radius * 0.2f, Mathf.Sin(a) * ring);
                // Same budget-scaled-height trap as Pillar Hall. These columns hang below the
                // vault (baseXZ.y = -radius*0.2), so the target is tighter than the hall's.
                AddPillarColumn(plan.PrismPoints, baseXZ, perPillar,
                    Mathf.Min(Range(rng, 6f, 8f), radius * 1.2f / Mathf.Max(1, perPillar - 1)), rng);
                plan.CloseStructure(); // each supporting column
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 16f));
        }

        /// <summary>A (p,q) torus knot standing across the flight path - a self-weaving loop to chase
        /// through. Emitted in thirds so structural painting can band the weave.</summary>
        static void TorusKnotChase(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            (int p, int q) = rng.Next(2) == 0 ? (2, 3) : (3, 2);
            float major = radius * Range(rng, 0.38f, 0.52f);
            float minor = major * Range(rng, 0.3f, 0.45f);
            float zAmp = Range(rng, 1.6f, 3f);
            var orient = Quaternion.Euler(Range(rng, -18f, 18f), Range(rng, -18f, 18f), 0f);

            int thirds = 3;
            int per = Mathf.Max(3, budget / thirds);
            for (int s = 0; s < thirds; s++)
            {
                AddTorusKnotSegment(plan.PrismPoints, orient, p, q, major, minor, zAmp,
                    s / (float)thirds, (s + 1f) / thirds, per, rng);
                plan.CloseStructure(); // each third of the weave
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A twisted plate band standing across the path (1 or 3 half-twists - a true Möbius
        /// ring at 1). The rolling orientation IS the read; crystal in the eye.</summary>
        static void MobiusRail(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            float ring = radius * Range(rng, 0.45f, 0.68f);
            float halfTwists = rng.Next(2) == 0 ? 1f : 3f;
            var tilt = Quaternion.Euler(Range(rng, -22f, 22f), Range(rng, -22f, 22f), 0f);

            int arcs = 3; // emitted in arcs so per-structure painting can band the ring
            int per = Mathf.Max(3, budget / arcs);
            for (int s = 0; s < arcs; s++)
            {
                AddMobiusArc(plan.PrismPoints, Vector3.zero, tilt, ring, per, halfTwists,
                    s / (float)arcs, (s + 1f) / arcs, rng);
                plan.CloseStructure();
            }
            plan.CrystalPoints.Add(Vector3.zero);
        }

        /// <summary>A corolla of strand petals curling out around the flight axis - fly the open
        /// throat, crystal at the heart.</summary>
        static void Rosette(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int petals = Mathf.Clamp(budget / RangeInt(rng, 5, 9), 4, 9);
            int perPetal = Mathf.Max(3, budget / petals);
            float curl = Range(rng, 110f, 160f);
            // Size the petal so its outward reach (petalRadius × (1 − cos curl)) stays inside the scene.
            float petalRadius = radius * 0.72f / (1f - Mathf.Cos(curl * Mathf.Deg2Rad));
            var center = new Vector3(0f, 0f, -length * 0.2f);
            var orient = Quaternion.Euler(Range(rng, -10f, 10f), Range(rng, -10f, 10f), 0f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);

            for (int p = 0; p < petals; p++)
            {
                AddPetalArc(plan.PrismPoints, center, orient, phase + p / (float)petals * Mathf.PI * 2f,
                    petalRadius, curl, perPetal, rng);
                plan.CloseStructure(); // each petal (t runs root → tip)
            }
            plan.CrystalPoints.Add(center + orient * Vector3.forward * 14f);
        }

        /// <summary>A rifled corkscrew of rideable plates widening along the run - carve the inside
        /// of the barrel to the crystal at the muzzle.</summary>
        static void TerraceSpiral(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            AddTerraceTreads(plan.PrismPoints, Range(rng, 8f, 15f), radius * Range(rng, 0.5f, 0.75f),
                Range(rng, 1.5f, 3f), length * Range(rng, 0.7f, 0.9f), budget, rng);
            plan.CloseStructure(); // one continuous surface (t runs entry → exit)
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 18f));
        }

        /// <summary>A banked plate road weaving through the scene - one or two lanes of deck to surf,
        /// rolling into every turn like a velodrome.</summary>
        static void RibbonChicane(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int lanes = RangeInt(rng, 1, 3);
            int steps = Mathf.Max(4, budget / lanes);
            float ampX = Range(rng, 0.2f, 0.42f) * radius;
            float ampY = Range(rng, 0.08f, 0.28f) * radius;
            float cyclesX = Range(rng, 1f, 2.2f);
            float cyclesY = Range(rng, 0.5f, 1.4f);
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            float bank = Range(rng, 30f, 80f);
            float laneGap = Range(rng, 9f, 14f);

            for (int lane = 0; lane < lanes; lane++)
            {
                float laneX = (lane - (lanes - 1) * 0.5f) * laneGap;
                Vector3 Spine(float t) => new(
                    laneX + Mathf.Sin(phase + t * cyclesX * Mathf.PI * 2f) * ampX,
                    Mathf.Sin(phase * 0.7f + t * cyclesY * Mathf.PI * 2f) * ampY - radius * 0.1f,
                    Mathf.Lerp(-length * 0.5f, length * 0.5f, t));
                AddSweptPath(plan.PrismPoints, Spine, steps, SweepMode.Deck, bank, rng);
                plan.CloseStructure(); // each lane (t runs entry → exit)
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, length * 0.5f + 18f));
        }

        /// <summary>Two facing curved shell walls - a split tube whose slot bends around you.</summary>
        static void SplitTube(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float tube = radius * Range(rng, 0.32f, 0.45f);
            float arc = Range(rng, 80f, 130f);
            float run = length * Range(rng, 0.6f, 0.8f);
            int perWall = Mathf.Max(4, budget / 2);
            int rows = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(perWall * 1.6f)));
            int cols = Mathf.Max(2, perWall / rows);
            float roll = Range(rng, 0f, 180f); // where the slot sits - side-side, over-under, any tilt

            for (int side = 0; side < 2; side++)
            {
                var orient = Quaternion.Euler(0f, 0f, roll + side * 180f);
                AddCylinderShell(plan.PrismPoints, Vector3.zero, orient, tube, arc, run, rows, cols, rng);
                plan.CloseStructure(); // each curved wall (t runs entry → exit)
            }
            plan.CrystalPoints.Add(new Vector3(0f, 0f, run * 0.5f + 20f));
        }

        // ── Medley (the composer) ────────────────────────────────────────────

        /// <summary>
        /// The combinatorial recipe: a SPINE (straight / arc / S-curve / helix drift) threaded with
        /// alternating MOTIFS (hoops, polygon gates, torus rings, shell dishes, blade crosses,
        /// clusters), roll advancing station to station. Four bag slots draw from this space, so the
        /// belt keeps producing constructions no fixed recipe list could enumerate - while every
        /// station still sits ON the spine, biased to be flown through.
        /// </summary>
        static void Medley(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int spineKind = rng.Next(4);
            float ampX = Range(rng, 0.15f, 0.38f) * radius;
            float ampY = Range(rng, 0.08f, 0.28f) * radius;
            float phase = Range(rng, 0f, Mathf.PI * 2f);

            Vector3 SpineAt(float t)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                return spineKind switch
                {
                    1 => new Vector3(Mathf.Sin(t * Mathf.PI) * ampX, Mathf.Sin(t * Mathf.PI) * ampY * 0.5f, z),
                    2 => new Vector3(Mathf.Sin(phase + t * Mathf.PI * 2f) * ampX,
                                     Mathf.Sin(phase * 0.6f + t * Mathf.PI * 1.3f) * ampY, z),
                    3 => new Vector3(Mathf.Cos(phase + t * Mathf.PI * 2f) * ampX * 0.7f,
                                     Mathf.Sin(phase + t * Mathf.PI * 2f) * ampY * 0.7f, z),
                    _ => new Vector3(0f, 0f, z),
                };
            }

            int stations = Mathf.Clamp(budget / RangeInt(rng, 9, 15), 3, 7);
            int perStation = Mathf.Max(3, budget / stations);
            // Two motifs alternating reads as a rhythm; six rolled at once reads as a junk drawer.
            int motifA = rng.Next(MotifCount);
            int motifB = rng.Next(MotifCount);
            float roll = Range(rng, 0f, 360f);
            float rollStep = Range(rng, -40f, 40f);

            for (int s = 0; s < stations; s++)
            {
                // Stations stay off the extreme ends (t ∈ [0.08, 0.92]) and the motif plane leans
                // only HALFWAY into the spine's tilt (bisecting with the flight axis) - both keep a
                // wide tilted motif at an end station inside the scene's advertised envelope.
                float t = Mathf.Lerp(0.08f, 0.92f, stations > 1 ? s / (float)(stations - 1) : 0.5f);
                Vector3 c = SpineAt(t);
                Vector3 tangent = (SpineAt(Mathf.Min(1f, t + 0.02f)) - SpineAt(Mathf.Max(0f, t - 0.02f))).normalized;
                Vector3 leaned = (tangent + Vector3.forward).normalized;
                var look = SpawnPoint.LookRotation(leaned, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
                EmitMotif(plan, rng, s % 2 == 0 ? motifA : motifB, c, look, perStation);
                plan.CloseStructure(); // each station is one substructure
                roll += rollStep;
            }
            plan.CrystalPoints.Add(SpineAt(1f) + Vector3.forward * 18f);
        }

        const int MotifCount = 6;

        /// <summary>One station's construction, oriented to the spine's own frame at that station.</summary>
        static void EmitMotif(MicroscenePlan plan, System.Random rng, int motif, Vector3 center,
            Quaternion look, int count)
        {
            switch (motif % MotifCount)
            {
                case 0: // hoop gate
                    AddHoop(plan.PrismPoints, center, look, Range(rng, 14f, 26f), count, rng);
                    break;
                case 1: // polygon gate
                {
                    int sides = RangeInt(rng, 3, 7);
                    AddPolygonGate(plan.PrismPoints, center, look, sides,
                        Range(rng, 15f, 24f), Mathf.Max(1, count / sides), Range(rng, 0f, 90f), rng);
                    break;
                }
                case 2: // torus ring
                    AddTorusRing(plan.PrismPoints, center, look, Range(rng, 16f, 26f), Range(rng, 4f, 8f), count, rng);
                    break;
                case 3: // shell dish - concave cap you skim into and slide off
                {
                    float sphere = Range(rng, 20f, 32f);
                    AddShellPatch(plan.PrismPoints, center - look * Vector3.forward * sphere * 0.75f, look,
                        sphere, Range(rng, 35f, 60f), count, rng);
                    break;
                }
                case 4: // blade cross - fan blades in the station plane
                {
                    int blades = RangeInt(rng, 2, 5);
                    int perBlade = Mathf.Max(2, count / blades);
                    var tmp = new List<SpawnPoint>();
                    for (int b = 0; b < blades; b++)
                        AddFanBlade(tmp, b / (float)blades * Mathf.PI * 2f, perBlade, Range(rng, 20f, 32f),
                            Range(rng, 0.1f, 0.7f), rng);
                    foreach (var p in tmp)
                        plan.PrismPoints.Add(new SpawnPoint(center + look * p.Position, look * p.Rotation, p.Scale));
                    break;
                }
                default: // cluster
                {
                    float spread = Range(rng, 9f, 15f);
                    for (int i = 0; i < count; i++)
                    {
                        var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                        plan.PrismPoints.Add(new SpawnPoint(center + InsideUnitSphere(rng) * spread, rot, ChunkScale(rng)));
                    }
                    break;
                }
            }
        }

        // ── Budget fitting (geometry) ────────────────────────────────────────

        /// <summary>
        /// Recipes must emit exactly <paramref name="budget"/> prism points so the conveyor can
        /// re-pose its fixed prism stock into any plan. Trims overshoot; pads undershoot with
        /// ambient scatter. Keeps <see cref="MicroscenePlan.Metas"/> in lockstep (the caller sweeps
        /// stragglers with <see cref="MicroscenePlan.CloseStructure"/> before fitting).
        /// </summary>
        static void FitToBudget(MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            while (plan.PrismPoints.Count > budget)
                plan.PrismPoints.RemoveAt(plan.PrismPoints.Count - 1);
            if (plan.Metas.Count > plan.PrismPoints.Count)
                plan.Metas.RemoveRange(plan.PrismPoints.Count, plan.Metas.Count - plan.PrismPoints.Count);

            bool padded = plan.PrismPoints.Count < budget;
            while (plan.PrismPoints.Count < budget)
            {
                var rot = Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));
                plan.PrismPoints.Add(new SpawnPoint(InsideUnitSphere(rng) * radius, rot, ChunkScale(rng, 0.9f)));
            }
            if (padded)
                plan.CloseStructure(); // the ambient pad is its own substructure
        }

        static void ClampCrystals(MicroscenePlan plan, System.Random rng, int maxCrystals)
        {
            while (plan.CrystalPoints.Count > Mathf.Max(0, maxCrystals))
                plan.CrystalPoints.RemoveAt(rng.Next(plan.CrystalPoints.Count));
        }

        // Theming (domain / kind / scale moods / crystal mix) lives in MicroscenePainter - it paints
        // along the structural metadata the recipes stamp above.
    }
}
