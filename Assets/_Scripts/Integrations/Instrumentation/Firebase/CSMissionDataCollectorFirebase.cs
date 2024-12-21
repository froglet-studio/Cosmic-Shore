using System;
using System.Globalization;
using System.Threading.Tasks;
using CosmicShore.Integrations.Instrumentation.Firebase;
using CosmicShore.Integrations.Instrumentation.Interfaces;
using Firebase.Analytics;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSMissionDataCollectorFirebase : IMissionAnalyzable
    {
        public async Task InitSDK()
        {
            
        }

        public void LogEventStartMission(string gameType, int intensity, string shipType, string captainName, int numberOfPlayers)
        {
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.GameType, gameType),
                new (CSCustomKeysFirebase.Intensity, intensity),
                new (CSCustomKeysFirebase.ShipType, shipType),
                new (CSCustomKeysFirebase.CaptainName, captainName),
                new (CSCustomKeysFirebase.NumberOfPlayers, numberOfPlayers)
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.StartMission, parameters);
        }

        public void LogEventCompleteMission(string gameType, int intensity, string shipType, string captainName, 
            int numberOfPlayers, int score, int reward, DateTime playTime)
        {
            var parameters = new Parameter[]
            {
                new (CSCustomKeysFirebase.GameType, gameType),
                new (CSCustomKeysFirebase.Intensity, intensity),
                new (CSCustomKeysFirebase.ShipType, shipType),
                new (CSCustomKeysFirebase.CaptainName, captainName),
                new (CSCustomKeysFirebase.NumberOfPlayers, numberOfPlayers),
                new (CSCustomKeysFirebase.Score, score),
                new (CSCustomKeysFirebase.Reward, reward),
                new (CSCustomKeysFirebase.PlayTime, playTime.ToString(CultureInfo.InvariantCulture))
            };
            
            FirebaseAnalytics.LogEvent(CSCustomEventsFirebase.CompleteMission, parameters);
        }
    }
}