using System;
using System.Globalization;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSTrainingDataCollectorFirebase : ITrainingAnalyzable
    {
        public void InitSDK()
        {
            
        }

        public void LogEventStartTraining(string gameType, int intensity, string shipType)
        {
            var parameters = new Parameter[]
            {
                new(CSCustomKeysFirebase.GameType, gameType),
                new(CSCustomKeysFirebase.Intensity, intensity),
                new(CSCustomKeysFirebase.ShipType, shipType)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.StartTraining, parameters);
        }

        public void LogEventCompleteTraining(string gameType, int intensity, string shipType, int score, int reward, DateTime playTime)
        {
            var parameters = new Parameter[]
            {
                new(CSCustomKeysFirebase.GameType, gameType),
                new(CSCustomKeysFirebase.Intensity, intensity),
                new(CSCustomKeysFirebase.ShipType, shipType),
                new(CSCustomKeysFirebase.Score, score),
                new(CSCustomKeysFirebase.Reward, reward),
                new(CSCustomKeysFirebase.PlayTime, playTime.ToString(CultureInfo.InvariantCulture))
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.CompleteTraining, parameters);
        }
    }
}