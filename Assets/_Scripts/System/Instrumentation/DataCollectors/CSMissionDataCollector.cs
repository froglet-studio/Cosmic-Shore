using System;
using System.Threading.Tasks;
using CosmicShore.Core;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class CSMissionDataCollector : IMissionAnalyzable
    {
        private readonly IMissionAnalyzable _missionDataCollectorFirebase = new CSMissionDataCollectorFirebase();
        public async Task InitSDK()
        {
            await Task.Delay(1);    // Hide console warning until this is connected
            CSDebug.Log("CSMissionDataCollector - Initializing Mission Data Collector.");
        }

        public void LogEventStartMission(string gameType, int intensity, string shipType, string captainName, int numberOfPlayers)
        {
            CSDebug.Log("CSMissionDataCollector - Triggering Start Mission event.");
            _missionDataCollectorFirebase.LogEventStartMission(gameType, intensity, shipType, captainName, numberOfPlayers);
        }

        public void LogEventCompleteMission(string gameType, int intensity, string shipType, string captainName, 
            int numberOfPlayers, int score, int reward, DateTime playTime)
        {
            CSDebug.Log("CSMissionDataCollector - Triggering Complete Mission event.");
            _missionDataCollectorFirebase.LogEventCompleteMission(gameType, intensity, shipType, captainName, numberOfPlayers, score, reward, playTime);
        }
    }
}