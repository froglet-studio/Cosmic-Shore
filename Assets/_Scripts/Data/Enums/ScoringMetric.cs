namespace CosmicShore.Data
{
    /// <summary>
    /// The single per-player stat a scoring rule aggregates by Domain. One metric drives a
    /// mode's HUD readout, its "remaining" counter, its end condition, and its scoreboard
    /// secondary line - so the number players watch can never diverge from the number that
    /// ends the game. Selected per mode on the mode's <c>ScoringRuleSO</c> asset.
    /// Always assign explicit values to avoid Unity serialization drift.
    /// </summary>
    public enum ScoringMetric
    {
        Crystals = 0,
        OmniCrystals = 1,
        ElementalCrystals = 2,
        Jousts = 3,
        Goals = 4,
        // Rampage and PeelTheCage: hostile prisms destroyed (reads
        // IRoundStats.HostilePrismsDestroyed). "Hostile" = everything except your own/your
        // teammates' PLAYER-LAID mass: other domains' trails, plus ALL environment mass
        // (flora/fauna carry non-roster owner names, so StatsManager classifies them hostile
        // regardless of color). Laying-then-shattering your own trail is worthless by
        // construction - trails ARE rostered, so the domain check filters them.
        PrismsDestroyed = 5,
        // Available, currently unused by any shipping mode: prisms you have STANDING (reads
        // IRoundStats.PrismsRemaining - incremented when you lay a prism, decremented when
        // ANYTHING destroys it, including a rival's ram and a fauna's bite). A LIVE stock, not
        // a cumulative total. The distinction matters for any future mode that wants the
        // ecology to move the score directly: under a cumulative counter nothing that eats
        // your mass can set you back, whereas under this one the swarm chewing your trail
        // un-scores you. PeelTheCage was authored on this metric and deliberately moved back to
        // PrismsDestroyed (see PeelTheCageScoringRuleSO's header for the trade).
        PrismsRemaining = 6,
        // Wildlife Liberation: fauna a player has KILLED (reads IRoundStats.LifeformsKilled).
        // Fed by CellRuntimeDataSO.OnFaunaKilled -> StatsManager.LifeformKilled, which only
        // credits attributed player kills - a creature that starves or is eaten by a predator
        // scores for nobody. Cumulative, so it is monotonic per player like every other race
        // metric; it is the one metric whose source is the ECOLOGY rather than prisms or
        // crystals, which is exactly the point of the mode that uses it.
        LifeformsKilled = 7,
        // Dog Fight: weighted gunnery score (reads IRoundStats.CombatPoints). Accumulated
        // server-side at the moment of each landed hit from the mode's own
        // ScoringRuleSO.PointsForCombatHit - a bullet is worth 1 and a missile 50 - so the
        // weighting lives in the mode's asset and this stays a plain cumulative int like every
        // other race metric. The two raw counts behind it (BulletHitsLanded /
        // MissileHitsLanded) are kept separately for the scoreboard breakdown and are NOT the
        // metric: 300 bullets and 6 rockets are the same score, and the board should say so.
        // The one metric whose source is vessel-vs-vessel combat rather than prisms, crystals,
        // or the ecology.
        CombatPoints = 8,
        // Hijack: prisms a player has STOLEN - flipped from another domain to their own
        // (reads IRoundStats.PrismStolen, which StatsManager.PrismStolen and the
        // Player.ReportPrismStolen_ServerRpc round-trip have been accumulating in every mode
        // since long before a mode read it). Cumulative and monotonic like the other race
        // metrics, and deliberately a COUNT rather than VolumeStolen: a friendly ride GROWS a
        // prism, so a volume metric would quietly pay a re-stealer more than the pilot who
        // took it first, and a goal row cannot say a volume. It is the first metric whose
        // source is OWNERSHIP rather than destruction - nothing is removed from the arena to
        // score it, which is what lets a whole mode be played inside the conserved-mass law
        // with no food web and no despawn.
        PrismsStolen = 9,
    }
}
