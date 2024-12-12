using System;
using CosmicShore._Scripts.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSArcadeDataCollector : IArcadeAnalyzable
    {
        private readonly IArcadeAnalyzable _arcadeDataCollectorFirebase = new CSArcadeDataCollectorFirebase();
        public void InitSDK()
        {
            Debug.Log("CSArcadeDataCollector - Initializing Arcade Data Collector.");
        }

        public void LogEventStartArcadeGame(string gameType, int intensity, string shipType)
        {
            Debug.Log("CSArcadeDataCollector - Triggering Start Arcade Game event.");
            _arcadeDataCollectorFirebase.LogEventStartArcadeGame(gameType, intensity, shipType);
        }

        public void LogEventCompleteArcadeGame(string gameType, int intensity, string shipType, int score, int reward,
            DateTime playTime)
        {
            Debug.Log("CSArcadeDataCollector - Triggering Complete Arcade Game event.");
            _arcadeDataCollectorFirebase.LogEventCompleteArcadeGame(gameType, intensity, shipType, score, reward,
                playTime);
        }
    }
}