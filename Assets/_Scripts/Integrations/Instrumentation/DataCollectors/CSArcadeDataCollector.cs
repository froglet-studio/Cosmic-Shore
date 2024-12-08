﻿using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSArcadeDataCollector : IArcadeAnalyzable
    {
        public void InitSDK()
        {
            Debug.Log("CSArcadeDataCollector - Initializing Arcade Data Collector.");
        }

        public void LogEventStartArcadeGame()
        {
            Debug.Log("CSArcadeDataCollector - Triggering Start Arcade Game event.");
        }

        public void LogEventCompleteArcadeGame()
        {
            Debug.Log("CSArcadeDataCollector - Triggering Complete Arcade Game event.");
        }
    }
}