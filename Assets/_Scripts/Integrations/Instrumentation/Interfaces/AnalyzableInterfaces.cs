namespace CosmicShore.Integrations.Instrumentation.Interfaces
{
    interface IAnalyzable
    {
        void InitSDK();
    }
    
    interface IStoreAnalyzable : IAnalyzable
    {
        void LogEventPurchaseCaptain();
        void LogEventPurchaseArcadeGame();
        void LogEventPurchaseMission();
        void LogEventWatchAd();
        void LogEventRedeemDailyReward();
    }

    interface IDailyChallengeAnalyzable : IAnalyzable
    {
        void LogEventStartDailyChallenge();
        void LogEventCompleteDailyChallenge();
    }

    interface IPlayerAnalyzable : IAnalyzable
    {
        void LogEventUpgradeCaptain();
    }

    interface IArcadeAnalyzable : IAnalyzable
    {
        void LogEventStartArcadeGame();
        void LogEventCompleteArcadeGame();
    }

    interface IMissionAnalyzable : IAnalyzable
    {
        void LogEventStartMission();
        void LogEventCompleteMission();
    }

    interface ITrainingAnalyzable : IAnalyzable
    {
        void LogEventStartTraining();
        void LogEventCompleteTraining();
    }
}