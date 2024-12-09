using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSPlayerDataCollector : IPlayerAnalyzable
    {
        public void InitSDK()
        {
            Debug.Log("CSPlayerDataCollector - Initializing Player Data Collector.");
        }

        public void LogEventUpgradeCaptain()
        {
            Debug.Log("CSPlayerDataCollector - Triggering Upgrade Captain event.");
        }
    }
}