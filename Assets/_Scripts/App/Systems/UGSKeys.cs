namespace CosmicShore.App.Systems
{
    /// <summary>
    /// Single source of truth for all UGS Cloud Save keys and analytics event names.
    /// </summary>
    public static class UGSKeys
    {
        // ── Cloud Save ──
        public const string PlayerProfile    = "player_profile";
        public const string PlayerStatsProfile = "PLAYER_STATS_PROFILE";

        // ── Analytics Events ──
        public const string EventPlayAgain = "play_again_pressed";
    }
}
