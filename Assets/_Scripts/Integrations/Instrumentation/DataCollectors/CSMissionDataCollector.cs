using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSMissionDataCollector : IMissionAnalyzable
    {
        public void InitSDK()
        {
            Debug.Log("CSMissionDataCollector - Initializing Mission Data Collector.");
        }

        public void LogEventStartMission()
        {
            Debug.Log("CSMissionDataCollector - Triggering Start Mission event.");
        }

        public void LogEventCompleteMission()
        {
            Debug.Log("CSMissionDataCollector - Triggering Complete Mission event.");
        }
    }
}