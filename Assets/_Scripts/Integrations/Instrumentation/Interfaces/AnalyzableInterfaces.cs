namespace CosmicShore.Integrations.Instrumentation.Interfaces
{
    interface IAnalyzable
    {
        void InitSDK();
    }
    
    interface IStoreAnalyzable : IAnalyzable
    {
        void PurchaseCaptain();
        void PurchaseArcadeGame();
        void PurchaseMission();
        void WatchAd();
        void RedeemDailyReward();
    }

    interface IDailyChallengeAnalyzable : IAnalyzable
    {
        void StartDailyChallenge();
        void CompleteDailyChallenge();
    }

    interface IPlayerAnalyzable : IAnalyzable
    {
        void UpgradeCaptain();
    }

    interface IArcadeAnalyzable : IAnalyzable
    {
        void StartArcadeGame();
        void CompleteArcadeGame();
    }

    interface IMissionAnalyzable : IAnalyzable
    {
        void StartMission();
        void CompleteMission();
    }

    interface ITrainingAnalyzable : IAnalyzable
    {
        void StartTraining();
        void CompleteTraining();
    }
}