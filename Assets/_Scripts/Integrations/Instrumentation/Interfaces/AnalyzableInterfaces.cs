using System;
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
        void LogEventStartDailyChallenge(string gameType, int intensity, string shipType, string captainName);
        void LogEventCompleteDailyChallenge(string gameType, int intensity, string shipType, string captainName, int score, int reward, DateTime playTime);
    }

    interface IPlayerAnalyzable : IAnalyzable
    {
        void LogEventUpgradeCaptain(string captainName, int captainLevel, string shipType);
    }

    interface IArcadeAnalyzable : IAnalyzable
    {
        void LogEventStartArcadeGame(string gameType, int intensity, string shipType);
        void LogEventCompleteArcadeGame(
            string gameType, int intensity, string shipType, int score, int reward, DateTime playTime);
    }

    interface IMissionAnalyzable : IAnalyzable
    {
        void LogEventStartMission(
            string gameType, int intensity, string shipType, string captainName, int numberOfPlayers);
        void LogEventCompleteMission(
            string gameType, int intensity, string shipType, string captainName, 
            int numberOfPlayers, int score, int reward, DateTime playTime);
    }

    interface ITrainingAnalyzable : IAnalyzable
    {
        void LogEventStartTraining(string gameType, int intensity, string shipType);
        void LogEventCompleteTraining(string gameType, int intensity, string shipType, int score, int reward, DateTime playTime);
    }

    interface IDeviceAnalyzale : IAnalyzable
    {
        void LogEventAppOpen();
        void LogEventAppClose();
    }
}