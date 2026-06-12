using System;
using System.Globalization;
using System.Threading.Tasks;
using CosmicShore.Core;
using Firebase.Analytics;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CSArcadeDataCollectorFirebase : IArcadeAnalyzable
    {
        
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSArcadeDataCollectorFirebase - Initializing Training Data Collector.");
        }

        public void LogEventStartArcadeGame(string gameType, int intensity, string shipType)
        {
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.GameType, gameType),
                new (CSCustomKeysFirebase.Intensity, intensity),
                new (CSCustomKeysFirebase.ShipType, shipType)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.StartArcadeGame, parameters);
        }

        public void LogEventCompleteArcadeGame(string gameType, int intensity, string shipType, int score, int reward, DateTime playTime)
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
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.CompleteArcadeGame, parameters);
        }
    }
}