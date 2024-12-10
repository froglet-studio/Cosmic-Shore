using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;

namespace CosmicShore.Integrations.Instrumentation.Firebase
{
    public class CSStoreDataCollectorFirebase : IStoreAnalyzable
    {
        
        public void InitSDK()
        {
        }

        public void LogEventPurchaseCaptain(string captainName)
        {
            var parameter = new Parameter(CSCustomKeysFirebase.KeyCaptainName, captainName);
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.EventPurchaseCaptain, parameter);
        }

        public void LogEventPurchaseArcadeGame(string arcadeGameName)
        {
            var parameter = new Parameter(CSCustomKeysFirebase.KeyArcadeGameName, arcadeGameName);
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.EventPurchaseArcadeGame, parameter);
        }

        public void LogEventPurchaseMission(string missionName)
        {
            var parameter = new Parameter(CSCustomKeysFirebase.KeyMissionName, missionName);
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.EventPurchaseMission, parameter);
        }

        public void LogEventWatchAd()
        {
            throw new System.NotImplementedException();
        }

        public void LogEventRedeemDailyReward()
        {
            throw new System.NotImplementedException();
        }
    }
}
