using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;
using System.Threading.Tasks;
using UnityEngine;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSPlayerDataCollectorFirebase : IPlayerAnalyzable
    {
        
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            Debug.Log("CSPlayerDataCollectorFirebase - Initializing Training Data Collector.");
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