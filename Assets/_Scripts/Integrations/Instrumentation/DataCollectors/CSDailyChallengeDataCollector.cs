using System;
using CosmicShore._Scripts.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSDailyChallengeDataCollector : IDailyChallengeAnalyzable
    {
        private readonly IDailyChallengeAnalyzable _dailyChallengeDataCollectorFirebase =
            new CSDailyChallengeDataCollectorFirebase();
        public void InitSDK()
        {
            Debug.Log("CSDailyChallengeDataCollector - Initializing Daily Challenge Data Collector.");
        }

        public void LogEventStartDailyChallenge(string gameType, int intensity, string shipType, string captainName)
        {
            Debug.Log("CSDailyChallengeDataCollector - Triggering Start Daily Challenge event.");
            _dailyChallengeDataCollectorFirebase.LogEventStartDailyChallenge(gameType, intensity, shipType, captainName);
        }

        public void LogEventCompleteDailyChallenge(string gameType, int intensity, string shipType, string captainName, int score, int reward, DateTime playTime)
        {
            Debug.Log("CSDailyChallengeDataCollector - Triggering Complete Daily Challenge event.");
        }
    }
}