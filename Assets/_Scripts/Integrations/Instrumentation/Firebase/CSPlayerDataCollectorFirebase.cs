using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSPlayerDataCollectorFirebase : IPlayerAnalyzable
    {
        
        public void InitSDK()
        {
            
        }

        
        public void LogEventUpgradeCaptain(string captainName, int captainLevel, string shipType)
        {
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.CaptainName, captainName),
                new (CSCustomKeysFirebase.CaptainLevel, captainLevel),
                new (CSCustomKeysFirebase.ShipType, shipType)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.UpgradeCaptain, parameters);
        }
    }
}