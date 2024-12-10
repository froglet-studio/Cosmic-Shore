using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSStoreDataCollector : IStoreAnalyzable
    {
        private readonly CSStoreDataCollectorFirebase _storeDataCollectorFirebase = new();
        public void InitSDK()
        {
            Debug.Log("CSStoreDataCollector - Initializing CS Store Data Collector");
        }

        public void LogEventPurchaseCaptain(string captainName)
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Captain event.");
            _storeDataCollectorFirebase.LogEventPurchaseCaptain(captainName);
        }

        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Arcade Game event.");
            _storeDataCollectorFirebase.LogEventPurchaseArcadeGame(arcadeGameName);
        }

        public void LogEventPurchaseMission(string missionName)
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Mission event.");
            _storeDataCollectorFirebase.LogEventPurchaseMission(missionName);
        }

        public void LogEventWatchAd()
        {
            Debug.Log("CSStoreDataCollector - Triggering Watch Ad event.");
        }

        public void LogEventRedeemDailyReward()
        {
            Debug.Log("CSStoreDataCollector - Triggering Redeem Daily Reward event.");
        }
    }
}
