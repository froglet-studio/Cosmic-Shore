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
    /// FLUSH: no prism interpenetrates another (Docs/ECOSYSTEM.md §49.10). Every family lays
    /// exactly the prisms it always laid - same count, positions, rotations, domains, kinds and
    /// random draws - and then shrinks, on one axis and only until their faces touch, the pairs
    /// it knows are neighbours (FLUSH FIT, end of file). The sizes stated below are therefore
    /// AUTHORED maxima: a trunk plank stated 7.5 long lands as long as its ~2.4 step along the
    /// strand, a leaf plate gives up the in-plane share it overlapped its neighbours by. Only a
    /// pair whose centres sit closer than the prism's 0.5 floor allows (a sea fan's eleven ribs
    /// converging on one root, a meander folding two coral rows onto each other) is moved, by
    /// the least that makes it separable - ~1.5% of the lays, a mean ~0.16 units.
    ///
    /// Collider budget: EVERY kind rides the LOD-cullable BoxCollider (active count is bounded by
    /// PrismColliderLodManager's radius, not population) - a shield swaps the mesh and the mass,
    /// never the collider, so the shielded tiers are free here too. They are still rationed to
    /// ~0.3% of the structure, because armoured mass is never food and super-shielded mass is
    /// removable only by an energised blade: they read as deliberate landmarks and they persist.
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
            _recCount = _cachedLays.Count;
            _meets = new List<(int a, int b)>(LayCapacity * 4);
            _groups.Clear();
            BuildWorldTree();
            BuildTerraces();
            BuildReefGardens();
            BuildKelpVeils();
            BuildMobiusCauseway();
            BuildAtolls();
            BuildGlimmerCurrents();
            BuildTideGarden();
            CrossFamilies();
            FlushAll();

            // The fit's bookkeeping is generation scratch: drop it with the build.
            _meets = null;
            _groups.Clear(); _branchLays.Clear();
        }

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(outerRadius, floorDepth, crownHeight, 2);

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
            var trunk = new int[strands, steps];
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
                    trunk[s, i] = Rec();
                    prev = p;
                }
            }
            for (int s = 0; s < strands; s++)
            {
                var strand = new List<int>(steps);
                for (int i = 0; i < steps; i++) strand.Add(trunk[s, i]);
                Group(Fam.Trunk, strand);
            }
            // Planks 7.5 long on a ~2.4 step: each touches the next planks along its own strand
            // (planks keep exactly their step); then each strand gives way where the wander
            // carries it across a neighbour strand at the same height. The wander is up to 1.1
            // rad against lanes 0.52 rad apart, so the next three lanes round can reach it.
            for (int s = 0; s < strands; s++)
                for (int i = 0; i < steps; i++)
                    for (int j = i + 1; j <= i + 3 && j < steps; j++)
                        Fit(trunk[s, i], trunk[s, j]);
            for (int s = 0; s < strands; s++)
                for (int ds = 1; ds <= 3; ds++)
                {
                    int s2 = (s + ds) % strands;
                    for (int i = 0; i < steps; i++)
                        for (int j = Mathf.Max(0, i - 3); j <= i + 3 && j < steps; j++)
                            Fit(trunk[s, i], trunk[s2, j]);
                }

            // Heartwood ring - the permanent bones of the world (super-shielded = invulnerable
            // and never food, so the landmark survives any match).
            const int heartCount = 48;
            float heartY = baseY + height * 0.4f;
            var heart = new int[heartCount];
            for (int i = 0; i < heartCount; i++)
            {
                float a = 2f * Mathf.PI * i / heartCount;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Emit(new Vector3(radial.x * 20f, heartY, radial.z * 20f),
                    SpawnPoint.LookRotation(tangent, radial),
                    new Vector3(3.2f, 3.2f, 3.2f), Domains.Gold, PrismKind.SuperShielded);
                heart[i] = Rec();
            }
            Group(Fam.Heartwood, new List<int>(heart));
            // Cubes 3.2 on a 2.6 chord: each gives way to the next two around the ring.
            for (int i = 0; i < heartCount; i++)
            {
                Fit(heart[i], heart[(i + 1) % heartCount]);
                Fit(heart[i], heart[(i + 2) % heartCount]);
            }

            // Root buttresses: nine ridges flaring outward along the bowl, three planks wide.
            const int buttresses = 9, buttSegs = 60;
            var butt = new int[buttresses, buttSegs, 3];
            var buttLen = new float[buttresses];
            for (int b = 0; b < buttresses; b++)
            {
                float a0 = 2f * Mathf.PI * b / 9f + 0.2f;
                float len = 95f + 55f * Hash01(b * 77 + _noiseSeed);
                buttLen[b] = len;
                const int segs = buttSegs;
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
                        butt[b, i, w + 1] = Rec();
                    }
                    prevMid = mid;
                }
            }
            for (int b = 0; b < buttresses; b++)
            {
                var ridge = new List<int>(buttSegs * 3);
                for (int i = 0; i < buttSegs; i++)
                    for (int w = 0; w < 3; w++) ridge.Add(butt[b, i, w]);
                Group(Fam.Buttress, ridge);
            }
            // Three lanes 4.4 apart under planks 4.8 wide, 2.8 long on a ~2 step: each plank
            // touches its lane neighbours and the next planks down its own lane.
            for (int b = 0; b < buttresses; b++)
                for (int i = 0; i < buttSegs; i++)
                    for (int w = 0; w < 3; w++)
                    {
                        for (int j = i + 1; j <= i + 2 && j < buttSegs; j++) Fit(butt[b, i, w], butt[b, j, w]);
                        for (int w2 = w + 1; w2 < 3; w2++)
                            for (int j = Mathf.Max(0, i - 2); j <= i + 2 && j < buttSegs; j++)
                                Fit(butt[b, i, w], butt[b, j, w2]);
                    }
            // Ridges wander (0.55 rad, against a 0.70 rad spacing) across the next ridges round:
            // at a plank's radius each of those is at its own t = (r - 26) / len.
            for (int b = 0; b < buttresses; b++)
                for (int i = 0; i < buttSegs; i++)
                    for (int w = 0; w < 3; w++)
                    {
                        int k = butt[b, i, w];
                        if (k < 0) continue;
                        Vector3 q = _cachedLays[k].Point.Position;
                        float rq = Mathf.Sqrt(q.x * q.x + q.z * q.z);
                        for (int db = 1; db <= 2; db++)
                        {
                            int b2 = (b + db) % buttresses;
                            int j0 = Mathf.RoundToInt((rq - 26f) / buttLen[b2] * (buttSegs - 1));
                            for (int j = Mathf.Max(0, j0 - 3); j <= j0 + 3 && j < buttSegs; j++)
                                for (int w2 = 0; w2 < 3; w2++) Fit(k, butt[b2, j, w2]);
                        }
                    }

            // Branches: golden-angle around the trunk's upper half, drooping under their own
            // weight, splitting twice, tipped with phyllotaxis blossom heads.
            const int branches = 30;
            _branchLays.Clear();
            for (int b = 0; b < branches; b++)
            {
                float t0 = 0.42f + 0.55f * (b / (float)branches);
                float yb = baseY + height * t0;
                float ang = b * GoldenAngle;
                float len = 150f * (1.18f - t0) + 45f;
                float droop = 55f * (1f - t0) + 18f;
                int from = _branchLays.Count;
                Branch(new Vector3(14f * Mathf.Cos(ang), yb, 14f * Mathf.Sin(ang)), ang, len, droop, 2, b, null);
                Group(Fam.Branch, _branchLays.GetRange(from, _branchLays.Count - from));
            }

            // Canopy: a fibonacci-spiral dome of leaf plates, thin axis along the dome normal.
            int leaves = Mathf.RoundToInt(7200 * density);
            var leaf = new int[leaves];
            var leafAt = new Vector2[leaves];   // each leaf's (x, z), for the fit's window scan
            var fruit = new List<(int leaf, int lay)>();
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
                leaf[i] = Rec();
                leafAt[i] = new Vector2(p.x, p.z);

                if (i % 140 == 70) // shielded fruit hanging under the canopy
                {
                    Emit(new Vector3(p.x, p.y - 6f, p.z), Quaternion.identity,
                        new Vector3(2.6f, 2.6f, 2.6f), Domains.Gold, PrismKind.Shielded);
                    fruit.Add((i, Rec()));
                }
            }
            // Plates ~27 square units on ~12 of dome each: every leaf meets the leaves within a
            // plate's reach of it - a golden-angle disc's radius grows with sqrt(index), so those
            // are the indices whose radius lies within reach, at its angle - and the footprint
            // gives way in-plane, never the thin axis the dome is read by.
            for (int i = 0; i < leaves; i++) FitCanopy(leaf, leafAt, leaf[i], 9.2f, i + 1);
            // For the families that reach the dome, its leaves in 40-unit patches (a golden-angle
            // disc's index order runs round the whole disc, so lay order is no patch).
            var patches = new Dictionary<int, List<int>>();
            for (int i = 0; i < leaves; i++)
            {
                if (leaf[i] < 0) continue;
                Vector3 q = _cachedLays[leaf[i]].Point.Position;
                int key = Mathf.FloorToInt((q.x + 200f) / 40f) * 64 + Mathf.FloorToInt((q.z + 200f) / 40f);
                if (!patches.TryGetValue(key, out var patch)) patches[key] = patch = new List<int>();
                patch.Add(leaf[i]);
            }
            foreach (var patch in patches.Values) Group(Fam.Canopy, patch);
            var fruitLays = new List<int>();
            foreach (var (_, lay) in fruit) fruitLays.Add(lay);
            Group(Fam.Canopy, fruitLays);
            // The fruit hang just under the dome.
            foreach (var (_, lay) in fruit) FitCanopy(leaf, leafAt, lay, 9f, 0);
        }

        /// <summary>Every lay a <see cref="Branch"/> call made (segments, thorns, petals, discs),
        /// in lay order - each top-level limb's run of it is one group of the tree.</summary>
        readonly List<int> _branchLays = new();

        /// <summary>Shrink the lay at <paramref name="k"/> against the canopy leaves (from index
        /// <paramref name="from"/> on) within <paramref name="reach"/> of it: leaf i sits at radius
        /// 165 sqrt(i / leaves), so the candidates are one index window, filtered by distance.</summary>
        void FitCanopy(int[] leaf, Vector2[] leafAt, int k, float reach, int from)
        {
            int leaves = leaf.Length;
            if (k < 0) return;
            Vector3 q = _cachedLays[k].Point.Position;
            float r0 = Mathf.Sqrt(q.x * q.x + q.z * q.z);
            if (r0 > 165f + reach || q.y < crownHeight - 46f - 30f - reach) return;
            float lo = Mathf.Max(0f, r0 - reach), hi = r0 + reach;
            int i0 = Mathf.Max(from, Mathf.FloorToInt(leaves * (lo / 165f) * (lo / 165f)));
            int i1 = Mathf.Min(leaves - 1, Mathf.CeilToInt(leaves * (hi / 165f) * (hi / 165f)));
            for (int i = i0; i <= i1; i++)
            {
                if (leaf[i] < 0) continue;
                float dx = leafAt[i].x - q.x, dz = leafAt[i].y - q.z;
                if (dx * dx + dz * dz > reach * reach) continue;
                Fit(leaf[i], k);
            }
        }

        void Branch(Vector3 root, float ang, float len, float droop, int level, int branchSeed, List<int> parentRoots)
        {
            int segs = Mathf.Max(9, (int)(len / 4.4f));
            var dir = new Vector3(Mathf.Cos(ang), 0.28f, Mathf.Sin(ang)).normalized;
            Vector3 p = root, prev = root;
            var seg = new int[segs + 1];
            var thorns = new List<(int seg, int lay)>();
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
                seg[i] = Rec();

                // Danger thorns on the underside of the lowest branch generation - a telegraphed
                // risk/reward run (danger prisms slam everyone, and reward danger-skim builds).
                if (level == 2 && i % 8 == 4 && t > 0.3f && t < 0.85f)
                {
                    Emit(new Vector3(p.x, p.y - 3.4f, p.z), Quaternion.identity,
                        new Vector3(1.3f, 3f, 1.3f), Domains.Ruby, PrismKind.Danger);
                    thorns.Add((i, Rec()));
                }
                prev = p;
            }

            // Blossom head: a phyllotaxis disc of petals at the tip.
            int petals = level == 2 ? 30 : 16;
            var petal = new int[petals];
            for (int k = 0; k < petals; k++)
            {
                float a = k * GoldenAngle;
                float rr = 3.1f * Mathf.Sqrt(k + 0.5f);
                var outward = new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a));
                Emit(p + new Vector3(rr * Mathf.Cos(a), 0.4f * rr, rr * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(outward, Vector3.up),
                    new Vector3(1.4f, 0.6f, 2f), Domains.Gold);
                petal[k] = Rec();
            }

            var roots = level > 0 ? new List<int>(12) : null;
            if (level > 0)
                for (int c = 0; c < 2; c++)
                    Branch(p, ang + (c * 2 - 1) * 0.85f + 0.22f * Mathf.Sin(branchSeed),
                        len * 0.56f, droop * 0.72f, level - 1, branchSeed * 5 + c + 1, roots);

            // A soft melting disc draped over some low branches (the arena's one open surrealism).
            int[] disc = null;
            if (level == 2 && branchSeed % 5 == 2)
            {
                var mid = root + dir * (len * 0.55f);
                disc = MeltDisc(new Vector3(mid.x, mid.y - droop * 0.3f, mid.z), ang, branchSeed);
            }

            // Flush: planks along the limb, each thorn under its plank, the blossom's petals among
            // themselves and against the limb's tip and the two child limbs springing from it,
            // the children against each other, and the melt disc against itself and its limb.
            for (int i = 1; i <= segs; i++)
                for (int j = i + 1; j <= i + 2 && j <= segs; j++) Fit(seg[i], seg[j]);
            foreach (var (i, lay) in thorns)
                for (int j = Mathf.Max(1, i - 2); j <= i + 2 && j <= segs; j++) Fit(lay, seg[j]);
            for (int k = 0; k < petals; k++)
                for (int k2 = k + 1; k2 < petals; k2++) Fit(petal[k], petal[k2]);
            for (int k = 0; k < petals; k++)
            {
                for (int j = 1; j <= segs; j++) Fit(petal[k], seg[j]);
                if (roots != null) foreach (int r in roots) Fit(petal[k], r);
            }
            if (roots != null)
                for (int m = 0; m < roots.Count; m++)
                {
                    for (int m2 = m + 1; m2 < roots.Count; m2++) Fit(roots[m], roots[m2]);
                    for (int j = Mathf.Max(1, segs - 3); j <= segs; j++) Fit(roots[m], seg[j]);
                }
            if (disc != null)
            {
                for (int m = 0; m < disc.Length; m++)
                {
                    for (int m2 = m + 1; m2 < disc.Length; m2++) Fit(disc[m], disc[m2]);
                    for (int j = 1; j <= segs; j++) Fit(disc[m], seg[j]);
                    foreach (var (_, lay) in thorns) Fit(disc[m], lay);
                }
                _branchLays.AddRange(disc);
            }

            for (int i = 1; i <= segs; i++) _branchLays.Add(seg[i]);
            foreach (var (_, lay) in thorns) _branchLays.Add(lay);
            _branchLays.AddRange(petal);
            if (parentRoots != null)
                for (int i = 1; i <= 6 && i <= segs; i++) parentRoots.Add(seg[i]);
        }

        int[] MeltDisc(Vector3 c, float ang, int discSeed)
        {
            float radius = 13f + 5f * Hash01(discSeed * 31);
            float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            var disc = new int[46];
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
                disc[i] = Rec();
            }
            return disc;
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
                var deck = new int[n, 5];
                var gates = new List<(int i, int[] lays)>();
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
                        deck[i, w] = Rec();
                    }
                    if (i % gateEvery == 0)
                        gates.Add((i, Gate(R, a, y, dom)));
                }

                for (int i0 = 0; i0 < n; i0 += 16)
                {
                    var arc = new List<int>(80);
                    for (int i = i0; i < n && i < i0 + 16; i++)
                        for (int w = 0; w < 5; w++) arc.Add(deck[i, w]);
                    Group(Fam.Deck, arc);
                }
                foreach (var (_, lays) in gates) Group(Fam.Gate, new List<int>(lays));
                // Planks 6.2 long on a 4.4 step: each touches the next planks round its own lane
                // (the undulation tilts the band, so the next-but-one too) and the lanes beside it.
                for (int i = 0; i < n; i++)
                    for (int w = 0; w < 5; w++)
                    {
                        for (int j = 1; j <= 2; j++) Fit(deck[i, w], deck[(i + j) % n, w]);
                        if (w < 4)
                            for (int j = -1; j <= 1; j++) Fit(deck[i, w], deck[(i + j + n) % n, w + 1]);
                    }
                // n is not always a multiple of ten gates: the last colonnade can stand a few
                // planks short of the first, close enough for their outer columns to meet.
                for (int g = 0; g < gates.Count; g++)
                {
                    var (gi, lays) = gates[g];
                    var (gj, other) = gates[(g + 1) % gates.Count];
                    if (((gj - gi + n) % n) * 4.4f > 2f * 9.5f + 8f) continue;
                    foreach (int k in lays)
                        foreach (int k2 in other) Fit(k, k2);
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
                var veil = new int[26];
                for (int i = 0; i < 26; i++)
                {
                    var drift = Curl(p, 0.008f, 15);
                    var step = new Vector3(drift.x * 2.4f + inward.x * 1.8f, -5.6f, drift.z * 2.4f + inward.z * 1.8f);
                    p += step;
                    Emit(p, SpawnPoint.LookRotation(step, Vector3.up), new Vector3(1.2f, 1.2f, 2.6f), Domains.Blue);
                    veil[i] = Rec();
                }
                Group(Fam.Veil, new List<int>(veil));
                // Drops 2.6 on a ~6 step: only a hard curl brings two together.
                for (int i = 0; i < 26; i++)
                    for (int j = i + 1; j <= i + 2 && j < 26; j++) Fit(veil[i], veil[j]);
            }
        }

        int[] Gate(float R, float a, float y, Domains dom)
        {
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var center = new Vector3(R * ca, y, R * sa);
            var tangent = new Vector3(-sa, 0f, ca);
            var lays = new int[2 * 12 + 9];

            for (int side = -1; side <= 1; side += 2)
            {
                int c0 = side < 0 ? 0 : 12;
                Vector3 basePos = center + tangent * (9.5f * side);
                float yaw = RangeF(0f, 360f);
                for (int h = 0; h < 11; h++)
                {
                    Emit(basePos + new Vector3(0f, 2f + h * 2.6f, 0f), Quaternion.Euler(0f, yaw, 0f),
                        Jit(new Vector3(2.3f, 2.5f, 2.3f), 0.1f), dom);
                    lays[c0 + h] = Rec();
                }

                // Shielded octahedron capital - the jewel that names the colonnade.
                Emit(basePos + new Vector3(0f, 2f + 11 * 2.6f, 0f), Quaternion.identity,
                    new Vector3(2.8f, 2.8f, 2.8f), Domains.Gold, PrismKind.Shielded);
                lays[c0 + 11] = Rec();
            }

            // Lintel arch spanning the two columns.
            for (int k = 0; k < 9; k++)
            {
                float t = k / 8f;
                float x = Mathf.Lerp(-9.5f, 9.5f, t);
                Emit(center + tangent * x + new Vector3(0f, 33f + 6f * Mathf.Sin(t * Mathf.PI), 0f),
                    SpawnPoint.LookRotation(tangent, Vector3.up),
                    new Vector3(2.3f, 1.2f, 3.2f), dom);
                lays[24 + k] = Rec();
            }

            // Drums 2.5 (jittered to 2.75) stacked on a 2.6 rise touch the drum above and the
            // capital; the lintel's 3.2 blocks on a 2.4 run touch along the arch and, at its two
            // ends, the capitals and top drums they rest over.
            for (int c0 = 0; c0 <= 12; c0 += 12)
                for (int h = 0; h < 11; h++) { Fit(lays[c0 + h], lays[c0 + h + 1]); if (h < 10) Fit(lays[c0 + h], lays[c0 + h + 2]); }
            for (int k = 0; k < 9; k++)
            {
                for (int k2 = k + 1; k2 <= k + 2 && k2 < 9; k2++) Fit(lays[24 + k], lays[24 + k2]);
                for (int h = 9; h <= 11; h++) { Fit(lays[24 + k], lays[h]); Fit(lays[24 + k], lays[12 + h]); }
            }
            return lays;
        }

        void Stair(float R1, float y1, float R2, float y2, float a0, int ri)
        {
            const int steps = 56;
            Domains dom = ri != 0 ? Domains.Gold : Domains.Jade;
            Vector3 prevMid = Vector3.zero;
            var stair = new int[steps, 3];
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
                    stair[i, w + 1] = Rec();
                }
                prevMid = mid;
            }
            // Treads touch the next treads up their lane and the lanes beside them.
            for (int i = 0; i < steps; i++)
                for (int w = 0; w < 3; w++)
                {
                    for (int j = i + 1; j <= i + 2 && j < steps; j++) Fit(stair[i, w], stair[j, w]);
                    for (int w2 = w + 1; w2 < 3; w2++)
                        for (int j = Mathf.Max(0, i - 2); j <= i + 2 && j < steps; j++) Fit(stair[i, w], stair[j, w2]);
                }
            var flight = new List<int>(steps * 3);
            for (int i = 0; i < steps; i++)
                for (int w = 0; w < 3; w++) flight.Add(stair[i, w]);
            Group(Fam.Stair, flight);
        }

        // ── 3. REEF GARDENS ──────────────────────────────────────────────────

        /// <summary>One coral mound's lays, kept for the flush fit: the brain coral by row (its
        /// dome is the lattice the other corals grow through) and every other organism as a
        /// group of its own.</summary>
        sealed class Mound
        {
            public Vector3 C;
            public float R;
            public int[][] Brain;
            public float[] RowY, RowR;   // each brain row's height and radius off the mound centre
            public readonly List<List<int>> Parts = new();
        }

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
                var mound = new Mound { C = c, R = 30f + 18f * Hash01(m * 17) };

                mound.Brain = BrainCoral(c, mound.R, dom, m);
                int rows = mound.Brain.Length;
                mound.RowY = new float[rows]; mound.RowR = new float[rows];
                for (int row = 0; row < rows; row++)
                {
                    float lat = (row / (float)rows) * Mathf.PI * 0.5f;
                    mound.RowY[row] = mound.R * Mathf.Sin(lat) * 0.6f;
                    mound.RowR[row] = mound.R * Mathf.Cos(lat);
                }
                for (int s = 0; s < 5; s++)
                {
                    float sa = a + s * 1.45f + 0.4f;
                    var tree = new List<int>();
                    Staghorn(c + new Vector3(22f * Mathf.Cos(sa), 0f, 22f * Mathf.Sin(sa)),
                        sa, 30f + 12f * Hash01(m * 7 + s), dom, 2, m * 10 + s, tree);
                    FitGroup(tree);   // ~50 lays: every fork against every twig
                    mound.Parts.Add(tree);
                }
                for (int f = 0; f < 4; f++)
                {
                    float fa = a + f * 1.7f;
                    mound.Parts.Add(SeaFan(c + new Vector3(30f * Mathf.Cos(fa), 4f, 30f * Mathf.Sin(fa)),
                        fa + 1.2f, 26f + 8f * f, dom));
                }
                for (int tb = 0; tb < 6; tb++)
                {
                    float ta = a + tb * 1.1f + 0.8f;
                    var tube = TubeSponge(c + new Vector3(17f * Mathf.Cos(ta), 0f, 17f * Mathf.Sin(ta)),
                        12f + 8f * Hash01(m + tb * 3), dom);
                    FitGroup(tube);
                    mound.Parts.Add(tube);
                }
                var anemone = Anemone(c + new Vector3(9f, 2f, -6f), m);
                FitGroup(anemone);
                mound.Parts.Add(anemone);

                // Flush (each organism has fitted itself above: a staghorn's forks, a fan's
                // converging ribs, a sponge's rings, the anemone's whorl): the brain coral's folds
                // along and across its rows; the organisms with each other where their bounds
                // meet, and with the dome they all grow through.
                var dome = new List<int>();
                foreach (var row in mound.Brain) dome.AddRange(row);
                Group(Fam.Reef, dome);
                foreach (var part in mound.Parts) Group(Fam.Reef, part);
                FitBrainRows(mound);
                for (int g = 0; g < mound.Parts.Count; g++)
                {
                    for (int g2 = g + 1; g2 < mound.Parts.Count; g2++) FitGroups(mound.Parts[g], mound.Parts[g2]);
                    foreach (int k in mound.Parts[g]) FitBrain(mound, k, 4.5f);
                }
            }
        }

        int[][] BrainCoral(Vector3 c, float R, Domains dom, int moundSeed)
        {
            int rows = (int)(R * 0.85f);
            var brain = new int[rows][];
            for (int row = 0; row < rows; row++)
            {
                float lat = (row / (float)rows) * Mathf.PI * 0.5f;
                float rr = R * Mathf.Cos(lat);
                float y = R * Mathf.Sin(lat) * 0.6f;
                int n = Mathf.Max(6, (int)(2f * Mathf.PI * rr / 3.4f));
                float wobA = 3.5f * N01(row * 0.7f, moundSeed * 9f, 0f, 6);
                var ring = brain[row] = new int[n];
                for (int i = 0; i < n; i++)
                {
                    ring[i] = -1;
                    float a = 2f * Mathf.PI * i / n;
                    float wr = rr + 3.4f * Mathf.Sin(a * 7f + wobA * 6.28f);
                    var p = c + new Vector3(wr * Mathf.Cos(a), y + 1.8f * Mathf.Sin(a * 7f + row), wr * Mathf.Sin(a));
                    // Meander folds stay porous - the mound is a garden to weave through, not a wall.
                    if (N01(p.x * 0.07f, p.y * 0.07f, p.z * 0.07f, 7) < 0.40f) continue;

                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var normal = (p - c).normalized;
                    Emit(p, SpawnPoint.LookRotation(tangent, normal), Jit(new Vector3(2.2f, 1.6f, 2.6f), 0.25f), dom);
                    ring[i] = Rec();
                }
            }
            return brain;
        }

        /// <summary>Brain-coral folds on a 3.4 step round each row and ~1-2 between rows, and a
        /// row's ±1.8 meander carries it past the next few: each fold touches the next two round
        /// its row and the nearest folds of the six rows above.</summary>
        void FitBrainRows(Mound m)
        {
            var brain = m.Brain;
            for (int row = 0; row < brain.Length; row++)
            {
                int n = brain[row].Length;
                for (int i = 0; i < n; i++)
                {
                    int k = brain[row][i];
                    if (k < 0) continue;
                    for (int j = 1; j <= 2; j++) Fit(k, brain[row][(i + j) % n]);
                    for (int r2 = row + 1; r2 <= row + 6 && r2 < brain.Length; r2++)
                    {
                        int n2 = brain[r2].Length;
                        int j0 = Mathf.RoundToInt(i * (float)n2 / n);
                        for (int j = j0 - 3; j <= j0 + 3; j++) Fit(k, brain[r2][((j % n2) + n2) % n2]);
                    }
                }
            }
        }

        /// <summary>Shrink the lay at <paramref name="k"/> against the brain-coral folds within
        /// <paramref name="reach"/> of it: the rows whose height band it is in, and round each
        /// of those the folds at its angle.</summary>
        void FitBrain(Mound m, int k, float reach)
        {
            if (k < 0) return;
            Vector3 q = _cachedLays[k].Point.Position - m.C;
            float rq = Mathf.Sqrt(q.x * q.x + q.z * q.z);
            if (rq > m.R + 3.4f + reach || q.y > 0.6f * m.R + 1.8f + reach) return;
            float aq = Mathf.Atan2(q.z, q.x);
            var brain = m.Brain;
            for (int row = 0; row < brain.Length; row++)
            {
                float y = m.RowY[row], rr = m.RowR[row];
                if (Mathf.Abs(q.y - y) > reach + 1.8f || Mathf.Abs(rq - rr) > reach + 3.4f) continue;
                int n = brain[row].Length;
                int i0 = Mathf.RoundToInt(aq / (2f * Mathf.PI) * n);
                int w = Mathf.Min(n / 2, Mathf.CeilToInt((reach + 3.4f) / Mathf.Max(0.5f, 2f * Mathf.PI * (rr - 3.4f) / n)) + 1);
                for (int j = i0 - w; j <= i0 + w; j++) Fit(brain[row][((j % n) + n) % n], k);
            }
        }

        void Staghorn(Vector3 root, float ang, float len, Domains dom, int level, int branchSeed, List<int> tree)
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
                tree.Add(Rec());
                prev = p;
            }

            if (level > 0)
            {
                for (int c = 0; c < 2; c++)
                    Staghorn(p, ang + (c * 2 - 1) * (0.7f + 0.2f * Hash01(branchSeed + c)),
                        len * 0.62f, dom, level - 1, branchSeed * 3 + c, tree);
            }
            else
            {
                // Fire-coral tip: the outermost twig ends hot - visible, growth-facing danger.
                Emit(p + dir * 3.2f, SpawnPoint.LookRotation(dir, Vector3.up),
                    new Vector3(1.8f, 1.8f, 3.6f), Domains.Ruby, PrismKind.Danger);
                tree.Add(Rec());
            }
        }

        List<int> SeaFan(Vector3 c, float facing, float R, Domains dom)
        {
            const int ribs = 11;
            var fan = new List<int>();
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
                    fan.Add(Rec());
                    prev = p;
                }
            }
            // Ribs 2.6 long on a 2.2 step: each touches the next along its rib and the ribs
            // beside it; the eleven ribs converge on one root (0.2 rad apart, 0.44 apart at the
            // first step), so near the root every rib meets every other.
            int n = (int)(R / 2.2f);
            for (int rib = 0; rib < ribs; rib++)
                for (int i = 0; i < n; i++)
                {
                    int k = fan[rib * n + i];
                    for (int j = i + 1; j <= i + 2 && j < n; j++) Fit(k, fan[rib * n + j]);
                    int far = i < 5 ? ribs - 1 : Mathf.Min(ribs - 1, rib + 2);
                    for (int r2 = rib + 1; r2 <= far; r2++)
                        for (int j = Mathf.Max(0, i - 2); j <= i + 2 && j < n; j++) Fit(k, fan[r2 * n + j]);
                }
            return fan;
        }

        List<int> TubeSponge(Vector3 c, float h, Domains dom)
        {
            int n = (int)(h / 1.7f);
            var tube = new List<int>(n * 6);
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 6; k++)
                {
                    float a = 2f * Mathf.PI * k / 6f + i * 0.3f;
                    Emit(c + new Vector3(2.6f * Mathf.Cos(a), i * 1.7f, 2.6f * Mathf.Sin(a)),
                        Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), new Vector3(1.2f, 1.5f, 1.2f), dom);
                    tube.Add(Rec());
                }
            return tube;
        }

        List<int> Anemone(Vector3 c, int moundSeed)
        {
            // One ruby danger cluster per mound - spikes in a tight golden-angle whorl, so the
            // hazard reads as a single organism, not scattered noise.
            var spikes = new List<int>(9);
            for (int k = 0; k < 9; k++)
            {
                float a = k * GoldenAngle;
                Emit(c + new Vector3(3.4f * Mathf.Cos(a), 2.2f + 1.6f * Hash01(moundSeed * 5 + k), 3.4f * Mathf.Sin(a)),
                    Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f),
                    new Vector3(1.1f, 3.4f, 1.1f), Domains.Ruby, PrismKind.Danger);
                spikes.Add(Rec());
            }
            return spikes;
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
                var stem = new int[segs];
                var blades = new List<(int i, int lay)>();
                for (int i = 0; i < segs; i++)
                {
                    float t = i / (float)segs;
                    var drift = Curl(p, 0.007f, 9);
                    p = new Vector3(p.x + drift.x * 2.2f, basePos.y + h * t, p.z + drift.z * 2.2f);
                    Domains dom = (i / 7) % 2 == 0 ? Domains.Jade : Domains.Gold;
                    var up = i == 0 ? Vector3.up : (p - prev).normalized;
                    Emit(p, SpawnPoint.LookRotation(up, Vector3.right), Jit(new Vector3(1.4f, 0.7f, 3.2f), 0.15f), dom);
                    stem[i] = Rec();
                    if (i % 3 == 1)
                    {
                        // Pinnate blades alternate sides along the strand.
                        float side = (i / 3) % 2 == 0 ? 1f : -1f;
                        var lateral = Vector3.Cross(up, Vector3.up).sqrMagnitude > 0.01f
                            ? Vector3.Cross(up, Vector3.up).normalized
                            : Vector3.right;
                        Emit(p + lateral * (2.6f * side), SpawnPoint.LookRotation(lateral * side, up),
                            new Vector3(1.6f, 0.6f, 3.4f), Domains.Jade);
                        blades.Add((i, Rec()));
                    }
                    prev = p;
                }
                // Stem planks 3.2 (jittered to 3.7) on a 3.4 rise touch the next up the strand;
                // each blade touches the stem it springs from and the blade a side-swap above.
                for (int i = 0; i < segs; i++)
                    for (int j = i + 1; j <= i + 2 && j < segs; j++) Fit(stem[i], stem[j]);
                for (int b = 0; b < blades.Count; b++)
                {
                    var (i, lay) = blades[b];
                    for (int j = Mathf.Max(0, i - 2); j <= i + 2 && j < segs; j++) Fit(lay, stem[j]);
                    for (int b2 = b + 1; b2 <= b + 2 && b2 < blades.Count; b2++) Fit(lay, blades[b2].lay);
                }
                var strand = new List<int>(stem);
                foreach (var (_, lay) in blades) strand.Add(lay);
                Group(Fam.Kelp, strand);   // the curl drift can carry one strand across another
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

            var road = new int[n, 5];   // [i, lane]; lanes 0 and 4 are the rail studs
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
                    road[i, w + 2] = Rec();
                }
            }
            for (int i0 = 0; i0 < n; i0 += 16)
            {
                var span = new List<int>(80);
                for (int i = i0; i < n && i < i0 + 16; i++)
                    for (int w = 0; w < 5; w++) span.Add(road[i, w]);
                Group(Fam.Causeway, span);
            }
            // Deck plates 5.8 along a ~3 step: each touches the next two along its lane (the
            // trefoil's bends shorten the inner lanes) and the lanes beside it.
            for (int i = 0; i < n; i++)
                for (int w = 0; w < 5; w++)
                {
                    for (int j = 1; j <= 2; j++) Fit(road[i, w], road[(i + j) % n, w]);
                    if (w < 4)
                        for (int j = -1; j <= 1; j++) Fit(road[i, w], road[(i + j + n) % n, w + 1]);
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
                var isle = new List<int>();

                // Islet body: stacked shrinking rings, sagging as they descend - stone mid-melt.
                const int layers = 9;
                var body = new int[layers][];
                for (int L = 0; L < layers; L++)
                {
                    float t = L / (float)(layers - 1);
                    float r2 = R * (1f - t * 0.85f) * (1f + 0.25f * Mathf.Sin(t * 9f + k));
                    float yy = -t * R * 1.3f;
                    int m = Mathf.Max(6, (int)(2f * Mathf.PI * r2 / 3.4f));
                    body[L] = new int[m];
                    for (int i = 0; i < m; i++)
                    {
                        float aa = 2f * Mathf.PI * i / m + L * 0.35f;
                        var radial = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa));
                        var tangent = new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa));
                        Emit(c + new Vector3(radial.x * r2, yy, radial.z * r2),
                            SpawnPoint.LookRotation(tangent, radial),
                            Jit(new Vector3(3f, 2f, 3f), 0.25f), isleDom);
                        body[L][i] = Rec();
                    }
                }

                // Stalactite drip tail.
                var drip = new int[16];
                for (int d = 0; d < 16; d++)
                {
                    float t = d / 15f;
                    float w = Mathf.Max(0.6f, 2f * (1f - t * 0.7f));
                    Emit(c + new Vector3(2.6f * Mathf.Sin(t * 7f + k), -R * 1.3f - d * 3f, 2.6f * Mathf.Cos(t * 5f)),
                        SpawnPoint.LookRotation(Vector3.down, Vector3.right),
                        new Vector3(w, w, 2.8f), Domains.Blue);
                    drip[d] = Rec();
                }

                // Nautilus log-spiral crown.
                const int spiralSteps = 60;
                Vector3 prevS = c;
                var crown = new List<int>(spiralSteps);
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
                    crown.Add(Rec());
                    prevS = p;
                }

                // Waterfall stream curling away below the islet.
                var fall = c + new Vector3(R * 0.7f, -3f, 0f);
                var stream = new List<int>(42);
                for (int i = 0; i < 42; i++)
                {
                    var drift = Curl(fall, 0.009f, 10);
                    var step = new Vector3(drift.x * 2.6f, -4.4f, drift.z * 2.6f);
                    fall += step;
                    Emit(fall, SpawnPoint.LookRotation(step, Vector3.up), new Vector3(1.1f, 1.1f, 2.6f), Domains.Blue);
                    stream.Add(Rec());
                }

                // Flush: each ring's blocks round the ring and against the two rings below it
                // (the sag and the per-ring twist put them at any angle), the drip tail down
                // itself and into the base, the crown's tight inner whorl along its spiral (and
                // across turns), the waterfall down itself and against the rings it leaves.
                for (int L = 0; L < layers; L++)
                {
                    int m = body[L].Length;
                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 1; j <= 2; j++) Fit(body[L][i], body[L][(i + j) % m]);
                        for (int L2 = L + 1; L2 <= L + 2 && L2 < layers; L2++)
                            foreach (int k2 in body[L2]) Fit(body[L][i], k2);
                    }
                }
                for (int d = 0; d < 16; d++)
                {
                    for (int d2 = d + 1; d2 <= d + 2 && d2 < 16; d2++) Fit(drip[d], drip[d2]);
                    if (d < 3)
                        for (int L = layers - 3; L < layers; L++)
                            foreach (int k2 in body[L]) Fit(drip[d], k2);
                }
                FitGroup(crown);
                for (int i = 0; i < stream.Count; i++)
                {
                    for (int j = i + 1; j <= i + 2 && j < stream.Count; j++) Fit(stream[i], stream[j]);
                    if (i < 8)
                        for (int L = 0; L < layers; L++)
                            foreach (int k2 in body[L]) Fit(stream[i], k2);
                }

                foreach (var ring in body) isle.AddRange(ring);
                isle.AddRange(drip); isle.AddRange(crown); isle.AddRange(stream);
                Group(Fam.Atoll, isle);
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
                var line = new List<int>(210);
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
                    line.Add(Rec());
                }
                // Dashes 4.4 on a 5.2 step clear along the line except where it turns hard.
                for (int i = 0; i < line.Count; i++)
                    for (int j = i + 1; j <= i + 2 && j < line.Count; j++) Fit(line[i], line[j]);
                // A streamline spirals inward and can cross itself (and its neighbours - the
                // cross-family pass names those); where the curl pins it against the band's
                // floor or ceiling it reflects back and forth over almost the same spot, and the
                // dashes it piles there part along the line - so a line names its own later
                // turns from further off.
                FitGroups(line, line, 3, 6f);
                Group(Fam.Glimmer, line);
            }
        }

        // ── 8. TIDE GARDEN ───────────────────────────────────────────────────

        void BuildTideGarden()
        {
            const int rows = 34;
            var dunes = new int[rows][];   // [row][i], -1 = a breathing gap
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
                var dune = dunes[row] = new int[n];
                for (int i = 0; i < n; i++)
                {
                    dune[i] = -1;
                    float a = 2f * Mathf.PI * i / n;
                    float wob = 16f * N01(Mathf.Cos(a) * 3f + row * 0.31f, row * 0.5f, Mathf.Sin(a) * 3f, 13);
                    // Dune gaps breathe - the floor is ripples with fly-through channels, not a lid.
                    if (N01(a * 2.3f, row * 0.8f, 4.5f, 14) < 0.34f) continue;
                    float rr = R + wob;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3(rr * Mathf.Cos(a), BowlY(rr) - 2f, rr * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, tangent),
                        Jit(new Vector3(3.4f, 5.4f, 0.9f), 0.3f), dom);
                    dune[i] = Rec();
                }
            }
            foreach (var dune in dunes)
                for (int i0 = 0; i0 < dune.Length; i0 += 16)
                    Group(Fam.Dune, new List<int>(new System.ArraySegment<int>(dune, i0, Mathf.Min(16, dune.Length - i0))));
            // Ripples 5.4 long on a 9.4 step clear round a row; the 0-16 wobble can carry a
            // ripple onto the next rows out (rows ~10 apart).
            for (int row = 0; row < rows; row++)
            {
                int n = dunes[row].Length;
                for (int i = 0; i < n; i++)
                {
                    Fit(dunes[row][i], dunes[row][(i + 1) % n]);
                    for (int r2 = row + 1; r2 <= row + 2 && r2 < rows; r2++)
                    {
                        int n2 = dunes[r2].Length;
                        int j0 = Mathf.RoundToInt(i * (float)n2 / n);
                        for (int j = j0 - 2; j <= j0 + 2; j++) Fit(dunes[row][i], dunes[r2][((j % n2) + n2) % n2]);
                    }
                }
            }

            // Pebble clusters - happy little stones between the dunes.
            int clusters = Mathf.RoundToInt(60 * density);
            for (int k = 0; k < clusters; k++)
            {
                float a = Hash01(k * 61 + _noiseSeed) * 2f * Mathf.PI;
                float rr = 90f + (outerRadius - 110f) * Hash01(k * 67 + 1);
                var c = new Vector3(rr * Mathf.Cos(a), BowlY(rr), rr * Mathf.Sin(a));
                var cluster = new List<int>(7);
                for (int j = 0; j < 7; j++)
                {
                    Emit(c + new Vector3(6f * Hash01(k * 7 + j) - 3f, j * 0.5f, 6f * Hash01(k * 9 + j) - 3f),
                        Quaternion.Euler(0f, Hash01(k * 13 + j) * 360f, 0f),
                        Jit(new Vector3(2.4f, 1.8f, 2.4f), 0.3f), Domains.Blue);
                    cluster.Add(Rec());
                }
                // Seven stones heaped in a 6x6 patch, half a unit apart in height.
                FitGroup(cluster);
                Group(Fam.Pebble, cluster);
            }
        }

        // ── FLUSH FIT ────────────────────────────────────────────────────────
        //
        // NO PRISM MAY INTERPENETRATE ANOTHER (Docs/ECOSYSTEM.md §49.10), and the rule is held
        // HERE, in the generator: every family lays exactly the prisms it always laid (same
        // positions, rotations, domains, kinds, same random draws in the same order), records
        // the lay index of each, and names the pairs it KNOWS are neighbours from its own
        // construction - the next plank along a strand, the next lane of a ribbon, the rows of a
        // coral, the leaves within a plate's reach of a golden-angle disc. Where two families
        // cross by design, each names its organisms (a strand, a coral, an arc of planks) and
        // the pairs come from the organisms whose bounds meet. Every named pair is then shrunk
        // just enough that its faces touch - the smallest shrink on one axis, so the silhouette,
        // the sizes' variety and the population all stand. Measured in the clip-audit harness
        // (.NET, min of six builds) the fit adds ~0.09 s to ~0.45 s of generation math - once per
        // build, since the lay list is cached - and no work at all once the garden is laid.

        /// <summary>The lay count at the last <see cref="Rec"/>.</summary>
        int _recCount;

        /// <summary>Index into the lay list of the prism the preceding <c>Emit</c> laid, or -1
        /// when the spawn clearance swallowed it. Called once right after every Emit.</summary>
        int Rec()
        {
            int c = _cachedLays.Count, r = c > _recCount ? c - 1 : -1;
            _recCount = c;
            return r;
        }

        /// <summary>A fitted pair lands a hair inside exact contact, so float noise never turns
        /// a touching pair into a clip.</summary>
        const float FlushMargin = 0.997f;

        /// <summary>The prism prefab's per-axis floor (PrismScaleAnimator clamps into [0.5, 100]):
        /// a fit never shrinks an axis below it, because the lay would grow it back.</summary>
        const float MinPrismAxis = 0.5f;

        // Per-instance scratch (an environment may generate off the main thread).
        readonly Vector3[] _axA = new Vector3[3], _axB = new Vector3[3];

        /// <summary>
        /// Shrink the prisms at lay indices <paramref name="a"/> and <paramref name="b"/> (-1 =
        /// culled, ignored) MINIMALLY so they no longer interpenetrate: over the 15 separating
        /// axes of the two boxes, pick the one axis of the pair (the same local axis on both, or
        /// a uniform shrink - or, <paramref name="mixed"/>, any axis of each) whose shrink keeps
        /// the most volume, and share the room along it as <see cref="SplitShrink"/> does -
        /// honouring the 0.5 floor. A pair that already clears is untouched. False = no shrink
        /// can separate them (their centres nearly coincide).
        /// </summary>
        bool Flush(int a, int b, bool mixed = false)
        {
            if (a < 0 || b < 0 || a == b) return true;
            Vector3 d = _fp[b] - _fp[a];
            Vector3 sa = _fs[a], sb = _fs[b];
            float reach = 0.5f * (sa.magnitude + sb.magnitude);
            if (d.sqrMagnitude >= reach * reach) return true;

            _axA[0] = _fx[a]; _axA[1] = _fy[a]; _axA[2] = _fz[a];
            _axB[0] = _fx[b]; _axB[1] = _fy[b]; _axB[2] = _fz[b];
            float va = sa.x * sa.y * sa.z, vb = sb.x * sb.y * sb.z;

            float bestKeep = -1f, bestFa = 1f, bestFb = 1f;
            int bestKa = -1, bestKb = -1;
            for (int t = 0; t < 15; t++)
            {
                Vector3 n = t < 3 ? _axA[t] : t < 6 ? _axB[t - 3]
                    : Vector3.Cross(_axA[(t - 6) / 3], _axB[(t - 6) % 3]);
                if (t >= 6)
                {
                    float len2 = n.sqrMagnitude;
                    if (len2 < 1e-6f) continue;
                    n /= Mathf.Sqrt(len2);
                }
                float dn = Mathf.Abs(Vector3.Dot(d, n));
                float pa0 = 0.5f * sa.x * Mathf.Abs(Vector3.Dot(_axA[0], n));
                float pa1 = 0.5f * sa.y * Mathf.Abs(Vector3.Dot(_axA[1], n));
                float pa2 = 0.5f * sa.z * Mathf.Abs(Vector3.Dot(_axA[2], n));
                float pb0 = 0.5f * sb.x * Mathf.Abs(Vector3.Dot(_axB[0], n));
                float pb1 = 0.5f * sb.y * Mathf.Abs(Vector3.Dot(_axB[1], n));
                float pb2 = 0.5f * sb.z * Mathf.Abs(Vector3.Dot(_axB[2], n));
                float total = dn - (pa0 + pa1 + pa2 + pb0 + pb1 + pb2);
                if (total >= 0f) return true;   // this axis already separates them
                if (t >= 6 && !mixed) continue;  // shrink along a face normal; edge axes only test

                for (int ka = 0; ka < 4; ka++)
                    for (int kb = 0; kb < 4; kb++)
                    {
                        if (!mixed && ka != kb) continue;
                        float pa = ka == 0 ? pa0 : ka == 1 ? pa1 : ka == 2 ? pa2 : pa0 + pa1 + pa2;
                        float pb = kb == 0 ? pb0 : kb == 1 ? pb1 : kb == 2 ? pb2 : pb0 + pb1 + pb2;
                        float avail = (total + pa + pb) * FlushMargin;
                        float flA = MinPrismAxis / (ka < 3 ? sa[ka] : Mathf.Min(sa.x, Mathf.Min(sa.y, sa.z)));
                        float flB = MinPrismAxis / (kb < 3 ? sb[kb] : Mathf.Min(sb.x, Mathf.Min(sb.y, sb.z)));
                        if (!SplitShrink(avail, pa, pb, flA, flB, out float fa, out float fb)) continue;
                        float keep = (ka < 3 ? fa : fa * fa * fa) * va + (kb < 3 ? fb : fb * fb * fb) * vb;
                        if (keep > bestKeep) { bestKeep = keep; bestKa = ka; bestKb = kb; bestFa = fa; bestFb = fb; }
                    }
            }
            if (bestKa < 0) return false;

            if (bestKa < 3) sa[bestKa] *= bestFa; else sa *= bestFa;
            if (bestKb < 3) sb[bestKb] *= bestFb; else sb *= bestFb;
            _fs[a] = sa; _fs[b] = sb;
            return true;
        }

        /// <summary>Every pair inside one small organism (a coral, a cluster).</summary>
        void FitGroup(List<int> g)
        {
            for (int i = 0; i < g.Count; i++)
                for (int j = i + 1; j < g.Count; j++) Fit(g[i], g[j]);
        }

        /// <summary>
        /// Every pair across two organisms (or, when <paramref name="a"/> IS <paramref name="b"/>,
        /// every pair at least <paramref name="skip"/> apart in it - a line against its own
        /// later turns), for the lays that reach each other at all: <paramref name="b"/> is cut
        /// into runs of 16 consecutive lays, and a lay of <paramref name="a"/> visits only the runs
        /// whose bounds it reaches. <paramref name="slack"/> widens the reach for a family whose
        /// own parts can push a lay into neighbours it did not start beside.
        /// </summary>
        void FitGroups(List<int> a, List<int> b, int skip = 0, float slack = 0.4f)
        {
            const int run = 16;
            int runs = (b.Count + run - 1) / run;
            if (_runLo.Length < runs) { _runLo = new Vector3[runs * 2]; _runHi = new Vector3[runs * 2]; }
            for (int r = 0; r < runs; r++)
            {
                Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
                for (int i = r * run; i < b.Count && i < (r + 1) * run; i++)
                {
                    if (b[i] < 0) continue;
                    var pt = _cachedLays[b[i]].Point;
                    Vector3 e = 0.5f * pt.Scale.magnitude * Vector3.one;
                    lo = Vector3.Min(lo, pt.Position - e); hi = Vector3.Max(hi, pt.Position + e);
                }
                _runLo[r] = lo; _runHi[r] = hi;
            }
            bool same = ReferenceEquals(a, b);
            for (int ia = 0; ia < a.Count; ia++)
            {
                int k = a[ia];
                if (k < 0) continue;
                var pt = _cachedLays[k].Point;
                float e = 0.5f * pt.Scale.magnitude + slack;
                Vector3 q = pt.Position;
                for (int r = 0; r < runs; r++)
                {
                    Vector3 lo = _runLo[r], hi = _runHi[r];
                    if (q.x + e < lo.x || q.x - e > hi.x || q.y + e < lo.y || q.y - e > hi.y || q.z + e < lo.z || q.z - e > hi.z) continue;
                    for (int i = r * run; i < b.Count && i < (r + 1) * run; i++)
                        if (!same || i >= ia + skip) Fit(k, b[i], slack);
                }
            }
        }

        Vector3[] _runLo = new Vector3[64], _runHi = new Vector3[64];

        /// <summary>The eight families, finer where one family's parts are laid by different
        /// rules (the tree's trunk, heartwood, buttresses, limbs and canopy).</summary>
        enum Fam { Trunk = 0, Heartwood = 1, Buttress = 2, Branch = 3, Canopy = 4, Deck = 5, Gate = 6, Stair = 7, Veil = 8, Reef = 9, Kelp = 10, Causeway = 11, Atoll = 12, Glimmer = 13, Dune = 14, Pebble = 15 }

        /// <summary>A spatially compact run of one family's lays (a strand, a coral, an arc of
        /// planks), with its bounds and the bounds of each 16-lay run inside it.</summary>
        sealed class LayGroup
        {
            public Fam Family;
            public List<int> Lays;
            public Vector3 Lo, Hi;
            public Vector3[] RunLo, RunHi;
        }

        readonly List<LayGroup> _groups = new();

        void Group(Fam family, List<int> lays) => _groups.Add(new LayGroup { Family = family, Lays = lays });

        /// <summary>
        /// The families cross each other by design - kelp rises through the terraces, the
        /// causeway threads the tree, the reef grows out of the dunes, the currents braid
        /// everything - so every two groups of DIFFERENT families (and two organisms of a family
        /// that <see cref="CrossesItself"/>) whose bounds meet name the pairs of their 16-lay
        /// runs whose bounds meet.
        /// Bounds are taken once, on the sizes as laid.
        /// </summary>
        void CrossFamilies()
        {
            const int run = 16;
            foreach (var g in _groups)
            {
                int runs = (g.Lays.Count + run - 1) / run;
                g.RunLo = new Vector3[runs]; g.RunHi = new Vector3[runs];
                g.Lo = Vector3.positiveInfinity; g.Hi = Vector3.negativeInfinity;
                for (int r = 0; r < runs; r++)
                {
                    Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
                    for (int i = r * run; i < g.Lays.Count && i < (r + 1) * run; i++)
                    {
                        if (g.Lays[i] < 0) continue;
                        var pt = _cachedLays[g.Lays[i]].Point;
                        Vector3 e = (0.5f * pt.Scale.magnitude + 0.2f) * Vector3.one;
                        lo = Vector3.Min(lo, pt.Position - e); hi = Vector3.Max(hi, pt.Position + e);
                    }
                    g.RunLo[r] = lo; g.RunHi[r] = hi;
                    g.Lo = Vector3.Min(g.Lo, lo); g.Hi = Vector3.Max(g.Hi, hi);
                }
            }
            // Sweep along x: a group meets only the groups whose x span starts inside its own.
            _groups.Sort((g, h) => g.Lo.x.CompareTo(h.Lo.x));
            for (int x = 0; x < _groups.Count; x++)
            {
                var a = _groups[x];
                for (int y = x + 1; y < _groups.Count && _groups[y].Lo.x <= a.Hi.x; y++)
                {
                    var b = _groups[y];
                    if (a.Family == b.Family && !CrossesItself(a.Family)) continue;
                    if (!Overlap(a.Lo, a.Hi, b.Lo, b.Hi)) continue;
                    for (int ra = 0; ra < a.RunLo.Length; ra++)
                    {
                        if (!Overlap(a.RunLo[ra], a.RunHi[ra], b.Lo, b.Hi)) continue;
                        for (int rb = 0; rb < b.RunLo.Length; rb++)
                        {
                            if (!Overlap(a.RunLo[ra], a.RunHi[ra], b.RunLo[rb], b.RunHi[rb])) continue;
                            for (int i = ra * run; i < a.Lays.Count && i < (ra + 1) * run; i++)
                                for (int j = rb * run; j < b.Lays.Count && j < (rb + 1) * run; j++)
                                    Fit(a.Lays[i], b.Lays[j]);
                        }
                    }
                }
            }
        }

        /// <summary>Families whose organisms can cross each other: two limbs of the tree, two
        /// drifting kelp strands, two streamlines, two islets or two pebble heaps.</summary>
        static bool CrossesItself(Fam f) => f is Fam.Branch or Fam.Kelp or Fam.Glimmer or Fam.Atoll or Fam.Pebble;

        static bool Overlap(Vector3 alo, Vector3 ahi, Vector3 blo, Vector3 bhi) =>
            alo.x <= bhi.x && blo.x <= ahi.x && alo.y <= bhi.y && blo.y <= ahi.y && alo.z <= bhi.z && blo.z <= ahi.z;

        /// <summary>Candidate pairs in the order the families named them (lay indices).</summary>
        List<(int a, int b)> _meets;

        /// <summary>
        /// Name a pair the generator knows may touch. Only a pair whose bounding spheres reach
        /// each other (with a little slack, for a part elsewhere moving one of them) is kept, so
        /// a family can name its neighbourhood generously.
        /// </summary>
        void Fit(int a, int b, float slack = 0.4f)
        {
            if (a < 0 || b < 0 || a == b) return;
            SpawnPoint A = _cachedLays[a].Point, B = _cachedLays[b].Point;
            float reach = 0.5f * (A.Scale.magnitude + B.Scale.magnitude) + slack;
            if ((B.Position - A.Position).sqrMagnitude < reach * reach) _meets.Add((a, b));
        }

        /// <summary>
        /// Fit every named pair flush, in the order they were named (a family's own chain first,
        /// so its planks keep the most). A pair no shrink can separate - two strands whose wander
        /// carries them THROUGH each other, centres closer than the 0.5 floor allows - is first
        /// parted by the smallest move that makes it separable (<see cref="Part"/>, the only place
        /// a prism moves); a part can unseat a pair already fitted, so the sweep repeats over the
        /// pairs until one parts nothing. Where a family PILES lays (a streamline the curl pins
        /// against its band, reflecting back and forth over one spot), parts along the cheapest
        /// axis can chase each other round the pile, so from the ninth sweep on a part pushes
        /// the later prism straight out along the line between the two centres instead - the
        /// pile then only ever spreads, and settles.
        /// </summary>
        void FlushAll()
        {
            int count = _cachedLays.Count;
            _fp = new Vector3[count]; _fs = new Vector3[count];
            _fx = new Vector3[count]; _fy = new Vector3[count]; _fz = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var pt = _cachedLays[i].Point;
                Quaternion q = pt.Rotation;
                _fp[i] = pt.Position; _fs[i] = pt.Scale;
                _fx[i] = q * Vector3.right; _fy[i] = q * Vector3.up; _fz[i] = q * Vector3.forward;
            }

            // Sweep 0 fits every pair; a later sweep re-fits only the pairs a part touched (a
            // pair whose prisms did not move only ever shrank, so it still clears).
            var movedIn = new int[count];
            for (int i = 0; i < movedIn.Length; i++) movedIn[i] = -1;
            for (int sweep = 0; sweep < 64; sweep++)
            {
                bool parted = false;
                foreach (var (a, b) in _meets)
                {
                    if (sweep > 0 && movedIn[a] < sweep - 1 && movedIn[b] < sweep - 1) continue;
                    if (Flush(a, b) || Flush(a, b, true)) continue;
                    Part(a, b, sweep >= 8);
                    Flush(a, b);
                    movedIn[a] = movedIn[b] = sweep;
                    parted = true;
                }
                if (!parted) break;
            }

            for (int i = 0; i < count; i++)
            {
                var lay = _cachedLays[i];
                _cachedLays[i] = new PrismLay(new SpawnPoint(_fp[i], lay.Point.Rotation, _fs[i]), lay.Domain, lay.Kind);
            }
            _fp = _fs = _fx = _fy = _fz = null;
        }

        /// <summary>The fit's working copy of the lays: positions, scales and local axes.</summary>
        Vector3[] _fp, _fs, _fx, _fy, _fz;

        /// <summary>Part the pair by the least travel that leaves it separable, half each way
        /// along the axis that needs least; or (<paramref name="outward"/>) move the later prism
        /// along the line between their centres, far enough to clear as they stand.</summary>
        void Part(int a, int b, bool outward)
        {
            Vector3 d = _fp[b] - _fp[a];
            Vector3 sa = _fs[a], sb = _fs[b];
            _axA[0] = _fx[a]; _axA[1] = _fy[a]; _axA[2] = _fz[a];
            _axB[0] = _fx[b]; _axB[1] = _fy[b]; _axB[2] = _fz[b];
            float bestTravel = float.MaxValue;
            Vector3 bestDir = Vector3.up;
            if (outward)
            {
                bestDir = d.sqrMagnitude > 1e-8f ? d.normalized : _axA[2];
                float need = 0f;
                for (int k = 0; k < 3; k++)
                    need += 0.5f * (sa[k] * Mathf.Abs(Vector3.Dot(_axA[k], bestDir)) + sb[k] * Mathf.Abs(Vector3.Dot(_axB[k], bestDir)));
                bestTravel = need * 1.01f + 1e-3f - d.magnitude;
            }
            for (int t = 0; t < 15 && !outward; t++)
            {
                Vector3 n = t < 3 ? _axA[t] : t < 6 ? _axB[t - 3]
                    : Vector3.Cross(_axA[(t - 6) / 3], _axB[(t - 6) % 3]);
                if (t >= 6)
                {
                    float len2 = n.sqrMagnitude;
                    if (len2 < 1e-6f) continue;
                    n /= Mathf.Sqrt(len2);
                }
                float dn = Vector3.Dot(d, n);
                float full = 0f;
                for (int k = 0; k < 3; k++)
                    full += 0.5f * (sa[k] * Mathf.Abs(Vector3.Dot(_axA[k], n)) + sb[k] * Mathf.Abs(Vector3.Dot(_axB[k], n)));
                for (int k = 0; k < 3; k++)
                {
                    // Room this axis needs with the pair's axis k at its floor and the rest as laid.
                    float ka = Mathf.Abs(Vector3.Dot(_axA[k], n)), kb = Mathf.Abs(Vector3.Dot(_axB[k], n));
                    float need = full - 0.5f * ((sa[k] - MinPrismAxis) * ka + (sb[k] - MinPrismAxis) * kb);
                    float travel = need * 1.01f + 1e-3f - Mathf.Abs(dn);
                    if (travel < bestTravel) { bestTravel = travel; bestDir = dn >= 0f ? n : -n; }
                }
            }
            if (bestTravel <= 0f) return;
            if (outward) { _fp[b] += bestDir * bestTravel; return; }
            Vector3 half = bestDir * (0.5f * bestTravel);
            _fp[a] -= half;
            _fp[b] += half;
        }

        /// <summary>Split a separating axis's free room <paramref name="avail"/> between two
        /// extents <paramref name="pa"/>/<paramref name="pb"/> projected on it the way a
        /// Voronoi wall would: each prism keeps up to HALF the room, a prism already inside its
        /// half keeps its size and lends the rest to the other, and a prism stopped by its floor
        /// hands the remainder over too. (Halving, not a proportional split, is what makes a
        /// chain fitted pair by pair land every plank at exactly its own step.)</summary>
        static bool SplitShrink(float avail, float pa, float pb, float flA, float flB, out float fa, out float fb)
        {
            fa = fb = 1f;
            if (avail <= 0f) return false;
            flA = Mathf.Min(1f, flA); flB = Mathf.Min(1f, flB);
            float half = 0.5f * avail;
            if (pa <= half) { fa = 1f; fb = (avail - pa) / pb; }
            else if (pb <= half) { fb = 1f; fa = (avail - pb) / pa; }
            else { fa = half / pa; fb = half / pb; }
            if (fa < flA)
            {
                fa = flA;
                fb = pb > 0f ? Mathf.Min(1f, (avail - flA * pa) / pb) : (avail >= flA * pa ? 1f : -1f);
            }
            else if (fb < flB)
            {
                fb = flB;
                fa = pa > 0f ? Mathf.Min(1f, (avail - flB * pb) / pa) : (avail >= flB * pb ? 1f : -1f);
            }
            return fa >= flA && fb >= flB;
        }
    }
}
