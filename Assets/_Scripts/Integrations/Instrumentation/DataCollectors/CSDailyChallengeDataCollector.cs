using System;
using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation;
using UnityEngine;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Integrations.Instrumentation
{
    public class CSDailyChallengeDataCollector : IDailyChallengeAnalyzable
    {
        private readonly IDailyChallengeAnalyzable _dailyChallengeDataCollectorFirebase =
            new CSDailyChallengeDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSDailyChallengeDataCollector - Initializing Daily Challenge Data Collector.");
        }

        public void LogEventStartDailyChallenge(string gameType, int intensity, string shipType, string captainName)
        {
            CSDebug.Log("CSDailyChallengeDataCollector - Triggering Start Daily Challenge event.");
            _dailyChallengeDataCollectorFirebase.LogEventStartDailyChallenge(gameType, intensity, shipType, captainName);
        }

        public void LogEventCompleteDailyChallenge(string gameType, int intensity, string shipType, string captainName, int score, int reward, DateTime playTime)
        {
            CSDebug.Log("CSDailyChallengeDataCollector - Triggering Complete Daily Challenge event.");
        }
    }
}
