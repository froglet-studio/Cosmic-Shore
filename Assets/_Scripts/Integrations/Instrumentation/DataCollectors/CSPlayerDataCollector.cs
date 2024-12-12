using CosmicShore._Scripts.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSPlayerDataCollector : IPlayerAnalyzable
    {
        private readonly IPlayerAnalyzable _playerDataCollectorFirebase = new CSPlayerDataCollectorFirebase();
        public void InitSDK()
        {
            Debug.Log("CSPlayerDataCollector - Initializing Player Data Collector.");
        }

        public void LogEventUpgradeCaptain(string captainName, int captainLevel, string shipType)
        {
            Debug.Log("CSPlayerDataCollector - Triggering Upgrade Captain event.");
            _playerDataCollectorFirebase.LogEventUpgradeCaptain(captainName, captainLevel, shipType);
        }
    }
}