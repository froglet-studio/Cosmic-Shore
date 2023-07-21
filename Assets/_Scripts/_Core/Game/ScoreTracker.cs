using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public enum ScoringModes
{
    VolumeDestroyed = 0,
    VolumeCreated = 1,
    TimePlayed = 2,
    TurnsPlayed = 3,
    VolumeStolen = 4,
    BlocksStolen = 5,
    TeamVolumeRemaining = 6,
}

public class ScoreTracker : MonoBehaviour
{
    [HideInInspector] public TMP_Text ActivePlayerScoreDisplay;
    
    [SerializeField] ScoringModes ScoringMode;
    [SerializeField] bool GolfRules;

    // TODO: p1 wire these display fields up through the HUD
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

    void Update()
    {
        if (turnStartTime == 0)
            return;

        if (ActivePlayerScoreDisplay != null)
        {
            var score = 0f;
            switch (ScoringMode)
            {
                case ScoringModes.VolumeDestroyed:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].volumeDestroyed;
                    break;
                case ScoringModes.VolumeCreated:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].volumeCreated;
                    break;
                case ScoringModes.VolumeStolen:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].volumeStolen;
                    break;
                case ScoringModes.TimePlayed:
                    // TODO: 1000 is a magic number to give more precision to time tracking as an integer value
                    score = playerScores[currentPlayerName] + (Time.time - turnStartTime) * 1000;
                    break;
                case ScoringModes.TurnsPlayed:
                    score = turnsPlayed;
                    break;
                case ScoringModes.BlocksStolen:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].blocksStolen;
                    break;
                case ScoringModes.TeamVolumeRemaining:
                    var teamStats = StatsManager.Instance.teamStats;  // TODO: Hardcoded player team to Green... reconsider
                    var greenVolume = teamStats.ContainsKey(Teams.Green) ? teamStats[Teams.Green].volumeRemaining : 0f;
                    var redVolume = teamStats.ContainsKey(Teams.Red) ? teamStats[Teams.Red].volumeRemaining : 0f;

                    score = (greenVolume - redVolume);
                    break;
            }

            ActivePlayerScoreDisplay.text = ((int) score).ToString();
        }
            
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
            case ScoringModes.VolumeStolen:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].volumeStolen;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.TimePlayed:
                // TODO: 1000 is a magic number to give more precision to time tracking as an integer value
                playerScores[currentPlayerName] += (Time.time - turnStartTime) * 1000;  
                break;
            case ScoringModes.TurnsPlayed:
                playerScores[currentPlayerName] = turnsPlayed;
                break;
            case ScoringModes.BlocksStolen:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].blocksStolen;
                StatsManager.Instance.ResetStats();
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
        //WinnerNameContainer.text = GetWinner();
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

            PlayerScoreContainers[i].gameObject.SetActive(false);
        }
    }
}