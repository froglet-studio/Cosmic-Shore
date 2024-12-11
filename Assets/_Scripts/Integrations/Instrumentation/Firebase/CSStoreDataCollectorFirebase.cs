using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;
using UnityEngine.Device;

namespace CosmicShore.Integrations.Instrumentation.Firebase
{
    public class CSStoreDataCollectorFirebase : IStoreAnalyzable
    {
        
        public void InitSDK()
        {
        }

        public void LogEventPurchaseCaptain(string captainName)
        {
            var parameters = new Parameter[1];
            parameters[0] = new Parameter(CSCustomKeysFirebase.CaptainName, captainName);
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.PurchaseCaptain, parameters);
        }

        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            var parameters = new Parameter[1];
            parameters[0] = new Parameter(CSCustomKeysFirebase.ArcadeGameName, arcadeGameName);
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.PurchaseArcadeGame, parameters);
        }

        public void LogEventPurchaseMission(string missionName)
        {
            var parameters = new Parameter[1];
            parameters[0] = new Parameter(CSCustomKeysFirebase.MissionName, missionName);
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.PurchaseMission, parameters);
        }

        public async Task LogEventWatchAd()
        {
            var parameters = new Parameter[2];
            parameters[0] = new Parameter(CSCustomKeysFirebase.UserId, SystemInfo.deviceUniqueIdentifier);
            
            var sessionId = await CSUtilitiesFirebase.GetSessionIdAsync();
            var paramSessionId = new Parameter(CSCustomKeysFirebase.SessionId, sessionId);
            
            parameters[1] = paramSessionId;
            
            FirebaseAnalytics.LogEvent(
                CSCustomEventsFirebase.WatchAdd,
                parameters);
        }

        public async Task LogEventRedeemDailyReward()
        {
            var parameters = new Parameter[2];
            parameters[0] = new Parameter(CSCustomKeysFirebase.UserId, SystemInfo.deviceUniqueIdentifier);
            
            var sessionId = await CSUtilitiesFirebase.GetSessionIdAsync();
            var paramSessionId = new Parameter(CSCustomKeysFirebase.SessionId, sessionId);
            
            parameters[1] = paramSessionId;
            
            FirebaseAnalytics.LogEvent(
                CSCustomEventsFirebase.ClaimDailyReward,
                parameters);
        }
    }
}
