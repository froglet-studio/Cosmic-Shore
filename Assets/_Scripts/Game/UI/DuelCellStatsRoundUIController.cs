using CosmicShore.SOAP;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class DuelCellStatsRoundUIController : MonoBehaviour
    {
        [SerializeField]
        GameDataSO gameData;
        
        [SerializeField]
        DuellCellStatsRowUIController ownRound1Row;
        
        [SerializeField]
        DuellCellStatsRowUIController ownRound2Row;
        
        [SerializeField]
        DuellCellStatsRowUIController opponentRound1Row;
        
        [SerializeField]
        DuellCellStatsRowUIController opponentRound2Row;

        [SerializeField]
        CanvasGroup canvasGroup;
        
        StatsRowData[]  statsRows = new StatsRowData[2];
        
        void OnEnable()
        {
            // Need to use TurnStarted, as RoundStarted will be called instantly after RoundEnded,
            // Turn Started will be called after client presses ready button and server accepts it.
            gameData.OnMiniGameTurnStarted.OnRaised += OnMiniGameTurnStarted;
            
            gameData.OnMiniGameRoundEnd.OnRaised += OnMiniGameRoundEnd;
            gameData.OnWinnerCalculated += OnWinnerCalculated;

            Hide();
        }

        void OnDisable()
        { 
            // Need to use TurnStarted, as RoundStarted will be called instantly after RoundEnded,
            // Turn Started will be called after client presses ready button and server accepts it.
            gameData.OnMiniGameTurnStarted.OnRaised -= OnMiniGameTurnStarted;
            
            gameData.OnMiniGameRoundEnd.OnRaised -= OnMiniGameRoundEnd;
            gameData.OnWinnerCalculated -= OnWinnerCalculated;
        }

        void OnMiniGameTurnStarted() => Hide();

        void OnMiniGameRoundEnd()
        {
            UpdateRowUIs();
            Show();
        }

        void UpdateRowUIs()
        {
            if (gameData.Players.Count != 2)
            {
                Debug.LogError("This should never happen!");
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var rowUIController = gameData.RoundsPlayed switch
                {
                    1 => gameData.Players[i].IsLocalUser ? ownRound1Row : opponentRound1Row,
                    2 => gameData.Players[i].IsLocalUser ? ownRound2Row : opponentRound2Row,
                    _ => null
                };

                if (!rowUIController)
                {
                    Debug.LogError($"This should never happen! Rounds Played: {gameData.RoundsPlayed}");
                    return;
                }

                DuellCellStatsRowUIController previousRoundRowUIController = null;
                if (gameData.RoundsPlayed == 2)
                {
                    previousRoundRowUIController = gameData.Players[i].IsLocalUser ? ownRound1Row : opponentRound1Row;
                    if (!previousRoundRowUIController)
                    {
                        Debug.LogError("This should never happen!");
                        return;
                    }
                }

                switch (gameData.RoundsPlayed)
                {
                    case 1:
                        UpdateRowUIForRound1(rowUIController, gameData.RoundStatsList[i]);
                        break;
                    case 2:
                        UpdateRowUIForRound2(rowUIController, previousRoundRowUIController, gameData.RoundStatsList[i]);
                        break;
                }
            }
        }

        void UpdateRowUIForRound1(DuellCellStatsRowUIController rowUIController, IRoundStats stats)
        {
            var data = new StatsRowData
            {
                VolumeCreated = stats.VolumeCreated,
                HostileVolumeDestroyed = stats.HostileVolumeDestroyed,
                FriendlyVolumeDestroyed = stats.FriendlyVolumeDestroyed,
                Score = stats.Score,
            };

            rowUIController.Data = data;
            rowUIController.UpdateRow();
        }
        
        void UpdateRowUIForRound2(DuellCellStatsRowUIController rowUIController, DuellCellStatsRowUIController previousRowUIController, IRoundStats stats)
        {
            var data = new StatsRowData
            {
                VolumeCreated = stats.VolumeCreated - previousRowUIController.Data.VolumeCreated,
                HostileVolumeDestroyed = stats.HostileVolumeDestroyed - previousRowUIController.Data.HostileVolumeDestroyed,
                FriendlyVolumeDestroyed = stats.FriendlyVolumeDestroyed - previousRowUIController.Data.FriendlyVolumeDestroyed,
                Score = stats.Score - previousRowUIController.Data.Score,
            };

            rowUIController.Data = data;
            rowUIController.UpdateRow();
        }

        void OnWinnerCalculated()
        {
            Show();
        }

        void Show()
        {
            canvasGroup.alpha = 1;
        }

        void Hide()
        {
            canvasGroup.alpha = 0;
        }
        
        public struct StatsRowData
        {
            public float VolumeCreated;
            public float HostileVolumeDestroyed;
            public float FriendlyVolumeDestroyed;
            public float Score;
        }
    }
}