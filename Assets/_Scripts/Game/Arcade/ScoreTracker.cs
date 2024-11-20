using CosmicShore.Game.UI;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using TMPro;
using UnityEngine;
using CosmicShore.Game.Arcade.Scoring;
using System;

namespace CosmicShore.Game.Arcade
{
    [System.Serializable]
    public struct AdditionalScoringConfig
    {
        public ScoringModes Mode;
        public bool UseGolfRules;
    }

    public class ScoreTracker : MonoBehaviour
    {
        [HideInInspector] public TMP_Text ActivePlayerScoreDisplay;

        [SerializeField] public ScoringModes ScoringMode;
        [SerializeField] public bool GolfRules;  // For primary scoring mode
        [SerializeField] private AdditionalScoringConfig[] AdditionalScoringModes;
        [HideInInspector] public GameCanvas GameCanvas;

        [Header("Optional Configuration")]
        [SerializeField] float TimePlayedScoreMultiplier = 1000f;
        [SerializeField] float ScoreNormalizationQuotient = 145.65f;

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
        }

        private BaseScoringMode CreateScoringMode(ScoringModes mode, bool useGolfRules)
        {
            BaseScoringMode newMode = mode switch
            {
                ScoringModes.HostileVolumeDestroyed => new HostileVolumeDestroyedScoring(ScoreNormalizationQuotient),
                ScoringModes.VolumeCreated => new VolumeCreatedScoring(ScoreNormalizationQuotient),
                ScoringModes.TimePlayed => new TimePlayedScoring(TimePlayedScoreMultiplier, ScoreNormalizationQuotient),
                ScoringModes.TurnsPlayed => new TurnsPlayedScoring(ScoreNormalizationQuotient),
                ScoringModes.VolumeStolen => new VolumeAndBlocksStolenScoring(false, ScoreNormalizationQuotient),
                ScoringModes.BlocksStolen => new VolumeAndBlocksStolenScoring(true, ScoreNormalizationQuotient),
                ScoringModes.TeamVolumeDifference => new TeamVolumeDifferenceScoring(ScoreNormalizationQuotient),
                ScoringModes.CrystalsCollected => new CrystalsCollectedScoring(CrystalsCollectedScoring.CrystalType.All, ScoreNormalizationQuotient),
                ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(CrystalsCollectedScoring.CrystalType.Omni, ScoreNormalizationQuotient),
                ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(CrystalsCollectedScoring.CrystalType.Elemental, ScoreNormalizationQuotient),
                _ => throw new ArgumentException($"Unknown scoring mode: {mode}")
            };
            newMode.UseGolfRules = useGolfRules;
            return newMode;
        }

        private void InitializeScoringMode()
        {
            if (AdditionalScoringModes == null || AdditionalScoringModes.Length == 0)
            {
                scoringMode = CreateScoringMode(ScoringMode, GolfRules);
                return;
            }

            var compositeScoringMode = new CompositeScoringMode(new[] { CreateScoringMode(ScoringMode, GolfRules) }, ScoreNormalizationQuotient);
            
            foreach (var config in AdditionalScoringModes)
            {
                compositeScoringMode.AddScoringMode(CreateScoringMode(config.Mode, config.UseGolfRules));
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

        void Update()
        {
            if (turnStartTime == 0 || ActivePlayerScoreDisplay == null || scoringMode == null)
                return;

            var score = scoringMode.CalculateScore(currentPlayerName, playerScores[currentPlayerName], turnStartTime);
            ActivePlayerScoreDisplay.text = ((int)score).ToString();
        }

        public virtual void EndTurn()
        {
            if (scoringMode == null)
                return;

            playerScores[currentPlayerName] = scoringMode.EndTurnScore(currentPlayerName, playerScores[currentPlayerName], turnStartTime);

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
