namespace CosmicShore.Core
{
    /// <summary>
    /// Single source of truth for all UGS Cloud Save keys and analytics event names.
    /// Each key maps to one ICloudDataRepository in the UGSDataService.
    /// </summary>
    public static class UGSKeys
    {
        // ── Cloud Save ──
        // Naming standard: SCREAMING_SNAKE_CASE keys, PascalCase JSON fields, epoch-ms UTC
        // timestamps, "{Mode}:{Intensity}" composite dictionary keys, and a SchemaVersion on
        // every root model. See Docs/Analytics/DATA_ARCHITECTURE.md §2.
        //
        // VESSEL_STATS was merged into HANGAR_DATA (one record per vessel) and
        // PLAYER_STATS_PROFILE became MODE_STATS (one uniform record per mode:intensity).
        public const string PlayerProfile         = "PLAYER_PROFILE";
        public const string ModeStats             = "MODE_STATS";
        public const string GameModeProgression   = "GAME_MODE_PROGRESSION";
        public const string HangarData            = "HANGAR_DATA";
        public const string CaptainProgress       = "CAPTAIN_PROGRESS";
        public const string TrainingProgress      = "TRAINING_PROGRESS";
        public const string DailyChallenge        = "DAILY_CHALLENGE";
        public const string EpisodeProgress       = "EPISODE_PROGRESS";
        public const string PlayerSettings        = "PLAYER_SETTINGS";
        public const string Squad                 = "SQUAD_DATA";
        public const string Loadout               = "LOADOUT_DATA";
        public const string QuestGraph            = "QUEST_GRAPH_PROGRESS";

        // ── Analytics Events ──
        // Every event (and its parameters) must also be declared in the UGS
        // dashboard Event Manager, or the backend silently discards it.

        // Phase 1 - foundation
        public const string EventPlayAgain     = "play_again_pressed";
        public const string EventGameStarted   = "game_started";
        public const string EventGameCompleted = "game_completed";
        public const string EventSessionEnded  = "session_ended";
        public const string EventUiAction      = "ui_action";

        // Phase 2 - activation
        public const string EventGameFirstLaunched = "game_first_launched";
        public const string EventMenuReady         = "menu_ready";
        public const string EventFreestyleEntered  = "freestyle_entered";
        public const string EventModeUnlocked      = "mode_unlocked";
        public const string EventIntensityUnlocked = "intensity_unlocked";
        public const string EventSettingChanged    = "setting_changed";

        // Phase 2 - economy
        public const string EventCrystalsEarned        = "crystals_earned";
        public const string EventCrystalsSpent         = "crystals_spent";
        public const string EventCrystalSpendBlocked   = "crystal_spend_blocked";
        public const string EventVesselUnlocked        = "vessel_unlocked";
        public const string EventCrystalBalanceSnapshot = "crystal_balance_snapshot";

        // Phase 2 - social
        public const string EventFriendRequestSent     = "friend_request_sent";
        public const string EventFriendRequestReceived = "friend_request_received";
        public const string EventFriendAdded           = "friend_added";
        public const string EventPartyInviteSent       = "party_invite_sent";
        public const string EventPartyInviteReceived   = "party_invite_received";
        public const string EventPartyJoined           = "party_joined";
        public const string EventMinigameFavorited     = "minigame_favorited";
        public const string EventShareTriggered        = "share_triggered";

        // Phase 2 - retention
        public const string EventQuestCompleted   = "quest_completed";
        public const string EventRepeatedGameFail = "repeated_game_fail";

        // Phase 3 - observability
        public const string EventCloudSaveFailed = "cloud_save_failed";
    }
}
