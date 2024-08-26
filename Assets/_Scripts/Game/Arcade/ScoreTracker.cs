using CosmicShore.Game.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Arcade
{
    public class ScoreTracker : MonoBehaviour
    {
        [HideInInspector] public TMP_Text ActivePlayerScoreDisplay;

        [SerializeField] public ScoringModes ScoringMode;
        [SerializeField] public bool GolfRules;
        [HideInInspector] public GameCanvas GameCanvas;

        [Header("Optional Configuration")]
        // Magic number to give more precision to time tracking as an integer value
        [SerializeField] float TimePlayedScoreMultiplier = 1000f;
        [SerializeField] float ScoreNormalizationQuotient = 145.65f;

        public Dictionary<string, float> playerScores { get; private set; } = new();
        public Dictionary<string, Teams> playerTeams { get; private set; } = new();
        string currentPlayerName;
        int turnsPlayed = 0;
        float turnStartTime;

        void Start()
        {
            ActivePlayerScoreDisplay = GameCanvas.MiniGameHUD.ScoreDisplay;
        }

        public virtual void StartTurn(string playerName, Teams playerTeam)
        {
            if (!playerScores.ContainsKey(playerName))
            {
                playerScores.Add(playerName, 0);
                playerTeams.Add(playerName, playerTeam);
            }

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
                    case ScoringModes.HostileVolumeDestroyed:
                        if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                            score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].hostileVolumeDestroyed / ScoreNormalizationQuotient;
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
                        score = playerScores[currentPlayerName] + (Time.time - turnStartTime) * TimePlayedScoreMultiplier;
                        break;
                    case ScoringModes.TurnsPlayed:
                        score = turnsPlayed;
                        break;
                    case ScoringModes.BlocksStolen:
                        if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                            score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].blocksStolen;
                        break;
                    case ScoringModes.TeamVolumeDifference:
                        var teamStats = StatsManager.Instance.teamStats;  // TODO: Hardcoded player team to Green... reconsider
                        var greenVolume = teamStats.ContainsKey(Teams.Green) ? teamStats[Teams.Green].volumeRemaining : 0f;
                        var redVolume = teamStats.ContainsKey(Teams.Red) ? teamStats[Teams.Red].volumeRemaining : 0f;

                        score = (greenVolume - redVolume) / ScoreNormalizationQuotient;
                        break;
                    case ScoringModes.CrystalsCollected:
                        if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                            score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].crystalsCollected;
                        break;
                    case ScoringModes.OmnirystalsCollected:
                        if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                            score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].omniCrystalsCollected;
                        break;
                    case ScoringModes.ElementalCrystalsCollected:
                        if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                            score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].elementalCrystalsCollected;
                        break;
                }

                ActivePlayerScoreDisplay.text = ((int)score).ToString();
            }
        }

        public virtual void EndTurn()
        {
            turnsPlayed++;

            switch (ScoringMode)
            {
                case ScoringModes.HostileVolumeDestroyed:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].hostileVolumeDestroyed / ScoreNormalizationQuotient;
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
                    playerScores[currentPlayerName] += (Time.time - turnStartTime) * TimePlayedScoreMultiplier;
                    break;
                case ScoringModes.TurnsPlayed:
                    playerScores[currentPlayerName] = turnsPlayed;
                    break;
                case ScoringModes.BlocksStolen:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].blocksStolen;
                    StatsManager.Instance.ResetStats();
                    break;
                case ScoringModes.TeamVolumeDifference:
                    var teamStats = StatsManager.Instance.teamStats;
                    var greenVolume = teamStats.ContainsKey(Teams.Green) ? teamStats[Teams.Green].volumeRemaining : 0f;
                    var redVolume = teamStats.ContainsKey(Teams.Red) ? teamStats[Teams.Red].volumeRemaining : 0f;
                    playerScores[currentPlayerName] = (greenVolume - redVolume) / ScoreNormalizationQuotient;
                    StatsManager.Instance.ResetStats();
                    break;
            }

            foreach (var playerTeam in playerTeams) // Add all the players back into the reset stats dictionary so the score will update at the start of the player's turn
                StatsManager.Instance.AddPlayer(playerTeam.Value, playerTeam.Key);
        }

        public List<int> GetScores()
        {
            var scores = new List<int>();
            foreach (var score in playerScores.Values)
                scores.Add((int)score);

            return scores;
        }

        public virtual string GetWinner()
        {
            bool minTie;
            bool maxTie;
            float minScore = float.MaxValue;
            float maxScore = float.MinValue;
            string minKey = "";
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

        public virtual Teams GetWinningTeam()
        {
            var winner = GetWinner();
            return playerTeams[winner];
        }

        public virtual int GetHighScore()
        {
            bool minTie;
            bool maxTie;
            float minScore = float.MaxValue;
            float maxScore = float.MinValue;
            foreach (var key in playerScores.Keys)
            {
                if (playerScores[key] <= minScore)
                {
                    minTie = playerScores[key] == minScore;
                    minScore = playerScores[key];
                }
                if (playerScores[key] >= maxScore)
                {
                    maxTie = playerScores[key] == maxScore;
                    maxScore = playerScores[key];
                }
            }

            if (GolfRules)
                return (int)minScore;
            else
                return (int)maxScore;
        }

        public virtual int GetScore(string playerName)
        {
            return (int)playerScores[playerName];
        }
    }
}