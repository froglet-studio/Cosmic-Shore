using System;
using CosmicShore._Scripts.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.DataCollectors
{
    public class CSTrainingDataCollector : ITrainingAnalyzable
    {
        private readonly ITrainingAnalyzable _trainingDataCollectorFirebase = new CSTrainingDataCollectorFirebase();
        public void InitSDK()
        {
            Debug.Log("CSTrainingDataCollector - Initializing Training Data Collector.");
        }

        public void LogEventStartTraining(string gameType, int intensity, string shipType)
        {
            Debug.Log("CSTrainingDataCollector - Triggering Start Training event.");
            _trainingDataCollectorFirebase.LogEventStartTraining(gameType, intensity, shipType);
        }

        public void LogEventCompleteTraining(
            string gameType, int intensity, string shipType, int score, int reward, DateTime playTime)
        {
            Debug.Log("CSTrainingDataCollector - Triggering Complete Training event.");
            _trainingDataCollectorFirebase.LogEventCompleteTraining(
                gameType, intensity, shipType, score, reward, playTime);
        }
    }
    
    
}