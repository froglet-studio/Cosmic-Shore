using System.Threading.Tasks;

namespace CosmicShore.Integrations.Instrumentation.Interfaces
{
    interface IAnalyzable
    {
        void InitSDK();
    }
    
    interface IStoreAnalyzable : IAnalyzable
    {
        void LogEventPurchaseCaptain(string captainName);
        void LogEventPurchaseArcadeGame(string arcadeGameName);
        void LogEventPurchaseMission(string missionName);
        Task LogEventWatchAd();
        Task LogEventRedeemDailyReward();
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

    interface IDeviceAnalyzale : IAnalyzable
    {
        void LogEventAppOpen();
        void LogEventAppClose();
    }
}