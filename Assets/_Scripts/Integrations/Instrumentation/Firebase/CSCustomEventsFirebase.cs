namespace CosmicShore.Integrations.Instrumentation.Firebase
{
    public class CSCustomEventsFirebase
    {
        #region Arcade Data Collector

        public const string StartArcadeGame = "cs_start_arcade_game";
        
        public const string CompleteArcadeGame = "cs_complete_arcade_game";
        
        #endregion


        #region Daily Challenge Data Collector

        public const string StartDailyChallenge = "cs_start_daily_challenge";
        
        public const string CompleteDailyChallenge = "cs_complete_daily_challenge";

        #endregion
        
        
        #region Device Data Collector

        public const string AppOpen = "cs_app_open";
        
        public const string AppClose = "cs_app_close";

        #endregion

        
        #region Mission Data Collector

        public const string StartMission = "cs_start_mission";
        
        public const string CompleteMission = "cs_complete_mission";

        #endregion


        #region Player Data Collector

        public const string UpgradeCaptain = "cs_upgrade_captain";

        #endregion
        
        
        #region Store Data Collector

        public const string PurchaseCaptain = "cs_purchase_captain";
        
        public const string PurchaseArcadeGame = "cs_purchase_arcade";
        
        public const string PurchaseMission = "cs_purchase_mission";
        
        public const string WatchAdd = "cs_watch_add";
        
        public const string ClaimDailyReward = "cs_claim_daily_reward";

        #endregion
        
        
        #region Training Game Data Collector
        
        public const string StartTraining = "cs_start_training";
        
        public const string CompleteTraining = "cs_complete_training";
        
        #endregion
        
    }
}