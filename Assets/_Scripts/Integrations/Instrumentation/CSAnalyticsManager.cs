using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.DataCollectors;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using CosmicShore.Utility.Singleton;

namespace CosmicShore.Integrations.Instrumentation
{
    
    public class CSAnalyticsManager : SingletonPersistent<CSAnalyticsManager>, 
        IStoreAnalyzable, 
        IDailyChallengeAnalyzable, 
        IPlayerAnalyzable, 
        IArcadeAnalyzable, 
        IMissionAnalyzable, 
        ITrainingAnalyzable,
        IDeviceAnalyzale
    {
        private readonly CSUtilitiesFirebase _utilitiesFirebase = new CSUtilitiesFirebase(); // This one doesn't have a specific interface yet
        private readonly IStoreAnalyzable _storeDataCollector = new CSStoreDataCollector();
        private readonly IPlayerAnalyzable _playerDataCollector = new CSPlayerDataCollector();
        private readonly IDailyChallengeAnalyzable _dailyChallengeDataCollector = new CSDailyChallengeDataCollector();
        private readonly IArcadeAnalyzable _arcadeDataCollector = new CSArcadeDataCollector();
        private readonly IMissionAnalyzable _missionDataCollector = new CSMissionDataCollector();
        private readonly ITrainingAnalyzable _trainingDataCollector = new CSTrainingDataCollector();
        private readonly IDeviceAnalyzale _deviceDataCollector = new CSDeviceDataCollector();
        
        private void Start()
        {
            InitSDK();
        }

        public void InitSDK()
        {
            _utilitiesFirebase.InitSDK();
            
            // The InitSDK functions are pretty much empty, they are for initializing additional APIS in individual Collectors
            _storeDataCollector.InitSDK();
            _playerDataCollector.InitSDK();
            _dailyChallengeDataCollector.InitSDK();
            _arcadeDataCollector.InitSDK();
            _missionDataCollector.InitSDK();
            _trainingDataCollector.InitSDK();
            _deviceDataCollector.InitSDK();
        }

        // Ready to go
        public void LogEventUpgradeCaptain()
        {
            _playerDataCollector.LogEventUpgradeCaptain();
        }

        // Ready to go 
        public void LogEventPurchaseCaptain(string captainName)
        {
            _storeDataCollector.LogEventPurchaseCaptain(captainName);
        }

        // Ready to go
        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            _storeDataCollector.LogEventPurchaseArcadeGame(arcadeGameName);
        }

        // Ready to go
        public void LogEventPurchaseMission(string missionName)
        {
            _storeDataCollector.LogEventPurchaseMission(missionName);
        }

        // Ready to go
        public async Task LogEventWatchAd()
        {
            await _storeDataCollector.LogEventWatchAd();
        }

        // Ready to go
        public async Task LogEventRedeemDailyReward()
        {
            await _storeDataCollector.LogEventRedeemDailyReward();
        }
        
        public void LogEventStartDailyChallenge()
        {
            _dailyChallengeDataCollector.LogEventStartDailyChallenge();
        }

        public void LogEventCompleteDailyChallenge()
        {
            _dailyChallengeDataCollector.LogEventCompleteDailyChallenge();
        }

        public void LogEventStartArcadeGame()
        {
            _arcadeDataCollector.LogEventStartArcadeGame();
        }

        public void LogEventCompleteArcadeGame()
        {
            _arcadeDataCollector.LogEventCompleteArcadeGame();
        }

        public void LogEventStartMission()
        {
            _missionDataCollector.LogEventStartMission();
        }

        public void LogEventCompleteMission()
        {
            _missionDataCollector.LogEventCompleteMission();
        }

        public void LogEventStartTraining()
        {
            _trainingDataCollector.LogEventStartTraining();
        }

        public void LogEventCompleteTraining()
        {
            _trainingDataCollector.LogEventCompleteTraining();
        }

        // Ready to go
        public void LogEventAppOpen()
        {
            _deviceDataCollector.LogEventAppOpen();
        }

        // Ready to go
        public void LogEventAppClose()
        {
            _deviceDataCollector.LogEventAppClose();
        }
    }
}
