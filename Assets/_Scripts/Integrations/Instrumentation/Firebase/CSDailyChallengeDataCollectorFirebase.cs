﻿using System;
using System.Globalization;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSDailyChallengeDataCollectorFirebase : IDailyChallengeAnalyzable
    {
        public void InitSDK()
        {
            
        }

        public void LogEventStartDailyChallenge(string gameType, int intensity, string shipType, string captainName)
        {
            var parameters = new Parameter[]
            {
                new(CSCustomKeysFirebase.GameType, gameType),
                new(CSCustomKeysFirebase.Intensity, intensity),
                new(CSCustomKeysFirebase.ShipType, shipType),
                new(CSCustomKeysFirebase.CaptainName, captainName)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.StartDailyChallenge, parameters);
        }

        public void LogEventCompleteDailyChallenge(string gameType, int intensity, string shipType, string captainName, int score, int reward, DateTime playTime)
        {
            var parameters = new Parameter[]
            {
                new(CSCustomKeysFirebase.GameType, gameType),
                new(CSCustomKeysFirebase.Intensity, intensity),
                new(CSCustomKeysFirebase.ShipType, shipType),
                new(CSCustomKeysFirebase.CaptainName, captainName),
                new(CSCustomKeysFirebase.Score, score),
                new(CSCustomKeysFirebase.Reward, reward),
                new(CSCustomKeysFirebase.PlayTime, playTime.ToString(CultureInfo.InvariantCulture))
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.CompleteDailyChallenge, parameters);
        }
    }
}