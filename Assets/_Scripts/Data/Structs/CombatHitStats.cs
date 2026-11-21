using System;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    // Lives in the CosmicShore.Data ASSEMBLY (a dependency-free payload) but keeps its
    // CosmicShore.Gameplay namespace, so no consumer changes. Docs/ASSEMBLY_SPLIT.md, Phase 2 step 1.
    /// <summary>
    /// One landed vessel-vs-vessel hit: who fired it, who wore it, and with what.
    /// <see cref="VictimName"/> is carried for diagnostics and for the toast feed - the score
    /// only ever moves for the SHOOTER, and only <c>StatsManager</c> decides that.
    /// </summary>
    [Serializable]
    public struct CombatHitStats
    {
        public string ShooterName;
        public string VictimName;
        public CombatHitClass HitClass;

        /// <summary>
        /// What this hit UPGRADES, as a missile proximity rank (0 = nothing; it is a fresh hit).
        ///
        /// <para>One rocket lands in up to three ranked classes against one victim - shockwave,
        /// blast, direct - and pays only its BEST. <c>VesselCombatHitLatch</c> admits a closer
        /// tier inside an open window and reports the tier it replaces here, so
        /// <c>CombatHitScoring</c> can credit the DIFFERENCE instead of the whole tier again and
        /// leave the raw hit COUNT alone. Without it an upgrade would either double-count (pay
        /// both tiers) or be impossible (first tier wins), and on an ordinary proximity kill the
        /// tier that lands first is the cheapest one.</para>
        /// </summary>
        public int SupersededRank;
    }
}
