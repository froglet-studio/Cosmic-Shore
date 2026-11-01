using System;

namespace CosmicShore.Gameplay
{
    /// <summary>What a bee is doing on its current trip (<see cref="BeeMath.Decide"/>).</summary>
    public enum BeeIntent
    {
        /// <summary>Visiting flowers: each visit hastens the plant's own growth (<see cref="Flora.Hasten"/>).</summary>
        Pollinate = 0,
        /// <summary>An opposing creature is near the hive: fly at it and sting a body prism off it (a transfer of its mass).</summary>
        Raid = 1,
        /// <summary>Carrying mass home: the hive's store takes it and the comb grows from it.</summary>
        Return = 2,
    }

    /// <summary>
    /// THE BEES' rules (Garrett, 2026-10-31; Docs/ECOSYSTEM.md §66), engine-free so they are held by edit-mode tests:
    /// the comb's shape, the hive's honey store (a mass LEDGER: what the bees bring home is exactly what the comb is
    /// built from, so nothing is minted), how many bees a comb keeps, and what a bee does next.
    /// </summary>
    public static class BeeMath
    {
        // ------------------------------------------------------------------ the comb

        static readonly int[][] HexDirs = { new[] { 1, 0 }, new[] { 1, -1 }, new[] { 0, -1 }, new[] { -1, 0 }, new[] { -1, 1 }, new[] { 0, 1 } };

        /// <summary>Cells in a hex disc of <paramref name="rings"/> rings round a centre cell (0 rings = 1 cell).</summary>
        public static int CellsInRings(int rings) => rings < 0 ? 0 : 1 + 3 * rings * (rings + 1);

        /// <summary>
        /// Comb cell <paramref name="k"/> of one comb as axial hex coordinates, laid as a spiral: the centre, then ring 1
        /// all the way round, then ring 2. So a comb grows outward one cell at a time and is a whole hexagon after every
        /// <see cref="CellsInRings"/> cells - the shape a real comb is drawn out in.
        /// </summary>
        public static void SpiralCell(int k, out int q, out int r)
        {
            q = 0; r = 0;
            if (k <= 0) return;
            int ring = 1;
            while (CellsInRings(ring) <= k) ring++;
            int i = k - CellsInRings(ring - 1);   // index round this ring, 0 .. 6*ring-1
            // start at the ring's SW corner (ring steps along direction 4), then walk the six sides
            q = HexDirs[4][0] * ring; r = HexDirs[4][1] * ring;
            int side = i / ring, step = i % ring;
            for (int s = 0; s < side; s++) { q += HexDirs[s][0] * ring; r += HexDirs[s][1] * ring; }
            q += HexDirs[side][0] * step; r += HexDirs[side][1] * step;
        }

        /// <summary>
        /// Where cell <paramref name="k"/> of the whole hive sits, in the hive's local frame: the hive is a stack of
        /// combs (<paramref name="cellsPerComb"/> each, spaced <paramref name="combSpacing"/> apart along z, alternately
        /// in front of and behind the first), each comb a hex disc in x-y with cells <paramref name="cellSpacing"/> apart.
        /// A cell's prism lies along z, so the comb reads as a slab of tubes.
        /// </summary>
        public static void CellPosition(int k, int cellsPerComb, float cellSpacing, float combSpacing, out float x, out float y, out float z)
        {
            int per = Math.Max(1, cellsPerComb);
            int comb = k / per, inComb = k % per;
            SpiralCell(inComb, out int q, out int r);
            x = cellSpacing * (q + r * 0.5f);
            y = cellSpacing * (r * 0.8660254f);
            // combs 0, 1, 2, 3... sit at 0, +1, -1, +2, -2 ... comb spacings
            int slot = (comb + 1) / 2;
            z = combSpacing * (comb % 2 == 1 ? slot : -slot);
        }

        // ------------------------------------------------------------------ the honey store

        /// <summary>
        /// The hive's store of brought-home mass, in prism volume. Deposits come only from bees (each a body prism
        /// stung off an opposing creature); a comb cell is built only by spending its own volume from here. So, at any
        /// moment, <c>Deposited == Spent + Stored</c>: the comb's growth past its seed is exactly what was carried in.
        /// </summary>
        public struct HoneyStore
        {
            public float Stored, Deposited, Spent;

            public void Deposit(float volume)
            {
                if (!(volume > 0f)) return;
                Stored += volume;
                Deposited += volume;
            }

            /// <summary>Spend <paramref name="cost"/> for one comb cell; false (and nothing spent) when the store is short.</summary>
            public bool TrySpend(float cost)
            {
                if (!(cost > 0f) || Stored + 1e-4f < cost) return false;
                Stored = Math.Max(0f, Stored - cost);
                Spent += cost;
                return true;
            }

