namespace CosmicShore.Core
{
    /// <summary>
    /// Single source of truth for all UGS Cloud Save keys and analytics event names.
    /// Each key maps to one ICloudDataRepository in the UGSDataService.
    /// </summary>
    public static class UGSKeys
    {
        // ── Cloud Save: Existing Keys ──
        public const string PlayerProfile         = "player_profile";
        public const string PlayerStatsProfile    = "PLAYER_STATS_PROFILE";
        public const string VesselStats           = "VESSEL_STATS";
        public const string GameModeProgression   = "GAME_MODE_PROGRESSION";

        // ── Cloud Save: New Keys ──
        public const string HangarData            = "HANGAR_DATA";
        public const string CaptainProgress       = "CAPTAIN_PROGRESS";
        public const string TrainingProgress      = "TRAINING_PROGRESS";
        public const string DailyChallenge        = "DAILY_CHALLENGE";
        public const string EpisodeProgress       = "EPISODE_PROGRESS";
        public const string PlayerSettings        = "PLAYER_SETTINGS";

        // ── Analytics Events ──
        // Every event (and its parameters) must also be declared in the UGS
        // dashboard Event Manager, or the backend silently discards it.
        public const string EventPlayAgain     = "play_again_pressed";
        public const string EventGameStarted   = "game_started";
        public const string EventGameCompleted = "game_completed";
        public const string EventSessionEnded  = "session_ended";
        public const string EventUiAction      = "ui_action";
        public const string EventAdImpression  = "ad_impression";
    }
}
