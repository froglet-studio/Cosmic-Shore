namespace CosmicShore.Data
{
    /// <summary>
    /// Which population a leaderboard view is ranking. One tab per member.
    ///
    /// <para><b>These are three different QUESTIONS asked of UGS, not three filters over one
    /// answer.</b> World is a page of the board. Friends is a lookup of specific player ids on the
    /// same board. Regional is a DIFFERENT board — Unity Gaming Services has no notion of a
    /// player's region, so "regional" can only mean "a board only that region submits to"
    /// (<see cref="CosmicShore.Core.WeeklyChallengeRegion"/>). Filtering a world page by region
    /// client-side looks equivalent and is not: you would only ever see the regional players who
    /// were already in the global top N, so a whole region could show an empty board.</para>
    ///
    /// <para>Values are pinned — a scope can be persisted as the tab the player last had open.</para>
    /// </summary>
    public enum LeaderboardScope
    {
        /// <summary>Everyone, fastest first. The default and the only one that always exists.</summary>
        World = 0,

        /// <summary>The player's own region's board. Off unless that region has an authored id.</summary>
        Regional = 1,

        /// <summary>The signed-in player's friends, ranked among themselves.</summary>
        Friends = 2,
    }
}
