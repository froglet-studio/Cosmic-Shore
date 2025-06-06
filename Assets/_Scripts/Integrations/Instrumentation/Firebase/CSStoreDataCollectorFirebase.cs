using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.Firebase
{
    public class CSStoreDataCollectorFirebase : IStoreAnalyzable
    {
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            Debug.Log("CSStoreDataCollectorFirebase - Initializing Training Data Collector.");
        }

        public void LogEventPurchaseCaptain(string captainName)
        {
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.CaptainName, captainName)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.PurchaseCaptain, parameters);
        }

        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.ArcadeGameName, arcadeGameName)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.PurchaseArcadeGame, parameters);
        }

        public void LogEventPurchaseMission(string missionName)
        {
            var parameters = new Parameter[]
            { 
                new (CSCustomKeysFirebase.MissionName, missionName)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.PurchaseMission, parameters);
        }

        public async Task LogEventWatchAd()
        {
            var sessionId = await CSUtilitiesFirebase.GetSessionIdAsync();
            
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.UserId, UnityEngine.Device.SystemInfo.deviceUniqueIdentifier),
                new (CSCustomKeysFirebase.SessionId, sessionId)
            };
            
            FirebaseAnalytics.LogEvent(
                CSCustomEventsFirebase.WatchAdd,
                parameters);
        }

        public async Task LogEventRedeemDailyReward()
        {
            var sessionId = await CSUtilitiesFirebase.GetSessionIdAsync();
            
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.UserId, UnityEngine.Device.SystemInfo.deviceUniqueIdentifier),
                new (CSCustomKeysFirebase.SessionId, sessionId)
            };
            
            FirebaseAnalytics.LogEvent(
                CSCustomEventsFirebase.ClaimDailyReward,
                parameters);
        }
    }
}
