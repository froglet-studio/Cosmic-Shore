// Tandava's SEVERING (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.11) - pure C#: no UnityEngine, System.Numerics only,
// so the SAME file compiles and RUNS in Tools/Build/swarm_core_harness.
//
// A swarm body is a cloud of members held in shape by its plan's wells. When the pilots cut clean through it - kill every
// member across a band of the body - what is left is two clouds with a gap between them, for the moment before the wound
// buds shut. This finds that moment: the live members, linked to every neighbour within a link distance (single linkage),
// fall into PIECES. The largest piece is the body; the next largest, if it is big enough to live on its own and small
// enough to be a piece rather than the body, is SEVERED (TandavaDirectorCore decides whether a sever may happen at all).
// Single members and small knots that drift off a body - a feed pose's orbiting guards, a straggler - are never pieces.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    public sealed class TandavaSever
    {
        int[] _parent = Array.Empty<int>(), _size = Array.Empty<int>();
        readonly Dictionary<long, int> _cellHead = new();
        int[] _next = Array.Empty<int>();

        /// <summary>The members (indices into <paramref name="pos"/>) of the piece a cut parted from the body, into
        /// <paramref name="piece"/> (cleared first). Returns how many: 0 when the body is whole, or when its second piece is
        /// smaller than <paramref name="minMembers"/> or larger than <paramref name="maxMembers"/>. <paramref name="link"/>
        /// is in the units of <paramref name="pos"/>: two live members closer than it are one piece.</summary>
        public int FindPiece(IReadOnlyList<Vector3> pos, IReadOnlyList<bool> alive, int count, float link,
                             int minMembers, int maxMembers, List<int> piece)
        {
            piece.Clear();
            if (count <= 1 || link <= 0f) return 0;
            Ensure(count);
            float inv = 1f / link, link2 = link * link;
            _cellHead.Clear();
            for (int i = 0; i < count; i++)
            {
                _parent[i] = i; _size[i] = 1; _next[i] = -1;
                if (!alive[i]) { _size[i] = 0; continue; }
                long key = Key(pos[i], inv);
                _next[i] = _cellHead.TryGetValue(key, out int head) ? head : -1;
                _cellHead[key] = i;
            }
            // link every live member to every live neighbour in its own and the 26 neighbouring cells
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                var p = pos[i];
                int cx = (int)MathF.Floor(p.X * inv), cy = (int)MathF.Floor(p.Y * inv), cz = (int)MathF.Floor(p.Z * inv);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!_cellHead.TryGetValue(Pack(cx + dx, cy + dy, cz + dz), out int j)) continue;
                    for (; j >= 0; j = _next[j])
                        if (j > i && Vector3.DistanceSquared(p, pos[j]) <= link2) Union(i, j);
                }
            }
            // the two biggest pieces
            int big = -1, second = -1;
            for (int i = 0; i < count; i++)
            {
                if (!alive[i] || Find(i) != i) continue;
                if (big < 0 || _size[i] > _size[big]) { second = big; big = i; }
                else if (second < 0 || _size[i] > _size[second]) second = i;
            }
            if (second < 0 || _size[second] < minMembers || _size[second] > maxMembers) return 0;
            for (int i = 0; i < count; i++) if (alive[i] && Find(i) == second) piece.Add(i);
            return piece.Count;
        }

        void Ensure(int n)
        {
            if (_parent.Length >= n) return;
            _parent = new int[n]; _size = new int[n]; _next = new int[n];
        }

        int Find(int i)
        {
            while (_parent[i] != i) { _parent[i] = _parent[_parent[i]]; i = _parent[i]; }
            return i;
        }

        void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (_size[a] < _size[b]) (a, b) = (b, a);
            _parent[b] = a; _size[a] += _size[b];
        }

        static long Key(Vector3 p, float inv) => Pack((int)MathF.Floor(p.X * inv), (int)MathF.Floor(p.Y * inv), (int)MathF.Floor(p.Z * inv));

        static long Pack(int x, int y, int z) => ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);
    }
}
