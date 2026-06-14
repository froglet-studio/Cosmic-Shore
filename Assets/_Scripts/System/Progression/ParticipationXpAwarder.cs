using CosmicShore.UI;        // PlayerDataService
using CosmicShore.Utility;   // GameDataSO, CSDebug
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Awards flat participation XP to the LOCAL player once per game (win or lose).
    /// Listens to the existing GameDataSO.OnMiniGameEnd SOAP channel so it covers every
    /// mode through one decoupled path. Mirrors GameModeProgressionService.
    /// </summary>
    public class ParticipationXpAwarder : MonoBehaviour
    {
        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        [Header("XP Reward")]
        [Tooltip("Participation XP awarded to the local player every game (win or lose). " +
                 "Feeds the menu XP progress bar via PlayerDataService.AddXP. Set 0 to disable.")]
        [SerializeField] private int xpPerGame = 25;

        bool _awardedThisGame;

        void Start()      // matches GameModeProgressionService: subscribe in Start
        {
            if (gameData == null) return;
            gameData.OnSessionStarted.OnRaised += ResetForNewGame;
            gameData.OnMiniGameEnd.OnRaised   += AwardParticipationXp;
        }

        void OnDestroy()
        {
            if (gameData == null) return;
            gameData.OnSessionStarted.OnRaised -= ResetForNewGame;
            gameData.OnMiniGameEnd.OnRaised    -= AwardParticipationXp;
        }

        void ResetForNewGame() => _awardedThisGame = false;

        void AwardParticipationXp()
        {
            if (_awardedThisGame || xpPerGame <= 0) return;
            var service = PlayerDataService.Instance;
            if (service == null) return;

            _awardedThisGame = true;
            int total = service.AddXP(xpPerGame);
            CSDebug.Log($"[ParticipationXp] Awarded {xpPerGame} XP. Total: {total}");
        }
    }
}
