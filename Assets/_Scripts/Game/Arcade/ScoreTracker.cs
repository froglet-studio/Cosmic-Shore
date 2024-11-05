using System;
using CosmicShore.Game.UI;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using TMPro;
using UnityEngine;

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

        public Dictionary<string, float> playerScores { get; } = new();
        public Dictionary<string, Teams> playerTeams { get; } = new();
        
        public static event Action OnScoreTrackerEnabled;
        
        string currentPlayerName;
        int turnsPlayed;
        float turnStartTime;

        void Start()
        {
            if (GameCanvas != null && GameCanvas.MiniGameHUD != null)
                ActivePlayerScoreDisplay = GameCanvas.MiniGameHUD.ScoreDisplay;
            else
                Debug.LogWarning("GameCanvas or MiniGameHUD is not assigned!");
        }

        public virtual void StartTurn(string playerName, Teams playerTeam)
        {
            if (playerScores.TryAdd(playerName, 0))
            {
                playerTeams.Add(playerName, playerTeam);
            }

            currentPlayerName = playerName;
            turnStartTime = Time.time;
        }

        void Update()
        {
            if (turnStartTime == 0)
                return;

            if (ActivePlayerScoreDisplay == null) return;
            
            var score = 0f;
            
            RoundStats roundStats;
            switch (ScoringMode)
            {
                case ScoringModes.HostileVolumeDestroyed:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.HostileVolumeDestroyed / ScoreNormalizationQuotient;
                    break;
                case ScoringModes.VolumeCreated:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.VolumeCreated;
                    break;
                case ScoringModes.VolumeStolen:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.VolumeStolen;
                    break;
                case ScoringModes.TimePlayed:
                    score = playerScores[currentPlayerName] + (Time.time - turnStartTime) * TimePlayedScoreMultiplier;
                    break;
                case ScoringModes.TurnsPlayed:
                    score = turnsPlayed;
                    break;
                case ScoringModes.BlocksStolen:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.BlocksStolen;
                    break;
                case ScoringModes.TeamVolumeDifference:
                    var teamStats = StatsManager.Instance.TeamStats;  // TODO: Hardcoded player team to Green... reconsider
                    var greenVolume = teamStats.TryGetValue(Teams.Jade, out roundStats) ? roundStats.VolumeRemaining : 0f;
                    var redVolume = teamStats.TryGetValue(Teams.Ruby, out roundStats) ? roundStats.VolumeRemaining : 0f;

                    score = (greenVolume - redVolume) / ScoreNormalizationQuotient;
                    break;
                case ScoringModes.CrystalsCollected:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.CrystalsCollected;
                    break;
                case ScoringModes.OmnirystalsCollected:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.OmniCrystalsCollected;
                    break;
                case ScoringModes.ElementalCrystalsCollected:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        score = playerScores[currentPlayerName] + roundStats.ElementalCrystalsCollected;
                    break;
                default:
                    Debug.LogWarning("ScoreTracker - Unknown Scoring Mode!");
                    break;
            }

            ActivePlayerScoreDisplay.text = ((int)score).ToString();
        }

        public virtual void EndTurn()
        {
            turnsPlayed++;
            
            RoundStats roundStats;
            
            switch (ScoringMode)
            {
                case ScoringModes.HostileVolumeDestroyed:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        playerScores[currentPlayerName] += roundStats.HostileVolumeDestroyed / ScoreNormalizationQuotient;
                    StatsManager.Instance.ResetStats();
                    break;
                case ScoringModes.VolumeCreated:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        playerScores[currentPlayerName] += roundStats.VolumeCreated;
                    StatsManager.Instance.ResetStats();
                    break;
                case ScoringModes.VolumeStolen:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        playerScores[currentPlayerName] += roundStats.VolumeStolen;
                    StatsManager.Instance.ResetStats();
                    break;
                case ScoringModes.TimePlayed:
                    playerScores[currentPlayerName] += (Time.time - turnStartTime) * TimePlayedScoreMultiplier;
                    break;
                case ScoringModes.TurnsPlayed:
                    playerScores[currentPlayerName] = turnsPlayed;
                    break;
                case ScoringModes.BlocksStolen:
                    if (StatsManager.Instance.PlayerStats.TryGetValue(currentPlayerName, out roundStats))
                        playerScores[currentPlayerName] += roundStats.BlocksStolen;
                    StatsManager.Instance.ResetStats();
                    break;
                case ScoringModes.TeamVolumeDifference:
                    var teamStats = StatsManager.Instance.TeamStats;
                    var greenVolume = teamStats.TryGetValue(Teams.Jade, out roundStats) ? roundStats.VolumeRemaining : 0f;
                    var redVolume = teamStats.TryGetValue(Teams.Ruby, out roundStats) ? roundStats.VolumeRemaining : 0f;
                    playerScores[currentPlayerName] = (greenVolume - redVolume) / ScoreNormalizationQuotient;
                    StatsManager.Instance.ResetStats();
                    break;
                case ScoringModes.CrystalsCollected:
                case ScoringModes.OmnirystalsCollected:
                case ScoringModes.ElementalCrystalsCollected:
                default:
                    Debug.LogWarning("ScoreTracker - Unknown Scoring Mode!");
                    break;
            }

            foreach (var playerTeam in playerTeams) // Add all the players back into the reset stats dictionary so the score will update at the start of the player's turn
                StatsManager.Instance.AddPlayer(playerTeam.Value, playerTeam.Key);
        }

        public List<int> GetScores()
        {
            return playerScores.Values.Select(score => (int)score).ToList();
        }

        public virtual string GetWinner()
        {
            return GolfRules
                ? playerScores.Aggregate((minPlayer, nextPlayer) => nextPlayer.Value < minPlayer.Value ? nextPlayer : minPlayer).Key
                : playerScores.Aggregate((maxPlayer, nextPlayer) => nextPlayer.Value > maxPlayer.Value ? nextPlayer : maxPlayer).Key;
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
            var minScore = float.MaxValue;
            var maxScore = float.MinValue;
            foreach (var key in playerScores.Keys)
            {
                if (playerScores[key] <= minScore)
                {
                    minTie = Mathf.Approximately(playerScores[key], minScore);
                    minScore = playerScores[key];
                }
                if (playerScores[key] >= maxScore)
                {
                    maxTie = Mathf.Approximately(playerScores[key], maxScore);
                    maxScore = playerScores[key];
                }
            }

            return GolfRules ? (int)minScore : (int)maxScore;
        }

        public virtual int GetScore(string playerName)
        {
            return (int)playerScores[playerName];
        }
    }
}