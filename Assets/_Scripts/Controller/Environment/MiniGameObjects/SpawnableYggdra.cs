using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Yggdra" - the World-Tree cell environment (~35k prisms): Atlantis's tree, distilled
    /// and grown grander for the freestyle rotation. A braided fourteen-strand trunk rises
    /// from a dune bowl to a fibonacci canopy dome; root buttresses flare into the floor;
    /// golden-angle branches droop, split twice, hang air-root vines, and end in phyllotaxis
    /// blossom heads; kelp veils sway up from the bowl; firefly currents orbit the crown.
    /// Landmarks: a super-shielded heartwood ring, shielded fruit, sparse danger thorns on
    /// the low branches. The companion road-city is <see cref="SpawnableDaedala"/>.
    ///
    /// FLUSH: no prism interpenetrates another (Docs/ECOSYSTEM.md §49.10). Every family lays
    /// exactly the prisms it always laid - same count, positions, rotations, domains, kinds and
    /// random draws - and then shrinks, on one axis and only until their faces touch, the pairs
    /// it knows are neighbours (FLUSH FIT, end of file). The sizes stated below are therefore
    /// AUTHORED maxima: a trunk plank stated 7.8 long lands as long as its ~2.2 step along the
    /// strand. Only a pair whose centres sit closer than the prism's 0.5 floor allows (two braid
    /// strands crossing, a firefly current the curl pins against the floor reflecting back over
    /// one spot) is moved, by the least that makes it separable - under 1% of the lays.
    /// </summary>
    public class SpawnableYggdra : CellEnvironmentSpawnableBase
    {
        const float Floor = -190f;
        const float Crown = 265f;
        const float OuterR = 380f;

        protected override int DefaultSeed => 11;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableYggdra), 3);

        float BowlY(float r) { float t = r / OuterR; return Floor + t * t * 90f; }

        protected override void BuildEnvironment()
        {
            _recCount = _cachedLays.Count;
            _meets = new List<(int a, int b)>(LayCapacity * 4);
            _groups.Clear();
            _branchLays.Clear();
            float baseY = Floor + 6f;
            float height = Crown - baseY;

            // Trunk: fourteen braided strands, ribbon planks long along the strand.
            const int strands = 14, steps = 220;
            var trunk = new int[strands, steps];
            for (int s = 0; s < 14; s++)
            {
                float phase = 2f * Mathf.PI * s / 14f;
                Domains dom = s % 3 != 0 ? Domains.Jade : Domains.Gold;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i < 220; i++)
                {
                    float t = i / 219f;
                    float y = baseY + height * t;
                    float r = 44f * Mathf.Pow(1f - t, 1.7f) + 12f + 30f * Mathf.Max(0f, t - 0.86f) * 7f;
                    // Phase-locked over/under weave (adjacent strands alternate) + a small
                    // centred wander keeps every strand in its own azimuthal lane - the braid
                    // signature. Strand 0 is the gold KING STRAND: a clean, wide, radially-thin
                    // helix ribbon (identical prism volume) - the premier roots-to-crown skim
                    // road, readable against the noisy Jade braid by its colour and calm.
                    float ang, radius;
                    Vector3 scale;
                    if (s == 0)
                    {
                        ang = t * 4.6f;
                        radius = r;
                        scale = new Vector3(9.6f, 0.6f, 7.8f);
                    }
                    else
                    {
                        ang = phase + t * 4.6f + 0.16f * (2f * N01(t * 5f, s * 3f, 0f, 0) - 1f);
                        float weave = 6f * Mathf.Sin(t * 9f * Mathf.PI + s * Mathf.PI);
                        float sway = 4.4f * N01(t * 3f, s * 7f, 1f, 1) - 2.2f;
                        radius = r + sway + weave;
                        scale = Jit(new Vector3(3.2f, 1.8f, 7.8f));
                    }
                    var p = new Vector3(radius * Mathf.Cos(ang), y, radius * Mathf.Sin(ang));
                    Vector3 radial = new Vector3(p.x, 0f, p.z).normalized;
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.up, radial)
                        : SpawnPoint.LookRotation(p - prev, radial);
                    Emit(p, rot, scale, dom);
                    trunk[s, i] = Rec();
                    prev = p;
                }
            }
            // Planks 7.8 long on a ~2.2 step: each touches the next planks along its own strand
            // (planks keep exactly their step), then gives way where the over/under weave carries
            // a neighbour strand across it at the same height. The phase lock keeps each strand
            // within ±0.16 rad of its lane (lanes 0.45 rad apart), so only the next two lanes
            // round can reach it - the king strand's 9.6 ribbon included.
            for (int s = 0; s < strands; s++)
            {
                var strand = new List<int>(steps);
                for (int i = 0; i < steps; i++)
                {
                    strand.Add(trunk[s, i]);
                    for (int j = i + 1; j <= i + 3 && j < steps; j++) Fit(trunk[s, i], trunk[s, j]);
                }
                Group(Fam.Trunk, strand);
            }
            for (int s = 0; s < strands; s++)
                for (int ds = 1; ds <= 2; ds++)
                {
                    int s2 = (s + ds) % strands;
                    for (int i = 0; i < steps; i++)
                        for (int j = Mathf.Max(0, i - 3); j <= i + 3 && j < steps; j++)
                            Fit(trunk[s, i], trunk[s2, j]);
                }

            // Heartwood ring - the permanent bones of the world.
            float heartY = baseY + height * 0.4f;
            var heart = new int[48];
            for (int i = 0; i < 48; i++)
            {
                float a = 2f * Mathf.PI * i / 48f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                // Girdles the trunk just OUTSIDE the strand envelope (long axis chained around
                // the circumference) so the permanent landmark reads as a continuous gold belt,
                // not dots buried in the braid. 8.192 long on a 5.24 chord: each block is fitted
                // to the next round the belt, so the belt closes flush.
                Emit(new Vector3(radial.x * 40f, heartY, radial.z * 40f),
                    SpawnPoint.LookRotation(tangent, radial),
                    new Vector3(2f, 2f, 8.192f), Domains.Gold, PrismKind.SuperShielded);
                heart[i] = Rec();
            }
            for (int i = 0; i < 48; i++)
            {
                Fit(heart[i], heart[(i + 1) % 48]);
                Fit(heart[i], heart[(i + 2) % 48]);
            }
            Group(Fam.Heartwood, new List<int>(heart));

            // Root buttresses.
            const int buttresses = 10, buttSegs = 58;
            var butt = new int[buttresses, buttSegs, 3];
            var buttLen = new float[buttresses];
            for (int b = 0; b < 10; b++)
            {
                float a0 = 2f * Mathf.PI * b / 10f;
                float len = 100f + 60f * Hash01(b * 77 + _noiseSeed);
                buttLen[b] = len;
                Vector3 prevMid = Vector3.zero;
                for (int i = 0; i < 58; i++)
                {
                    float t = i / 57f;
                    float rr = 27f + t * len;
                    float y = BowlY(rr) + 30f * (1f - t) * (1f - t) + 3f;
                    float a = a0 + 0.55f * Mathf.Sin(t * 2.6f + b);
                    var mid = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), Vector3.up)
                        : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                    for (int w = -1; w <= 1; w++)
                    {
                        Emit(new Vector3(mid.x - w * 4.4f * Mathf.Sin(a), mid.y, mid.z + w * 4.4f * Mathf.Cos(a)),
                            rot, Jit(new Vector3(4.8f, 1.6f, 2.8f)), Domains.Jade);
                        butt[b, i, w + 1] = Rec();
                    }
                    prevMid = mid;
                }
            }
            // Three lanes 4.4 apart under planks 4.8 wide, 2.8 long on a ~2 step: each plank
            // touches its lane neighbours and the next planks down its own lane; ridges wander
            // (0.55 rad, against a 0.63 rad spacing) across the next ridges round, and at a
            // plank's radius each of those is at its own t = (r - 27) / len.
            for (int b = 0; b < buttresses; b++)
            {
                var ridge = new List<int>(buttSegs * 3);
                for (int i = 0; i < buttSegs; i++)
                    for (int w = 0; w < 3; w++)
                    {
                        int k = butt[b, i, w];
                        ridge.Add(k);
                        for (int j = i + 1; j <= i + 2 && j < buttSegs; j++) Fit(k, butt[b, j, w]);
                        for (int w2 = w + 1; w2 < 3; w2++)
                            for (int j = Mathf.Max(0, i - 2); j <= i + 2 && j < buttSegs; j++) Fit(k, butt[b, j, w2]);
                        if (k < 0) continue;
                        Vector3 q = _cachedLays[k].Point.Position;
                        float rq = Mathf.Sqrt(q.x * q.x + q.z * q.z);
                        for (int db = 1; db <= 2; db++)
                        {
                            int b2 = (b + db) % buttresses;
                            int j0 = Mathf.RoundToInt((rq - 27f) / buttLen[b2] * (buttSegs - 1));
                            for (int j = Mathf.Max(0, j0 - 3); j <= j0 + 3 && j < buttSegs; j++)
                                for (int w2 = 0; w2 < 3; w2++) Fit(k, butt[b2, j, w2]);
                        }
                    }
                Group(Fam.Buttress, ridge);
            }

            // Branches: golden-angle fan, two split generations, vines on every third.
            for (int b = 0; b < 34; b++)
            {
                float t0 = 0.40f + 0.57f * (b / 34f);
                float yb = baseY + height * t0;
                float ang = b * GoldenAngle;
                int from = _branchLays.Count;
                Branch(new Vector3(14f * Mathf.Cos(ang), yb, 14f * Mathf.Sin(ang)), ang,
                    165f * (1.18f - t0) + 48f, 58f * (1f - t0) + 18f, 2, b, null);
                Group(Fam.Branch, _branchLays.GetRange(from, _branchLays.Count - from));
            }

            // Canopy dome.
            int leaves = Scaled(9400);
            var leaf = new int[leaves];
            var leafAt = new Vector2[leaves];   // each leaf's (x, z), for the fit's window scan
            var fruit = new List<int>();
            for (int i = 0; i < leaves; i++)
            {
                leaf[i] = -1;
                float u = i / (float)leaves;
                float a = i * GoldenAngle;
                float rr = 180f * Mathf.Sqrt(u);
                float dome = 95f * Mathf.Cos(u * Mathf.PI * 0.5f);
                float wob = 18f * N01(Mathf.Cos(a) * 3f + 3f, u * 6f, Mathf.Sin(a) * 3f, 5);
                // Three vessel-sized partings spiral from the (solid) crown cap to the rim
                // so the canopy is a dappled roof with flyways, not a sealed shell.
                float lane = Mathf.Repeat(a + 0.8f * Mathf.Sqrt(u), 2f * Mathf.PI / 3f);
                if (rr > 45f && lane * rr < 13f) continue;
                var p = new Vector3(rr * Mathf.Cos(a), Crown - 50f + dome * 0.62f + wob - 32f * u, rr * Mathf.Sin(a));
                var normal = (Vector3.up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.55f * u)).normalized;
                var azimuthal = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Emit(p, SpawnPoint.LookRotation(normal, azimuthal),
                    Jit(new Vector3(6.4f, 4.2f, 0.7f)), i % 4 != 0 ? Domains.Jade : Domains.Gold);
                leaf[i] = Rec();
                leafAt[i] = new Vector2(p.x, p.z);
                if (i % 150 == 75)
                {
                    Emit(new Vector3(p.x, p.y - 6f, p.z), Quaternion.identity,
                        new Vector3(2.6f, 2.6f, 2.6f), Domains.Gold, PrismKind.Shielded);
                    fruit.Add(Rec());
                }
            }
            // Plates ~27 square units on ~11 of dome each: every leaf meets the leaves within a
            // plate's reach of it - leaf i sits at radius 180 sqrt(i / leaves), so those are one
            // index window, at its angle - and the footprint gives way in-plane, never the thin
            // axis the dome is read by. The fruit hang just under the dome.
            for (int i = 0; i < leaves; i++) FitCanopy(leaf, leafAt, leaf[i], 9.2f, i + 1);
            foreach (int k in fruit) FitCanopy(leaf, leafAt, k, 9f, 0);
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
            Group(Fam.Canopy, fruit);

            // Kelp veils.
            for (int s = 0; s < 64; s++)
            {
                float a = s * GoldenAngle;
                float rr = 95f + (OuterR - 120f) * Mathf.Sqrt(Hash01(s * 911 + _noiseSeed));
                var basePos = new Vector3(rr * Mathf.Cos(a), BowlY(rr), rr * Mathf.Sin(a));
                float h = 160f + 140f * Hash01(s * 13 + 1);
                int segs = (int)(h / 3.4f);
                Vector3 p = basePos, prev = basePos;
                var stem = new int[segs];
                var blades = new List<(int i, int lay)>();
                for (int i = 0; i < segs; i++)
                {
                    float t = i * 3.4f / h;
                    var drift = Curl(p, 0.007f, 9);
                    p = new Vector3(p.x + drift.x * 2.2f, basePos.y + h * t, p.z + drift.z * 2.2f);
                    var up = i == 0 ? Vector3.up : (p - prev).normalized;
                    Emit(p, SpawnPoint.LookRotation(up, Vector3.right),
                        Jit(new Vector3(1.4f, 0.7f, 3.2f), 0.15f),
                        (i / 7) % 2 == 0 ? Domains.Jade : Domains.Gold);
                    stem[i] = Rec();
                    if (i % 3 == 1)
                    {
                        float side = (i / 3) % 2 == 0 ? 1f : -1f;
                        var lateral = Vector3.Cross(up, Vector3.up).sqrMagnitude > 0.01f
                            ? Vector3.Cross(up, Vector3.up).normalized : Vector3.right;
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

            // Dune floor.
            var dunes = new int[31][];   // [row][i], -1 = a breathing gap
            for (int row = 0; row < 31; row++)
            {
                float R = 70f + (OuterR - 85f) * row / 31f;
                int n = (int)(2f * Mathf.PI * R / 9.8f);
                Domains dom = (row % 3) switch { 0 => Domains.Jade, 1 => Domains.Blue, _ => Domains.Gold };
                var dune = dunes[row] = new int[n];
                for (int i = 0; i < n; i++)
                {
                    dune[i] = -1;
                    float a = 2f * Mathf.PI * i / n;
                    float wob = 16f * N01(Mathf.Cos(a) * 3f + row * 0.31f, row * 0.5f, Mathf.Sin(a) * 3f, 13);
                    if (N01(a * 2.3f, row * 0.8f, 4.5f, 14) < 0.36f) continue;
                    float rr = R + wob;
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Emit(new Vector3(rr * Mathf.Cos(a), BowlY(rr) - 2f, rr * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, tangent),
                        Jit(new Vector3(3.4f, 5.4f, 0.9f), 0.3f), dom);
                    dune[i] = Rec();
                }
                for (int i0 = 0; i0 < n; i0 += 16)
                    Group(Fam.Dune, new List<int>(new System.ArraySegment<int>(dune, i0, Mathf.Min(16, n - i0))));
            }
            // Ripples 5.4 long on a 9.8 step clear round a row; the 0-16 wobble can carry a
            // ripple onto the next rows out (rows ~10 apart).
            for (int row = 0; row < dunes.Length; row++)
            {
                int n = dunes[row].Length;
                for (int i = 0; i < n; i++)
                {
                    Fit(dunes[row][i], dunes[row][(i + 1) % n]);
                    for (int r2 = row + 1; r2 <= row + 2 && r2 < dunes.Length; r2++)
                    {
                        int n2 = dunes[r2].Length;
                        int j0 = Mathf.RoundToInt(i * (float)n2 / n);
                        for (int j = j0 - 2; j <= j0 + 2; j++) Fit(dunes[row][i], dunes[r2][((j % n2) + n2) % n2]);
                    }
                }
            }

            // Firefly currents orbiting the tree.
            int lines = Scaled(14);
            for (int s = 0; s < lines; s++)
            {
                float a = s * GoldenAngle * 3.7f;
                var p = new Vector3(300f * Mathf.Cos(a), 40f + 120f * Hash01(s * 29 + _noiseSeed) - 60f, 300f * Mathf.Sin(a));
                var line = new List<int>(190);
                for (int i = 0; i < 190; i++)
                {
                    var v = Curl(p, 0.006f, 12);
                    var toCentre = new Vector3(-p.x, 0f, -p.z).normalized;
                    var orbit = new Vector3(-p.z, 0f, p.x).normalized;
                    var d = (v * 0.8f + toCentre * 0.28f + orbit * 0.7f).normalized;
                    if ((p.y + d.y * 5f < Floor + 12f && d.y < 0f) || (p.y + d.y * 5f > Crown + 30f && d.y > 0f))
                        d = new Vector3(d.x, -d.y, d.z);
                    p += d * 5f;
                    if (Mathf.Sqrt(p.x * p.x + p.z * p.z) > OuterR * 1.15f) break;
                    Emit(p, SpawnPoint.LookRotation(d, Vector3.up),
                        new Vector3(1.7f, 0.8f, 4f), s % 3 != 0 ? Domains.Gold : Domains.Blue);
                    line.Add(Rec());
                }
                // Dashes 4 on a 5 step clear along the line except where it turns hard.
                for (int i = 0; i < line.Count; i++)
                    for (int j = i + 1; j <= i + 2 && j < line.Count; j++) Fit(line[i], line[j]);
                // A current spirals inward and can cross itself (and its neighbours - the
                // cross-family pass names those); where the curl pins it against the floor or
                // ceiling it reflects back and forth over almost the same spot, and the dashes it
                // piles there part along the line - so a line names its own later turns from
                // further off.
                FitGroups(line, line, 3, 6f);
                Group(Fam.Firefly, line);
            }

            CrossFamilies();
            FlushAll();

            // The fit's bookkeeping is generation scratch: drop it with the build.
            _meets = null;
            _groups.Clear(); _branchLays.Clear();
        }

        /// <summary>Every lay a <see cref="Branch"/> call made (segments, thorns, petals,
        /// vines), in lay order - each top-level limb's run of it is one group of the tree.</summary>
        readonly List<int> _branchLays = new();

        /// <summary>Shrink the lay at <paramref name="k"/> against the canopy leaves (from index
        /// <paramref name="from"/> on) within <paramref name="reach"/> of it: leaf i sits at radius
        /// 180 sqrt(i / leaves), so the candidates are one index window, filtered by distance.</summary>
        void FitCanopy(int[] leaf, Vector2[] leafAt, int k, float reach, int from)
        {
            int leaves = leaf.Length;
            if (k < 0) return;
            Vector3 q = _cachedLays[k].Point.Position;
            float r0 = Mathf.Sqrt(q.x * q.x + q.z * q.z);
            if (r0 > 180f + reach || q.y < Crown - 50f - 32f - reach) return;
            float lo = Mathf.Max(0f, r0 - reach), hi = r0 + reach;
            int i0 = Mathf.Max(from, Mathf.FloorToInt(leaves * (lo / 180f) * (lo / 180f)));
            int i1 = Mathf.Min(leaves - 1, Mathf.CeilToInt(leaves * (hi / 180f) * (hi / 180f)));
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
                p = new Vector3(p.x + 7f * N01(t * 4f, branchSeed * 3f, level * 9f, 2) - 3.5f, p.y - droop * t * t, p.z);
                Emit(p, SpawnPoint.LookRotation(p - prev, Vector3.up), Jit(new Vector3(
                    2.6f * (1f - 0.3f * t) + 0.8f, 1.5f, 4.6f * (1f - 0.4f * t) + 1.2f)), Domains.Jade);
                seg[i] = Rec();
                if (level == 2 && i % 8 == 4 && t > 0.3f && t < 0.85f)
                {
                    Emit(new Vector3(p.x, p.y - 3.4f, p.z), Quaternion.identity,
                        new Vector3(1.3f, 3f, 1.3f), Domains.Ruby, PrismKind.Danger);
                    thorns.Add((i, Rec()));
                }
                prev = p;
            }

            int petals = level == 2 ? 30 : 16;
            var petal = new int[petals];
            for (int k = 0; k < petals; k++)
            {
                float a = k * GoldenAngle;
                float rr = 3.1f * Mathf.Sqrt(k + 0.5f);
                Emit(p + new Vector3(rr * Mathf.Cos(a), 0.4f * rr, rr * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)), Vector3.up),
                    new Vector3(1.4f, 0.6f, 2f), Domains.Gold);
                petal[k] = Rec();
            }

            var roots = level > 0 ? new List<int>(12) : null;
            if (level > 0)
                for (int c = 0; c < 2; c++)
                    Branch(p, ang + (c * 2 - 1) * 0.85f + 0.22f * Mathf.Sin(branchSeed),
                        len * 0.56f, droop * 0.72f, level - 1, branchSeed * 5 + c + 1, roots);

            // Air-root vine hanging from every third branch's midpoint.
            int[] vine = null;
            if (level >= 1 && branchSeed % 3 == 1)
            {
                var mid = root + dir * (len * 0.6f);
                mid = new Vector3(mid.x, mid.y - droop * 0.36f, mid.z);
                int vl = (int)(14f + 10f * Hash01(branchSeed * 7));
                vine = new int[vl];
                for (int v = 0; v < vl; v++)
                {
                    Emit(new Vector3(mid.x + 1.5f * Mathf.Sin(v * 0.7f + branchSeed), mid.y - v * 2.6f,
                            mid.z + 1.5f * Mathf.Cos(v * 0.5f)),
                        Quaternion.identity, new Vector3(0.8f, 2.4f, 0.8f), Domains.Jade);
                    vine[v] = Rec();
                }
            }

            // Flush: planks along the limb, each thorn under its plank, the blossom's petals among
            // themselves and against the limb and the two child limbs springing from its tip, the
            // children against each other, and the vine down itself and up into its limb and the
            // thorns hanging beside it.
            for (int i = 1; i <= segs; i++)
                for (int j = i + 1; j <= i + 2 && j <= segs; j++) Fit(seg[i], seg[j]);
            foreach (var (i, lay) in thorns)
                for (int j = Mathf.Max(1, i - 2); j <= i + 2 && j <= segs; j++) Fit(lay, seg[j]);
            for (int k = 0; k < petals; k++)
            {
                for (int k2 = k + 1; k2 < petals; k2++) Fit(petal[k], petal[k2]);
                for (int j = 1; j <= segs; j++) Fit(petal[k], seg[j]);
                if (roots != null) foreach (int r in roots) Fit(petal[k], r);
            }
            if (roots != null)
                for (int m = 0; m < roots.Count; m++)
                {
                    for (int m2 = m + 1; m2 < roots.Count; m2++) Fit(roots[m], roots[m2]);
                    for (int j = Mathf.Max(1, segs - 3); j <= segs; j++) Fit(roots[m], seg[j]);
                }
            if (vine != null)
                for (int v = 0; v < vine.Length; v++)
                {
                    for (int v2 = v + 1; v2 <= v + 2 && v2 < vine.Length; v2++) Fit(vine[v], vine[v2]);
                    if (v < 3) for (int j = 1; j <= segs; j++) Fit(vine[v], seg[j]);
                    if (v < 4) foreach (var (_, lay) in thorns) Fit(vine[v], lay);
                }

            for (int i = 1; i <= segs; i++) _branchLays.Add(seg[i]);
            foreach (var (_, lay) in thorns) _branchLays.Add(lay);
            _branchLays.AddRange(petal);
            if (vine != null) _branchLays.AddRange(vine);
            if (parentRoots != null)
                for (int i = 1; i <= 6 && i <= segs; i++) parentRoots.Add(seg[i]);
        }

        // ── FLUSH FIT ────────────────────────────────────────────────────────
        //
        // NO PRISM MAY INTERPENETRATE ANOTHER (Docs/ECOSYSTEM.md §49.10), and the rule is held
        // HERE, in the generator: every family lays exactly the prisms it always laid (same
        // positions, rotations, domains, kinds, same random draws in the same order), records
        // the lay index of each, and names the pairs it KNOWS are neighbours from its own
        // construction - the next plank along a strand, the next lane of a ribbon, the leaves
        // within a plate's reach of a golden-angle disc. Where two families cross by design,
        // each names its organisms (a strand, a limb, an arc of ripples) and the pairs come from
        // the organisms whose bounds meet. Every named pair is then shrunk just enough that its
        // faces touch - the smallest shrink on one axis, so the silhouette, the sizes' variety
        // and the population all stand. The same fit SpawnableAtlantis (the garden this tree
        // was distilled from) runs, kept self-contained here. Measured in the clip-audit harness
        // (.NET, min of eight builds) it adds ~0.05 s to ~0.24 s of generation math - once per
        // build, since the lay list is cached - and no work at all once the tree is laid.

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

        /// <summary>The families, one per rule a part of the world is laid by.</summary>
        enum Fam { Trunk, Heartwood, Buttress, Branch, Canopy, Kelp, Dune, Firefly }

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
        /// The families cross each other by design - the limbs spring from inside the braid and
        /// reach into the dome, kelp rises through the limbs, the buttresses flare out of the
        /// trunk into the dunes, the currents orbit through everything - so every two groups of
        /// DIFFERENT families (and two organisms of a family that <see cref="CrossesItself"/>)
        /// whose bounds meet name the pairs of their 16-lay runs whose bounds meet.
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
        /// drifting kelp strands, two firefly currents.</summary>
        static bool CrossesItself(Fam f) => f is Fam.Branch or Fam.Kelp or Fam.Firefly;

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
