using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The nested gyroid's POPULATION (Docs/ECOSYSTEM.md §58.9): one shared lattice frame per (cell, species) and the
    /// book of which octagon tiles are claimed and which are open. The same model as the gyroid flora's octagon
    /// colony (<see cref="GyroidColonyFrontier"/> / <see cref="GyroidOctagonRegistry"/>, §32.7) - a plant IS one
    /// octagon tile; a COMPLETE plant offers its unclaimed neighbouring tiles; once per cycle the whole population
    /// births ONE plant at a uniformly random open tile - so the colony wanders through the triply periodic
    /// structure instead of filling a ball or a cube.
    ///
    /// <para>What it does NOT need from the gyroid's book: float centre dedupe, misalignment gates, seed-pose
    /// tables. This species' lattice is an exact periodic table (<see cref="NestedGyroidTemplate"/>), so a tile has
    /// an INTEGER address - its period cell and its octagon - and a claim is a set lookup. Every member of a
    /// colony shares one frame, so neighbouring plants' prisms come from one periodic fit and never overlap.</para>
    ///
    /// <para>Bookkeeping only - no MonoBehaviour, no update loop. Living plants drive the cycle from their own grow
    /// ticks, so it survives any individual death and stops with the population. Cleared per cell by
    /// <see cref="Cell"/>'s world reset (the same two sites that clear the gyroid's book).</para>
    /// </summary>
    public sealed class NestedGyroidColony
    {
        /// <summary>One octagon tile of the infinite periodic lattice: a period cell and an octagon index.</summary>
        public readonly struct Tile : System.IEquatable<Tile>
        {
            public readonly int X, Y, Z, Octagon;
            public Tile(int x, int y, int z, int octagon) { X = x; Y = y; Z = z; Octagon = octagon; }
            public bool Equals(Tile o) => X == o.X && Y == o.Y && Z == o.Z && Octagon == o.Octagon;
            public override bool Equals(object obj) => obj is Tile t && Equals(t);
            public override int GetHashCode() => ((X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791)) * 31 + Octagon;
            public override string ToString() => $"({X},{Y},{Z})#{Octagon}";

            /// <summary>The four tiles that share a boundary with this one (the template's measured neighbour table).</summary>
            public IEnumerable<Tile> Neighbors()
            {
                for (int k = 0; k < 4; k++)
                {
                    int e = Octagon * 4 + k;
                    var sh = NestedGyroidTemplate.NeighborShift[e];
                    yield return new Tile(X + (int)sh.X, Y + (int)sh.Y, Z + (int)sh.Z, NestedGyroidTemplate.NeighborOctagon[e]);
                }
            }
        }

        /// <summary>World position of the lattice origin, the frame's rotation, and the world period.</summary>
        public readonly Vector3 Origin;
        public readonly Quaternion Rotation;
        public readonly float Period;

        readonly Dictionary<Tile, NestedGyroidFlora> _claims = new();
        readonly List<Tile> _frontier = new();
        readonly HashSet<Tile> _offered = new();
        float _nextCycleAt;

        NestedGyroidColony(Vector3 origin, Quaternion rotation, float period)
        {
            Origin = origin;
            Rotation = rotation;
            Period = period;
        }

        public int Members => _claims.Count;
        public int OpenTiles => _frontier.Count;

        /// <summary>World pose of a tile's crystal: where a plant owning it is rooted, wearing the colony's rotation.</summary>
        public Vector3 TileWorld(Tile t)
        {
            var c = NestedGyroidLattice.TileCenter(t.X, t.Y, t.Z, t.Octagon, Period);
            return Origin + Rotation * new Vector3(c.X, c.Y, c.Z);
        }

        public bool IsClaimed(Tile t) => _claims.ContainsKey(t);

        public bool TryClaim(Tile t, NestedGyroidFlora plant)
        {
            if (_claims.ContainsKey(t)) return false;
            _claims[t] = plant;
            return true;
        }

        /// <summary>A plant that died or was torn down frees its tile, so the population can regrow into it.</summary>
        public void Release(Tile t, NestedGyroidFlora plant)
        {
            if (_claims.TryGetValue(t, out var owner) && owner == plant) _claims.Remove(t);
            // The tile is open lattice again only if a living member borders it.
            foreach (var n in t.Neighbors())
                if (_claims.ContainsKey(n)) { Offer(t); break; }
        }

        /// <summary>A COMPLETE plant offers every unclaimed neighbouring tile.</summary>
        public void ContributeNeighbors(Tile t)
        {
            foreach (var n in t.Neighbors()) Offer(n);
        }

        void Offer(Tile t)
        {
            if (_claims.ContainsKey(t) || !_offered.Add(t)) return;
            _frontier.Add(t);
        }

        /// <summary>The population clock: true once per <paramref name="period"/> (the first call anchors it).</summary>
        public bool TryBeginCycle(float period, float stagger)
        {
            if (period <= 0f) return false;
            if (_nextCycleAt <= 0f)
            {
                _nextCycleAt = Time.time + period + stagger;
                return false;
            }
            if (Time.time < _nextCycleAt) return false;
            while (_nextCycleAt <= Time.time) _nextCycleAt += period;
            return true;
        }

        /// <summary>
        /// Removes and returns a uniformly random OPEN tile. Claimed tiles are discarded on the way, and so are tiles
        /// <paramref name="accept"/> refuses (a crystal inside a control-zone nucleus): those are not lattice this colony
        /// can ever grow into, so they leave the book for good rather than being redrawn every cycle.
        /// </summary>
        public bool TryPopRandom(out Tile tile, System.Predicate<Tile> accept = null)
        {
            while (_frontier.Count > 0)
            {
                int i = Random.Range(0, _frontier.Count);
                tile = _frontier[i];
                _frontier[i] = _frontier[^1];
                _frontier.RemoveAt(_frontier.Count - 1);
                _offered.Remove(tile);
                if (_claims.ContainsKey(tile)) continue;
                if (accept != null && !accept(tile)) continue;
                return true;
            }
            tile = default;
            return false;
        }

        /// <summary>
        /// A tile a SEEDED plant can join now: a random frontier tile when a member has matured, else a random
        /// unclaimed neighbour of a random member - so a second seed joins the structure while the founder is
        /// still growing instead of founding a rival frame on top of it. False only when no member borders an
        /// acceptable free tile.
        /// </summary>
        public bool TryAnyOpenTile(out Tile tile, System.Predicate<Tile> accept = null)
        {
            if (TryPopRandom(out tile, accept)) return true;
            if (_claims.Count == 0) return false;

            var members = new List<Tile>(_claims.Keys);
            int start = Random.Range(0, members.Count);
            for (int m = 0; m < members.Count; m++)
            {
                var member = members[(start + m) % members.Count];
                int k0 = Random.Range(0, 4), k = 0;
                foreach (var n in member.Neighbors())
                {
                    // Rotate the four neighbours by a random offset (two passes cover every one).
                    if (k++ < k0) continue;
                    if (!_claims.ContainsKey(n) && (accept == null || accept(n))) { tile = n; return true; }
                }
                k = 0;
                foreach (var n in member.Neighbors())
                {
                    if (k++ >= k0) break;
                    if (!_claims.ContainsKey(n) && (accept == null || accept(n))) { tile = n; return true; }
                }
            }
            tile = default;
            return false;
        }

        /// <summary>Puts a popped tile back on the frontier - a birth the spawner refused (a full pool, a cap reached
        /// between the gate and the spawn) is not the lattice's fault.</summary>
        public void Requeue(Tile tile) => Offer(tile);

        // ------------------------------------------------------------------ the books

        static readonly Dictionary<(Cell, FloraConfigurationSO), NestedGyroidColony> s_books = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload() => s_books.Clear();

        /// <summary>The (cell, species) colony, or null when none is alive.</summary>
        public static NestedGyroidColony Find(Cell cell, FloraConfigurationSO species)
        {
            if (!cell || !species) return null;
            if (!s_books.TryGetValue((cell, species), out var c)) return null;
            if (c.Members > 0) return c;
            s_books.Remove((cell, species));   // an extinct colony's frame is forgotten; the next founder lays a new one
            return null;
        }

        /// <summary>Lays a new colony frame. Keyed on (cell, species) when both exist; otherwise the colony is the
        /// plant's own (a hand-placed or toy plant with no lineage grows, coordinates nothing).</summary>
        public static NestedGyroidColony Found(Cell cell, FloraConfigurationSO species, Vector3 origin, Quaternion rotation, float period)
        {
            var c = new NestedGyroidColony(origin, rotation, period);
            if (cell && species) s_books[(cell, species)] = c;
            return c;
        }

        /// <summary>Drops every colony of a cell (its world was reset).</summary>
        public static void Clear(Cell cell)
        {
            if (!cell) return;
            var dead = new List<(Cell, FloraConfigurationSO)>();
            foreach (var key in s_books.Keys) if (key.Item1 == cell) dead.Add(key);
            foreach (var key in dead) s_books.Remove(key);
        }
    }
}
