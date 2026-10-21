using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Daedala" - the road-city cell environment (~34k prisms): Atlantis's built half,
    /// expanded into a full Escher marble city. Four undulating ring-terraces climb toward
    /// the rim, joined by spiral stairs and studded with colonnade gates; TWO interleaved
    /// trefoil causeways of opposite chirality roll a full twist each; triumphal arches,
    /// radial aqueduct spans, helical minaret towers, plaza rosettes, floating melt-atolls
    /// with nautilus crowns, and catenary lantern strings knit it together. Marble gold +
    /// lapis blue; shielded capitals/finials/shrines are the jewels. No danger - the city
    /// is the safe wonder; its risk is getting lost.
    ///
    /// <para>Every family is laid FLUSH (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10): each prism is emitted already fitted, from geometry the generator has in hand -
    /// there is no clearance pass over the finished list and no baked table. Prism counts,
    /// domains, kinds and rotations are the authored ones. Within a family a prism is sized to
    /// its neighbours (a chain link to the mid-planes either side, a ring tile to its bisector
    /// planes, a stacked block to the step). Where two structures cross (a causeway through a
    /// terrace, an aqueduct through a stair, a minaret through a gate) the pair meeting there
    /// gives way - see <see cref="GiveWay"/>, which finds the few earlier prisms beside a new
    /// one by each structure's own layout arithmetic: the one that loses less shrinks as far as
    /// contact, and a prism moves only when no size could clear it.
    /// <c>python3 Tools/Build/audit_prism_clipping.py SpawnableDaedala -v</c> is the gate.</para>
    /// </summary>
    public class SpawnableDaedala : CellEnvironmentSpawnableBase
    {
        protected override int DefaultSeed => 23;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableDaedala), 4);

        static readonly (float R, float yc, Domains dom)[] Rings =
        {
            (150f, -95f, Domains.Gold),
            (230f, -25f, Domains.Blue),
            (310f, 45f, Domains.Gold),
            (390f, 115f, Domains.Blue),
        };

        /// <summary>Radial spacing of the five deck lanes of a terrace.</summary>
        const float LaneStep = 6.2f;

        protected override void BuildEnvironment()
        {
            Prepare();
            BuildTerraces();
            BuildCauseways();
            BuildArches();
            BuildAqueducts();
            BuildTowersAndLanterns();
            BuildPlazas();
            BuildAtolls();
        }

        // ── Flush fitting (shared by every family below) ───────────────────────────────────

        /// <summary>Fitted sizes aim a hair under exact contact, so float noise never tips a pair
        /// that should TOUCH into one that clips.</summary>
        const float Flush = 0.995f;

        /// <summary>The prism scale animator's per-axis floor; a laid size under it is grown back,
        /// so a fit never asks for less.</summary>
        const float MinSize = 0.5f;

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

            /// <summary>A box at <paramref name="c"/> of size <paramref name="s"/> sharing
            /// <paramref name="axes"/>'s orientation.</summary>
            public Box(Vector3 c, in Box axes, Vector3 s)
            {
                C = c; S = s; X = axes.X; Y = axes.Y; Z = axes.Z; Radius = 0.5f * s.magnitude;
            }

            public float Support(Vector3 n) => 0.5f * (
                Mathf.Abs(Vector3.Dot(X, n)) * S.x + Mathf.Abs(Vector3.Dot(Y, n)) * S.y + Mathf.Abs(Vector3.Dot(Z, n)) * S.z);

            public Vector3 Axis(int a) => a == 0 ? X : a == 1 ? Y : Z;

            /// <summary>Candidate separating axis k of this box and <paramref name="o"/>: the
            /// centre line, then each box's three face normals. An axis that separates the
            /// projections is proof the boxes do not meet.</summary>
            public Vector3 Candidate(int k, in Box o, Vector3 unitD) => k switch
            {
                0 => unitD, 1 => X, 2 => Y, 3 => Z, 4 => o.X, 5 => o.Y, _ => o.Z,
            };
        }

        /// <summary>
        /// Half the box's extent along the unit direction <paramref name="n"/> (its support): a box
        /// sits wholly on its own side of a plane at distance h from its centre when this is at most h.
        /// </summary>
        static float Support(Quaternion rot, Vector3 size, Vector3 n) => new Box(Vector3.zero, rot, size).Support(n);

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

        /// <summary>The largest uniform factor (capped at 1) at which the box stays on its own
        /// side of the mid-plane to a neighbour centred at <paramref name="nbr"/>.</summary>
        static float MidplaneScale(in Box b, Vector3 nbr)
        {
            Vector3 d = nbr - b.C;
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            float sup = b.Support(d / dist);
            return sup < 1e-6f ? 1f : Mathf.Min(1f, 0.5f * dist * Flush / sup);
        }

        /// <summary>
        /// A chain link fitted to the mid-planes to its neighbours: its length along
        /// <paramref name="axis"/> shrinks first; where even the floor length would cross a
        /// mid-plane (a link on a tight bend) the other two sizes shrink with it, uniformly.
        /// </summary>
        static Vector3 FitLink(Vector3 c, Quaternion rot, Vector3 size, int axis, Vector3 prev, Vector3 next,
            bool hasPrev = true, bool hasNext = true) =>
            FitLink(new Box(c, rot, size), axis, prev, next, hasPrev, hasNext);

        /// <summary><see cref="FitLink(Vector3, Quaternion, Vector3, int, Vector3, Vector3, bool, bool)"/>
        /// for a box whose axes are already known.</summary>
        static Vector3 FitLink(Box b, int axis, Vector3 prev, Vector3 next, bool hasPrev = true, bool hasNext = true)
        {
            Vector3 c = b.C, size = b.S;
            float len = size[axis];
            if (hasPrev) len = Mathf.Min(len, FitAxis(b, axis, prev));
            if (hasNext) len = Mathf.Min(len, FitAxis(b, axis, next));
            if (len >= MinSize) { size[axis] = len; return size; }
            size[axis] = MinSize;
            b = new Box(c, b, size);
            float k = 1f;
            if (hasPrev) k = Mathf.Min(k, MidplaneScale(b, prev));
            if (hasNext) k = Mathf.Min(k, MidplaneScale(b, next));
            return size * k;
        }

        // ── Crossings: the later structure gives way ───────────────────────────────────────

        /// <summary>The structures a later prism can meet, in the order they are laid.</summary>
        enum Part { Deck, Gate, Stair, Causeway, Arch, Aqueduct, Tower, Lantern, Plaza, Atoll }

        /// <summary>Largest half-diagonal of any prism this city lays (an arch pier at full
        /// jitter): a prism farther than its own half-diagonal plus this from a centre cannot
        /// touch that prism.</summary>
        const float MaxRadius = 3.1f;

        // Where each structure's prisms landed in the lay list (-1: not laid yet, or cleared by a
        // spawn pad), so a later prism can find the few earlier ones beside it by arithmetic.
        float[][] _deckY;
        int[][] _deckIdx, _gateIdx, _stairIdx, _cwIdx;
        Vector3[][] _cwPts;
        (int start, int end)[] _arches, _aqueducts, _towers, _strings, _plazas, _atolls;
        Vector3[] _archCentre, _towerBase, _strP1, _strP2, _plazaCentre, _atollCentre;
        float[] _towerH, _atollReach;
        readonly List<int> _near = new List<int>(64), _cluster = new List<int>(256);
        // Per structure kind laid after the causeways: the radius and height range its laid prisms
        // span, so a lookup skips a whole kind the point is nowhere near.
        readonly float[] _spanR0 = new float[10], _spanR1 = new float[10], _spanY0 = new float[10], _spanY1 = new float[10];
        readonly List<int> _cwNear = new List<int>(256);

        void Prepare()
        {
            int rings = Rings.Length;
            _deckY = new float[rings][];
            _deckIdx = new int[rings][];
            _gateIdx = new int[rings][];
            _stairIdx = new int[rings - 1][];
            for (int ri = 0; ri < rings; ri++)
            {
                int n = DeckCount(ri);
                _deckY[ri] = new float[n];
                for (int i = 0; i < n; i++)
                    _deckY[ri][i] = Rings[ri].yc + Und(2f * Mathf.PI * i / n, ri);
                _deckIdx[ri] = Fill(new int[n * 5]);
                _gateIdx[ri] = Fill(new int[GateCount(ri) * 33]);
                if (ri < rings - 1) _stairIdx[ri] = Fill(new int[7 * 52 * 3]);
            }
            _cwIdx = new int[2][];
            _cwPts = new Vector3[2][];
            _arches = new (int, int)[18]; _archCentre = new Vector3[18];
            _aqueducts = new (int, int)[11];
            _towers = new (int, int)[14]; _towerBase = new Vector3[14]; _towerH = new float[14];
            _strings = new (int, int)[30]; _strP1 = new Vector3[30]; _strP2 = new Vector3[30];
            _plazas = new (int, int)[18]; _plazaCentre = new Vector3[18];
            _atolls = new (int, int)[15]; _atollCentre = new Vector3[15]; _atollReach = new float[15];
            for (int k = 0; k < 10; k++)
            {
                _spanR0[k] = _spanY0[k] = float.PositiveInfinity;
                _spanR1[k] = _spanY1[k] = float.NegativeInfinity;
            }
        }

        /// <summary>Widen <paramref name="part"/>'s span by a structure just laid.</summary>
        (int, int) Span(Part part, int start)
        {
            int k = (int)part;
            for (int i = start; i < _cachedLays.Count; i++)
            {
                var q = _cachedLays[i].Point.Position;
                float r = Mathf.Sqrt(q.x * q.x + q.z * q.z);
                _spanR0[k] = Mathf.Min(_spanR0[k], r); _spanR1[k] = Mathf.Max(_spanR1[k], r);
                _spanY0[k] = Mathf.Min(_spanY0[k], q.y); _spanY1[k] = Mathf.Max(_spanY1[k], q.y);
            }
            return (start, _cachedLays.Count);
        }

        bool InSpan(Part part, float r, float y, float reach)
        {
            int k = (int)part;
            return r > _spanR0[k] - reach && r < _spanR1[k] + reach && y > _spanY0[k] - reach && y < _spanY1[k] + reach;
        }

        /// <summary>|a - b| wrapped into [0, pi] (angles in radians).</summary>
        static float AngleGap(float a, float b)
        {
            float d = a - b;
            return Mathf.Abs(d - 2f * Mathf.PI * Mathf.Round(d / (2f * Mathf.PI)));
        }

        static int[] Fill(int[] a)
        {
            for (int i = 0; i < a.Length; i++) a[i] = -1;
            return a;
        }

        /// <summary>Where the prism just emitted (when the lay list held <paramref name="at"/>
        /// prisms) landed, or -1 if a spawn pad's clearance dropped it. Each family calls Emit
        /// itself, so the clip audit still names the family that laid a prism.</summary>
        int Landed(int at) => _cachedLays.Count > at ? at : -1;

        Box Laid(int idx)
        {
            var p = _cachedLays[idx].Point;
            return new Box(p.Position, p.Rotation, Vector3.Max(p.Scale, new Vector3(MinSize, MinSize, MinSize)));
        }

        List<int> _into;

        void Near(int idx, Vector3 p, float reach)
        {
            if (idx < 0) return;
            if ((_cachedLays[idx].Point.Position - p).sqrMagnitude < reach * reach) _into.Add(idx);
        }

        void NearRange((int start, int end) range, Vector3 p, float reach)
        {
            for (int i = range.start; i < range.end; i++) Near(i, p, reach);
        }

        static float Angle(Vector3 p)
        {
            float a = Mathf.Atan2(p.z, p.x);
            return a < 0f ? a + 2f * Mathf.PI : a;
        }

        /// <summary>
        /// Every earlier prism (of a structure laid before <paramref name="mine"/>'s, or of an
        /// earlier one of the same kind) whose centre lies within <paramref name="reach"/> of
        /// <paramref name="p"/>, found by each structure's own layout arithmetic - which ring
        /// plate, which gate, which stair step, which aqueduct station is beside the point - never
        /// by searching the lay list. <paramref name="cwNear"/> lists the causeway stations near
        /// the structure being laid (the trefoils are the one shape a point cannot be inverted
        /// onto, so each structure collects its stations once, up front).
        /// </summary>
        void Gather(Vector3 p, float reach, Part mine, List<int> cwNear) => Gather(p, reach, mine, cwNear, _near);

        /// <summary>Gather once for a whole cluster of prisms about to be laid (a gate, a stair
        /// step, a causeway station, a plaza...) lying within <paramref name="radius"/> of
        /// <paramref name="centre"/>; each prism's <see cref="GiveWay"/> then reads that list.</summary>
        void GatherCluster(Vector3 centre, float radius, Part mine, List<int> cwNear = null) =>
            Gather(centre, radius + MaxRadius, mine, cwNear, _cluster);

        /// <summary><see cref="GatherCluster"/> for the run of prisms centred at
        /// <paramref name="pts"/>[<paramref name="i0"/>..<paramref name="i1"/>) (each at most
        /// <paramref name="prismRadius"/> across from its centre).</summary>
        void GatherRun(Vector3[] pts, int i0, int i1, float prismRadius, Part mine, List<int> cwNear = null)
        {
            i1 = Mathf.Min(i1, pts.Length);
            Vector3 c = Vector3.zero;
            for (int i = i0; i < i1; i++) c += pts[i];
            c /= Mathf.Max(1, i1 - i0);
            float r = 0f;
            for (int i = i0; i < i1; i++) r = Mathf.Max(r, (pts[i] - c).magnitude);
            GatherCluster(c, r + prismRadius, mine, cwNear);
        }

        void Gather(Vector3 p, float reach, Part mine, List<int> cwNear, List<int> into)
        {
            _into = into;
            into.Clear();
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            float a = Angle(p);
            for (int ri = 0; ri < Rings.Length; ri++)
            {
                var (R, yc, _) = Rings[ri];
                if (p.y < yc - 35f - reach || p.y > yc + 80f + reach) continue;   // decks +-31, gates to ~+75
                int n = _deckY[ri].Length;
                float step = 2f * Mathf.PI / n;
                if (mine > Part.Deck && Mathf.Abs(r - R) < 2f * LaneStep + reach)
                {
                    float j = a / step;
                    int span = Mathf.CeilToInt(reach / (Mathf.Max(r, 1f) * step)) + 1;
                    for (int jj = Mathf.FloorToInt(j) - span; jj <= Mathf.CeilToInt(j) + span; jj++)
                    {
                        int m = ((jj % n) + n) % n;
                        for (int w = 0; w < 5; w++)
                            if (Mathf.Abs(R + (w - 2) * LaneStep - r) < reach) Near(_deckIdx[ri][m * 5 + w], p, reach);
                    }
                }
                if (mine >= Part.Gate && Mathf.Abs(r - R) < 4f + reach)
                {
                    int ge = GateEvery(ri);
                    for (int g = 0; g < GateCount(ri); g++)
                        if (AngleGap(a, g * ge * step) * R < 12f + reach)
                            for (int m = 0; m < 33; m++) Near(_gateIdx[ri][g * 33 + m], p, reach);
                }
            }
            if (mine >= Part.Gate)
                for (int ri = 0; ri < Rings.Length - 1; ri++)
                {
                    float R1 = Rings[ri].R, R2 = Rings[ri + 1].R;
                    if (r < R1 - 8f - reach || r > R2 + 8f + reach) continue;
                    if (p.y < Rings[ri].yc - 35f - reach || p.y > Rings[ri + 1].yc + 40f + reach) continue;
                    float tr = (r - R1) / (R2 - R1);
                    for (int s = 0; s < 7; s++)
                    {
                        float a0 = 2f * Mathf.PI * s / 7f + ri * 0.6f;
                        if (AngleGap(a0 + 1.9f * Mathf.Clamp01(tr), a) * r > 12f + reach + 1.9f * r * 0.15f) continue;
                        int ic = Mathf.RoundToInt(Mathf.Clamp01(tr) * 51f);
                        int span = Mathf.CeilToInt((reach + 6f) / 1.5f);
                        for (int i = Mathf.Max(0, ic - span); i <= Mathf.Min(51, ic + span); i++)
                            for (int w = 0; w < 3; w++) Near(_stairIdx[ri][(s * 52 + i) * 3 + w], p, reach);
                    }
                }
            if (mine > Part.Causeway && cwNear != null)
                foreach (int code in cwNear)
                {
                    int cw = code >> 16, i = code & 0xffff;
                    if ((_cwPts[cw][i] - p).sqrMagnitude > (reach + 10f) * (reach + 10f)) continue;
                    for (int w = 0; w < 5; w++) Near(_cwIdx[cw][i * 5 + w], p, reach);
                }
            if (mine >= Part.Arch && InSpan(Part.Arch, r, p.y, reach))
                for (int k = 0; k < 18; k++)
                    if ((_archCentre[k] - p).sqrMagnitude < (32f + reach) * (32f + reach)) NearRange(_arches[k], p, reach);
            if (mine >= Part.Aqueduct && InSpan(Part.Aqueduct, r, p.y, reach))
                for (int k = 0; k < 11; k++)
                {
                    float ak = 2f * Mathf.PI * k / 11f + 0.45f;
                    float off = -Mathf.Sin(ak) * p.x + Mathf.Cos(ak) * p.z;
                    float along = Mathf.Cos(ak) * p.x + Mathf.Sin(ak) * p.z;
                    if (Mathf.Abs(off) < 6f + reach && along > 150f - reach && along < 390f + reach)
                        NearRange(_aqueducts[k], p, reach);
                }
            if (mine >= Part.Tower && InSpan(Part.Tower, r, p.y, reach))
                for (int k = 0; k < 14; k++)
                {
                    var b = _towerBase[k];
                    float dx = p.x - b.x, dz = p.z - b.z;
                    if (dx * dx + dz * dz < (12f + reach) * (12f + reach) && p.y > b.y - reach && p.y < b.y + _towerH[k] + 8f + reach)
                        NearRange(_towers[k], p, reach);
                }
            if (mine >= Part.Lantern && InSpan(Part.Lantern, r, p.y, reach))
                for (int k = 0; k < 30; k++)
                {
                    if (_strings[k].end <= _strings[k].start) continue;
                    Vector3 d = _strP2[k] - _strP1[k];
                    float t = Mathf.Clamp01(Vector3.Dot(p - _strP1[k], d) / d.sqrMagnitude);
                    Vector3 q = _strP1[k] + d * t;
                    float dy = p.y - q.y;
                    q.y = p.y;
                    if ((q - p).sqrMagnitude < (reach + 2f) * (reach + 2f) && dy < reach + 2f && dy > -34f - reach)
                        NearRange(_strings[k], p, reach);
                }
            if (mine >= Part.Plaza && InSpan(Part.Plaza, r, p.y, reach))
                for (int k = 0; k < 18; k++)
                    if ((_plazaCentre[k] - p).sqrMagnitude < (17f + reach) * (17f + reach)) NearRange(_plazas[k], p, reach);
            if (mine >= Part.Atoll && InSpan(Part.Atoll, r, p.y, reach))
                for (int k = 0; k < 15; k++)
                    if ((_atollCentre[k] - p).sqrMagnitude < (_atollReach[k] + reach) * (_atollReach[k] + reach)) NearRange(_atolls[k], p, reach);
        }

        /// <summary>The causeway stations within <paramref name="radius"/> (plus a causeway's
        /// half-width) of a structure - a sphere round <paramref name="a"/>, or a capsule from
        /// <paramref name="a"/> to <paramref name="b"/> - as <c>cw &lt;&lt; 16 | station</c>.</summary>
        List<int> CausewayNear(Vector3 a, float radius, List<int> into) => CausewayNear(a, a, radius, into);

        List<int> CausewayNear(Vector3 a, Vector3 b, float radius, List<int> into)
        {
            into.Clear();
            float rr = (radius + 14f) * (radius + 14f);
            Vector3 ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int cw = 0; cw < 2; cw++)
                for (int i = 0; i < _cwPts[cw].Length; i++)
                {
                    Vector3 q = _cwPts[cw][i];
                    float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / len2);
                    if ((a + ab * t - q).sqrMagnitude < rr) into.Add(cw << 16 | i);
                }
            return into;
        }

        /// <summary>
        /// The prism about to be laid and every earlier prism it would pass through give way to
        /// each other (its own structure's earlier prisms are fitted to it already and only touch,
        /// so they never count). Nothing is spawned until the whole list is built, so an earlier
        /// prism can still be resized: of the two, the one that loses less volume shrinks (one
        /// axis shortened to contact, or all three scaled to contact, whichever keeps more), or
        /// both shrink together. Only when no size at or above the floor clears them - one centre
        /// inside the other, or against it - does the new prism move, at its own size, by the
        /// smallest offset that clears it of everything beside it (see <see cref="Escape"/>). Most
        /// prisms have nothing laid beside them and cost one empty-list check.
        /// </summary>
        void GiveWay(ref Vector3 c, Quaternion rot, ref Vector3 size, Part mine, List<int> cwNear = null, int locked = 0,
            bool clustered = false)
        {
            var list = _cluster;
            if (!clustered)
            {
                Gather(c, 0.5f * size.magnitude + MaxRadius, mine, cwNear);
                list = _near;
            }
            if (list.Count == 0) return;   // nothing laid beside it: the common case, at no cost
            var me = new Box(c, rot, size);
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                for (int ni = 0; ni < list.Count && !moved; ni++)
                {
                    int idx = list[ni];
                    var lp = _cachedLays[idx].Point;
                    float or = 0.5f * Vector3.Max(lp.Scale, new Vector3(MinSize, MinSize, MinSize)).magnitude;
                    if ((lp.Position - me.C).sqrMagnitude >= (me.Radius + or) * (me.Radius + or)) continue;
                    var o = Laid(idx);
                    if (!Clips(me, o)) continue;
                    Vector3 d = o.C - me.C;

                    float meVol = size.x * size.y * size.z, oVol = o.S.x * o.S.y * o.S.z;
                    Vector3 mine2 = BestShrink(me, o, d, out float mineLoss);
                    Vector3 theirs = BestShrink(o, me, -d, out float theirLoss);
                    if (mineLoss <= theirLoss && mineLoss < meVol)
                        size = mine2;
                    else if (theirLoss < oVol)
                        Resize(idx, theirs);
                    else
                    {
                        float k = PairScale(me, o, d);
                        if (k * Mathf.Min(size.x, Mathf.Min(size.y, size.z)) >= MinSize &&
                            k * Mathf.Min(o.S.x, Mathf.Min(o.S.y, o.S.z)) >= MinSize)
                        {
                            size *= k;
                            Resize(idx, o.S * k);
                        }
                        else
                        {
                            // Look a move's length further round, so the escape sees what is
                            // beside where it might land (its own structure's prisms included).
                            Gather(c, me.Radius + MaxRadius + 4f, mine, cwNear);
                            c += Escape(me, o, d, locked);
                            list = _near;
                            moved = true;
                        }
                    }
                    me = new Box(c, rot, size);
                }
                if (!moved) break;
                Gather(c, me.Radius + MaxRadius, mine, cwNear);
            }
        }

        /// <summary>True unless one of the seven candidate axes separates the boxes (touching
        /// faces are flush, not a clip).</summary>
        static bool Clips(in Box a, in Box b)
        {
            Vector3 d = b.C - a.C;
            float dist = d.magnitude;
            if (dist >= a.Radius + b.Radius) return false;
            Vector3 u = dist > 1e-5f ? d / dist : Vector3.up;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = a.Candidate(k, b, u);
                if (Mathf.Abs(Vector3.Dot(d, L)) >= a.Support(L) + b.Support(L) - 1e-4f) return false;
            }
            return true;
        }

        /// <summary>The size of <paramref name="b"/> that clears the fixed <paramref name="a"/>
        /// (<paramref name="d"/> = a - b) and keeps the most volume - one axis shortened, or all
        /// three scaled - with the volume it loses (+inf when no size at the floor clears).</summary>
        static Vector3 BestShrink(in Box b, in Box a, Vector3 d, out float loss)
        {
            float vol = b.S.x * b.S.y * b.S.z;
            loss = float.PositiveInfinity;
            Vector3 best = b.S;
            for (int axis = 0; axis < 3; axis++)
            {
                float len = FitAgainst(b, axis, a, d);
                if (len < MinSize) continue;
                var s = b.S;
                s[axis] = Mathf.Min(s[axis], len);
                float l = vol - s.x * s.y * s.z;
                if (l < loss) { loss = l; best = s; }
            }
            float k = ScaleAgainst(b, a, d);
            if (k * Mathf.Min(b.S.x, Mathf.Min(b.S.y, b.S.z)) >= MinSize)
            {
                var s = b.S * k;
                float l = vol - s.x * s.y * s.z;
                if (l < loss) { loss = l; best = s; }
            }
            return best;
        }

        /// <summary>The largest uniform factor for both boxes at which some candidate axis
        /// separates them.</summary>
        static float PairScale(in Box a, in Box b, Vector3 d)
        {
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 u = d / dist;
            float best = 0f;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = a.Candidate(k, b, u);
                float sum = a.Support(L) + b.Support(L);
                if (sum > 1e-6f) best = Mathf.Max(best, Mathf.Abs(Vector3.Dot(d, L)) * Flush / sum);
            }
            return Mathf.Min(1f, best);
        }

        /// <summary>
        /// The smallest move of <paramref name="b"/> that clears the fixed <paramref name="a"/>
        /// and every other earlier prism beside it: each candidate axis is tried both ways (back
        /// out, or through to the far side), with no component along the <paramref name="locked"/>
        /// local axes (bit 0 x, 1 y, 2 z) - the axes a prism meets its own structure's neighbours
        /// on (a chain's next link, a stair's next tread) - and the shortest move that leaves the prism
        /// clear of all of them wins (moves along its own thinnest axis, off the face of a plate,
        /// win ties up to twice the length). If none clears all, the shortest that clears
        /// <paramref name="a"/> is taken and the next pass carries on from there.
        /// </summary>
        Vector3 Escape(in Box b, in Box a, Vector3 d, int locked)
        {
            float dist = d.magnitude;
            Vector3 u = dist > 1e-5f ? d / dist : Vector3.up;
            int thin = -1;
            for (int ax = 0; ax < 3; ax++)
                if ((locked & (1 << ax)) == 0 && (thin < 0 || b.S[ax] < b.S[thin])) thin = ax;
            Vector3 bestAll = Vector3.zero, bestAny = Vector3.zero;
            float allLen = float.PositiveInfinity, anyLen = float.PositiveInfinity;
            for (int k = -1; k < 7; k++)
            {
                Vector3 L = k < 0 ? b.Axis(thin) : b.Candidate(k, a, u);
                for (int ax = 0; ax < 3; ax++)
                    if ((locked & (1 << ax)) != 0) L -= b.Axis(ax) * Vector3.Dot(L, b.Axis(ax));
                if (L.sqrMagnitude < 1e-6f) continue;
                L.Normalize();
                float dl = Vector3.Dot(d, L), need = (b.Support(L) + a.Support(L)) / Flush + 1e-3f;
                for (int side = 0; side < 2; side++)
                {
                    float t = side == 0 ? dl - need : dl + need;   // b moves by t along L
                    float len = Mathf.Abs(t) * (k < 0 ? 0.5f : 1f);  // the thin axis wins ties up to 2x
                    if (len >= allLen) continue;
                    if (len < anyLen) { anyLen = len; bestAny = L * t; }
                    var moved = new Box(b.C + L * t, b, b.S);
                    bool clear = true;
                    foreach (int idx in _near)   // its own structure's earlier prisms too
                        if (Clips(moved, Laid(idx))) { clear = false; break; }
                    if (clear) { allLen = len; bestAll = L * t; }
                }
            }
            return allLen < float.PositiveInfinity ? bestAll : bestAny;
        }

        /// <summary>Resize an earlier prism in the lay list (nothing is spawned yet).</summary>
        void Resize(int idx, Vector3 size)
        {
            var lay = _cachedLays[idx];
            _cachedLays[idx] = new PrismLay(new SpawnPoint(lay.Point.Position, lay.Point.Rotation, size), lay.Domain, lay.Kind);
        }

        /// <summary>The largest size along local axis <paramref name="axis"/> of box
        /// <paramref name="b"/> (its other sizes kept) at which some candidate axis still separates
        /// it from the fixed box <paramref name="a"/> (<paramref name="d"/> = a - b).</summary>
        static float FitAgainst(in Box b, int axis, in Box a, Vector3 d)
        {
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 dir = b.Axis(axis), u = d / dist;
            float best = 0f;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = b.Candidate(k, a, u);
                float along = Mathf.Abs(Vector3.Dot(dir, L));
                float room = Mathf.Abs(Vector3.Dot(d, L)) * Flush - a.Support(L) - (b.Support(L) - 0.5f * along * b.S[axis]);
                if (room < 0f) continue;
                if (along < 1e-5f) return float.PositiveInfinity;
                best = Mathf.Max(best, 2f * room / along);
            }
            return best;
        }

        /// <summary>The largest uniform factor for box <paramref name="b"/> at which some candidate
        /// axis separates it from the fixed box <paramref name="a"/>.</summary>
        static float ScaleAgainst(in Box b, in Box a, Vector3 d)
        {
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 u = d / dist;
            float best = 0f;
            for (int k = 0; k < 7; k++)
            {
                Vector3 L = b.Candidate(k, a, u);
                float sup = b.Support(L);
                if (sup < 1e-6f) continue;
                best = Mathf.Max(best, (Mathf.Abs(Vector3.Dot(d, L)) * Flush - a.Support(L)) / sup);
            }
            return Mathf.Min(1f, best);
        }

        // ── Terraces, gates, stairs ────────────────────────────────────────────────────────

        static int DeckCount(int ri) => (int)(2f * Mathf.PI * Rings[ri].R / 3.5f);
        static int GateEvery(int ri) => Mathf.Max(1, DeckCount(ri) / 11);
        static int GateCount(int ri) => (DeckCount(ri) - 1) / GateEvery(ri) + 1;

        void BuildTerraces()
        {
            for (int ri = 0; ri < Rings.Length; ri++)
            {
                var (R, yc, dom) = Rings[ri];
                int n = DeckCount(ri);
                int gateEvery = GateEvery(ri);
                float tanHalf = Mathf.Tan(Mathf.PI / n);
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float y = _deckY[ri][i];
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var deckRot = SpawnPoint.LookRotation(Vector3.up, tangent);
                    for (int w = 0; w < 5; w++)
                    {
                        float rr = R + (w - 2) * LaneStep;
                        // A ring of flat plates is flush when each one's INNER corners meet its
                        // neighbours' on the bisector plane between them: tangential length
                        // 2 (rr - half width) tan(pi / n), ~3.2 at the 3.5 step (the authored 6.2
                        // overlapped each neighbour by nearly half). The jitter keeps the width and
                        // thickness; heights never matter, since the bisector plane is vertical.
                        var s = Jit(new Vector3(4f, 6.2f, 1f), 0.12f);
                        s.y = Mathf.Min(s.y, 2f * (rr - 0.5f * s.x) * tanHalf * Flush);
                        int at = _cachedLays.Count;
                        Emit(new Vector3(rr * Mathf.Cos(a), y + (w % 2) * 0.5f, rr * Mathf.Sin(a)), deckRot, s, dom);
                        _deckIdx[ri][i * 5 + w] = Landed(at);
                    }
                    if (i % gateEvery == 0)
                        Gate(ri, i, n, gateEvery, ri % 2 != 0 ? Domains.Jade : dom);
                }

                if (ri < Rings.Length - 1)
                {
                    var next = Rings[ri + 1];
                    for (int s = 0; s < 7; s++)
                        Stair(R, yc, next.R, next.yc, 2f * Mathf.PI * s / 7f + ri * 0.6f, ri, s);
                }
            }
        }

        /// <summary>The top of the centre-lane deck (lane 2, radius R) under a footprint at angle
        /// <paramref name="a"/> reaching <paramref name="halfArc"/> along the ring - the highest
        /// plate it overlaps, at the thickest jitter.</summary>
        float DeckTopUnder(int ri, float a, float halfArc) => DeckTopAt(ri, Rings[ri].R, 0f, a, halfArc, 2, 2);

        /// <summary>Plate step from a gate to its neighbour on the ring (the gate loop wraps: the
        /// last gate can stand only a few plates short of gate 0).</summary>
        static int GateStep(int i, int n, int gateEvery, int side)
        {
            int last = (n - 1) / gateEvery * gateEvery;
            if (side > 0) return i == last ? n - i : gateEvery;
            return i == 0 ? n - last : gateEvery;
        }

        void Gate(int ri, int i, int n, int gateEvery, Domains dom)
        {
            float R = Rings[ri].R;
            float a = 2f * Mathf.PI * i / n;
            float y = _deckY[ri][i];
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var center = new Vector3(R * ca, y, R * sa);
            var tangent = new Vector3(-sa, 0f, ca);
            const float pillarStep = 2.6f, capSize = 2.8f, lintelY = 33f;
            int g = i / gateEvery, slot = 0;
            float liftL = 0f, liftR = 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 basePos = center + tangent * (9.5f * side);
                float yaw = RangeF(0f, 360f);
                var pillarRot = Quaternion.Euler(0f, yaw, 0f);

                // The last gate of a ring stands a few plates short of gate 0, so the two facing
                // pillars (and their capitals and lintel ends) stand ~1.5 apart: each narrows to
                // the mid-plane between them.
                int nbrStep = GateStep(i, n, gateEvery, side);
                float na = a + side * 2f * Mathf.PI * nbrStep / n;
                var nbrBase = new Vector3(R * Mathf.Cos(na), y, R * Mathf.Sin(na))
                              + new Vector3(-Mathf.Sin(na), 0f, Mathf.Cos(na)) * (9.5f * -side);
                Vector3 across = nbrBase - basePos;
                across.y = 0f;
                float gap = across.magnitude;
                across /= Mathf.Max(gap, 1e-5f);
                float widthCap = gap * Flush / (Mathf.Abs(Vector3.Dot(pillarRot * Vector3.right, across)) +
                                                Mathf.Abs(Vector3.Dot(pillarRot * Vector3.forward, across)));
                float capCap = gap * Flush / (Mathf.Abs(across.x) + Mathf.Abs(across.z));

                // The pillar stands ON the deck where its foot is: the deck undulates over the
                // 9.5 to the foot, and where it rises the foot block shrinks to fit above it, or -
                // buried past the floor - the whole pillar lifts by the smallest step that seats it.
                float footHalf = 0.5f * 2.3f * 1.1f * 1.415f;
                float deckTop = DeckTopUnder(ri, Angle(basePos), footHalf);
                float room = basePos.y + 2f - deckTop;     // the foot block's centre above the deck top
                float lift = room * Flush >= 0.5f * MinSize ? 0f : 0.5f * MinSize / Flush - room;
                if (side < 0) liftL = lift; else liftR = lift;
                basePos.y += lift;

                float c = Mathf.Min(capSize, capCap);
                for (int h = 0; h < 11; h++)
                {
                    if (h % 4 == 0)   // a run of up to four blocks (the last run takes the capital)
                        GatherCluster(basePos + new Vector3(0f, 2f + (h + (h == 8 ? 2f : 1.5f)) * pillarStep, 0f),
                            (h == 8 ? 2.5f : 1.5f) * pillarStep + 2.5f, Part.Gate);
                    // Stacked at the 2.6 step, so 2.6 tall at most (the jitter reached 2.75);
                    // the top block stops at the capital's face.
                    var s = Jit(new Vector3(2.3f, 2.5f, 2.3f), 0.1f);
                    float cy = 2f + h * pillarStep;
                    float maxH = pillarStep * Flush;
                    if (h == 10) maxH = Mathf.Min(maxH, 2f * Flush * (2f + 11 * pillarStep - 0.5f * c - cy));
                    if (h == 0) maxH = Mathf.Min(maxH, 2f * Flush * (room + lift));
                    s.y = Mathf.Min(s.y, maxH);
                    s.x = Mathf.Min(s.x, widthCap);
                    s.z = Mathf.Min(s.z, widthCap);
                    var pos = basePos + new Vector3(0f, cy, 0f);
                    GiveWay(ref pos, pillarRot, ref s, Part.Gate, clustered: true);
                    int atPillar = _cachedLays.Count;
                    Emit(pos, pillarRot, s, dom);
                    _gateIdx[ri][g * 33 + slot++] = Landed(atPillar);
                }
                var capPos = basePos + new Vector3(0f, 2f + 11 * pillarStep, 0f);
                var cs = new Vector3(c, c, c);
                GiveWay(ref capPos, Quaternion.identity, ref cs, Part.Gate, clustered: true);
                int atCap = _cachedLays.Count;
                Emit(capPos, Quaternion.identity, cs, Domains.Gold, PrismKind.Shielded);
                _gateIdx[ri][g * 33 + slot++] = Landed(atCap);
            }

            // The lintel rides its two pillar tops (each end follows its pillar's lift). Its 3.2
            // blocks sit 2.375 apart, so wherever two neighbours are within a block height of each
            // other (the flat crown) they are 2.375 long; the steep ends keep 3.2.
            var lintelRot = SpawnPoint.LookRotation(tangent, Vector3.up);
            int lastGate = (n - 1) / gateEvery * gateEvery;
            float LintelH(int kk) => lintelY + 6f * Mathf.Sin(kk / 8f * Mathf.PI) + Mathf.Lerp(liftL, liftR, kk / 8f);
            GatherCluster(center + new Vector3(0f, lintelY + 3f + 0.5f * (liftL + liftR), 0f), 10.5f + 0.5f * Mathf.Abs(liftL - liftR), Part.Gate);
            for (int k = 0; k < 9; k++)
            {
                float x = Mathf.Lerp(-9.5f, 9.5f, k / 8f);
                float len = 3.2f;
                for (int nb = k - 1; nb <= k + 1; nb += 2)
                    if (nb >= 0 && nb <= 8 && Mathf.Abs(LintelH(nb) - LintelH(k)) < 1.2f / Flush)
                        len = Mathf.Min(len, 19f / 8f * Flush);
                // The wrap pair's facing lintel ends stand as close as their pillars.
                if ((k == 0 && i == 0) || (k == 8 && i == lastGate))
                {
                    int nbrStep = GateStep(i, n, gateEvery, k == 0 ? -1 : 1);
                    float gapArc = 2f * Mathf.PI * R * nbrStep / n - 19f;
                    len = Mathf.Min(len, Mathf.Abs(gapArc) * Flush);
                }
                var pos = center + tangent * x + new Vector3(0f, LintelH(k), 0f);
                var s = new Vector3(2.3f, 1.2f, len);
                GiveWay(ref pos, lintelRot, ref s, Part.Gate, clustered: true);
                int at = _cachedLays.Count;
                Emit(pos, lintelRot, s, dom);
                _gateIdx[ri][g * 33 + slot++] = Landed(at);
            }
        }

        /// <summary>The terrace deck's undulation at angle <paramref name="a"/> on ring
        /// <paramref name="ring"/> - identical math/args to BuildTerraces, so stairs and
        /// plazas dock onto the exact deterministic deck height instead of floating beside it.</summary>
        float Und(float a, int ring) =>
            22f * Mathf.Sin(a * 3f + ring * 1.7f)
            + 9f * N01(Mathf.Cos(a) * 4f + 9f, ring * 5f, Mathf.Sin(a) * 4f, 3);

        /// <summary>The vertical span (bottom, top) of every deck plate of ring
        /// <paramref name="ri"/> under a footprint at radius <paramref name="r"/> +-
        /// <paramref name="halfR"/> and angle <paramref name="a"/> +- <paramref name="halfArc"/>,
        /// at the thickest jitter.</summary>
        void DeckBands(int ri, float r, float halfR, float a, float halfArc, List<Vector2> into)
        {
            float R = Rings[ri].R;
            int n = _deckY[ri].Length;
            float step = 2f * Mathf.PI / n;
            for (int w = 0; w < 5; w++)
            {
                float rr = R + (w - 2) * LaneStep;
                if (Mathf.Abs(rr - r) >= halfR + 0.5f * 4f * 1.12f) continue;
                float plateHalf = rr * Mathf.Tan(Mathf.PI / n);
                float j = Mathf.Repeat(a, 2f * Mathf.PI) / step;
                int reach = Mathf.CeilToInt((halfArc + plateHalf) / (rr * step)) + 1;
                for (int jj = Mathf.FloorToInt(j) - reach; jj <= Mathf.CeilToInt(j) + reach; jj++)
                {
                    int m = ((jj % n) + n) % n;
                    if (AngleGap(a, m * step) * rr >= halfArc + plateHalf) continue;
                    float mid = _deckY[ri][m] + (w % 2) * 0.5f;
                    into.Add(new Vector2(mid - 0.56f, mid + 0.56f));
                }
            }
        }

        /// <summary>The smallest vertical offset (up or down) that leaves the span
        /// [<paramref name="lo"/>, <paramref name="hi"/>] clear of every band.</summary>
        static float ClearOfBands(float lo, float hi, List<Vector2> bands)
        {
            bool Blocked(float dy)
            {
                foreach (var b in bands)
                    if (lo + dy < b.y && hi + dy > b.x) return true;
                return false;
            }
            if (!Blocked(0f)) return 0f;
            float best = float.PositiveInfinity;
            foreach (var b in bands)
            {
                float up = b.y - lo + 0.01f, down = b.x - hi - 0.01f;
                if (Mathf.Abs(up) < Mathf.Abs(best) && !Blocked(up)) best = up;
                if (Mathf.Abs(down) < Mathf.Abs(best) && !Blocked(down)) best = down;
            }
            return float.IsPositiveInfinity(best) ? 0f : best;
        }

        readonly List<Vector2> _bands = new List<Vector2>(32);

        /// <summary>The top of the deck plates of ring <paramref name="ri"/> (lanes
        /// <paramref name="w0"/>..<paramref name="w1"/>) under a footprint at radius
        /// <paramref name="r"/> +- <paramref name="halfR"/> and angle <paramref name="a"/>
        /// +- <paramref name="halfArc"/> (the odd lanes' 0.5 rise and the thickest jitter
        /// counted), or -inf off the deck.</summary>
        float DeckTopAt(int ri, float r, float halfR, float a, float halfArc, int w0 = 0, int w1 = 4)
        {
            float R = Rings[ri].R;
            float top = float.NegativeInfinity;
            int n = _deckY[ri].Length;
            float step = 2f * Mathf.PI / n;
            for (int w = w0; w <= w1; w++)
            {
                float rr = R + (w - 2) * LaneStep;
                if (Mathf.Abs(rr - r) >= halfR + 0.5f * 4f * 1.12f) continue;
                float plateHalf = rr * Mathf.Tan(Mathf.PI / n);
                float j = Mathf.Repeat(a, 2f * Mathf.PI) / step;
                int reach = Mathf.CeilToInt((halfArc + plateHalf) / (rr * step)) + 1;
                for (int jj = Mathf.FloorToInt(j) - reach; jj <= Mathf.CeilToInt(j) + reach; jj++)
                {
                    int m = ((jj % n) + n) % n;
                    float arc = AngleGap(a, m * step) * rr;
                    if (arc < halfArc + plateHalf)
                        top = Mathf.Max(top, _deckY[ri][m] + (w % 2) * 0.5f + 0.5f * 1.12f);
                }
            }
            return top;
        }

        void Stair(float R1, float y1, float R2, float y2, float a0, int ri, int si)
        {
            Domains dom = ri % 2 != 0 ? Domains.Gold : Domains.Blue;
            Vector3 prevMid = Vector3.zero;
            for (int i = 0; i < 52; i++)
            {
                float t = i / 51f;
                float a = a0 + t * 1.9f;
                float R = Mathf.Lerp(R1, R2, t);
                float y = Mathf.Lerp(y1 + Und(a0, ri), y2 + Und(a0 + 1.9f, ri + 1), t) + 6f * Mathf.Sin(t * Mathf.PI);
                var mid = new Vector3(R * Mathf.Cos(a), y, R * Mathf.Sin(a));
                Quaternion rot = i == 0
                    ? SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up)
                    : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                prevMid = mid;

                // The three treads stand 4.6 apart across the step: each is as wide as that leaves
                // (the jitter took 4.4 to 4.84 and overlapped its neighbour tread).
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float widthCap = 4.6f * Mathf.Abs(Vector3.Dot(radial, rot * Vector3.right)) * Flush;

                // Where the stair runs out over a terrace deck (it docks onto the decks it joins,
                // and its first and last dozen steps cross the deck's lanes), a step that would
                // cut through a plate moves up onto it or down under it, whichever is the smaller
                // offset that clears every plate beneath its footprint. A step clear of the decks
                // never moves.
                var probe = new Vector3(4.84f, 0.99f, 3.3f);
                float vHalf = Support(rot, probe, Vector3.up) / Flush;
                float halfR = 4.6f + Support(rot, probe, radial);
                float halfArc = Support(rot, probe, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
                _bands.Clear();
                for (int rg = ri; rg <= ri + 1; rg++) DeckBands(rg, R, halfR, a, halfArc, _bands);
                float lift = ClearOfBands(y - vHalf, y + vHalf, _bands);

                GatherCluster(mid + new Vector3(0f, lift, 0f), 4.6f + 3f, Part.Stair);
                for (int w = -1; w <= 1; w++)
                {
                    float rr = R + w * 4.6f;
                    var s = Jit(new Vector3(4.4f, 0.9f, 3f), 0.1f);
                    s.x = Mathf.Min(s.x, widthCap);
                    var pos = new Vector3(rr * Mathf.Cos(a), y + lift, rr * Mathf.Sin(a));
                    GiveWay(ref pos, rot, ref s, Part.Stair, null, 0b001, true);   // never into the next tread across
                    int at = _cachedLays.Count;
                    Emit(pos, rot, s, dom);
                    _stairIdx[ri][(si * 52 + i) * 3 + w + 1] = Landed(at);
                }
            }
        }

        // ── Causeways ──────────────────────────────────────────────────────────────────────

        void BuildCauseways()
        {
            // Twin trefoils, opposite chirality, offset heights - the city's woven arteries.
            for (int cw = 0; cw < 2; cw++)
            {
                float scale = cw == 0 ? 120f : 88f;
                float yoff = cw == 0 ? 25f : 60f;
                float chir = cw == 0 ? 1f : -1f;
                Domains deckDom = cw == 0 ? Domains.Gold : Domains.Blue;
                Domains railDom = cw == 0 ? Domains.Blue : Domains.Gold;
                int n = Scaled(cw == 0 ? 1290 : 960);

                var pts = _cwPts[cw] = new Vector3[n];
                _cwIdx[cw] = Fill(new int[n * 5]);
                for (int i = 0; i < n; i++)
                {
                    float t = 2f * Mathf.PI * i / n * chir;
                    pts[i] = new Vector3(
                        (Mathf.Sin(t) + 2f * Mathf.Sin(2f * t)) * scale,
                        -Mathf.Sin(3f * t) * scale * 0.9f + yoff,
                        (Mathf.Cos(t) - 2f * Mathf.Cos(2f * t)) * scale);
                }
                // The rolled frame of every station first, so each lane link can be fitted to
                // where its neighbours along the lane actually are.
                var sides = new Vector3[n];
                var rots = new Quaternion[n];
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = pts[i];
                    Vector3 tangent = (pts[(i + 1) % n] - p).normalized;
                    float theta = 2f * Mathf.PI * i / n;
                    Vector3 side = Vector3.Cross(tangent, Vector3.up);
                    side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
                    Vector3 up = Vector3.Cross(side, tangent).normalized;
                    Vector3 rolledSide = side * Mathf.Cos(theta) + up * Mathf.Sin(theta);
                    Vector3 rolledNormal = Vector3.Cross(tangent, rolledSide).normalized;
                    sides[i] = rolledSide;
                    rots[i] = SpawnPoint.LookRotation(rolledNormal, tangent);
                }

                for (int i = 0; i < n; i++)
                {
                    int ip = (i + n - 1) % n, inx = (i + 1) % n;
                    GatherCluster(pts[i], 2f * 4.9f + 2.5f, Part.Causeway);
                    var frame = new Box(pts[i], rots[i], Vector3.zero);   // the station's axes, shared by its lanes
                    for (int w = -2; w <= 2; w++)
                    {
                        Vector3 q = pts[i] + sides[i] * (w * 4.9f);
                        Vector3 qPrev = pts[ip] + sides[ip] * (w * 4.9f), qNext = pts[inx] + sides[inx] * (w * 4.9f);
                        // Every lane is a chain along the curve: its link is as long as the room to
                        // its neighbours' mid-planes (~2.9 at the station step - the deck's 5.6
                        // overlapped by half; on a bend the inner lane is shorter than the outer).
                        bool rail = w == -2 || w == 2;
                        var s = FitLink(new Box(q, frame, rail ? new Vector3(2.8f, 2f, 2.8f) : Jit(new Vector3(4f, 5.6f, 1.1f), 0.12f)), 1, qPrev, qNext);
                        // Where the trefoil sweeps through a terrace, a stair or a gate, it gives way.
                        GiveWay(ref q, rots[i], ref s, Part.Causeway, null, 0b011, true);   // off the road's face only
                        int at = _cachedLays.Count;
                        Emit(q, rots[i], s, rail ? railDom : deckDom, rail && i % 40 == 0 ? PrismKind.Shielded : PrismKind.Plain);
                        _cwIdx[cw][i * 5 + w + 2] = Landed(at);
                    }
                }
            }
        }

        // ── Arches, aqueducts ──────────────────────────────────────────────────────────────

        void BuildArches()
        {
            var archPts = new Vector3[26];
            var archSize = new Vector3(2.6f, 2.2f, 3.4f);
            var pierSize = new Vector3(3.2f, 3f, 3.2f);
            const float pierStep = 3.2f;
            for (int k = 0; k < 18; k++)
            {
                float a = 2f * Mathf.PI * k / 18f + 0.31f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var basePos = new Vector3(285f * ca, 46f, 285f * sa);
                var tangent = new Vector3(-sa, 0f, ca);
                var outward = new Vector3(ca, 0f, sa);
                Vector3 ArchPoint(int i)
                {
                    float aa = Mathf.PI * i / 25f;
                    return basePos + tangent * (-18f * Mathf.Cos(aa)) + new Vector3(0f, 30f * Mathf.Sin(aa), 0f);
                }
                Quaternion ArchRot(int i) => i == 0
                    ? SpawnPoint.LookRotation(Vector3.up, outward)
                    : SpawnPoint.LookRotation(ArchPoint(i) - ArchPoint(i - 1), outward);

                // The arch's feet sprang from INSIDE its piers (its first and last blocks sat on
                // the piers' bottom blocks, centre on centre, and its legs ran up inside the pier
                // columns) - no size clears that. Each pier steps out along the arch's line just
                // far enough to stand beside the leg, flanking it as a buttress.
                var pierRot = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                float pierTop = 46f + 5 * pierStep + 0.5f * pierSize.y * 1.12f;
                float legReach = 0f;
                for (int i = 0; i < 26; i++)
                {
                    Vector3 p = ArchPoint(i);
                    var r = ArchRot(i);
                    if (p.y - Support(r, archSize, Vector3.up) >= pierTop) continue;
                    legReach = Mathf.Max(legReach, Mathf.Abs(Vector3.Dot(p - basePos, tangent)) + Support(r, archSize, tangent));
                }
                float pierOff = legReach + Support(pierRot, pierSize * 1.12f, tangent) / Flush;

                _archCentre[k] = basePos + new Vector3(0f, 15f, 0f);
                var cwNear = CausewayNear(_archCentre[k], 32f, _cwNear);
                int start = _cachedLays.Count;
                for (int i = 0; i < 26; i++) archPts[i] = ArchPoint(i);
                for (int i = 0; i < 26; i++)
                {
                    if (i % 6 == 0) GatherRun(archPts, i, i + 6, 2.4f, Part.Arch, cwNear);
                    // The ring of voussoirs is a chain: each block as long as the room to its
                    // neighbours' mid-planes (the 3.4 overlapped where the arch turns at its crown).
                    Vector3 p = ArchPoint(i);
                    var rot = ArchRot(i);
                    var s = FitLink(p, rot, archSize, 2, i > 0 ? ArchPoint(i - 1) : p, i < 25 ? ArchPoint(i + 1) : p, i > 0, i < 25);
                    GiveWay(ref p, rot, ref s, Part.Arch, cwNear, 0b100, true);
                    Emit(p, rot, s, Domains.Gold);
                }
                for (int side = -1; side <= 1; side += 2)
                    for (int h = 0; h < 6; h++)
                    {
                        if (h % 3 == 0)
                            GatherCluster(basePos + tangent * (pierOff * side) + new Vector3(0f, (h + 1f) * pierStep, 0f), pierStep + 3f, Part.Arch, cwNear);
                        var s = Jit(pierSize, 0.12f);
                        s.y = Mathf.Min(s.y, pierStep * Flush);   // stacked at the 3.2 step
                        var p = basePos + tangent * (pierOff * side) + new Vector3(0f, h * pierStep, 0f);
                        GiveWay(ref p, pierRot, ref s, Part.Arch, cwNear, 0, true);
                        Emit(p, pierRot, s, Domains.Blue);
                    }
                _arches[k] = Span(Part.Arch, start);
            }
        }

        void BuildAqueducts()
        {
            for (int k = 0; k < 11; k++)
            {
                float a = 2f * Mathf.PI * k / 11f + 0.45f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var prevMid = new Vector3(155f * ca, -90f, 155f * sa);
                int start = _cachedLays.Count;
                var cwNear = CausewayNear(new Vector3(155f * ca, -90f, 155f * sa), new Vector3(385f * ca, 112f, 385f * sa), 18f, _cwNear);
                for (int i = 0; i < 44; i++)
                {
                    float t = i / 43f;
                    float rr = Mathf.Lerp(155f, 385f, t);
                    float y = Mathf.Lerp(-90f, 112f, t) + 10f * Mathf.Sin(t * Mathf.PI * 3f);
                    var mid = new Vector3(rr * ca, y, rr * sa);
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(new Vector3(ca, 0.88f, sa), Vector3.up)
                        : SpawnPoint.LookRotation(mid - prevMid, Vector3.up);
                    prevMid = mid;
                    GatherCluster(mid - new Vector3(0f, 6f, 0f), 13f, Part.Aqueduct, cwNear);
                    // Lapis water-channel rails on gold piers - the aqueduct holds the city's
                    // two-tone band; Jade stays reserved for the gate accents. Where a span runs
                    // through a terrace deck, a stair or a causeway, it gives way there.
                    for (int w = -1; w <= 1; w += 2)
                    {
                        var p = new Vector3(rr * ca - w * 3.4f * sa, y, rr * sa + w * 3.4f * ca);
                        var s = new Vector3(2.6f, 1.4f, 3.6f);
                        GiveWay(ref p, rot, ref s, Part.Aqueduct, cwNear, 0, true);
                        Emit(p, rot, s, Domains.Blue);
                    }
                    if (i % 5 == 2)
                        for (int hh = 0; hh < 4; hh++)
                        {
                            var p = new Vector3(rr * ca, y - 4f - hh * 3.4f, rr * sa);
                            var s = new Vector3(1.8f, 3f, 1.8f);
                            GiveWay(ref p, Quaternion.identity, ref s, Part.Aqueduct, cwNear, 0, true);
                            Emit(p, Quaternion.identity, s, Domains.Gold);
                        }
                }
                _aqueducts[k] = Span(Part.Aqueduct, start);
            }
        }

        // ── Towers and lanterns ────────────────────────────────────────────────────────────

        void BuildTowersAndLanterns()
        {
            var helix = new Vector3[0];
            // Helical minarets.
            for (int k = 0; k < 14; k++)
            {
                float a = k * GoldenAngle * 2.3f;
                float rr = 185f + 170f * Hash01(k * 37 + _noiseSeed);
                var basePos = new Vector3(rr * Mathf.Cos(a), -60f + 120f * Hash01(k * 53), rr * Mathf.Sin(a));
                float h = 70f + 40f * Hash01(k * 11);
                _towerBase[k] = basePos;
                _towerH[k] = h;
                int n = (int)(h / 1.4f);
                Vector3 Helix(int i)
                {
                    float t = i / (float)n;
                    float aa = t * 5.2f * Mathf.PI;
                    float r2 = 9f * (1f - t * 0.6f);
                    return basePos + new Vector3(r2 * Mathf.Cos(aa), h * t, r2 * Mathf.Sin(aa));
                }
                int start = _cachedLays.Count;
                var cwNear = CausewayNear(basePos, basePos + new Vector3(0f, h + 6f, 0f), 15f, _cwNear);
                if (helix.Length < n) helix = new Vector3[n];
                for (int i = 0; i < n; i++) helix[i] = Helix(i);
                for (int i = 0; i < n; i++)
                {
                    if (i % 6 == 0) GatherRun(helix, i, Mathf.Min(i + 6, n), 2.1f, Part.Tower, cwNear);
                    // The helix steps 1.7-2.8 per block (tighter as it narrows): each block as
                    // long as the room to its neighbours' mid-planes (the 3 overlapped). Where
                    // a minaret rises through a deck, stair, gate, causeway, arch or aqueduct,
                    // the minaret gives way.
                    var p = Helix(i);
                    var rot = SpawnPoint.LookRotation(p - (i == 0 ? basePos : Helix(i - 1)), Vector3.up);
                    var s = FitLink(p, rot, new Vector3(2.6f, 1.2f, 3f), 2, i > 0 ? Helix(i - 1) : p, Helix(i + 1), i > 0, i < n - 1);
                    GiveWay(ref p, rot, ref s, Part.Tower, cwNear, 0b100, true);
                    Emit(p, rot, s, k % 2 != 0 ? Domains.Gold : Domains.Blue);
                }
                var fp = basePos + new Vector3(0f, h + 4f, 0f);
                var fs = new Vector3(2.6f, 2.6f, 2.6f);
                GatherCluster(fp, 2.3f, Part.Tower, cwNear);
                GiveWay(ref fp, Quaternion.identity, ref fs, Part.Tower, cwNear, 0, true);
                Emit(fp, Quaternion.identity, fs, Domains.Ruby, PrismKind.Shielded);
                _towers[k] = Span(Part.Tower, start);
            }

            // Catenary lantern strings tied tower-top to tower-top (varying stride so the
            // second lap of k picks different partner pairs). Stride 7 of 14 and the closing
            // stride 11 (= -3) re-tie pairs an earlier lap tied, and every string's end lantern
            // hung on the very same point at its tower's top, so: a string's end lantern hangs
            // one lantern-height under the ends already tied at that tower (a short drop of
            // lanterns down the tower's axis), and a string hung a SECOND time between the same
            // two towers drops its whole length with its ends, so it hangs just under its twin
            // - a double garland - instead of inside it. Where a string crosses another string
            // or a structure, the lantern there gives way.
            var lanternSize = new Vector3(1.1f, 1.1f, 1.6f);
            var stackBottom = new float[14];
            for (int k = 0; k < 30; k++)
            {
                int i1 = k % 14;
                int stride = 3 + 4 * (k / 14);
                int i2 = (i1 + stride) % 14;
                var p1 = _towerBase[i1] + new Vector3(0f, _towerH[i1], 0f);
                var p2 = _towerBase[i2] + new Vector3(0f, _towerH[i2], 0f);
                var rot = SpawnPoint.LookRotation(p2 - p1, Vector3.up);
                float vHalf = Support(rot, lanternSize, Vector3.up);
                bool twin = false;
                for (int e = 0; e < k; e++)
                {
                    int e1 = e % 14, e2 = (e1 + 3 + 4 * (e / 14)) % 14;
                    twin |= (e1 == i1 && e2 == i2) || (e1 == i2 && e2 == i1);
                }
                float Drop(int tower)
                {
                    float centre = stackBottom[tower] > 0f ? (stackBottom[tower] + vHalf) / Flush : 0f;
                    stackBottom[tower] = centre + vHalf;
                    return centre;
                }
                float d1 = Drop(i1), d2 = Drop(i2);
                _strP1[k] = p1;
                _strP2[k] = p2;
                int start = _cachedLays.Count;
                _strings[k] = (start, start);
                var cwNear = CausewayNear(p1 - new Vector3(0f, 16f, 0f), p2 - new Vector3(0f, 16f, 0f), 24f, _cwNear);
                for (int i = 0; i < 34; i++)
                {
                    float t = i / 33f;
                    float sag = 28f * Mathf.Sin(t * Mathf.PI);
                    float drop = twin ? Mathf.Lerp(d1, d2, t) : i == 0 ? d1 : i == 33 ? d2 : 0f;
                    var p = new Vector3(Mathf.Lerp(p1.x, p2.x, t), Mathf.Lerp(p1.y, p2.y, t) - sag - drop, Mathf.Lerp(p1.z, p2.z, t));
                    Domains dom = (i % 4) switch { 0 => Domains.Gold, 1 => Domains.Jade, 2 => Domains.Ruby, _ => Domains.Blue };
                    var s = lanternSize;
                    GiveWay(ref p, rot, ref s, Part.Lantern, cwNear);
                    Emit(p, rot, s, dom);
                }
                _strings[k] = Span(Part.Lantern, start);
            }
        }

        // ── Plazas, atolls ─────────────────────────────────────────────────────────────────

        /// <summary>The index offsets at which a phyllotactic disc's nearest neighbours sit
        /// (consecutive Fibonacci numbers, as on any golden-angle spiral).</summary>
        static readonly int[] PlazaNeighbourOffsets = { 1, 2, 3, 5, 8, 13, 21, 34 };

        void BuildPlazas()
        {
            var tiles = new Vector3[64];
            for (int k = 0; k < 18; k++)
            {
                int ri = k % 4;
                var (R, yc, _) = Rings[ri];
                float a = 2f * Mathf.PI * Hash01(k * 61 + _noiseSeed);
                // Landing pads hover a consistent 8 units beside their road (docked to the
                // deck's undulation). Ruby stays exclusive to the shielded tower finials.
                var cx = new Vector3((R + 16f) * Mathf.Cos(a), yc + Und(a, ri) + 8f, (R + 16f) * Mathf.Sin(a));
                Domains dom = (k % 3) switch { 0 => Domains.Gold, 1 => Domains.Blue, _ => Domains.Jade };
                Vector3 Tile(int i)
                {
                    float aa = i * GoldenAngle;
                    float rr = 14f * Mathf.Sqrt(i / 64f);
                    return cx + new Vector3(rr * Mathf.Cos(aa), 1.5f * Mathf.Sin(rr * 0.8f), rr * Mathf.Sin(aa));
                }
                _plazaCentre[k] = cx;
                for (int i = 0; i < 64; i++) tiles[i] = Tile(i);
                var cwNear = CausewayNear(cx, 17f, _cwNear);
                int start = _cachedLays.Count;
                for (int i = 0; i < 64; i++)
                {
                    float aa = i * GoldenAngle;
                    var p = tiles[i];
                    var rot = SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa)));
                    // Square tiles on a sunflower spiral (~3.1 apart, closer at the centre): each
                    // tile's footprint shrinks to the mid-planes to its spiral neighbours, keeping
                    // its thickness. A pad that overhangs a stair or a deck gives way there.
                    float side = 2.8f;
                    var tile = new Box(p, rot, new Vector3(1f, 1f, 0f));
                    var slab = new Box(p, rot, new Vector3(0f, 0f, 0.6f));
                    foreach (int off in PlazaNeighbourOffsets)
                        for (int sg = -1; sg <= 1; sg += 2)
                        {
                            int j = i + sg * off;
                            if (j < 0 || j >= 64) continue;
                            Vector3 d = tiles[j] - p;
                            float dist = d.magnitude;
                            side = Mathf.Min(side, (0.5f * dist * Flush - slab.Support(d / dist)) / tile.Support(d / dist));
                        }
                    var s = new Vector3(side, side, 0.6f);
                    GiveWay(ref p, rot, ref s, Part.Plaza, cwNear);
                    Emit(p, rot, s, dom);
                }
                _plazas[k] = Span(Part.Plaza, start);
            }
        }

        void BuildAtolls()
        {
            for (int k = 0; k < 15; k++)
            {
                float a = k * GoldenAngle * 2.1f;
                float rr = 170f + 200f * Hash01(k * 37 + _noiseSeed);
                float y = 150f + 120f * Hash01(k * 53 + 3);
                var cx = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                float R = 14f + 11f * Hash01(k * 11);
                Domains dom = k % 2 != 0 ? Domains.Blue : Domains.Gold;
                float LevelR(int L)
                {
                    float t = L / 7f;
                    return R * (1f - t * 0.85f) * (1f + 0.25f * Mathf.Sin(t * 9f + k));
                }
                float levelStep = R * 1.3f / 7f;
                _atollCentre[k] = cx;
                _atollReach[k] = 1.6f * R + 4f;
                var cwNear = CausewayNear(cx, _atollReach[k], _cwNear);
                int start = _cachedLays.Count;
                for (int L = 0; L < 8; L++)
                {
                    float t = L / 7f;
                    float r2 = LevelR(L);
                    int m = Mathf.Max(6, (int)(2f * Mathf.PI * r2 / 3.4f));
                    float tanHalf = Mathf.Tan(Mathf.PI / m);
                    // The levels step down by R * 1.3 / 7: where a neighbouring level's ring
                    // overlaps this one radially, a block is at most one level step tall.
                    bool stacked = (L > 0 && Mathf.Abs(LevelR(L - 1) - r2) < 2f * 1.25f / Flush) ||
                                   (L < 7 && Mathf.Abs(LevelR(L + 1) - r2) < 2f * 1.25f / Flush);
                    GatherCluster(cx + new Vector3(0f, -t * R * 1.3f, 0f), r2 + 2.7f, Part.Atoll, cwNear);
                    for (int i = 0; i < m; i++)
                    {
                        float aa = 2f * Mathf.PI * i / m + L * 0.35f;
                        var radial = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa));
                        // A ring of blocks: tangential length to the bisector planes at the inner
                        // face (as the terrace decks), the jitter kept on the other two sizes.
                        var s = Jit(new Vector3(3f, 2f, 3f), 0.25f);
                        s.y = Mathf.Min(s.y, Mathf.Max(MinSize, 2f * (r2 - MinSize / (2f * tanHalf * Flush))));
                        s.z = Mathf.Min(s.z, 2f * (r2 - 0.5f * s.y) * tanHalf * Flush);
                        if (stacked) s.x = Mathf.Min(s.x, levelStep * Flush);
                        var p = cx + new Vector3(radial.x * r2, -t * R * 1.3f, radial.z * r2);
                        var rot = SpawnPoint.LookRotation(new Vector3(-radial.z, 0f, radial.x), radial);
                        GiveWay(ref p, rot, ref s, Part.Atoll, cwNear, 0, true);
                        Emit(p, rot, s, dom);
                    }
                }
                // The nautilus crown, a log spiral chained link to link (its links start 0.7
                // apart where the spiral is tight, so they shrink to the room there).
                Vector3 Crown(int i)
                {
                    float t = i / 40f;
                    float aa = t * 2.4f * 2f * Mathf.PI;
                    float r2 = 1.6f * Mathf.Exp(0.22f * aa);
                    return cx + new Vector3(r2 * Mathf.Cos(aa), R * 0.3f + t * R * 0.7f, r2 * Mathf.Sin(aa));
                }
                bool InCrown(int i) => 1.6f * Mathf.Exp(0.22f * (i / 40f * 2.4f * 2f * Mathf.PI)) <= R * 1.5f;
                GatherCluster(cx + new Vector3(0f, 0.65f * R, 0f), 1.5f * R + 0.35f * R + 1.5f, Part.Atoll, cwNear);
                Vector3 prevS = cx;
                for (int i = 0; i < 40; i++)
                {
                    if (!InCrown(i)) break;
                    var p = Crown(i);
                    Quaternion rot = i == 0
                        ? SpawnPoint.LookRotation(Vector3.forward, Vector3.up)
                        : SpawnPoint.LookRotation(p - prevS, Vector3.up);
                    var s = FitLink(p, rot, new Vector3(1.7f, 1.1f, 1f), 2, i > 0 ? Crown(i - 1) : p, Crown(i + 1),
                        i > 0, i < 39 && InCrown(i + 1));
                    prevS = p;
                    GiveWay(ref p, rot, ref s, Part.Atoll, cwNear, 0b100, true);
                    Emit(p, rot, s, Domains.Gold);
                }
                _atolls[k] = Span(Part.Atoll, start);
            }
        }
    }
}
