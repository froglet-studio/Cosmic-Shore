using CosmicShore.Integrations.Instrumentation.DataCollectors;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using CosmicShore.Utility.Singleton;

namespace CosmicShore.Integrations.Instrumentation
{
    public class CSAnalyticsManager : SingletonPersistent<CSAnalyticsManager>, IStoreAnalyzable, IDailyChallengeAnalyzable, IPlayerAnalyzable, IArcadeAnalyzable, IMissionAnalyzable, ITrainingAnalyzable
    {
        private readonly CSStoreDataCollector _storeDataCollector = new();
        private readonly CSPlayerDataCollector _playerDataCollector = new();
        private readonly CSDailyChallengeDataCollector _dailyChallengeDataCollector = new();
        private readonly CSArcadeDataCollector _arcadeDataCollector = new();
        private readonly CSMissionDataCollector _missionDataCollector = new();
        private readonly CSTrainingDataCollector _trainingDataCollector = new();

        private void Start()
        {
            InitSDK();
        }

        public void InitSDK()
        {
            _storeDataCollector.InitSDK();
            _playerDataCollector.InitSDK();
            _dailyChallengeDataCollector.InitSDK();
            _arcadeDataCollector.InitSDK();
            _missionDataCollector.InitSDK();
            _trainingDataCollector.InitSDK();
        }

        public void UpgradeCaptain()
        {
            _playerDataCollector.UpgradeCaptain();
        }

        public void PurchaseCaptain()
        {
            _storeDataCollector.PurchaseCaptain();
        }

        public void PurchaseArcadeGame()
        {
            _storeDataCollector.PurchaseArcadeGame();
        }

        public void PurchaseMission()
        {
            _storeDataCollector.PurchaseMission();
        }

        public void WatchAd()
        {
            _storeDataCollector.WatchAd();
        }

        public void RedeemDailyReward()
        {
            _storeDataCollector.RedeemDailyReward();
        }

        public void StartDailyChallenge()
        {
            _dailyChallengeDataCollector.StartDailyChallenge();
        }

        public void CompleteDailyChallenge()
        {
            _dailyChallengeDataCollector.CompleteDailyChallenge();
        }

        public void StartArcadeGame()
        {
            _arcadeDataCollector.StartArcadeGame();
        }

        public void CompleteArcadeGame()
        {
            _arcadeDataCollector.CompleteArcadeGame();
        }

        public void StartMission()
        {
            _missionDataCollector.StartMission();
        }

        public void CompleteMission()
        {
            _missionDataCollector.CompleteMission();
        }

        public void StartTraining()
        {
            _trainingDataCollector.StartTraining();
        }

        public void CompleteTraining()
        {
            _trainingDataCollector.CompleteTraining();
        }
    }
}
