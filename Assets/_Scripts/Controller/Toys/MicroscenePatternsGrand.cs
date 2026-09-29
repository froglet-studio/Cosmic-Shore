using System.Collections.Generic;
using UnityEngine;
using static CosmicShore.Gameplay.PrismGeometry;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Wanderway's GRAND assemblies — monument-scale set pieces authored for a belt whose
    /// per-scene prism budget is measured in thousands, not dozens.
    ///
    /// Why these are a separate family from the classic recipes in <see cref="MicroscenePatterns"/>:
    /// the classic forty are hand-tuned in ABSOLUTE world units around a ~80-unit scene, and they
    /// derive their part counts by DIVIDING the budget (a gate run is always 3-6 gates). Handed
    /// thousands of prisms they get denser, never bigger — solid rings inside a mostly-empty
    /// envelope. The classic set is therefore generated at its design radius and scaled up bodily
    /// (<see cref="MicroscenePatterns.DesignRadius"/>), which preserves its proportions exactly;
    /// these grand recipes instead take the scene radius as their own basis and MULTIPLY their part
    /// counts with the budget, so more mass buys more architecture: more bays in the nave, more
    /// branches on the tree, more shells in the vault.
    ///
    /// The construction vocabulary is deliberately the same one the authored cell environments use
    /// (<c>SpawnableYggdra</c>'s trunk-and-canopy, <c>SpawnableOrrery</c>'s nested armillary,
    /// <c>SpawnableAtlantis</c>' terraced city, <c>SpawnableGeode</c>'s shell-and-spikes,
    /// <c>SpawnableZephyr</c>'s curl-noise veils) — the freestyle six are the proof that a
    /// 30k-prism world reads as a PLACE, and the conveyor now transports one.
    ///
    /// Contract, identical to the classic recipes: emit into <see cref="MicroscenePlan.PrismPoints"/>
    /// and call <see cref="MicroscenePlan.CloseStructure"/> after each substructure (bay, branch,
    /// ring, shell) so <see cref="MicroscenePainter"/> can theme WITH the architecture; drop crystal
    /// points into <see cref="MicroscenePlan.CrystalPoints"/>; know nothing about domain or kind;
    /// draw ONLY from the supplied <see cref="System.Random"/>. Emit approximately
    /// <c>budget</c> points — the shared <c>FitToBudget</c> trims the overflow and pads a shortfall
    /// with ambient scatter, so a recipe that undershoots badly reads as confetti.
    /// </summary>
    public static class MicroscenePatternsGrand
    {
        public const int Count = 8;

        static readonly string[] Names =
        {
            "Cathedral", "World Tree", "Orrery", "Sunken City",
            "Leviathan", "Geode Vault", "Aurora Veil", "Hypersphere",
        };

        public static string Name(int grandRecipe) => Names[Mathf.Abs(grandRecipe) % Count];

        /// <summary>None of the grand assemblies request flora/fauna — they are architecture. The
        /// living recipes stay in the classic set, so a belt keeps its menageries.</summary>
        public static void Build(int grandRecipe, MicroscenePlan plan, System.Random rng, int budget, float radius)
        {
            float length = radius * 2.2f;
            switch (Mathf.Abs(grandRecipe) % Count)
            {
                case 0: Cathedral(plan, rng, budget, radius, length); break;
                case 1: WorldTree(plan, rng, budget, radius, length); break;
                case 2: Orrery(plan, rng, budget, radius, length); break;
                case 3: SunkenCity(plan, rng, budget, radius, length); break;
                case 4: Leviathan(plan, rng, budget, radius, length); break;
                case 5: GeodeVault(plan, rng, budget, radius, length); break;
                case 6: AuroraVeil(plan, rng, budget, radius, length); break;
                case 7: Hypersphere(plan, rng, budget, radius, length); break;
            }
        }

        // ── Shared helpers ───────────────────────────────────────────────────

        /// <summary>Share of the budget, floored at 1 so a tiny budget still emits something.</summary>
        static int Share(int budget, float fraction) => Mathf.Max(1, Mathf.RoundToInt(budget * fraction));

        /// <summary>Seeded divergence-free curl noise — the same field the cell environments and
        /// the painting toolkit use, so grand assemblies drift organically instead of reading as
        /// clean CAD.</summary>
        static Vector3 Curl(Vector3 p, float freq, int seed) =>
            PaintingStrokeToolkit.CurlNoise(p, freq, seed);

        static int NoiseSeed(System.Random rng) => rng.Next(int.MinValue, int.MaxValue);

        /// <summary>A non-degenerate up-vector for LookRotation against an arbitrary direction.</summary>
        static Vector3 UpFor(Vector3 dir) =>
            Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;

        /// <summary>Drop a crystal at a local point, nudged off the structure so it is skimmable
        /// rather than buried inside a wall.</summary>
        static void Pickup(MicroscenePlan plan, Vector3 at) => plan.CrystalPoints.Add(at);

        // ── 0. Cathedral ─────────────────────────────────────────────────────

        /// <summary>
        /// A nave you fly down: two colonnades of piers, a ribbed vault arching overhead bay by
        /// bay, a clerestory of panes riding above the arcade, flying buttresses outside, and a
        /// rose window closing the far end. Bay count scales with the budget, so a richer belt
        /// builds a longer cathedral rather than a denser one.
        /// </summary>
        static void Cathedral(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float halfSpan = radius * 0.42f;                       // half the nave width
            float pierHeight = radius * 0.55f;
            int bays = Mathf.Clamp(budget / 110, 4, 14);
            // A bay emits ~1.6 × perBay across piers, rib, clerestory and (every other bay) a
            // buttress. Aim the bays at ~60% of the budget so the rose window and a modest
            // overshoot fit — FitToBudget trims an overshoot, but PADS a shortfall with ambient
            // scatter, and a grand assembly that is half confetti is the failure to avoid.
            int perBay = Mathf.Max(12, budget * 6 / Mathf.Max(1, bays * 10));

            for (int b = 0; b < bays; b++)
            {
                float t = bays > 1 ? b / (float)(bays - 1) : 0.5f;
                float z = Mathf.Lerp(-length * 0.46f, length * 0.34f, t);

                // Paired piers.
                for (int side = -1; side <= 1; side += 2)
                {
                    AddPillarColumn(plan.PrismPoints,
                        new Vector3(side * halfSpan, -pierHeight * 0.5f, z),
                        Mathf.Max(3, perBay / 3), pierHeight / Mathf.Max(3, perBay / 3) * 1.6f, rng);
                    plan.CloseStructure();
                }

                // Rib vault over the bay. AddArch already spans in ±x and arcs DOWN in y from its
                // centre (apex at the centre, springings a radius below), which is exactly a rib
                // across the nave — the centre is lifted so the springings meet the pier tops.
                AddArch(plan.PrismPoints, new Vector3(0f, pierHeight * 0.5f, z),
                    halfSpan * 1.12f, Mathf.Max(6, perBay / 2), Range(rng, 150f, 178f), rng);
                plan.CloseStructure();

                // Clerestory: a short run of panes riding above each arcade.
                for (int side = -1; side <= 1; side += 2)
                {
                    int panes = Mathf.Max(2, perBay / 6);
                    for (int i = 0; i < panes; i++)
                    {
                        float u = panes > 1 ? i / (float)(panes - 1) : 0.5f;
                        var pos = new Vector3(side * halfSpan * 1.02f,
                            pierHeight * Mathf.Lerp(0.35f, 0.72f, u),
                            z + Range(rng, -length * 0.012f, length * 0.012f));
                        plan.PrismPoints.Add(new SpawnPoint(pos,
                            SpawnPoint.LookRotation(Vector3.right * side, Vector3.up), PaneScale(rng, 0.8f)));
                    }
                    plan.CloseStructure();
                }

                // Flying buttress every other bay.
                if (b % 2 == 1)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int n = Mathf.Max(4, perBay / 5);
                        int s = side;
                        float zz = z;
                        AddSweptPath(plan.PrismPoints, u => new Vector3(
                                s * Mathf.Lerp(halfSpan * 1.75f, halfSpan * 1.02f, u),
                                Mathf.Lerp(-pierHeight * 0.9f, pierHeight * 0.45f, u * u),
                                zz),
                            n, SweepMode.Strand, 0f, rng);
                        plan.CloseStructure();
                    }
                }
            }

            // Rose window closing the far end: a polygon frame filled with petal tracery.
            var roseCenter = new Vector3(0f, 0f, length * 0.38f);
            var roseOrient = Quaternion.identity;
            float roseRadius = radius * 0.4f;
            AddPolygonGate(plan.PrismPoints, roseCenter, roseOrient, RangeInt(rng, 8, 13), roseRadius,
                Mathf.Max(3, budget / 160), Range(rng, 0f, 40f), rng);
            plan.CloseStructure();

            int petals = RangeInt(rng, 6, 11);
            for (int p = 0; p < petals; p++)
            {
                AddPetalArc(plan.PrismPoints, roseCenter, roseOrient,
                    p / (float)petals * Mathf.PI * 2f, roseRadius * 0.55f, Range(rng, 55f, 95f),
                    Mathf.Max(4, budget / 200), rng);
                plan.CloseStructure();
            }

            Pickup(plan, new Vector3(0f, 0f, -length * 0.3f));
            Pickup(plan, new Vector3(0f, pierHeight * 0.4f, length * 0.1f));
            Pickup(plan, roseCenter - Vector3.forward * radius * 0.2f);
        }

        // ── 1. World Tree ────────────────────────────────────────────────────

        /// <summary>
        /// A colossal tree grown along the flight axis: a braided trunk you can orbit, boughs
        /// curling off it under a curl-noise field, a phyllotaxis canopy at the crown, and root
        /// buttresses flaring back the way you came. <c>SpawnableYggdra</c>'s idiom at belt scale.
        /// </summary>
        static void WorldTree(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int seed = NoiseSeed(rng);
            float trunkRadius = radius * 0.13f;
            float lean = Range(rng, -0.12f, 0.12f);

            // The trunk runs along the FLIGHT axis (+z), not world-up: the belt's scenes are places
            // you fly THROUGH, so the tree is something you thread rather than something you land at.
            Vector3 Spine(float t) => new(
                Mathf.Sin(t * 2.2f) * radius * 0.05f + lean * radius * t,
                Mathf.Cos(t * 1.7f) * radius * 0.04f,
                Mathf.Lerp(-length * 0.46f, length * 0.3f, t));

            // Braided trunk: several strands helixing around the spine.
            int strands = RangeInt(rng, 4, 8);
            int perStrand = Share(budget, 0.30f) / strands;
            for (int s = 0; s < strands; s++)
            {
                float phase = s / (float)strands * Mathf.PI * 2f;
                float turns = Range(rng, 1.1f, 2.4f);
                AddSweptPath(plan.PrismPoints, t =>
                {
                    float a = phase + t * turns * Mathf.PI * 2f;
                    float rr = trunkRadius * Mathf.Lerp(1.25f, 0.55f, t);
                    return Spine(t) + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0f);
                }, Mathf.Max(6, perStrand), SweepMode.Strand, 0f, rng);
                plan.CloseStructure();
            }

            // Boughs: curl-noise driven, springing from the upper trunk.
            int boughs = Mathf.Clamp(budget / 90, 5, 18);
            int perBough = Share(budget, 0.38f) / boughs;
            for (int b = 0; b < boughs; b++)
            {
                float t0 = Range(rng, 0.35f, 0.92f);
                Vector3 root = Spine(t0);
                Vector3 outward = Quaternion.AngleAxis(b * 137.5f + Range(rng, -18f, 18f), Vector3.forward)
                                  * Vector3.right;
                // Reach + curl displacement must stay inside the scene's lateral envelope
                // (the belt clamps anchors on the advertised radius; mass outside it falls out
                // of the host cell's sense radius and stops being ecosystem-visible).
                float reach = radius * Range(rng, 0.34f, 0.62f);
                int bs = seed + b * 17;
                AddSweptPath(plan.PrismPoints, u =>
                {
                    Vector3 p = root + outward * (reach * u) + Vector3.forward * (radius * 0.18f * u * u);
                    return p + Curl(p * 0.012f, 1f, bs) * (radius * 0.12f * u);
                }, Mathf.Max(5, perBough), SweepMode.Strand, 0f, rng);
                plan.CloseStructure();
            }

            // Canopy: a phyllotaxis cap over the crown.
            // The cap's apex points along +z, so its centre must sit far enough back that
            // apex = centre.z + sphereRadius stays inside the envelope.
            AddShellPatch(plan.PrismPoints, Spine(1f), Quaternion.identity,
                radius * 0.42f, Range(rng, 70f, 105f), Share(budget, 0.22f), rng);
            plan.CloseStructure();

            // Root buttresses flaring back down the approach.
            int roots = RangeInt(rng, 4, 8);
            int perRoot = Share(budget, 0.10f) / roots;
            for (int r = 0; r < roots; r++)
            {
                float a = r / (float)roots * Mathf.PI * 2f + Range(rng, -0.2f, 0.2f);
                Vector3 dir = new(Mathf.Cos(a), Mathf.Sin(a), 0f);
                AddSweptPath(plan.PrismPoints, u =>
                        Spine(0.05f) + dir * (radius * 0.5f * u) - Vector3.forward * (length * 0.08f * u),
                    Mathf.Max(4, perRoot), SweepMode.Strand, 0f, rng);
                plan.CloseStructure();
            }

            Pickup(plan, Spine(0.55f) + Vector3.right * trunkRadius * 3f);
            Pickup(plan, Spine(1f));
        }

        // ── 2. Orrery ────────────────────────────────────────────────────────

        /// <summary>
        /// A nested armillary you fly INTO: concentric hoops at wildly different tilts, each
        /// carrying a small shell-body riding its circumference, around a dense core. Ring count
        /// scales with the budget. <c>SpawnableOrrery</c>'s idiom at belt scale.
        /// </summary>
        static void Orrery(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int rings = Mathf.Clamp(budget / 150, 3, 9);
            int ringBudget = Share(budget, 0.62f) / rings;

            for (int i = 0; i < rings; i++)
            {
                float t = rings > 1 ? i / (float)(rings - 1) : 0.5f;
                float rr = Mathf.Lerp(radius * 0.22f, radius * 0.90f, t);
                var tilt = Quaternion.Euler(Range(rng, -80f, 80f), Range(rng, -80f, 80f), Range(rng, 0f, 360f));

                // A torus ring rather than a flat hoop — a ring you can see the thickness of.
                AddTorusRing(plan.PrismPoints, Vector3.zero, tilt, rr,
                    Mathf.Max(1.5f, rr * 0.045f), Mathf.Max(10, ringBudget), rng);
                plan.CloseStructure();

                // A body riding the ring.
                float a = Range(rng, 0f, Mathf.PI * 2f);
                Vector3 bodyAt = tilt * new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0f);
                AddShellPatch(plan.PrismPoints, bodyAt, Quaternion.LookRotation(bodyAt.normalized),
                    rr * Range(rng, 0.06f, 0.13f), 180f, Mathf.Max(6, Share(budget, 0.03f)), rng);
                plan.CloseStructure();

                if (i % 2 == 0) Pickup(plan, bodyAt * 1.15f);
            }

            // The core: a dense little sun at the centre of the machine.
            AddShellPatch(plan.PrismPoints, Vector3.zero, Quaternion.identity,
                radius * 0.12f, 180f, Share(budget, 0.14f), rng);
            plan.CloseStructure();

            // Polar spindle so the machine has an axis to read against.
            AddSweptPath(plan.PrismPoints, t => new Vector3(0f, 0f, Mathf.Lerp(-length * 0.42f, length * 0.42f, t)),
                Mathf.Max(8, Share(budget, 0.06f)), SweepMode.Strand, 0f, rng);
            plan.CloseStructure();

            Pickup(plan, new Vector3(0f, 0f, -length * 0.3f));
        }

        // ── 3. Sunken City ───────────────────────────────────────────────────

        /// <summary>
        /// A drowned terraced city on a plaza: stepped ziggurats of square terraces, causeways
        /// slung between them, and a central spire. <c>SpawnableAtlantis</c>' idiom at belt scale.
        /// </summary>
        static void SunkenCity(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float floorY = -radius * 0.45f;

            // Plaza floor — a broad wave sheet the city stands on.
            int gridN = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Share(budget, 0.22f))), 5, 26);
            AddWaveSheet(plan.PrismPoints, gridN, gridN, radius * 1.05f, length * 0.9f, radius * 0.05f, rng);
            plan.CloseStructure();

            int blocks = Mathf.Clamp(budget / 170, 3, 10);
            int perBlock = Share(budget, 0.52f) / blocks;
            var tops = new List<Vector3>(blocks);

            for (int b = 0; b < blocks; b++)
            {
                float a = b / (float)blocks * Mathf.PI * 2f + Range(rng, -0.25f, 0.25f);
                float ring = radius * Range(rng, 0.22f, 0.72f);
                var at = new Vector3(Mathf.Cos(a) * ring, floorY, Mathf.Sin(a) * ring * 1.1f);

                int terraces = RangeInt(rng, 3, 7);
                float baseSize = radius * Range(rng, 0.16f, 0.30f);
                float step = radius * Range(rng, 0.05f, 0.09f);
                for (int s = 0; s < terraces; s++)
                {
                    float f = s / (float)terraces;
                    AddPolygonGate(plan.PrismPoints, at + Vector3.up * (step * s * 2f),
                        Quaternion.Euler(90f, Range(rng, 0f, 30f), 0f),
                        4, Mathf.Lerp(baseSize, baseSize * 0.25f, f),
                        Mathf.Max(2, perBlock / (terraces * 4)), 0f, rng);
                }
                plan.CloseStructure();
                tops.Add(at + Vector3.up * (step * terraces * 2f));
            }

            // Causeways between consecutive rooftops — decks to ride.
            int spans = Mathf.Max(0, tops.Count - 1);
            int perSpan = spans > 0 ? Share(budget, 0.16f) / spans : 0;
            for (int i = 0; i < spans; i++)
            {
                Vector3 a = tops[i], c = tops[i + 1];
                Vector3 mid = (a + c) * 0.5f + Vector3.up * radius * Range(rng, 0.04f, 0.14f);
                AddSweptPath(plan.PrismPoints, t =>
                {
                    // quadratic bezier sag
                    float u = 1f - t;
                    return u * u * a + 2f * u * t * mid + t * t * c;
                }, Mathf.Max(4, perSpan), SweepMode.Deck, 0.35f, rng);
                plan.CloseStructure();
            }

            // The spire. AddPillarColumn takes a per-SEGMENT length and centres the column on its
            // baseXZ, so the segment must be derived from a height target — passing a fixed segment
            // makes the column's height scale with the budget and shoot out of the scene.
            int spireSegments = Mathf.Clamp(Share(budget, 0.10f), 6, 60);
            AddPillarColumn(plan.PrismPoints, new Vector3(0f, floorY + radius * 0.45f, 0f),
                spireSegments, radius * 0.9f / spireSegments, rng);
            plan.CloseStructure();

            foreach (var top in tops) Pickup(plan, top + Vector3.up * radius * 0.08f);
        }

        // ── 4. Leviathan ─────────────────────────────────────────────────────

        /// <summary>
        /// The skeleton of something enormous, lying across your path: a serpentine spine, ribs
        /// arcing out and down from it, dorsal fins, and a jaw-arch at the head you fly through.
        /// </summary>
        static void Leviathan(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float amp = radius * Range(rng, 0.16f, 0.34f);
            float waves = Range(rng, 0.7f, 1.6f);
            float roll = Range(rng, 0f, Mathf.PI * 2f);

            Vector3 Spine(float t) => new(
                Mathf.Sin(t * waves * Mathf.PI * 2f + roll) * amp,
                Mathf.Cos(t * waves * Mathf.PI * 1.3f + roll) * amp * 0.45f,
                Mathf.Lerp(-length * 0.46f, length * 0.46f, t));

            AddSweptPath(plan.PrismPoints, Spine, Mathf.Max(12, Share(budget, 0.16f)), SweepMode.Strand, 0f, rng);
            plan.CloseStructure();

            int ribs = Mathf.Clamp(budget / 80, 6, 22);
            int perRib = Share(budget, 0.62f) / (ribs * 2);
            for (int r = 0; r < ribs; r++)
            {
                float t = ribs > 1 ? r / (float)(ribs - 1) : 0.5f;
                Vector3 at = Spine(t);
                // Ribs taper toward the tail.
                // Sin(1.45) ≈ 0.99, so a rib reaches ~span laterally off a spine already up to
                // `amp` off-axis — sized so the pair stays inside the scene's lateral envelope.
                float span = radius * Mathf.Lerp(0.55f, 0.16f, t) * Range(rng, 0.85f, 1.15f);

                for (int side = -1; side <= 1; side += 2)
                {
                    int s = side;
                    Vector3 anchor = at;
                    AddSweptPath(plan.PrismPoints, u =>
                            anchor + new Vector3(s * span * Mathf.Sin(u * 1.45f),
                                -span * (1f - Mathf.Cos(u * 1.45f)) * 0.85f, 0f),
                        Mathf.Max(4, perRib), SweepMode.Strand, 0f, rng);
                    plan.CloseStructure();
                }

                // Dorsal fin every third vertebra.
                if (r % 3 == 0)
                {
                    Vector3 anchor = at;
                    AddSweptPath(plan.PrismPoints, u => anchor + Vector3.up * (span * 0.55f * u),
                        Mathf.Max(3, perRib / 2), SweepMode.Fin, 0f, rng);
                    plan.CloseStructure();
                }
            }

            // The jaw: a wide gate at the head, tilted like a mouth mid-yawn.
            Vector3 head = Spine(1f);
            AddPolygonGate(plan.PrismPoints, head + Vector3.forward * radius * 0.12f,
                Quaternion.Euler(Range(rng, -18f, 18f), Range(rng, -18f, 18f), Range(rng, 0f, 360f)),
                RangeInt(rng, 5, 9), radius * 0.42f, Mathf.Max(3, Share(budget, 0.03f)), 0f, rng);
            plan.CloseStructure();

            Pickup(plan, head);
            Pickup(plan, Spine(0.5f) + Vector3.up * radius * 0.25f);
        }

        // ── 5. Geode Vault ───────────────────────────────────────────────────

        /// <summary>
        /// A hollow rock you fly inside: a shingled outer shell, a mouth cut through it, and the
        /// whole interior bristling with crystal spikes pointing at the core.
        /// <c>SpawnableGeode</c>'s idiom at belt scale.
        /// </summary>
        static void GeodeVault(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            float shell = radius * 0.85f;

            // Shell: several overlapping caps, leaving the -z pole open as the entry mouth.
            int caps = RangeInt(rng, 4, 8);
            int perCap = Share(budget, 0.46f) / caps;
            for (int c = 0; c < caps; c++)
            {
                var orient = Quaternion.Euler(Range(rng, -70f, 70f), Range(rng, -70f, 70f), Range(rng, 0f, 360f));
                AddShellPatch(plan.PrismPoints, Vector3.zero, orient, shell, Range(rng, 55f, 90f), perCap, rng);
                plan.CloseStructure();
            }

            // The mouth: a hoop framing the way in.
            AddHoop(plan.PrismPoints, new Vector3(0f, 0f, -shell * 0.92f), Quaternion.identity,
                radius * 0.34f, Mathf.Max(10, Share(budget, 0.05f)), rng);
            plan.CloseStructure();

            // Spikes: inward-pointing crystals rooted on the shell.
            int spikes = Mathf.Clamp(budget / 40, 10, 60);
            int perSpike = Share(budget, 0.40f) / spikes;
            for (int s = 0; s < spikes; s++)
            {
                Vector3 dir = OnUnitSphere(rng);
                if (dir.z < -0.55f) dir.z = -dir.z; // keep the mouth clear
                dir = dir.normalized;
                Vector3 root = dir * shell;
                float reach = shell * Range(rng, 0.30f, 0.72f);
                AddSweptPath(plan.PrismPoints, u => root - dir * (reach * u),
                    Mathf.Max(3, perSpike), SweepMode.Strand, 0f, rng);
                plan.CloseStructure();
            }

            // A cluster at the heart — the payoff for flying in.
            AddShellPatch(plan.PrismPoints, Vector3.zero, Quaternion.identity,
                radius * 0.10f, 180f, Share(budget, 0.08f), rng);
            plan.CloseStructure();

            Pickup(plan, Vector3.zero);
            Pickup(plan, new Vector3(0f, 0f, -shell * 1.15f));
        }

        // ── 6. Aurora Veil ───────────────────────────────────────────────────

        /// <summary>
        /// Layered curtains of prism ribbon hanging in space, each sheet driven by a curl-noise
        /// field so it folds and drifts like an aurora. Nothing to crash into head-on — a place to
        /// weave. <c>SpawnableZephyr</c>'s idiom at belt scale.
        /// </summary>
        static void AuroraVeil(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int sheets = Mathf.Clamp(budget / 130, 4, 12);
            int perSheet = Share(budget, 0.94f) / sheets;
            int ribbonsPerSheet = Mathf.Clamp(perSheet / 22, 2, 8);
            int perRibbon = Mathf.Max(6, perSheet / Mathf.Max(1, ribbonsPerSheet));

            for (int s = 0; s < sheets; s++)
            {
                int seed = NoiseSeed(rng);
                float baseX = Mathf.Lerp(-radius * 0.58f, radius * 0.58f, sheets > 1 ? s / (float)(sheets - 1) : 0.5f);
                float drift = Range(rng, -radius * 0.1f, radius * 0.1f);
                // CurlNoise magnitude is O(1) but not bounded at 1 — keep base + drift + amp
                // comfortably under the scene's lateral envelope.
                float amp = radius * Range(rng, 0.10f, 0.18f);

                for (int r = 0; r < ribbonsPerSheet; r++)
                {
                    float y = Mathf.Lerp(-radius * 0.55f, radius * 0.55f,
                        ribbonsPerSheet > 1 ? r / (float)(ribbonsPerSheet - 1) : 0.5f);
                    float yy = y;
                    float bx = baseX;
                    float dr = drift;
                    float am = amp;
                    int sd = seed;
                    AddSweptPath(plan.PrismPoints, t =>
                    {
                        var p = new Vector3(bx + dr * t, yy, Mathf.Lerp(-length * 0.42f, length * 0.42f, t));
                        return p + Curl(p * 0.006f, 1f, sd) * am;
                    }, perRibbon, SweepMode.Fin, 0f, rng);
                    plan.CloseStructure();
                }

                if (s % 3 == 0) Pickup(plan, new Vector3(baseX, 0f, Range(rng, -length * 0.3f, length * 0.3f)));
            }
        }

        // ── 7. Hypersphere ───────────────────────────────────────────────────

        /// <summary>
        /// Nested geodesic shells with a bore drilled clean through them: from outside a solid
        /// world, from the approach a tunnel of concentric rings you thread. The classic
        /// concentric-shell read of the freestyle cells, made flyable.
        /// </summary>
        static void Hypersphere(MicroscenePlan plan, System.Random rng, int budget, float radius, float length)
        {
            int shells = Mathf.Clamp(budget / 260, 2, 6);
            float bore = radius * Range(rng, 0.14f, 0.24f);
            int perShell = Share(budget, 0.80f) / shells;

            for (int s = 0; s < shells; s++)
            {
                float t = shells > 1 ? s / (float)(shells - 1) : 0.5f;
                float rr = Mathf.Lerp(radius * 0.34f, radius * 0.96f, t);

                // Phyllotaxis over the whole sphere, rejecting the bore corridor so the tunnel
                // stays open all the way through.
                int emitted = 0, attempts = 0;
                while (emitted < perShell && attempts < perShell * 4)
                {
                    attempts++;
                    float u = (attempts + 0.5f) / (perShell * 1.6f);
                    float polar = Mathf.Acos(Mathf.Clamp(1f - 2f * (u % 1f), -1f, 1f));
                    float azim = attempts * 2.39996323f;
                    var dir = new Vector3(
                        Mathf.Sin(polar) * Mathf.Cos(azim),
                        Mathf.Sin(polar) * Mathf.Sin(azim),
                        Mathf.Cos(polar));
                    var pos = dir * rr;
                    if (new Vector2(pos.x, pos.y).sqrMagnitude < bore * bore) continue; // the bore

                    plan.PrismPoints.Add(new SpawnPoint(pos,
                        SpawnPoint.LookRotation(dir, UpFor(dir)), PaneScale(rng, 0.9f)));
                    emitted++;
                }
                plan.CloseStructure();

                // A ring lining the bore where it pierces this shell — the tunnel's ribs.
                for (int end = -1; end <= 1; end += 2)
                {
                    float z = end * Mathf.Sqrt(Mathf.Max(0.01f, rr * rr - bore * bore));
                    AddHoop(plan.PrismPoints, new Vector3(0f, 0f, z), Quaternion.identity,
                        bore * 1.08f, Mathf.Max(8, Share(budget, 0.02f)), rng);
                    plan.CloseStructure();
                }
            }

            Pickup(plan, Vector3.zero);
            Pickup(plan, new Vector3(0f, 0f, -radius * 0.75f));
            Pickup(plan, new Vector3(0f, 0f, radius * 0.75f));
        }
    }
}
