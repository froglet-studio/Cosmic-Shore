using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public enum ScoringModes
{
    VolumeDestroyed,
    VolumeCreated,
    TimePlayed,
    TurnsPlayed
}

public class ScoreTracker : MonoBehaviour
{
    [SerializeField] ScoringModes ScoringMode;
    [SerializeField] bool GolfRules;

    [SerializeField] TMP_Text WinnerNameContainer;
    [SerializeField] List<TMP_Text> PlayerNameContainers;
    [SerializeField] List<TMP_Text> PlayerScoreContainers;

    Dictionary<string, float> playerScores = new ();
    string currentPlayerName;
    int turnsPlayed = 0;
    float turnStartTime;

    public virtual void StartTurn(string playerName)
    {
        if (!playerScores.ContainsKey(playerName))
            playerScores.Add(playerName, 0);

        currentPlayerName = playerName;
        turnStartTime = Time.time;
    }

    public virtual void EndTurn()
    {
        turnsPlayed++;

        switch (ScoringMode)
        {
            case ScoringModes.VolumeDestroyed:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].volumeDestroyed;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.VolumeCreated:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].volumeCreated;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.TimePlayed:
                // TODO: 1000 is a magic number to give more precision to time tracking as an integer value
                playerScores[currentPlayerName] += (Time.time - turnStartTime) * 1000;  
                break;
            case ScoringModes.TurnsPlayed:
                playerScores[currentPlayerName] = turnsPlayed;
                break;
        }
    }

    public virtual string GetWinner()
    {
        bool minTie;
        bool maxTie;
        float minScore = float.MaxValue; 
        float maxScore = float.MinValue;
        string minKey ="";
        string maxKey = "";
        foreach (var key in playerScores.Keys)
        {
            if (playerScores[key] <= minScore)
            {
                minTie = playerScores[key] == minScore;
                minScore = playerScores[key];
                minKey = key;
            }
            if (playerScores[key] >= maxScore)
            {
                maxTie = playerScores[key] == maxScore;
                maxScore = playerScores[key];
                maxKey = key;
            }
        }

        if (GolfRules)
            return minKey;
        else 
            return maxKey;
    }

    public virtual int GetScore(string playerName) 
    {
        return (int) playerScores[playerName];
    }

    public virtual void DisplayScores()
    {
        WinnerNameContainer.text = GetWinner();
        for (var i = 0; i < playerScores.Keys.Count; i++)
        {
            string key = playerScores.Keys.Skip(i).Take(1).First();
            PlayerNameContainers[i].text = key;
            PlayerScoreContainers[i].text = playerScores[key].ToString();
        }
        for (var i = playerScores.Keys.Count; i<PlayerNameContainers.Count; i++)
        {
            PlayerNameContainers[i].text = "";
            PlayerScoreContainers[i].text = "";
        }
    }
}