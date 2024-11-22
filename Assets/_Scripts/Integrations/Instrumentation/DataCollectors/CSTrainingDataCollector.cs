using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSTrainingDataCollector : ITrainingAnalyzable
    {
        public void InitSDK()
        {
            Debug.Log("CSTrainingDataCollector - Initializing Training Data Collector.");
        }

        public void StartTraining()
        {
            Debug.Log("CSTrainingDataCollector - Triggering Start Training event.");
        }

        public void CompleteTraining()
        {
            Debug.Log("CSTrainingDataCollector - Triggering Complete Training event.");
        }
    }
}