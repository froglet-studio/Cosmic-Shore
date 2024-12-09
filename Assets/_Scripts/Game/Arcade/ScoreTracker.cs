using CosmicShore.Game.UI;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using TMPro;
using UnityEngine;
using CosmicShore.Game.Arcade.Scoring;
using System;
using System.Collections;

namespace CosmicShore.Game.Arcade
{
    [System.Serializable]
    public struct AdditionalScoringConfig
    {
        public ScoringModes Mode;
        public float Multiplier;
    }

    public class ScoreTracker : MonoBehaviour
    {
        [HideInInspector] public TMP_Text ActivePlayerScoreDisplay;
        [SerializeField] int initialScore = 0;
        [SerializeField] public ScoringModes ScoringMode;
        [SerializeField] public bool GolfRules;  // For primary scoring mode
        [SerializeField] private AdditionalScoringConfig[] AdditionalScoringModes;
        [HideInInspector] public GameCanvas GameCanvas;

        [Header("Optional Configuration")]
        [SerializeField] float ScoreMultiplier = 1f; //0.00686f for volume

        public Dictionary<string, float> playerScores { get; } = new();
        public Dictionary<string, Teams> playerTeams { get; } = new();
        
        string currentPlayerName;
        float turnStartTime;
        BaseScoringMode scoringMode;

        void Start()
        {
            if (GameCanvas != null && GameCanvas.MiniGameHUD != null)
                ActivePlayerScoreDisplay = GameCanvas.MiniGameHUD.ScoreDisplay;
            else
                Debug.LogWarning("GameCanvas or MiniGameHUD is not assigned!");

            InitializeScoringMode();
            StartCoroutine(UpdateScoreDisplayCoroutine());
        }

        private BaseScoringMode CreateScoringMode(ScoringModes mode, float multiplier)
        {
            BaseScoringMode newMode = mode switch
            {
                ScoringModes.HostileVolumeDestroyed => new HostileVolumeDestroyedScoring(multiplier),
                ScoringModes.VolumeCreated => new VolumeCreatedScoring(multiplier),
                ScoringModes.TimePlayed => new TimePlayedScoring(multiplier),
                ScoringModes.TurnsPlayed => new TurnsPlayedScoring(multiplier),
                ScoringModes.VolumeStolen => new VolumeAndBlocksStolenScoring(multiplier, false),
                ScoringModes.BlocksStolen => new VolumeAndBlocksStolenScoring(multiplier, true),
                ScoringModes.TeamVolumeDifference => new TeamVolumeDifferenceScoring(multiplier),
                ScoringModes.CrystalsCollected => new CrystalsCollectedScoring(CrystalsCollectedScoring.CrystalType.All, multiplier),
                ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(CrystalsCollectedScoring.CrystalType.Omni, multiplier),
                ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(CrystalsCollectedScoring.CrystalType.Elemental, multiplier),
                _ => throw new ArgumentException($"Unknown scoring mode: {mode}")
            };
            return newMode;
        }

        private void InitializeScoringMode()
        {
            if (AdditionalScoringModes == null || AdditionalScoringModes.Length == 0)
            {
                scoringMode = CreateScoringMode(ScoringMode, ScoreMultiplier);
                return;
            }

            var compositeScoringMode = new CompositeScoringMode(new[] { CreateScoringMode(ScoringMode, ScoreMultiplier) });
            
            foreach (var config in AdditionalScoringModes)
            {
                compositeScoringMode.AddScoringMode(CreateScoringMode(config.Mode, config.Multiplier));
            }

            scoringMode = compositeScoringMode;
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

        IEnumerator UpdateScoreDisplayCoroutine()
        {
            while (true)
            {
                if (turnStartTime == 0 || ActivePlayerScoreDisplay == null || scoringMode == null) { }
                else
                {
                    var score = scoringMode.CalculateScore(currentPlayerName, playerScores[currentPlayerName], turnStartTime) + initialScore;
                    ActivePlayerScoreDisplay.text = ((int)score).ToString();
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
       
        public virtual void EndTurn()
        {
            if (scoringMode == null)
                return;

            playerScores[currentPlayerName] = scoringMode.EndTurnScore(currentPlayerName, playerScores[currentPlayerName], turnStartTime) + initialScore;

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
