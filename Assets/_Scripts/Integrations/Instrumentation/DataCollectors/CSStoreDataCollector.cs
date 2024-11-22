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

        public void PurchaseCaptain()
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Captain event.");
        }

        public void PurchaseArcadeGame()
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Arcade Game event.");;
        }

        public void PurchaseMission()
        {
            Debug.Log("CSStoreDataCollector - Triggering Purchase Mission event.");
        }

        public void WatchAd()
        {
            Debug.Log("CSStoreDataCollector - Triggering Watch Ad event.");
        }

        public void RedeemDailyReward()
        {
            Debug.Log("CSStoreDataCollector - Triggering Redeem Daily Reward event.");
        }
    }
}
