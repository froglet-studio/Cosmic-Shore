using CosmicShore.Game.Arcade.Scoring;
using CosmicShore.Game.UI;
using System;
using UnityEngine;
using CosmicShore.SOAP;
using Unity.Netcode;
using UnityEngine.Serialization;


namespace CosmicShore.Game.Arcade
{
    public class ScoreTracker : NetworkBehaviour
    {
        [SerializeField]
        protected MiniGameDataSO miniGameData;
        
        [SerializeField] bool golfRules;  // For primary scoring mode
        
        [FormerlySerializedAs("ScoringConfigs")] 
        [SerializeField] ScoringConfig[] scoringConfigs;

        BaseScoring[] scoringArray;

        protected virtual void OnEnable()
        {
            miniGameData.OnMiniGameInitialized += InitializeScoringMode;
            miniGameData.OnMiniGameEnd += CalculateWinnerAndInvokeEvent;
        }

        protected virtual void OnDisable()
        {
            miniGameData.OnMiniGameInitialized -= InitializeScoringMode;
            miniGameData.OnMiniGameEnd -= CalculateWinnerAndInvokeEvent;
        }
        
        void CalculateWinnerAndInvokeEvent()
        {
            CalculateScores();
            SortAndInvokeResults();
        }

        protected void SortAndInvokeResults()
        {
            miniGameData.SortRoundStats(golfRules);
            miniGameData.InvokeWinnerCalculated();
        }
        
        protected void InitializeScoringMode()
        {
            if (scoringConfigs == null || scoringConfigs.Length == 0)
            {
                Debug.LogError("No Scoring Configs were provided.");
                return;
            }

            int arrayLength = scoringConfigs.Length;
            scoringArray = new BaseScoring[arrayLength];
            for (int i = 0; i < arrayLength; i++)
            {
                scoringArray[i] = CreateScoring(scoringConfigs[i].Mode, scoringConfigs[i].Multiplier);
            }
        }
        
        BaseScoring CreateScoring(ScoringModes mode, float multiplier)
        {
            BaseScoring newScoring = mode switch
            {
                ScoringModes.HostileVolumeDestroyed => new HostileVolumeDestroyedScoring(miniGameData, multiplier),
                ScoringModes.VolumeCreated => new VolumeCreatedScoring(miniGameData, multiplier),
                ScoringModes.TimePlayed => new TimePlayedScoring(miniGameData, multiplier),
                ScoringModes.TurnsPlayed => new TurnsPlayedScoring(miniGameData, multiplier),
                ScoringModes.VolumeStolen => new VolumeAndBlocksStolenScoring(miniGameData, multiplier),
                ScoringModes.BlocksStolen => new VolumeAndBlocksStolenScoring(miniGameData, multiplier, true),
                ScoringModes.TeamVolumeDifference => new TeamVolumeDifferenceScoring(miniGameData, multiplier),
                ScoringModes.CrystalsCollected => new CrystalsCollectedScoring(miniGameData, multiplier),
                ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(miniGameData, multiplier, CrystalsCollectedScoring.CrystalType.Omni),
                ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(miniGameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental),
                ScoringModes.CrystalsCollectedScaleWithSize => new CrystalsCollectedScoring(miniGameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental, true),
                _ => throw new ArgumentException($"Unknown scoring mode: {mode}")
            };
            return newScoring;
        }

        protected void CalculateScores()
        {
            if (miniGameData.RoundStatsList.Count == 0)
            {
                Debug.LogError("This should never happen!");
                return;
            }

            foreach (var scoring in scoringArray)
                scoring.CalculateScore();
            
            /* StatsManager.Instance.ResetStats();

            var playerScores = scoreData.RoundStatsList;
            // Add all the players back into the reset stats dictionary so the score will update at the start of the player's turn
            foreach (var score in playerScores)
                StatsManager.Instance.AddPlayer(score.Team, score.Name);*/
        }
    }

    [System.Serializable]
    public struct ScoringConfig
    {
        public ScoringModes Mode;
        public float Multiplier; //0.00686f for volume
    }
}

        // TODO - scoring.CalculateScore need to be done when scoring is needed, rather than running on update for more realtime sync.
        /*IEnumerator UpdateScoreDisplayCoroutine()
        {
            while (true)
            {
                if (turnStartTime == 0 || ActivePlayerScoreDisplay == null || scoring == null) { }
                else
                {
                    var score = scoring.CalculateScore(currentPlayerName, playerScores[currentPlayerName], turnStartTime) + initialScore;
                    ActivePlayerScoreDisplay.text = ((int)score).ToString();
                }
                yield return new WaitForSeconds(0.5f);
            }
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
        
        public virtual string GetWinner()
        {
            
        }

        public virtual Teams GetWinningTeam()
        {
            var winner = GetWinner();
            return playerTeams[winner];
        }
        
        public virtual string GetWinner()
        {
            return GolfRules
                ? playerScores.Aggregate((minPlayer, nextPlayer) => nextPlayer.Value < minPlayer.Value ? nextPlayer : minPlayer).Key
                : playerScores.Aggregate((maxPlayer, nextPlayer) => nextPlayer.Value > maxPlayer.Value ? nextPlayer : maxPlayer).Key;
        }
            
        void Start()
        {
            if (GameCanvas != null && GameCanvas.MiniGameHUD != null)
                ActivePlayerScoreDisplay = GameCanvas.MiniGameHUD.View.ScoreDisplay;
            else
                Debug.LogWarning("GameCanvas or MiniGameHUD is not assigned!");* /

            InitializeScoringMode();
            StartCoroutine(UpdateScoreDisplayCoroutine());
        }
         
         private void InitializeScoringMode()
        {
            if (AdditionalScoringModes == null || AdditionalScoringModes.Length == 0)
            {
                scoring = CreateScoringMode(ScoringMode, ScoreMultiplier);
                return;
            }

            var compositeScoringMode = new CompositeScoring(new[] { CreateScoringMode(ScoringMode, ScoreMultiplier) });
            
            foreach (var config in AdditionalScoringModes)
            {
                compositeScoringMode.AddScoringMode(CreateScoringMode(config.Mode, config.Multiplier));
            }

            scoring = compositeScoringMode;
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
        
        public virtual void EndTurn()
        {
            if (scoring == null)
                return;

            // playerScores[currentPlayerName] = scoring.EndTurnScore(currentPlayerName, playerScores[currentPlayerName], turnStartTime) + initialScore;
            playerScores[currentPlayerName] = scoring.EndTurnScore(currentPlayerName, playerScores[currentPlayerName]);

            foreach (var playerTeam in playerTeams) // Add all the players back into the reset stats dictionary so the score will update at the start of the player's turn
                StatsManager.Instance.AddPlayer(playerTeam.Value, playerTeam.Key);
        }
        
        public List<int> GetScores()
        {
            return playerScores.Values.Select(score => (int)score).ToList();
        }
        
        }*/