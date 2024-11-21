using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSDailyChallengeDataCollector : IDailyChallengeAnalyzable
    {
        public void InitSDK()
        {
            Debug.Log("CSDailyChallengeDataCollector - Initializing Daily Challenge Data Collector.");
        }

        public void StartDailyChallenge()
        {
            Debug.Log("CSDailyChallengeDataCollector - Triggering Start Daily Challenge event.");
        }

        public void CompleteDailyChallenge()
        {
            Debug.Log("CSDailyChallengeDataCollector - Triggering Complete Daily Challenge event.");
        }
    }
}
