using _Scripts._Core.Playfab_Models;
using PlayFab;
using PlayFab.ClientModels;
using StarWriter.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardManager : SingletonPersistent<LeaderboardManager>
{
    [System.Serializable]
    public struct LeaderboardEntryV2
    {
        public int Position;
        public int Score;
        public string DisplayName;

        public LeaderboardEntryV2(string displayName, int score, int position)
        {
            DisplayName = displayName;
            Score = score;
            Position = position;
        }
    }

    public void UpdateGameplayStatistic(MiniGames gameMode, ShipTypes shipType, int intensity, List<int> scores)
    {
        // Build list of statistics to update
        // One entry for each score for specific game mode/ship combination
        // One entry for each score for game mode any ship

        Debug.Log($"UpdateGamplayStats - gameMode:{gameMode}, shipType:{shipType}, intensity:{intensity}, score0:{scores[0]}");
        List<StatisticUpdate> stats = new List<StatisticUpdate>();

        foreach (var score in scores)
        {
            stats.Add(new StatisticUpdate() {
                StatisticName = GetGameplayStatKey(gameMode, shipType),
                Value = score
            });

            stats.Add(new StatisticUpdate()
            {
                StatisticName = GetGameplayStatKey(gameMode, ShipTypes.Any),
                Value = score
            });
        }
        UpdatePlayerStatistic(stats, new Dictionary<string, string>() { { "Intensity", intensity.ToString() } });

    }

    public string GetGameplayStatKey(MiniGames gameMode, ShipTypes shipType)
    {
        var statKey = gameMode.ToString().ToUpper() + "_" + shipType.ToString().ToUpper();

        Debug.Log("GetGameplayStatKey: " +  statKey);

        return statKey;
    }

    void UpdatePlayerStatistic(List<StatisticUpdate> stats)
    {
        UpdatePlayerStatistic(stats, new());
    }

    void UpdatePlayerStatistic(List<StatisticUpdate> stats, Dictionary<string, string> customTags)
    {
        customTags.Add("BuildNumber", Application.buildGUID);
        PlayFabClientAPI.UpdatePlayerStatistics(
            new()
            {
                AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
                CustomTags = customTags,
                Statistics = stats,
            },
            response => 
            {
                Debug.Log("UpdatePlayerStatistic success: " + response.ToString());
            },
            error => 
            {
                Debug.Log("UpdatePlayerStatistic failure: " + error.GenerateErrorReport());
            }
        );
    }

    public delegate void LoadLeaderboardCallBack(List<LeaderboardEntryV2> entries);

    public void FetchLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
    {
        FetchLeaderboard(leaderboardName, new(), callback);
    }

    public void FetchLeaderboard(string leaderboardName, Dictionary<string, string> customTags, LoadLeaderboardCallBack callback)
    {
        PlayFabClientAPI.GetLeaderboardAroundPlayer(
            new GetLeaderboardAroundPlayerRequest()
            {
                AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
                StatisticName = leaderboardName,
                CustomTags = customTags,
            },
            response =>
            {
                List<LeaderboardEntryV2> entries = new List<LeaderboardEntryV2>();
                foreach (var entry in response.Leaderboard)
                {
                    entries.Add(new LeaderboardEntryV2(entry.Profile.DisplayName, entry.StatValue, entry.Position));
                }

                callback(entries);

                Debug.Log("UpdatePlayerStatistic success: " + response.ToString());
            },
            error =>
            {
                Debug.Log("UpdatePlayerStatistic failure: " + error.GenerateErrorReport());
            }
        );
    }

    /*
    public void RequestLeaderboard()
    {
        PlayFabClientAPI.GetLeaderboard(new GetLeaderboardRequest
        {
            StatisticName = "HighScore",
            StartPosition = 0,
            MaxResultsCount = 10
        }, result => DisplayLeaderboard(result), FailureCallback);
    }
    */
}