            /// <summary>Give back a spend whose cell could not be laid (the pool was empty): the ledger stays whole.</summary>
            public void Refund(float cost)
            {
                if (!(cost > 0f)) return;
                cost = Math.Min(cost, Spent);
                Spent -= cost;
                Stored += cost;
            }
        }

        /// <summary>
        /// The store kept PER DOMAIN (Garrett, 2026-11-01: "build a structure made from all three"). Mass carried home keeps
        /// the domain it was stung from, and a comb cell is paid for by ONE domain's mass and laid in that domain - so the
        /// comb is built from the three domains in exactly the proportion its bees brought them home, and mass is conserved
        /// per domain as well as in total. <see cref="TrySpend"/> pays from the domain holding the most that can pay alone
        /// (ties: the lower domain value), so the comb grows in the colours it has most of.
        /// </summary>
        public sealed class HoneyLedger
        {
            public const int Slots = 5;   // indexed by the Domains value (Jade 1, Ruby 2, Blue 3, Gold 4)
            readonly HoneyStore[] _by = new HoneyStore[Slots];

            public float Stored { get { float s = 0f; for (int d = 0; d < Slots; d++) s += _by[d].Stored; return s; } }
            public float Deposited { get { float s = 0f; for (int d = 0; d < Slots; d++) s += _by[d].Deposited; return s; } }
            public float Spent { get { float s = 0f; for (int d = 0; d < Slots; d++) s += _by[d].Spent; return s; } }
            public HoneyStore Of(int domain) => domain >= 0 && domain < Slots ? _by[domain] : default;

            public void Deposit(int domain, float volume)
            {
                if (domain < 0 || domain >= Slots) return;
                _by[domain].Deposit(volume);
            }

            /// <summary>Pay one cell's <paramref name="cost"/> from one domain; false (nothing spent) when no domain can pay alone.</summary>
            public bool TrySpend(float cost, out int domain)
            {
                domain = -1;
                float best = -1f;
                for (int d = 0; d < Slots; d++)
                    if (_by[d].Stored + 1e-4f >= cost && _by[d].Stored > best) { best = _by[d].Stored; domain = d; }
                if (domain < 0 || !_by[domain].TrySpend(cost)) { domain = -1; return false; }
                return true;
            }

            public void Refund(int domain, float cost)
            {
                if (domain < 0 || domain >= Slots) return;
                _by[domain].Refund(cost);
            }
        }

        // ------------------------------------------------------------------ the colony

        /// <summary>
        /// How many bees a comb of <paramref name="combCells"/> cells keeps: <paramref name="minBees"/> for the seed comb,
        /// one more for every <paramref name="cellsPerBee"/> cells past the seed, never above <paramref name="maxBees"/>.
        /// A hive raises bees as its comb grows; it is the CELL's production freeze, not this, that stops a hive at Frenzy.
        /// </summary>
        public static int BeesFor(int combCells, int seedCells, int cellsPerBee, int minBees, int maxBees)
        {
            if (maxBees <= 0) return 0;
            int extra = cellsPerBee > 0 ? Math.Max(0, combCells - seedCells) / cellsPerBee : 0;
            return Math.Max(0, Math.Min(maxBees, minBees + extra));
        }

        /// <summary>
        /// What a bee does next. A full load always goes home. An intruder at the hive is raided (defending the hive is
        /// how a hive gets fed). Otherwise a bee that carries something goes home once it has made its visits, and an
        /// empty bee pollinates.
        /// </summary>
        public static BeeIntent Decide(float carried, float carryCapacity, bool intruder, int visits, int visitsPerTrip)
        {
            if (carryCapacity > 0f && carried >= carryCapacity) return BeeIntent.Return;
            if (intruder) return BeeIntent.Raid;
            if (carried > 0f && visits >= Math.Max(1, visitsPerTrip)) return BeeIntent.Return;
            return BeeIntent.Pollinate;
        }

        /// <summary>
        /// A deterministic pick among <paramref name="count"/> candidates for bee <paramref name="salt"/> on visit
        /// <paramref name="visit"/>: neighbouring bees fan out over different flowers instead of all landing on the
        /// nearest one, and a bee does not land on the same flower twice running.
        /// </summary>
        public static int PickIndex(int count, int salt, int visit)
        {
            if (count <= 0) return -1;
            unchecked
            {
                uint h = (uint)salt * 2654435761u ^ (uint)visit * 40503u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (int)(h % (uint)count);
            }
        }
    }
}
