using CosmicShore.Gameplay;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Atlantis" - the intensity-4 Scurry (Crystal Capture) environment: a drowned garden-city
    /// grown around a world-tree, replacing the gyroid lattice with an organic wonderland
    /// (~70k prisms) whose forms carry the layered order of Earth biomes instead of a single
    /// repeating mathematical cell.
    ///
    /// Composition (all centred on the SegmentSpawner origin, which is also the crystal core -
    /// crystals respawn inside r&lt;=170, so the space players actually race through is the heart
    /// of the painting, not its edge):
    ///
    ///   1. WORLD-TREE - braided helical trunk strands rising from the seafloor to a
    ///      fibonacci-spiral canopy dome; root buttresses flare into the dune field; golden-angle
    ///      branches droop under their own catenary weight and end in phyllotaxis blossom heads.
    ///      A super-shielded heartwood ring marks the permanent bones of the world; shielded
    ///      "fruit" hang under the canopy; sparse danger thorns run the undersides of low branches.
    ///   2. TERRACES - three concentric undulating ring-terraces at rising heights, joined by
    ///      spiral stair-ribbons and punctuated by colonnade gates (fly-through arches) whose
    ///      capitals are shielded octahedra. Waterfall veils spill off the inner lip.
    ///   3. REEF GARDENS - seven coral mounds on the seafloor bowl: meander-folded brain coral,
    ///      recursive staghorn branches (danger fire-coral tips), radiating sea fans, tube
    ///      sponges, and one ruby anemone per mound (telegraphed danger cluster).
    ///   4. KELP VEILS - curl-noise-swayed strands rising 170-320 units off the bowl with
    ///      alternating pinnate blades.
    ///   5. MOBIUS CAUSEWAY - one grand trefoil ribbon road threading tree, terraces, and reef;
    ///      the deck's surface normal rolls a full turn per circuit, so both faces are skimmable
    ///      in one lap. Shielded shrine markers stud the rails.
    ///   6. ATOLLS - floating islets of stacked, melting rock rings with dripping stalactite
    ///      tails, nautilus-spiral crowns, and waterfall streams that curl away downwind.
    ///   7. GLIMMER CURRENTS - divergence-free curl-field streamlines that spiral inward and
    ///      braid the negative space between families, leading the eye (and a skimming vessel)
    ///      from any point of the arena toward the core.
    ///   8. TIDE GARDEN - a paraboloid seafloor bowl of meandering dune rows (sand-ripple order,
    ///      porous enough to fly through) with scattered pebble clusters.
    ///
    /// Every family derives prism orientation from its own construction frame (strand tangent,
    /// surface normal, flow direction) so sparse prisms read as continuous flowing forms, and
    /// every family draws per-prism scale from its own proportion family with independent jitter -
    /// the same grain of 3-dimensional scale diversity as <see cref="PrismGeometry"/>.
    ///
    /// DETERMINISM: multiplayer clients each build the environment locally with no seed sync
    /// (like every scene-placed spawnable), so generation must be fully deterministic. All
    /// randomness flows from the serialized seed through one System.Random plus the seeded
    /// noise in <see cref="PaintingStrokeToolkit"/>; a seed of 0 falls back to a fixed default
    /// rather than time-seeding.
    ///
    /// Collider budget: plain/danger prisms ride the LOD-cullable BoxCollider (active count is
    /// bounded by PrismColliderLodManager's radius, not population). Shielded/super-shielded
    /// prisms carry always-on convex MeshColliders, so they are rationed to ~0.3% of the
    /// structure and only where they read as deliberate landmarks.
    ///
    /// Steady-state note: ~69k live prisms is ~2.8x the largest previously profiled cohort (the
    /// 25k geodesic shells). The O(population) systems (scale/material managers, spatial index,
    /// volume sums, collider-LOD scan) are Burst-batched and grow dynamically, but this
    /// population has not yet been device-profiled - soak the Crystal Capture scene at
    /// intensity 4 per Docs/PERFORMANCE_OPTIMIZATION.md before ship, and if it exceeds budget
    /// pull <see cref="density"/> down to the largest value the capture validates.
    /// </summary>
    public class SpawnableAtlantis : CellEnvironmentSpawnableBase
    {
        [Header("Composition")]
        [Tooltip("Outer radius of the main mass (terrace ring 3 / dune rim). Currents and atolls reach ~25% past this. Keep well inside the r=1200 membrane.")]
        [SerializeField] float outerRadius = 430f;

        [Tooltip("Seafloor bowl base depth at the centre. The bowl rises ~100 units toward the rim.")]
        [SerializeField] float floorDepth = -195f;

        [Tooltip("Height of the world-tree crown; the canopy dome sits just below this.")]
        [SerializeField] float crownHeight = 235f;

        // Base-class serialized fields carried by the shipped prefab: prism, density
        // (population scale), spawnClearRadius 14 + the five Crystal Capture pad points.

        protected override int DefaultSeed => 7;
        protected override int LayCapacity => 75000;

        protected override void BuildEnvironment()
        {
            BuildWorldTree();
            BuildTerraces();
            BuildReefGardens();
            BuildKelpVeils();
            BuildMobiusCauseway();
            BuildAtolls();
            BuildGlimmerCurrents();
            BuildTideGarden();
        }

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(outerRadius, floorDepth, crownHeight, 1);

        float BowlY(float r)
        {
            // Paraboloid seafloor: floorDepth at the centre rising ~100 units at the rim.
            float t = r / outerRadius;
            return floorDepth + t * t * 100f;
        }

        // ── 1. WORLD-TREE ────────────────────────────────────────────────────

        void BuildWorldTree()
        {
            float baseY = floorDepth + 6f;
            float height = crownHeight - baseY;

            // Braided trunk: strands twist around the axis, tapering from a root flare to a
            // slender waist, swelling again at the crown. Ribbon planks run long along the strand.
            const int strands = 12;
            const int steps = 200;
            for (int s = 0; s < strands; s++)
            {
                float phase = 2f * Mathf.PI * s / strands;
                Domains dom = s % 3 != 0 ? Domains.Jade : Domains.Gold;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)(steps - 1);
                    float y = baseY + height * t;
                    float r = 40f * Mathf.Pow(1f - t, 1.7f) + 11f + 26f * Mathf.Max(0f, t - 0.86f) * 7f;
                    float ang = phase + t * 4.2f + 1.1f * N01(t * 5f, s * 3f, 0f, 0);
                    float sway = 10f * N01(t * 3f, s * 7f, 1f, 1) - 5f;
                    var p = new Vector3((r + sway) * Mathf.Cos(ang), y, (r + sway) * Mathf.Sin(ang));

                    Vector3 radial = new Vector3(p.x, 0f, p.z).normalized;
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.up, radial)
                        : SpawnPoint.LookRotation(p - prev, radial);
                    Emit(p, rot, Jit(new Vector3(3.2f, 1.8f, 7.5f)), dom);
                    prev = p;
                }
            }

            // Heartwood ring - the permanent bones of the world (super-shielded = invulnerable
            // and never food, so the landmark survives any match).
            const int heartCount = 48;
            float heartY = baseY + height * 0.4f;
            for (int i = 0; i < heartCount; i++)
            {
                float a = 2f * Mathf.PI * i / heartCount;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Emit(new Vector3(radial.x * 20f, heartY, radial.z * 20f),
                    SpawnPoint.LookRotation(tangent, radial),
                    new Vector3(3.2f, 3.2f, 3.2f), Domains.Gold, PrismKind.SuperShielded);
            }

            // Root buttresses: nine ridges flaring outward along the bowl, three planks wide.
            for (int b = 0; b < 9; b++)
            {
                float a0 = 2f * Mathf.PI * b / 9f + 0.2f;
                float len = 95f + 55f * Hash01(b * 77 + _noiseSeed);
                const int segs = 60;
                Vector3 prevMid = Vector3.zero;
                for (int i = 0; i < segs; i++)
                {
                    float t = i / (float)(segs - 1);
                    float rr = 26f + t * len;
                    float y = BowlY(rr) + 30f * (1f - t) * (1f - t) + 3f;
                    float a = a0 + 0.55f * Mathf.Sin(t * 2.6f + b);
                    var mid = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), Vector3.up)
                        : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                    for (int w = -1; w <= 1; w++)
                    {
                        var q = new Vector3(mid.x - w * 4.4f * Mathf.Sin(a), mid.y, mid.z + w * 4.4f * Mathf.Cos(a));
                        Emit(q, rot, Jit(new Vector3(4.8f, 1.6f, 2.8f)), Domains.Jade);
                    }
                    prevMid = mid;
                }
            }

            // Branches: golden-angle around the trunk's upper half, drooping under their own
            // weight, splitting twice, tipped with phyllotaxis blossom heads.
            const int branches = 30;
            for (int b = 0; b < branches; b++)
            {
                float t0 = 0.42f + 0.55f * (b / (float)branches);
                float yb = baseY + height * t0;
                float ang = b * GoldenAngle;
                float len = 150f * (1.18f - t0) + 45f;
                float droop = 55f * (1f - t0) + 18f;
                Branch(new Vector3(14f * Mathf.Cos(ang), yb, 14f * Mathf.Sin(ang)), ang, len, droop, 2, b);
            }

            // Canopy: a fibonacci-spiral dome of leaf plates, thin axis along the dome normal.
            int leaves = Mathf.RoundToInt(7200 * density);
            for (int i = 0; i < leaves; i++)
            {
                float u = i / (float)leaves;
                float a = i * GoldenAngle;
                float rr = 165f * Mathf.Sqrt(u);
                float dome = 88f * Mathf.Cos(u * Mathf.PI * 0.5f);
                float wob = 17f * N01(Mathf.Cos(a) * 3f + 3f, u * 6f, Mathf.Sin(a) * 3f, 5);
                var p = new Vector3(rr * Mathf.Cos(a), crownHeight - 46f + dome * 0.62f + wob - 30f * u, rr * Mathf.Sin(a));

                // Leaf lies tangent to the dome: thin axis tilts outward with the slope.
                var normal = (Vector3.up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.55f * u)).normalized;
                var azimuthal = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Emit(p, SpawnPoint.LookRotation(normal, azimuthal),
                    Jit(new Vector3(6.4f, 4.2f, 0.7f)), i % 4 != 0 ? Domains.Jade : Domains.Gold);

                if (i % 140 == 70) // shielded fruit hanging under the canopy
                    Emit(new Vector3(p.x, p.y - 6f, p.z), Quaternion.identity,
                        new Vector3(2.6f, 2.6f, 2.6f), Domains.Gold, PrismKind.Shielded);
            }
        }

        void Branch(Vector3 root, float ang, float len, float droop, int level, int branchSeed)
        {
            int segs = Mathf.Max(9, (int)(len / 4.4f));
            var dir = new Vector3(Mathf.Cos(ang), 0.28f, Mathf.Sin(ang)).normalized;
            Vector3 p = root, prev = root;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                p = root + dir * (len * t);
                p = new Vector3(
                    p.x + 7f * N01(t * 4f, branchSeed * 3f, level * 9f, 2) - 3.5f,
                    p.y - droop * t * t,
                    p.z);
                var rot = SpawnPoint.LookRotation(p - prev, Vector3.up);
                Emit(p, rot, Jit(new Vector3(
                    2.6f * (1f - 0.3f * t) + 0.8f,
                    1.5f,
                    4.6f * (1f - 0.4f * t) + 1.2f)), Domains.Jade);

                // Danger thorns on the underside of the lowest branch generation - a telegraphed
                // risk/reward run (danger prisms slam everyone, and reward danger-skim builds).
                if (level == 2 && i % 8 == 4 && t > 0.3f && t < 0.85f)
                    Emit(new Vector3(p.x, p.y - 3.4f, p.z), Quaternion.identity,
                        new Vector3(1.3f, 3f, 1.3f), Domains.Ruby, PrismKind.Danger);
                prev = p;
            }

            // Blossom head: a phyllotaxis disc of petals at the tip.
            int petals = level == 2 ? 30 : 16;
            for (int k = 0; k < petals; k++)
            {
                float a = k * GoldenAngle;
                float rr = 3.1f * Mathf.Sqrt(k + 0.5f);
                var outward = new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a));
                Emit(p + new Vector3(rr * Mathf.Cos(a), 0.4f * rr, rr * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(outward, Vector3.up),
                    new Vector3(1.4f, 0.6f, 2f), Domains.Gold);
            }

            if (level > 0)
                for (int c = 0; c < 2; c++)
                    Branch(p, ang + (c * 2 - 1) * 0.85f + 0.22f * Mathf.Sin(branchSeed),
                        len * 0.56f, droop * 0.72f, level - 1, branchSeed * 5 + c + 1);

            // A soft melting disc draped over some low branches (the arena's one open surrealism).
            if (level == 2 && branchSeed % 5 == 2)
            {
                var mid = root + dir * (len * 0.55f);
                MeltDisc(new Vector3(mid.x, mid.y - droop * 0.3f, mid.z), ang, branchSeed);
            }
        }

        void MeltDisc(Vector3 c, float ang, int discSeed)
        {
            float radius = 13f + 5f * Hash01(discSeed * 31);
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            for (int i = 0; i < 46; i++)
            {
                float a = i * GoldenAngle;
                float rr = radius * Mathf.Sqrt(Hash01(discSeed * 97 + i));
                float x = rr * Mathf.Cos(a), z = rr * Mathf.Sin(a);
                float sag = 0.06f * rr * rr * (1f + 0.5f * Mathf.Sin(a * 2f + discSeed));
                var p = c + new Vector3(x * ca - z * sa, -sag, x * sa + z * ca);
                // Thin axis tips over as the rim melts downward.
                var normal = (Vector3.up + new Vector3(x * ca - z * sa, 0f, x * sa + z * ca).normalized * (sag * 0.04f)).normalized;
                Emit(p, SpawnPoint.LookRotation(normal, new Vector3(ca, 0f, sa)),
                    new Vector3(3f, 3f, 0.55f), Domains.Blue);
            }
        }

        // ── 2. TERRACES ──────────────────────────────────────────────────────

        void BuildTerraces()
        {
            // (radius, centre height, domain) per ring - rising toward the rim like hillside
            // rice terraces, each band five planks wide and undulating.
            float r1 = outerRadius * 0.407f, r2 = outerRadius * 0.609f, r3 = outerRadius * 0.814f;
            var rings = new (float R, float yc, Domains dom)[]
            {
                (r1, -70f, Domains.Jade),
                (r2, 12f, Domains.Gold),
                (r3, 96f, Domains.Blue),
            };

            for (int ri = 0; ri < rings.Length; ri++)
            {
                var (R, yc, dom) = rings[ri];
                int n = (int)(2f * Mathf.PI * R / 4.4f);
                int gateEvery = Mathf.Max(1, n / 10);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float und = 26f * Mathf.Sin(a * 3f + ri * 1.7f)
                                + 11f * N01(Mathf.Cos(a) * 4f + 9f, ri * 5f, Mathf.Sin(a) * 4f, 3);
                    float y = yc + und;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var deckRot = SpawnPoint.LookRotation(Vector3.up, tangent);
                    for (int w = 0; w < 5; w++)
                    {
                        float rr = R + (w - 2) * 6.4f;
                        Emit(new Vector3(rr * Mathf.Cos(a), y + (w % 2) * 0.5f, rr * Mathf.Sin(a)),
                            deckRot, Jit(new Vector3(4f, 6.2f, 1f), 0.12f), dom);
                    }
                    if (i % gateEvery == 0)
                        Gate(R, a, y, dom);
                }

                if (ri < rings.Length - 1)
                {
                    var next = rings[ri + 1];
                    for (int s = 0; s < 6; s++)
                        Stair(R, yc, next.R, next.yc, 2f * Mathf.PI * s / 6f + ri * 0.5f, ri);
                }
            }

            // Waterfall veils spilling off the first terrace's inner lip toward the bowl.
            for (int k = 0; k < 8; k++)
            {
                float a = 2f * Mathf.PI * k / 8f + 0.3f;
                var inward = new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a));
                var p = new Vector3((r1 - 10f) * Mathf.Cos(a), -74f, (r1 - 10f) * Mathf.Sin(a));
                for (int i = 0; i < 26; i++)
                {
                    var drift = Curl(p, 0.008f, 15);
                    var step = new Vector3(drift.x * 2.4f + inward.x * 1.8f, -5.6f, drift.z * 2.4f + inward.z * 1.8f);
                    p += step;
                    Emit(p, SpawnPoint.LookRotation(step, Vector3.up), new Vector3(1.2f, 1.2f, 2.6f), Domains.Blue);
                }
            }
        }

        void Gate(float R, float a, float y, Domains dom)
        {
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var center = new Vector3(R * ca, y, R * sa);
            var tangent = new Vector3(-sa, 0f, ca);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 basePos = center + tangent * (9.5f * side);
                float yaw = RangeF(0f, 360f);
                for (int h = 0; h < 11; h++)
                    Emit(basePos + new Vector3(0f, 2f + h * 2.6f, 0f), Quaternion.Euler(0f, yaw, 0f),
                        Jit(new Vector3(2.3f, 2.5f, 2.3f), 0.1f), dom);

                // Shielded octahedron capital - the jewel that names the colonnade.
                Emit(basePos + new Vector3(0f, 2f + 11 * 2.6f, 0f), Quaternion.identity,
                    new Vector3(2.8f, 2.8f, 2.8f), Domains.Gold, PrismKind.Shielded);
            }

            // Lintel arch spanning the two columns.
            for (int k = 0; k < 9; k++)
            {
                float t = k / 8f;
                float x = Mathf.Lerp(-9.5f, 9.5f, t);
                Emit(center + tangent * x + new Vector3(0f, 33f + 6f * Mathf.Sin(t * Mathf.PI), 0f),
                    SpawnPoint.LookRotation(tangent, Vector3.up),
                    new Vector3(2.3f, 1.2f, 3.2f), dom);
            }
        }

        void Stair(float R1, float y1, float R2, float y2, float a0, int ri)
        {
            const int steps = 56;
            Domains dom = ri != 0 ? Domains.Gold : Domains.Jade;
            Vector3 prevMid = Vector3.zero;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                float a = a0 + t * 1.9f; // sweep ~110 degrees while climbing
                float R = Mathf.Lerp(R1, R2, t);
                float y = Mathf.Lerp(y1, y2, t) + 6f * Mathf.Sin(t * Mathf.PI);
                var mid = new Vector3(R * Mathf.Cos(a), y, R * Mathf.Sin(a));
                Quaternion rot = i == 0
                    ? SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up)
                    : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                for (int w = -1; w <= 1; w++)
                {
                    float rr = R + w * 4.6f;
                    Emit(new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a)), rot,
                        Jit(new Vector3(4.4f, 0.9f, 3f), 0.1f), dom);
                }
                prevMid = mid;
            }
        }

        // ── 3. REEF GARDENS ──────────────────────────────────────────────────

        void BuildReefGardens()
        {
            var moundDomains = new[]
            {
                Domains.Jade, Domains.Gold, Domains.Ruby, Domains.Jade,
                Domains.Blue, Domains.Gold, Domains.Jade,
            };
            const int mounds = 7;
            for (int m = 0; m < mounds; m++)
            {
                float a = 2f * Mathf.PI * m / mounds + 0.45f;
                float rr = 140f + 150f * Hash01(m * 131 + _noiseSeed);
                var c = new Vector3(rr * Mathf.Cos(a), BowlY(rr), rr * Mathf.Sin(a));
                Domains dom = moundDomains[m];

                BrainCoral(c, 30f + 18f * Hash01(m * 17), dom, m);
                for (int s = 0; s < 5; s++)
                {
                    float sa = a + s * 1.45f + 0.4f;
                    Staghorn(c + new Vector3(22f * Mathf.Cos(sa), 0f, 22f * Mathf.Sin(sa)),
                        sa, 30f + 12f * Hash01(m * 7 + s), dom, 2, m * 10 + s);
                }
                for (int f = 0; f < 4; f++)
                {
                    float fa = a + f * 1.7f;
                    SeaFan(c + new Vector3(30f * Mathf.Cos(fa), 4f, 30f * Mathf.Sin(fa)),
                        fa + 1.2f, 26f + 8f * f, dom);
                }
                for (int tb = 0; tb < 6; tb++)
                {
                    float ta = a + tb * 1.1f + 0.8f;
                    TubeSponge(c + new Vector3(17f * Mathf.Cos(ta), 0f, 17f * Mathf.Sin(ta)),
                        12f + 8f * Hash01(m + tb * 3), dom);
                }
                Anemone(c + new Vector3(9f, 2f, -6f), m);
            }
        }

        void BrainCoral(Vector3 c, float R, Domains dom, int moundSeed)
        {
            int rows = (int)(R * 0.85f);
            for (int row = 0; row < rows; row++)
            {
                float lat = (row / (float)rows) * Mathf.PI * 0.5f;
                float rr = R * Mathf.Cos(lat);
                float y = R * Mathf.Sin(lat) * 0.6f;
                int n = Mathf.Max(6, (int)(2f * Mathf.PI * rr / 3.4f));
                float wobA = 3.5f * N01(row * 0.7f, moundSeed * 9f, 0f, 6);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float wr = rr + 3.4f * Mathf.Sin(a * 7f + wobA * 6.28f);
                    var p = c + new Vector3(wr * Mathf.Cos(a), y + 1.8f * Mathf.Sin(a * 7f + row), wr * Mathf.Sin(a));
                    // Meander folds stay porous - the mound is a garden to weave through, not a wall.
                    if (N01(p.x * 0.07f, p.y * 0.07f, p.z * 0.07f, 7) < 0.40f) continue;

                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var normal = (p - c).normalized;
                    Emit(p, SpawnPoint.LookRotation(tangent, normal), Jit(new Vector3(2.2f, 1.6f, 2.6f), 0.25f), dom);
                }
            }
        }

        void Staghorn(Vector3 root, float ang, float len, Domains dom, int level, int branchSeed)
        {
            int segs = Mathf.Max(5, (int)(len / 3f));
            var dir = new Vector3(Mathf.Cos(ang), 0.9f, Mathf.Sin(ang)).normalized;
            Vector3 p = root, prev = root;
            for (int i = 0; i < segs; i++)
            {
                float t = i / (float)segs;
                p += dir * 3.2f + new Vector3(
                    1.6f * N01(t * 5f, branchSeed * 3f, 1f, 8) - 0.8f, 0f,
                    1.6f * N01(t * 5f, branchSeed * 3f, 2f, 8) - 0.8f);
                Emit(p, SpawnPoint.LookRotation(p - prev, Vector3.up), Jit(new Vector3(1.5f, 1.5f, 3.2f), 0.2f), dom);
                prev = p;
            }

            if (level > 0)
            {
                for (int c = 0; c < 2; c++)
                    Staghorn(p, ang + (c * 2 - 1) * (0.7f + 0.2f * Hash01(branchSeed + c)),
                        len * 0.62f, dom, level - 1, branchSeed * 3 + c);
            }
            else
            {
                // Fire-coral tip: the outermost twig ends hot - visible, growth-facing danger.
                Emit(p + dir * 3.2f, SpawnPoint.LookRotation(dir, Vector3.up),
                    new Vector3(1.8f, 1.8f, 3.6f), Domains.Ruby, PrismKind.Danger);
            }
        }

        void SeaFan(Vector3 c, float facing, float R, Domains dom)
        {
            const int ribs = 11;
            // The fan spreads in a plane; every rib's thin axis is the shared fan normal.
            var fanNormal = new Vector3(-Mathf.Sin(facing), 0f, Mathf.Cos(facing));
            for (int rib = 0; rib < ribs; rib++)
            {
                float ra = facing - 1f + 2f * rib / (ribs - 1);
                int segs = (int)(R / 2.2f);
                Vector3 prev = c;
                for (int i = 1; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    float rr = R * t;
                    var p = c + new Vector3(rr * Mathf.Cos(ra), rr * 0.75f * t + rr * 0.3f * t * t, rr * Mathf.Sin(ra));
                    Emit(p, SpawnPoint.LookRotation(p - prev, fanNormal), new Vector3(1.2f, 0.8f, 2.6f), dom);
                    prev = p;
                }
            }
        }

        void TubeSponge(Vector3 c, float h, Domains dom)
        {
            int n = (int)(h / 1.7f);
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 6; k++)
                {
                    float a = 2f * Mathf.PI * k / 6f + i * 0.3f;
                    Emit(c + new Vector3(2.6f * Mathf.Cos(a), i * 1.7f, 2.6f * Mathf.Sin(a)),
                        Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), new Vector3(1.2f, 1.5f, 1.2f), dom);
                }
        }

        void Anemone(Vector3 c, int moundSeed)
        {
            // One ruby danger cluster per mound - spikes in a tight golden-angle whorl, so the
            // hazard reads as a single organism, not scattered noise.
            for (int k = 0; k < 9; k++)
            {
                float a = k * GoldenAngle;
                Emit(c + new Vector3(3.4f * Mathf.Cos(a), 2.2f + 1.6f * Hash01(moundSeed * 5 + k), 3.4f * Mathf.Sin(a)),
                    Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f),
                    new Vector3(1.1f, 3.4f, 1.1f), Domains.Ruby, PrismKind.Danger);
            }
        }

        // ── 4. KELP VEILS ────────────────────────────────────────────────────

        void BuildKelpVeils()
        {
            int strandCount = Mathf.RoundToInt(70 * density);
            for (int s = 0; s < strandCount; s++)
            {
                float a = s * GoldenAngle;
                float rr = 95f + (outerRadius - 130f) * Mathf.Sqrt(Hash01(s * 911 + _noiseSeed));
                var basePos = new Vector3(rr * Mathf.Cos(a), BowlY(rr), rr * Mathf.Sin(a));
                float h = 170f + 150f * Hash01(s * 13 + 1);
                int segs = (int)(h / 3.4f);
                Vector3 p = basePos, prev = basePos;
                for (int i = 0; i < segs; i++)
                {
                    float t = i / (float)segs;
                    var drift = Curl(p, 0.007f, 9);
                    p = new Vector3(p.x + drift.x * 2.2f, basePos.y + h * t, p.z + drift.z * 2.2f);
                    Domains dom = (i / 7) % 2 == 0 ? Domains.Jade : Domains.Gold;
                    var up = i == 0 ? Vector3.up : (p - prev).normalized;
                    Emit(p, SpawnPoint.LookRotation(up, Vector3.right), Jit(new Vector3(1.4f, 0.7f, 3.2f), 0.15f), dom);
                    if (i % 3 == 1)
                    {
                        // Pinnate blades alternate sides along the strand.
                        float side = (i / 3) % 2 == 0 ? 1f : -1f;
                        var lateral = Vector3.Cross(up, Vector3.up).sqrMagnitude > 0.01f
                            ? Vector3.Cross(up, Vector3.up).normalized
                            : Vector3.right;
                        Emit(p + lateral * (2.6f * side), SpawnPoint.LookRotation(lateral * side, up),
                            new Vector3(1.6f, 0.6f, 3.4f), Domains.Jade);
                    }
                    prev = p;
                }
            }
        }

        // ── 5. MOBIUS CAUSEWAY ───────────────────────────────────────────────

        void BuildMobiusCauseway()
        {
            int n = Mathf.RoundToInt(1050 * density);
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = 2f * Mathf.PI * i / n;
                // Trefoil path threading tree, terraces, and reef.
                pts[i] = new Vector3(
                    (Mathf.Sin(t) + 2f * Mathf.Sin(2f * t)) * 128f,
                    -Mathf.Sin(3f * t) * 128f + 30f,
                    (Mathf.Cos(t) - 2f * Mathf.Cos(2f * t)) * 128f);
            }

            for (int i = 0; i < n; i++)
            {
                Vector3 p = pts[i];
                Vector3 tangent = (pts[(i + 1) % n] - p).normalized;
                // The ribbon's surface normal rolls one full turn per circuit - each half reads
                // as a Mobius band, and both faces of the deck become skim surfaces in one lap.
                float theta = 2f * Mathf.PI * i / n;
                Vector3 side = Vector3.Cross(tangent, Vector3.up);
                side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
                Vector3 up = Vector3.Cross(side, tangent).normalized;
                Vector3 rolledSide = side * Mathf.Cos(theta) + up * Mathf.Sin(theta);
                Vector3 rolledNormal = Vector3.Cross(tangent, rolledSide).normalized;

                for (int w = -2; w <= 2; w++)
                {
                    Vector3 q = p + rolledSide * (w * 5.2f);
                    if (w == -2 || w == 2)
                    {
                        // Rail studs; every 36th is a shielded shrine marker.
                        bool shrine = i % 36 == 0;
                        Emit(q, SpawnPoint.LookRotation(rolledNormal, tangent),
                            shrine ? new Vector3(2.8f, 2f, 2.8f) : Jit(new Vector3(3f, 1.8f, 3f), 0.15f),
                            Domains.Blue, shrine ? PrismKind.Shielded : PrismKind.Plain);
                    }
                    else
                    {
                        Emit(q, SpawnPoint.LookRotation(rolledNormal, tangent),
                            Jit(new Vector3(4.2f, 5.8f, 1.1f), 0.12f), Domains.Gold);
                    }
                }
            }
        }

        // ── 6. ATOLLS ────────────────────────────────────────────────────────

        void BuildAtolls()
        {
            const int isles = 13;
            for (int k = 0; k < isles; k++)
            {
                float a = k * GoldenAngle * 2.1f;
                float rr = 150f + 300f * Hash01(k * 37 + _noiseSeed);
                float y = 125f + 175f * Hash01(k * 53 + 3);
                var c = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                float R = 16f + 13f * Hash01(k * 11);
                Domains isleDom = k % 2 != 0 ? Domains.Blue : Domains.Jade;

                // Islet body: stacked shrinking rings, sagging as they descend - stone mid-melt.
                const int layers = 9;
                for (int L = 0; L < layers; L++)
                {
                    float t = L / (float)(layers - 1);
                    float r2 = R * (1f - t * 0.85f) * (1f + 0.25f * Mathf.Sin(t * 9f + k));
                    float yy = -t * R * 1.3f;
                    int m = Mathf.Max(6, (int)(2f * Mathf.PI * r2 / 3.4f));
                    for (int i = 0; i < m; i++)
                    {
                        float aa = 2f * Mathf.PI * i / m + L * 0.35f;
                        var radial = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa));
                        var tangent = new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa));
                        Emit(c + new Vector3(radial.x * r2, yy, radial.z * r2),
                            SpawnPoint.LookRotation(tangent, radial),
                            Jit(new Vector3(3f, 2f, 3f), 0.25f), isleDom);
                    }
                }

                // Stalactite drip tail.
                for (int d = 0; d < 16; d++)
                {
                    float t = d / 15f;
                    float w = Mathf.Max(0.6f, 2f * (1f - t * 0.7f));
                    Emit(c + new Vector3(2.6f * Mathf.Sin(t * 7f + k), -R * 1.3f - d * 3f, 2.6f * Mathf.Cos(t * 5f)),
                        SpawnPoint.LookRotation(Vector3.down, Vector3.right),
                        new Vector3(w, w, 2.8f), Domains.Blue);
                }

                // Nautilus log-spiral crown.
                const int spiralSteps = 60;
                Vector3 prevS = c;
                for (int i = 0; i < spiralSteps; i++)
                {
                    float t = i / (float)spiralSteps;
                    float aa = t * 2.4f * 2f * Mathf.PI;
                    float r2 = 1.6f * Mathf.Exp(0.22f * aa);
                    if (r2 > R * 1.6f) break;
                    var p = c + new Vector3(r2 * Mathf.Cos(aa), R * 0.3f + t * R * 0.75f, r2 * Mathf.Sin(aa));
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up)
                        : SpawnPoint.LookRotation(p - prevS, Vector3.up);
                    Emit(p, rot, new Vector3(1.3f, 1.4f, 2.2f), Domains.Gold);
                    prevS = p;
                }

                // Waterfall stream curling away below the islet.
                var fall = c + new Vector3(R * 0.7f, -3f, 0f);
                for (int i = 0; i < 42; i++)
                {
                    var drift = Curl(fall, 0.009f, 10);
                    var step = new Vector3(drift.x * 2.6f, -4.4f, drift.z * 2.6f);
                    fall += step;
                    Emit(fall, SpawnPoint.LookRotation(step, Vector3.up), new Vector3(1.1f, 1.1f, 2.6f), Domains.Blue);
                }
            }
        }

        // ── 7. GLIMMER CURRENTS ──────────────────────────────────────────────

        void BuildGlimmerCurrents()
        {
            int lines = Mathf.RoundToInt(18 * density);
            float yMin = floorDepth + 10f;
            const float yMax = 290f;
            for (int s = 0; s < lines; s++)
            {
                float a = s * GoldenAngle * 3.7f;
                float rr = outerRadius * 1.16f;
                var p = new Vector3(rr * Mathf.Cos(a), 120f * Hash01(s * 29 + _noiseSeed) - 40f, rr * Mathf.Sin(a));
                for (int i = 0; i < 210; i++)
                {
                    var v = Curl(p, 0.006f, 12);
                    var toCentre = new Vector3(-p.x, 0f, -p.z).normalized;
                    var orbit = new Vector3(-p.z, 0f, p.x).normalized;
                    var d = (v + toCentre * 0.32f + orbit * 0.55f).normalized;
                    // Keep the streamline inside the composition's vertical band - reflect the
                    // drift rather than clamping position, so the line bends, never kinks.
                    if ((p.y + d.y * 5.2f < yMin && d.y < 0f) || (p.y + d.y * 5.2f > yMax && d.y > 0f))
                        d = new Vector3(d.x, -d.y, d.z);
                    p += d * 5.2f;
                    float horiz = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                    if (horiz > outerRadius * 1.25f) break;
                    Emit(p, SpawnPoint.LookRotation(d, Vector3.up),
                        new Vector3(1.9f, 0.9f, 4.4f), s % 3 != 0 ? Domains.Blue : Domains.Gold);
                }
            }
        }

        // ── 8. TIDE GARDEN ───────────────────────────────────────────────────

        void BuildTideGarden()
        {
            const int rows = 34;
            for (int row = 0; row < rows; row++)
            {
                float R = 70f + (outerRadius - 80f) * row / rows;
                int n = (int)(2f * Mathf.PI * R / 9.4f);
                Domains dom = (row % 3) switch
                {
                    0 => Domains.Jade,
                    1 => Domains.Blue,
                    _ => Domains.Gold,
                };
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float wob = 16f * N01(Mathf.Cos(a) * 3f + row * 0.31f, row * 0.5f, Mathf.Sin(a) * 3f, 13);
                    // Dune gaps breathe - the floor is ripples with fly-through channels, not a lid.
                    if (N01(a * 2.3f, row * 0.8f, 4.5f, 14) < 0.34f) continue;
                    float rr = R + wob;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3(rr * Mathf.Cos(a), BowlY(rr) - 2f, rr * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, tangent),
                        Jit(new Vector3(3.4f, 5.4f, 0.9f), 0.3f), dom);
                }
            }

            // Pebble clusters - happy little stones between the dunes.
            int clusters = Mathf.RoundToInt(60 * density);
            for (int k = 0; k < clusters; k++)
            {
                float a = Hash01(k * 61 + _noiseSeed) * 2f * Mathf.PI;
                float rr = 90f + (outerRadius - 110f) * Hash01(k * 67 + 1);
                var c = new Vector3(rr * Mathf.Cos(a), BowlY(rr), rr * Mathf.Sin(a));
                for (int j = 0; j < 7; j++)
                    Emit(c + new Vector3(6f * Hash01(k * 7 + j) - 3f, j * 0.5f, 6f * Hash01(k * 9 + j) - 3f),
                        Quaternion.Euler(0f, Hash01(k * 13 + j) * 360f, 0f),
                        Jit(new Vector3(2.4f, 1.8f, 2.4f), 0.3f), Domains.Blue);
            }
        }
    }
}
