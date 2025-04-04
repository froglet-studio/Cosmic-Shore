using CosmicShore._Scripts.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using System.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSPlayerDataCollector : IPlayerAnalyzable
    {
        private readonly IPlayerAnalyzable _playerDataCollectorFirebase = new CSPlayerDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            Debug.Log("CSPlayerDataCollector - Initializing Player Data Collector.");
        }

        public void LogEventUpgradeCaptain(string captainName, int captainLevel, string shipType)
        {
            Debug.Log("CSPlayerDataCollector - Triggering Upgrade Captain event.");
            _playerDataCollectorFirebase.LogEventUpgradeCaptain(captainName, captainLevel, shipType);
        }
    }
}