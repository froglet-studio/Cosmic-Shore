using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSStoreDataCollector : IStoreAnalyzable
    {
        private readonly IStoreAnalyzable _storeDataCollectorFirebase = new CSStoreDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
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

        public async Task LogEventWatchAd()
        {
            Debug.Log("CSStoreDataCollector - Triggering Watch Ad event.");
            await _storeDataCollectorFirebase.LogEventWatchAd();
        }

        public async Task LogEventRedeemDailyReward()
        {
            Debug.Log("CSStoreDataCollector - Triggering Redeem Daily Reward event.");
            await _storeDataCollectorFirebase.LogEventRedeemDailyReward();
        }
    }
}
