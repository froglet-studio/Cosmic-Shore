using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Geode" - the crystal-cathedral cell environment (~34k prisms) and the rotation's
    /// angular, serene pole: a colossal geode cracked open along a flyable 110-unit rift. Two
    /// rough husk hemispheres carry inward crystal linings; giant druse spike clusters grow
    /// toward the centre (their first spike tipped with a super-shielded stella - the "perfect
    /// crystals"); a floating heart-crystal cluster with an orbiting shard ring holds the
    /// middle; banded agate ribbons wrap the outside equator; crystal dust drifts through the
    /// cavity and straight light shafts cross it through the crack. Amethyst blue + garnet
    /// ruby + citrine gold. No danger anywhere - stillness is the identity.
    ///
    /// <para>Every family is laid FLUSH (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10), fitted here at authoring time from geometry the generator already has - nothing
    /// resolves clipping at load. Prism counts, domains, kinds and rotations are the authored
    /// ones; sizes shrink only as far as contact, and a prism moves only where no size could
    /// clear it (the coincident roots of a spray, a spike threaded through another).
    /// <c>python3 Tools/Build/audit_prism_clipping.py SpawnableGeode -v</c> is the gate.</para>
    /// </summary>
    public class SpawnableGeode : CellEnvironmentSpawnableBase
    {
        const float HuskR = 250f;
        const float CrackGap = 110f;

        protected override int DefaultSeed => 67;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableGeode), 4);

        static readonly Domains[] CrystalDoms = { Domains.Ruby, Domains.Blue, Domains.Gold };

        /// <summary>The inner crystal lining shard behind a husk plate.</summary>
        static readonly Vector3 LiningSize = new Vector3(1.4f, 1.4f, 4.6f);

        protected override void BuildEnvironment()
        {
            BuildHusks();
            BuildDruses();
            BuildHeart();
            BuildAgateBands();
            BuildDust();
            BuildLightShafts();
        }

        static Vector3 SpherePoint(int i, int n)
        {
            float u = (i + 0.5f) / n;
            float y = 1f - 2f * u;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        // ── Flush fitting (shared by every family below) ───────────────────────────────────

        /// <summary>Fitted sizes aim a hair under exact contact, so float noise never tips a pair
        /// that should TOUCH into one that clips.</summary>
        const float Flush = 0.995f;

        /// <summary>
        /// Half the box's extent along the unit direction <paramref name="n"/> (its support): a box
        /// sits wholly on its own side of a plane at distance h from its centre when this is at most h.
        /// </summary>
        static float Support(Quaternion rot, Vector3 size, Vector3 n) => 0.5f * (
            Mathf.Abs(Vector3.Dot(rot * Vector3.right, n)) * size.x +
            Mathf.Abs(Vector3.Dot(rot * Vector3.up, n)) * size.y +
            Mathf.Abs(Vector3.Dot(rot * Vector3.forward, n)) * size.z);

        /// <summary>
        /// The largest size along the box's local axis <paramref name="axis"/> (0 x, 1 y, 2 z), its
        /// other two sizes kept, at which it stays on its own side of the mid-plane to a neighbour
        /// centred at <paramref name="nbr"/>. Two neighbours fitted to the same mid-plane can only
        /// touch, so a chain fitted this way to both its neighbours is flush however it bends.
        /// +inf when the axis lies in the plane.
        /// </summary>
        static float FitAxis(in Box b, int axis, Vector3 nbr)
        {
            Vector3 d = nbr - b.C;
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 n = d / dist;
            float ax = Mathf.Abs(Vector3.Dot(b.X, n)), ay = Mathf.Abs(Vector3.Dot(b.Y, n)), az = Mathf.Abs(Vector3.Dot(b.Z, n));
            float rest = (axis == 0 ? 0f : ax * b.S.x) + (axis == 1 ? 0f : ay * b.S.y) + (axis == 2 ? 0f : az * b.S.z);
            float along = axis == 0 ? ax : axis == 1 ? ay : az;
            return along < 1e-5f ? float.PositiveInfinity : (dist * Flush - rest) / along;
        }

        /// <summary>A prism reduced to what a fit reads: centre, unit axes, full size.</summary>
        readonly struct Box
        {
            public readonly Vector3 C, X, Y, Z, S;
            public readonly float Radius;   // half the diagonal: nothing beyond it can touch

            public Box(Vector3 c, Quaternion r, Vector3 s)
            {
                C = c; S = s;
                X = r * Vector3.right; Y = r * Vector3.up; Z = r * Vector3.forward;
                Radius = 0.5f * s.magnitude;
            }

            public float Support(Vector3 n) => 0.5f * (
                Mathf.Abs(Vector3.Dot(X, n)) * S.x + Mathf.Abs(Vector3.Dot(Y, n)) * S.y + Mathf.Abs(Vector3.Dot(Z, n)) * S.z);

            /// <summary>Candidate separating axis k of this box and <paramref name="o"/>: the
            /// centre line, then each box's three face normals (an axis that separates the
            /// projections is proof the boxes do not meet; these seven find it for any pair this
            /// environment lays).</summary>
            public Vector3 Axis(int k, in Box o, Vector3 unitD) => k switch
            {
                0 => unitD, 1 => X, 2 => Y, 3 => Z, 4 => o.X, 5 => o.Y, _ => o.Z,
            };
        }

        /// <summary>
        /// The largest uniform factor (capped at 1) for BOTH boxes at which some candidate axis
        /// separates them. Symmetric in the pair, so each of two neighbours that takes the minimum
        /// over its pairs has room only to touch.
        /// </summary>
        static float PairScale(in Box a, in Box b)
        {
            Vector3 d = b.C - a.C;
            float dist = d.magnitude;
            if (dist >= (a.Radius + b.Radius) / Flush) return 1f;
            if (dist < 1e-5f) return 0f;
            float best = 0f;
            Vector3 u = d / dist;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = a.Axis(k, b, u);
                float sum = a.Support(L) + b.Support(L);
                if (sum < 1e-6f) continue;
                best = Mathf.Max(best, Mathf.Abs(Vector3.Dot(d, L)) * Flush / sum);
                if (best >= 1f) return 1f;
            }
            return best;
        }

        /// <summary>The largest uniform factor (capped at 1) for box <paramref name="b"/> at which
        /// some candidate axis separates it from the FIXED box <paramref name="a"/> - for a prism
        /// laid after <paramref name="a"/> that must yield all the room.</summary>
        static float ScaleAgainst(in Box b, in Box a)
        {
            Vector3 d = a.C - b.C;
            float dist = d.magnitude;
            if (dist >= (a.Radius + b.Radius) / Flush) return 1f;
            if (dist < 1e-5f) return 0f;
            float best = 0f;
            Vector3 u = d / dist;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = b.Axis(k, a, u);
                float sup = b.Support(L);
                if (sup < 1e-6f) continue;
                best = Mathf.Max(best, (Mathf.Abs(Vector3.Dot(d, L)) * Flush - a.Support(L)) / sup);
                if (best >= 1f) return 1f;
            }
            return best;
        }

        /// <summary>
        /// The largest size along local axis <paramref name="axis"/> of box <paramref name="b"/>
        /// (the other two sizes kept) at which some candidate axis still separates it from the
        /// FIXED box <paramref name="a"/>.
        /// </summary>
        static float FitAgainst(in Box b, int axis, in Box a)
        {
            Vector3 d = a.C - b.C;
            float dist = d.magnitude;
            if (dist >= (a.Radius + b.Radius) / Flush) return b.S[axis];
            if (dist < 1e-5f) return 0f;
            Vector3 dir = axis == 0 ? b.X : axis == 1 ? b.Y : b.Z;
            Vector3 rest = b.S;
            rest[axis] = 0f;
            float best = 0f;
            Vector3 u = d / dist;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = b.Axis(k, a, u);
                float restSup = 0.5f * (Mathf.Abs(Vector3.Dot(b.X, L)) * rest.x + Mathf.Abs(Vector3.Dot(b.Y, L)) * rest.y +
                                        Mathf.Abs(Vector3.Dot(b.Z, L)) * rest.z);
                float room = Mathf.Abs(Vector3.Dot(d, L)) * Flush - a.Support(L) - restSup;
                if (room < 0f) continue;
                float along = Mathf.Abs(Vector3.Dot(dir, L));
                if (along < 1e-5f) return float.PositiveInfinity;
                best = Mathf.Max(best, 2f * room / along);
            }
            return best;
        }

        // ── Husks ──────────────────────────────────────────────────────────────────────────

        /// <summary>The index offsets at which a husk plate's nearest neighbours sit on the
        /// 9400-point Fibonacci sphere (consecutive Fibonacci numbers, as on any golden-angle
        /// lattice). Every plate fits itself to these, both ways, so each pair is fitted from both
        /// sides with the same numbers.</summary>
        static readonly int[] HuskNeighbourOffsets = { 21, 34, 55, 89, 144 };

        void BuildHusks()
        {
            const int n = 9400;
            var centres = new Vector3[n];
            var liningCentres = new Vector3[n];
            var sizes = new Vector3[n];
            var rots = new Quaternion[n];
            var liningRots = new Quaternion[n];
            var kept = new bool[n];
            var lined = new bool[n];
            var fit = new float[n];
            float liningRadius = 0.5f * LiningSize.magnitude;
            for (int half = 0; half < 2; half++)
            {
                float sign = half == 0 ? 1f : -1f;
                // Every plate and lining of this half first - the same one noise read and the same
                // one jitter draw per plate, in the same order, as the lay itself made them - so a
                // plate can see where its lattice neighbours sit and how big they are.
                for (int i = 0; i < n; i++)
                {
                    var d = SpherePoint(i, n);
                    kept[i] = sign * d.x >= 0.09f; // this hemisphere only, with crack margin
                    if (!kept[i]) continue;
                    float wob = 1f + 0.10f * N01(d.x * 3f + half * 9f, d.y * 3f, d.z * 3f, 0);
                    float az = i * GoldenAngle;
                    var up = new Vector3(-Mathf.Sin(az), 0f, Mathf.Cos(az));
                    rots[i] = SpawnPoint.LookRotation(d, up);
                    centres[i] = new Vector3(d.x * HuskR * wob + sign * CrackGap * 0.5f, d.y * HuskR * wob, d.z * HuskR * wob);
                    sizes[i] = Jit(new Vector3(5.6f, 5.2f, 1.3f), 0.3f);
                    fit[i] = 1f;
                    // Inner crystal lining: an inward-pointing shard behind most husk plates.
                    lined[i] = Hash01(i * 13 + half * 7 + _noiseSeed) < 0.78f;
                    if (!lined[i]) continue;
                    float rl = HuskR * wob - 9f;
                    liningRots[i] = SpawnPoint.LookRotation(-d, up);
                    liningCentres[i] = new Vector3(d.x * rl + sign * CrackGap * 0.5f, d.y * rl, d.z * rl);
                }

                // The +-30% jitter overlaps two LARGE neighbours across the lattice diagonal or a
                // wobble step. A plate keeps its jittered shape and shrinks only as far as contact
                // with each lattice neighbour (the pair's shared factor, found once per pair) and
                // with the lining shard behind it (the lining never resizes) - 95 of the ~8,500
                // plates shrink at all. Pairs too far apart to touch at any jitter
                // are passed over on their distance alone.
                for (int i = 0; i < n; i++)
                {
                    if (!kept[i]) continue;
                    float ri = 0.5f * sizes[i].magnitude;
                    foreach (int off in HuskNeighbourOffsets)
                    {
                        int j = i + off;
                        if (j >= n || !kept[j]) continue;
                        float rj = 0.5f * sizes[j].magnitude;
                        if ((centres[j] - centres[i]).sqrMagnitude < (ri + rj) * (ri + rj) / (Flush * Flush))
                        {
                            float k = PairScale(new Box(centres[i], rots[i], sizes[i]), new Box(centres[j], rots[j], sizes[j]));
                            fit[i] = Mathf.Min(fit[i], k);
                            fit[j] = Mathf.Min(fit[j], k);
                        }
                        if (lined[j] && (liningCentres[j] - centres[i]).sqrMagnitude < (ri + liningRadius) * (ri + liningRadius) / (Flush * Flush))
                            fit[i] = Mathf.Min(fit[i], ScaleAgainst(new Box(centres[i], rots[i], sizes[i]), new Box(liningCentres[j], liningRots[j], LiningSize)));
                        if (lined[i] && (liningCentres[i] - centres[j]).sqrMagnitude < (rj + liningRadius) * (rj + liningRadius) / (Flush * Flush))
                            fit[j] = Mathf.Min(fit[j], ScaleAgainst(new Box(centres[j], rots[j], sizes[j]), new Box(liningCentres[i], liningRots[i], LiningSize)));
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    if (!kept[i]) continue;
                    Emit(centres[i], rots[i], sizes[i] * fit[i], Domains.Blue);
                    if (lined[i]) Emit(liningCentres[i], liningRots[i], LiningSize, CrystalDoms[i % 3]);
                }
            }
        }

        // ── Sprays (druses and the heart) ──────────────────────────────────────────────────

        /// <summary>Spacing of the prisms along a druse spike; each is exactly this long (times
        /// <see cref="Flush"/>), end to end, so a spike is one flush column.</summary>
        const float DruseStep = 2.2f;

        /// <summary>
        /// The width growth rate a spike of a SPRAY (several spikes rooted at one point) may take:
        /// its prism at distance s along it stays on its own side of the bisector plane to every
        /// sibling - the plane through the root that mirrors one spike onto the other, so it is the
        /// same plane for every prism of both - when its width is at most
        /// <c>rate * (s * Flush - length / 2)</c>. The narrowest-angled sibling sets the rate.
        /// </summary>
        static float SprayWidthRate(Vector3[] dirs, int count, int self, Quaternion rot)
        {
            Vector3 x = rot * Vector3.right, y = rot * Vector3.up;
            float rate = float.PositiveInfinity;
            for (int q = 0; q < count; q++)
            {
                if (q == self) continue;
                Vector3 m = dirs[q] - dirs[self];
                float twoSinHalf = m.magnitude;          // |v - u| = 2 sin(angle / 2)
                if (twoSinHalf < 1e-5f) return 0f;
                m /= twoSinHalf;
                rate = Mathf.Min(rate, twoSinHalf / (Mathf.Abs(Vector3.Dot(x, m)) + Mathf.Abs(Vector3.Dot(y, m))));
            }
            return rate;
        }

        /// <summary>
        /// How far along a spray spike its first prism must start so that prism can be at least the
        /// prism scale animator's 0.5 floor wide and still clear every sibling (see
        /// <see cref="SprayWidthRate"/>). At the shared root every spike's first prism sat on the
        /// SAME point - coincident centres no size can fix - so each spike slides out along its own
        /// axis by this much, its smallest clear start.
        /// </summary>
        static float SprayStart(float rate, float length) => (0.5f * length + 0.5f / rate) / Flush;

        /// <summary>The width a spray prism at distance <paramref name="s"/> along its spike can
        /// take: the authored taper, narrowed where the siblings are still close.</summary>
        static float SprayWidth(float authored, float rate, float s, float length) =>
            Mathf.Min(authored, rate * (s * Flush - 0.5f * length));

        /// <summary>One druse spike: root, direction, prism count, and the spray fit.</summary>
        struct Spike
        {
            public Vector3 Root, Dir;
            public int Len;
            public float Rate, Start;
            public float Reach => Start + Len * DruseStep + 1f;   // past the stella's far face
            public Vector3 Mid => Root + Dir * (0.5f * (Start + Reach));
            public float Bound => 0.5f * (Reach - Start) + 2f;    // a sphere holding every prism
            /// <summary>The size of the prism at distance s along the spike (the stella past the
            /// last prism).</summary>
            public Vector3 SizeAt(float s)
            {
                if (s > Start + (Len - 0.5f) * DruseStep) return new Vector3(2f, 2f, 2f);
                float w = SprayWidth(2.6f * (1f - Mathf.Clamp01((s - Start) / (Len * DruseStep)) * 0.75f), Rate, s, DruseStep * Flush);
                return new Vector3(w, w, DruseStep * Flush);
            }
        }

        /// <summary>Closest points of segments p0 + u*[0, la] and q0 + v*[0, lb] (unit u, v), as
        /// distances along each.</summary>
        static void ClosestOnSegments(Vector3 p0, Vector3 u, float la, Vector3 q0, Vector3 v, float lb,
            out float sa, out float sb)
        {
            Vector3 w = p0 - q0;
            float b = Vector3.Dot(u, v), dd = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
            float den = 1f - b * b;
            sa = den > 1e-6f ? Mathf.Clamp((b * e - dd) / den, 0f, la) : 0f;
            sb = Mathf.Clamp(b * sa + e, 0f, lb);
            sa = Mathf.Clamp(b * sb - dd, 0f, la);
        }

        /// <summary>The druse spikes as built (and their per-cluster counts), kept for the dust
        /// that drifts between them to fit against.</summary>
        readonly Spike[] _druseSpikes = new Spike[24 * 10];
        readonly int[] _druseCounts = new int[24];
        readonly Vector3[] _druseCentres = new Vector3[24];   // a sphere holding each whole druse
        readonly float[] _druseRadii = new float[24];

        /// <summary>Prism <paramref name="i"/> of a druse spike (its stella at i = Len), exactly
        /// as BuildDruses lays it.</summary>
        static void DrusePrism(in Spike s, int i, out Vector3 pos, out Quaternion rot, out Vector3 size)
        {
            rot = SpawnPoint.LookRotation(s.Dir, Vector3.up);
            if (i >= s.Len)
            {
                float sTip = s.Start + s.Len * DruseStep;
                float st = Mathf.Min(2f, SprayWidth(2f, s.Rate, sTip, 2f));
                pos = s.Root + s.Dir * sTip;
                size = new Vector3(st, st, st);
                return;
            }
            float t = i / (float)s.Len;
            float at = s.Start + t * s.Len * DruseStep;
            float w = SprayWidth(2.6f * (1f - t * 0.75f), s.Rate, at, DruseStep * Flush);
            pos = s.Root + s.Dir * at;
            size = new Vector3(w, w, DruseStep * Flush);
        }

        void BuildDruses()
        {
            // Every spike first (no draws here - all of it is Hash01 and the golden angle), so a
            // cluster can be fitted against the clusters already grown.
            var spikes = _druseSpikes;
            var counts = _druseCounts;
            var dirs = new Vector3[10];
            // Each near earlier spike's lever (the offset a unit lean moves the gap by), its gap
            // and its contact distance.
            var gaps = new (Vector3 lever, float dist, float need)[16];
            int total = 0;
            for (int k = 0; k < 24; k++)
            {
                float u = Hash01(k * 77 + _noiseSeed);
                float a = k * GoldenAngle * 2.3f;
                float y = (u * 2f - 1f) * 0.85f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var d = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                float sign = d.x >= 0f ? 1f : -1f;
                var basePos = new Vector3(d.x * HuskR * 0.96f + sign * CrackGap * 0.5f, d.y * HuskR * 0.96f, d.z * HuskR * 0.96f);
                var inward = (-basePos).normalized;
                int count = counts[k] = 6 + (int)(5f * Hash01(k * 13));
                for (int sp = 0; sp < count; sp++)
                {
                    float sa = sp * GoldenAngle;
                    dirs[sp] = (inward + new Vector3(Mathf.Cos(sa), Mathf.Sin(sa), Mathf.Cos(sa + 2f)) * 0.35f).normalized;
                }

                // A spike of this cluster that THREADS a spike of an earlier one (their axes pass
                // closer than the two prisms' half-widths) clips whatever its size - the axes
                // nearly meet - so it leans about its own root by the smallest angle that opens
                // every such gap to contact, passing each earlier spike on whichever side is
                // nearer (one threaded between two siblings of another druse swings round the
                // nearer one). Seven of the 188 spikes lean, by 0.3 to 1.4 degrees.
                for (int pass = 0; pass < 6; pass++)
                {
                    bool leaned = false;
                    for (int sp = 0; sp < count; sp++)
                    {
                        ref var s = ref spikes[total + sp];
                        s.Root = basePos;
                        s.Dir = dirs[sp];
                        s.Len = (int)(34f + 30f * Hash01(k * 7 + sp));
                        s.Rate = SprayWidthRate(dirs, count, sp, SpawnPoint.LookRotation(dirs[sp], Vector3.up));
                        s.Start = SprayStart(s.Rate, DruseStep * Flush);
                    }
                    for (int sp = 0; sp < count; sp++)
                    {
                        var b = spikes[total + sp];
                        int nc = 0;
                        bool threaded = false;
                        for (int o = 0; o < total && nc < gaps.Length; o++)
                        {
                            var a0 = spikes[o];
                            float reachSum = a0.Bound + b.Bound + 4f;
                            if ((a0.Mid - b.Mid).sqrMagnitude > reachSum * reachSum) continue;
                            ClosestOnSegments(a0.Root, a0.Dir, a0.Reach, b.Root, b.Dir, b.Reach, out float sa, out float sb);
                            if (sb < b.Start - 1f || sa < a0.Start - 1f) continue;
                            Vector3 m = b.Root + b.Dir * sb - (a0.Root + a0.Dir * sa);
                            float dist = m.magnitude;
                            m = dist > 1e-4f ? m / dist : Vector3.Cross(a0.Dir, b.Dir).normalized;
                            float need = (Support(SpawnPoint.LookRotation(a0.Dir, Vector3.up), a0.SizeAt(sa), m) +
                                          Support(SpawnPoint.LookRotation(b.Dir, Vector3.up), b.SizeAt(sb), m)) / Flush + 0.05f;
                            if (dist >= need + 4f) continue;           // too far to matter for any lean tried below
                            gaps[nc++] = (m * Mathf.Max(sb, 1f), dist, need);
                            threaded |= dist < need;
                        }
                        if (!threaded) continue;
                        // Smallest lean (a direction offset, to first order) that leaves every near
                        // spike at contact distance or more, trying each gap's either side.
                        Vector3 bestLean = Vector3.zero;
                        float bestLen = float.PositiveInfinity;
                        for (int c = 0; c < 2 * nc; c++)
                        {
                            var (lever, dist0, need0) = gaps[c >> 1];
                            float target = (c & 1) == 0 ? need0 : -need0;
                            Vector3 lean = lever * ((target - dist0) / lever.sqrMagnitude);
                            if (lean.sqrMagnitude >= bestLen) continue;
                            bool clear = true;
                            for (int g = 0; g < nc && clear; g++)
                                clear = Mathf.Abs(gaps[g].dist + Vector3.Dot(gaps[g].lever, lean)) >= gaps[g].need - 1e-3f;
                            if (clear) { bestLean = lean; bestLen = lean.sqrMagnitude; }
                        }
                        if (float.IsPositiveInfinity(bestLen)) continue;
                        dirs[sp] = (b.Dir + bestLean).normalized;
                        leaned = true;
                    }
                    if (!leaned) break;
                }
                Vector3 mid = Vector3.zero;
                for (int sp = 0; sp < count; sp++) mid += spikes[total + sp].Mid / count;
                float rad = 0f;
                for (int sp = 0; sp < count; sp++) rad = Mathf.Max(rad, (spikes[total + sp].Mid - mid).magnitude + spikes[total + sp].Bound);
                _druseCentres[k] = mid;
                _druseRadii[k] = rad;
                total += count;
            }

            for (int k = 0, first = 0; k < 24; first += counts[k], k++)
                for (int sp = 0; sp < counts[k]; sp++)
                {
                    // Flush spray: the prisms butt end to end along the spike (2.2 long at the 2.2
                    // step - they were 3.4, overlapping by half), the spike starts at its smallest
                    // clear distance from the shared root, and near the root, where the siblings
                    // are still close, its prisms are narrowed to the bisector planes - a druse
                    // fanning out of one point like real crystal growth (DrusePrism).
                    for (int i = 0; i < spikes[first + sp].Len; i++)
                    {
                        DrusePrism(spikes[first + sp], i, out var pos, out var rot, out var size);
                        Emit(pos, rot, size, CrystalDoms[(k + sp) % 3]);
                    }
                    // ONLY the first spike of each cluster is the "perfect crystal" stella -
                    // 24 landmarks total; shielding every tip buried them (and doubled the
                    // always-on mesh-collider ration).
                    DrusePrism(spikes[first + sp], spikes[first + sp].Len, out var tipPos, out var tipRot, out var tipSize);
                    Emit(tipPos, tipRot, tipSize, CrystalDoms[(k + sp) % 3], sp == 0 ? PrismKind.SuperShielded : PrismKind.Plain);
                }
        }

        /// <summary>The heart's twelve spikes as built (direction, spray fit), kept for the light
        /// shafts that cross the heart's airspace to fit against.</summary>
        readonly Vector3[] _heartDirs = new Vector3[12];
        readonly float[] _heartRates = new float[12], _heartStarts = new float[12];

        const float HeartStep = 38f / 20f;

        /// <summary>Heart spike prism <paramref name="i"/> (0-19) of spike <paramref name="sp"/>,
        /// or its tip stella at <paramref name="i"/> = 20, exactly as BuildHeart lays it.</summary>
        void HeartPrism(int sp, int i, out Vector3 pos, out Quaternion rot, out Vector3 size)
        {
            var dir = _heartDirs[sp];
            rot = SpawnPoint.LookRotation(dir, Vector3.up);
            float rate = _heartRates[sp], s0 = _heartStarts[sp];
            float sTip = s0 + 38f;
            float st = Mathf.Min(2.4f, SprayWidth(2.4f, rate, sTip, 2.4f));
            if (i >= 20)
            {
                pos = dir * sTip;
                size = new Vector3(st, st, st);
                return;
            }
            float t = i / 20f;
            float s = s0 + t * 38f;
            float w = SprayWidth(3.2f * (1f - t * 0.6f), rate, s, HeartStep * Flush);
            // The last prism stops at the tip stella's face (the 2.4 stella overlapped it).
            float L = Mathf.Min(HeartStep * Flush, 2f * Flush * (sTip - 0.5f * st - s));
            pos = dir * s;
            size = new Vector3(w, w, L);
        }

        void BuildHeart()
        {
            // The heart cluster is a spray from the origin, built flush the way a druse is (see
            // BuildDruses): 1.9-long prisms at the 1.9 step (they were 4), each spike starting at
            // its smallest clear distance from the shared centre, narrowed near it.
            for (int sp = 0; sp < 12; sp++)
            {
                float sa = sp * GoldenAngle;
                _heartDirs[sp] = new Vector3(Mathf.Cos(sa), Mathf.Sin(sa * 1.7f), Mathf.Sin(sa)).normalized;
            }
            for (int sp = 0; sp < 12; sp++)
            {
                _heartRates[sp] = SprayWidthRate(_heartDirs, 12, sp, SpawnPoint.LookRotation(_heartDirs[sp], Vector3.up));
                _heartStarts[sp] = SprayStart(_heartRates[sp], HeartStep * Flush);
            }
            for (int sp = 0; sp < 12; sp++)
            {
                for (int i = 0; i < 20; i++)
                {
                    HeartPrism(sp, i, out var pos, out var rot, out var size);
                    Emit(pos, rot, size, sp % 3 != 0 ? Domains.Gold : Domains.Ruby);
                }
                HeartPrism(sp, 20, out var tipPos, out var tipRot, out var tipSize);
                Emit(tipPos, tipRot, tipSize, Domains.Gold, PrismKind.Shielded);
            }
            for (int i = 0; i < 90; i++)
            {
                float a = 2f * Mathf.PI * i / 90f;
                // Chain along the true 3D derivative (the wave's vertical pitch reaches ~43
                // degrees) so the orbit reads as one continuous undulating strand.
                var deriv = new Vector3(-58f * Mathf.Sin(a), 54f * Mathf.Cos(a * 3f), 58f * Mathf.Cos(a));
                Emit(new Vector3(58f * Mathf.Cos(a), 18f * Mathf.Sin(a * 3f), 58f * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(deriv, Vector3.up), new Vector3(1.3f, 1f, 3.4f), Domains.Blue);
            }
        }

        // ── Agate bands, dust, light shafts ────────────────────────────────────────────────

        void BuildAgateBands()
        {
            var bandDoms = new[] { Domains.Blue, Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Gold, Domains.Ruby, Domains.Blue };
            for (int band = 0; band < 7; band++)
            {
                float R = HuskR * 1.06f + band * 7f;
                float yb = (band - 2) * 26f;
                int n = (int)(2f * Mathf.PI * R / 2.5f);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float x = R * Mathf.Cos(a);
                    if (Mathf.Abs(x) < CrackGap * 0.42f) continue; // bands break at the crack
                    // Full husk-half offset + 15 extra radius clears the wobbled shell at the
                    // x-poles; thin axis = radial normal with the long axis chained along the
                    // band (shingle idiom) so the ribbon wraps continuously - as long as the room
                    // to each neighbour's mid-plane (~2.6 at the 2.5 step on radius R + 15; the
                    // authored 3.6 overlapped its neighbours by a third).
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var rot = SpawnPoint.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), tangent);
                    var p = BandPoint(R, yb, band, i, n);
                    var box = new Box(p, rot, new Vector3(2.2f, 3.6f, 1f));
                    var size = box.S;
                    size.y = Mathf.Min(size.y,
                        FitAxis(box, 1, BandPoint(R, yb, band, i - 1, n)),
                        FitAxis(box, 1, BandPoint(R, yb, band, i + 1, n)));
                    Emit(p, rot, size, bandDoms[band]);
                }
            }
        }

        /// <summary>The centre of agate-band prism <paramref name="i"/> of <paramref name="n"/>
        /// (the husk-half offset follows the side of the crack it falls on).</summary>
        static Vector3 BandPoint(float R, float yb, int band, int i, int n)
        {
            float a = 2f * Mathf.PI * i / n;
            float xoff = Mathf.Cos(a) > 0f ? CrackGap * 0.5f : -CrackGap * 0.5f;
            return new Vector3((R + 15f) * Mathf.Cos(a) + xoff, yb + 9f * Mathf.Sin(a * 2f + band), (R + 15f) * Mathf.Sin(a));
        }

        static readonly Vector3 DustSize = new Vector3(0.9f, 0.9f, 1.8f);

        /// <summary>
        /// A dust mote that drifts into a druse spike: it shrinks only as far as contact
        /// (<paramref name="k"/>), and one whose centre lies inside the spike - no size clears it -
        /// is pushed straight out from the spike's axis to touch it at full size, the smallest
        /// offset that clears it. A mote tests a druse's spikes only when it is within the
        /// druse's reach of its root, and fits only to the prisms beside it.
        /// </summary>
        Vector3 ClearOfDruses(Vector3 p, Quaternion rot, Vector3 size, out float k)
        {
            k = 1f;
            for (int c = 0, first = 0; c < 24; first += _druseCounts[c], c++)
            {
                float cr = _druseRadii[c] + 2f;
                if ((p - _druseCentres[c]).sqrMagnitude > cr * cr) continue;
                for (int sp = 0; sp < _druseCounts[c]; sp++)
                {
                    ref var s = ref _druseSpikes[first + sp];
                    float r = s.Bound + 2f;
                    if ((p - s.Mid).sqrMagnitude > r * r) continue;
                    float along = Mathf.Clamp(Vector3.Dot(p - s.Root, s.Dir), s.Start, s.Reach);
                    Vector3 off = p - (s.Root + s.Dir * along);
                    if (off.sqrMagnitude > 16f) continue;
                    int near = Mathf.Clamp(Mathf.RoundToInt((along - s.Start) / DruseStep), 0, s.Len);
                    float od = off.magnitude;
                    Vector3 u = od > 1e-4f ? off / od : SpawnPoint.LookRotation(s.Dir, Vector3.up) * Vector3.right;
                    float kk = 1f, reach = 0f;
                    for (int i = Mathf.Max(0, near - 1); i <= Mathf.Min(s.Len, near + 1); i++)
                    {
                        DrusePrism(s, i, out var dp, out var dr, out var ds);
                        kk = Mathf.Min(kk, ScaleAgainst(new Box(p, rot, size), new Box(dp, dr, ds)));
                        reach = Mathf.Max(reach, (Support(dr, ds, u) + Support(rot, size, u)) / Flush);
                    }
                    if (kk * size.x >= 0.5f) { k = Mathf.Min(k, kk); continue; }   // the 0.5 floor
                    p = s.Root + s.Dir * along + u * reach;
                }
            }
            return p;
        }

        void BuildDust()
        {
            int n = Scaled(6100);
            var pos = new Vector3[n];
            var rots = new Quaternion[n];
            var fit = new float[n];
            var live = new bool[n];
            for (int i = 0; i < n; i++)
            {
                live[i] = Hash01(i * 13) >= 0.3f;
                if (!live[i]) continue;
                float a = i * GoldenAngle * 1.7f;
                float rr = 40f + 170f * Mathf.Sqrt(Hash01(i * 7));
                float y = (Hash01(i * 11) * 2f - 1f) * 160f;
                rots[i] = Quaternion.Euler(0f, Hash01(i * 3 + _noiseSeed) * 360f, 0f);
                pos[i] = ClearOfDruses(new Vector3(rr * Mathf.Cos(a) * 0.9f, y, rr * Mathf.Sin(a)), rots[i], DustSize, out fit[i]);
            }

            // Mote i sits at angle i * 1.7 golden angles, so two motes can only meet where their
            // angles nearly agree: where their index gap is a close return of that rotation (the
            // gap times the turn lands within tau of a whole number of turns). Those gaps are a
            // fixed ~130 of the 6,100, found once; a mote checks only the motes that far along
            // (most fail on height alone), and a pair that would clip shrinks together just to
            // contact (8 pairs of the 4,262 motes).
            float reach = DustSize.magnitude;                       // two half-diagonals
            double turn = GoldenAngle * 1.7 / (2.0 * System.Math.PI);
            double tau = reach / (0.9 * 40.0 * 2.0 * System.Math.PI) + 1e-3;   // nearest motes sit at radius 40 (x 0.9)
            var gaps = new System.Collections.Generic.List<int>();
            for (int q = 1; q < n; q++)
            {
                double f = q * turn;
                if (System.Math.Abs(f - System.Math.Round(f)) < tau) gaps.Add(q);
            }
            for (int i = 0; i < n; i++)
            {
                if (!live[i]) continue;
                foreach (int q in gaps)
                {
                    int j = i + q;
                    if (j >= n) break;
                    if (!live[j] || Mathf.Abs(pos[j].y - pos[i].y) >= reach || (pos[j] - pos[i]).sqrMagnitude >= reach * reach) continue;
                    float k = PairScale(new Box(pos[i], rots[i], DustSize * fit[i]), new Box(pos[j], rots[j], DustSize * fit[j]));
                    if (k >= 1f) continue;
                    if (k * Mathf.Min(fit[i], fit[j]) * DustSize.x >= 0.5f) { fit[i] *= k; fit[j] *= k; continue; }
                    // Coincident in all but name: the later mote steps out along the line between
                    // them to touch at its own size.
                    Vector3 d = pos[j] - pos[i];
                    Vector3 u = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
                    var bi = new Box(pos[i], rots[i], DustSize * fit[i]);
                    var bj = new Box(pos[j], rots[j], DustSize * fit[j]);
                    pos[j] = pos[i] + u * ((bi.Support(u) + bj.Support(u)) / Flush);
                }
            }

            for (int i = 0; i < n; i++)
                if (live[i]) Emit(pos[i], rots[i], DustSize * fit[i], CrystalDoms[i % 3]);
        }

        void BuildLightShafts()
        {
            // God-rays knife diagonally DOWN the rift slab (|x| < ~77) from the upper rim
            // past the heart's airspace - telegraphing the crack as THE entrance from
            // outside and lighting the cathedral interior. Never pierces the husk walls.
            var dir = new Vector3(30f, -520f, 130f);
            var shaftRot = SpawnPoint.LookRotation(dir, Vector3.up);
            var unit = dir.normalized;
            for (int k = 0; k < 12; k++)
            {
                float x0 = -33f + 6f * k;
                float z0 = -170f + 28f * k;
                for (int i = 0; i < 46; i++)
                {
                    float t = i / 45f;
                    var p = new Vector3(x0 + 30f * t, 265f - 520f * t, z0 + 130f * t);
                    // A ray dash that runs into a heart spike stops at its face - the shafts are
                    // dashed already, so the spike reads as passing through a gap in the ray.
                    var size = new Vector3(1f, 1f, 5.2f);
                    for (int sp = 0; sp < 12; sp++)
                    {
                        // Where along the spike the dash passes closest (a cheap line-line read;
                        // only the few dashes that pass within reach fit to its prisms).
                        ClosestOnSegments(Vector3.zero, _heartDirs[sp], _heartStarts[sp] + 40f,
                            p - unit * 2.6f, unit, 5.2f, out float sa, out float sb);
                        Vector3 gap = p - unit * 2.6f + unit * sb - _heartDirs[sp] * sa;
                        if (gap.sqrMagnitude > 16f) continue;
                        int near = Mathf.Clamp(Mathf.RoundToInt((sa - _heartStarts[sp]) / HeartStep), 0, 20);
                        for (int i2 = Mathf.Max(0, near - 2); i2 <= Mathf.Min(20, near + 2); i2++)
                        {
                            HeartPrism(sp, i2, out var hp, out var hr, out var hs);
                            size.z = Mathf.Min(size.z, FitAgainst(new Box(p, shaftRot, size), 2, new Box(hp, hr, hs)));
                        }
                    }
                    Emit(p, shaftRot, size, k % 2 != 0 ? Domains.Gold : Domains.Blue);
                }
            }
        }
    }
}
