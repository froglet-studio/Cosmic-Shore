using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSStoreDataCollector : IStoreAnalyzable
    {
        private readonly IStoreAnalyzable _storeDataCollectorFirebase = new CSStoreDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSStoreDataCollector - Initializing CS Store Data Collector");
        }

        public void LogEventPurchaseCaptain(string captainName)
        {
            CSDebug.Log("CSStoreDataCollector - Triggering Purchase Captain event.");
            _storeDataCollectorFirebase.LogEventPurchaseCaptain(captainName);
        }

        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            CSDebug.Log("CSStoreDataCollector - Triggering Purchase Arcade Game event.");
            _storeDataCollectorFirebase.LogEventPurchaseArcadeGame(arcadeGameName);
        }

        public void LogEventPurchaseMission(string missionName)
        {
            CSDebug.Log("CSStoreDataCollector - Triggering Purchase Mission event.");
            _storeDataCollectorFirebase.LogEventPurchaseMission(missionName);
        }

        public async Task LogEventWatchAd()
        {
            CSDebug.Log("CSStoreDataCollector - Triggering Watch Ad event.");
            await _storeDataCollectorFirebase.LogEventWatchAd();
        }

        public async Task LogEventRedeemDailyReward()
        {
            CSDebug.Log("CSStoreDataCollector - Triggering Redeem Daily Reward event.");
            await _storeDataCollectorFirebase.LogEventRedeemDailyReward();
        }
    }
}
