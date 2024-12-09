using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSStoreDataCollector : IStoreAnalyzable
    {
        public void InitSDK()
        {
            Debug.Log("CSStoreDataCollector - Initializing CS Store Data Collector");
        }

        public void LogEventPurchaseCaptain()
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Captain event.");
        }

        public void LogEventPurchaseArcadeGame()
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Arcade Game event.");;
        }

        public void LogEventPurchaseMission()
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Mission event.");
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
