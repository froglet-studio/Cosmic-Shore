using CosmicShore.Core;
using System.Threading.Tasks;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CSPlayerDataCollector : IPlayerAnalyzable
    {
        private readonly IPlayerAnalyzable _playerDataCollectorFirebase = new CSPlayerDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSPlayerDataCollector - Initializing Player Data Collector.");
        }

        public void LogEventUpgradeCaptain(string captainName, int captainLevel, string shipType)
        {
            CSDebug.Log("CSPlayerDataCollector - Triggering Upgrade Captain event.");
            _playerDataCollectorFirebase.LogEventUpgradeCaptain(captainName, captainLevel, shipType);
        }
    }
}