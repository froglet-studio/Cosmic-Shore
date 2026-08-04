using System;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Payload for one periodic fauna spawn-cycle tick (a "wave") - raised by the
    /// cell's life spawner every fixed BaseFaunaSpawnTime period, per fauna species
    /// loop. Carries the wave's spawn color and whether that color is a genuine
    /// NUCLEUS claim (node control read from environment volume inside the nucleus)
    /// rather than a fallback spawn color, so scoring systems (Brood Rush) can award
    /// the wave to the controlling team without re-deriving cell state.
    /// </summary>
    [Serializable]
    public struct FaunaWaveData
    {
        /// <summary>ID of the cell whose spawner ticked.</summary>
        public int CellId;

        /// <summary>The wave's spawn color (the cell's controlling domain at tick time).</summary>
        public Domains Domain;

        /// <summary>Fauna actually spawned this tick (0 when the population is at cap or prey-gated).</summary>
        public int SpawnedCount;

        /// <summary>
        /// True when <see cref="Domain"/> is a real nucleus claim
        /// (<c>Cell.TryGetNucleusClaim</c>) - false for fallback spawn colors and
        /// for cells without a nucleus control zone.
        /// </summary>
        public bool NucleusControlled;

        public FaunaWaveData(int cellId, Domains domain, int spawnedCount, bool nucleusControlled)
        {
            CellId = cellId;
            Domain = domain;
            SpawnedCount = spawnedCount;
            NucleusControlled = nucleusControlled;
        }
    }
}
