using CosmicShore.Core;
using Firebase.Analytics;
using System.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CSPlayerDataCollectorFirebase : IPlayerAnalyzable
    {
        
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSPlayerDataCollectorFirebase - Initializing Training Data Collector.");
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